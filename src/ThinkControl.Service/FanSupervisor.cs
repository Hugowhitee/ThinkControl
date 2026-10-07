using System.Text.Json;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.Hardware.Lenovo;

namespace ThinkControl.Service;

internal sealed record CoolingSupervisorSnapshot(
    string Profile,
    string? ProfileId,
    int? AppliedLevel,
    int? AppliedPercent,
    double? SmoothedTemperatureC,
    string Status,
    bool SafetyOverride,
    FanCharacterizationSnapshot Characterization);

/// <summary>
/// Sole owner of ThinkControl fan writes. Graph curves, manual requests and fan
/// characterization are serialized here so two callers can never fight over a
/// hardware provider. Lenovo Auto is the fail-safe for missing telemetry, unsafe
/// heat, provider changes, cancellation and service shutdown.
/// </summary>
internal sealed class FanSupervisor : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MinimumUpshiftDwell = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan MinimumDownshiftDwell = TimeSpan.FromSeconds(14);
    private static readonly TimeSpan SyncWriteTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CalibrationSettleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CalibrationSampleSpacing = TimeSpan.FromMilliseconds(6200);
    private const int CalibrationSampleCount = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IFanHardwareController _hardware;
    private readonly Action<string> _log;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly SemaphoreSlim _controlWake = new(0, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly string _calibrationPath;

    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _characterizationCts;
    private Task? _loopTask;
    private Task? _characterizationTask;

    private FanCurveDefinition? _activeCurve;
    private int? _manualLevel;
    private int? _manualPercent;
    private int? _appliedLevel;
    private int? _appliedPercent;
    private int? _curveTargetPercent;
    private double? _smoothedTemperatureC;
    private LenovoFanControlKind _managedFanControlKind = LenovoFanControlKind.None;
    private bool _safetyOverride;
    private int _recoverySensorSamples;
    private bool _autoHandoffConfirmed;
    private string _status = "Lenovo firmware owns fan control";
    private DateTimeOffset _lastOutputChange = DateTimeOffset.MinValue;
    private int? _pendingLevel;
    private int? _pendingPercent;
    private DateTimeOffset _pendingLevelSince = DateTimeOffset.MinValue;
    private DateTimeOffset _pendingPercentSince = DateTimeOffset.MinValue;

    private bool _characterizationRunning;
    private int? _characterizationLevel;
    private string _characterizationStatus = "Not calibrated yet";
    private readonly List<FanLevelCalibrationSnapshot> _calibration = [];
    private readonly List<FanLevelCalibrationSnapshot> _characterizationCandidate = [];
    private readonly HashSet<int> _unstableLevels = [];
    private int? _audibleFromLevel;
    private bool _disposed;

    internal FanSupervisor(IFanHardwareController hardware, Action<string>? log = null)
    {
        _hardware = hardware;
        _log = log ?? ServiceLog.Write;
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ThinkControl");
        _calibrationPath = Path.Combine(folder, "fan-calibration.json");
        LoadCalibration();
    }

    internal void Start(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_loopTask is not null)
                return;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            CancellationToken token = _runCts.Token;
            _loopTask = Task.Run(() => LoopAsync(token), token);
        }
    }

    internal CoolingSupervisorSnapshot Snapshot()
    {
        lock (_gate)
        {
            string profile = _manualPercent.HasValue
                ? $"Manual {_manualPercent.Value}%"
                : _manualLevel.HasValue
                    ? $"Manual EC step {_manualLevel.Value}"
                    : _activeCurve?.Name ?? "Lenovo Auto";
            FanLevelCalibrationSnapshot[] visibleCalibration = (_characterizationRunning
                    ? _characterizationCandidate
                    : _calibration)
                .OrderBy(point => point.Level)
                .ToArray();
            return new CoolingSupervisorSnapshot(
                profile,
                _activeCurve?.Id,
                _appliedLevel,
                _appliedPercent,
                _smoothedTemperatureC,
                _status,
                _safetyOverride,
                new FanCharacterizationSnapshot(
                    _characterizationRunning,
                    _characterizationLevel,
                    visibleCalibration.Length,
                    _hardware.FanCalibrationStates.Count,
                    _characterizationStatus,
                    _audibleFromLevel,
                    visibleCalibration));
        }
    }

    internal bool SetProfile(string? raw, out string? error)
    {
        error = null;
        string normalized = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized is "lenovo auto" or "auto")
            return ReturnToAuto(out error);

        FanCurveDefinition? curve = normalized switch
        {
            "quiet" or "silent" => FanCurveDefaults.Quiet,
            "balanced" or "normal" => FanCurveDefaults.Balanced,
            "max cooling" or "maxcooling" or "cool" => FanCurveDefaults.MaxCooling,
            _ => null
        };
        if (curve is null)
        {
            error = "Fan profile must be Lenovo Auto, Quiet, Balanced, Max cooling or a validated named graph profile.";
            return false;
        }
        return SetCurve(curve, out error);
    }

    internal bool SetCurve(FanCurveDefinition? definition, out string? error)
    {
        error = null;
        if (definition is null || string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Name))
        {
            error = "Fan profile metadata is missing.";
            return false;
        }
        if (!FanCurveGraphPolicy.TryNormalize(definition.Points, out FanCurvePoint[] points, out error))
            return false;
        if (!CanEnterManagedCooling(out LenovoHardwareStatus? status, out error, allowHotCurve: true) || status is null)
            return false;
        if (status.FanControlKind == LenovoFanControlKind.ThinkPadEcDiscrete && !HasCompleteCalibration())
        {
            error = "Measure the supported fan speeds before applying a custom curve.";
            return false;
        }

        string id = definition.Id.Trim();
        string name = definition.Name.Trim();
        if (id.Length > 80 || name.Length > 40)
        {
            error = "Fan profile name or id is too long.";
            return false;
        }

        lock (_gate)
        {
            _activeCurve = new FanCurveDefinition(id, name, points);
            _manualLevel = null;
            _manualPercent = null;
            _appliedLevel = null;
            _appliedPercent = null;
            _curveTargetPercent = null;
            _smoothedTemperatureC = status.ControlTemperatureC!.Value;
            _managedFanControlKind = status.FanControlKind;
            _safetyOverride = FanCurvePolicy.RequiresFirmwareSafetyHandoff(status.ControlTemperatureC.Value);
            _recoverySensorSamples = 0;
            _status = _safetyOverride ? $"{name} is saved. Firmware is cooling the system before the curve resumes."
                : status.FanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm
                ? $"{name} curve active: Lenovo target RPM control"
                : $"{name} curve active: measured EC states";
            _lastOutputChange = DateTimeOffset.MinValue;
            ClearPendingTransitionLocked();
        }
        SignalControlWake();
        return true;
    }

    // Compatibility for short-lived alpha.16 development settings that stored six
    // temperature thresholds instead of a named 8-point graph.
    internal bool SetCustomCurve(IReadOnlyList<double>? thresholds, out string? error)
    {
        error = null;
        if (!FanCurvePolicy.TryValidateCustomThresholds(thresholds, out double[] normalized, out error))
            return false;

        FanCurvePoint[] points =
        [
            new(35, 0), new(normalized[0], 16), new(normalized[1], 32), new(normalized[2], 48),
            new(normalized[3], 64), new(normalized[4], 80), new(normalized[5], 94), new(92, 100)
        ];
        Array.Sort(points, (a, b) => a.TemperatureC.CompareTo(b.TemperatureC));
        if (!FanCurveGraphPolicy.TryNormalize(points, out _, out _))
            return SetCurve(FanCurveDefaults.Balanced with { Id = "custom:migrated", Name = "Custom" }, out error);
        return SetCurve(new FanCurveDefinition("custom:migrated", "Custom", points), out error);
    }

    private bool CanEnterManagedCooling(out LenovoHardwareStatus? status, out string? error, bool allowHotCurve = false)
    {
        error = null;
        status = null;
        lock (_gate)
        {
            if (_characterizationRunning)
            {
                error = "Fan calibration is running. Stop or finish it before selecting a fan profile.";
                return false;
            }
        }

        status = _hardware.ReadStatus();
        if (!status.CanFanControl || status.FanControlKind == LenovoFanControlKind.None || !status.ControlTemperatureC.HasValue)
        {
            error = "Managed cooling requires a verified fan-control provider and a valid control-temperature sensor.";
            return false;
        }
        if (FanCurvePolicy.RequiresFirmwareSafetyHandoff(status.ControlTemperatureC.Value))
        {
            if (!ReturnHardwareToAutoSerialized(out error)) return false;
            if (allowHotCurve) return true;
            error = "The system is too hot to enter managed cooling. Lenovo firmware keeps control until temperature falls.";
            return false;
        }
        return true;
    }

    internal bool SetManualLevel(int level, out string? error)
    {
        error = null;
        if (!_hardware.FanCalibrationStates.Contains(level))
        {
            error = "This fan speed is not supported by the current provider.";
            return false;
        }
        if (!CanEnterManagedCooling(out LenovoHardwareStatus? preflight, out error) || preflight is null)
            return false;
        if (preflight.FanControlKind != LenovoFanControlKind.ThinkPadEcDiscrete)
        {
            error = "Raw EC steps are diagnostic controls for the discrete X9 EC fallback only. The active Lenovo OEM provider uses target RPM instead.";
            return false;
        }
        if (!SetHardwareLevelSerialized(level, out error))
            return false;

        int estimated = EstimatePercentForState(level);
        lock (_gate)
        {
            _activeCurve = null;
            _manualLevel = level;
            _manualPercent = null;
            _appliedLevel = level;
            _appliedPercent = estimated;
            _curveTargetPercent = null;
            _smoothedTemperatureC = preflight.ControlTemperatureC!.Value;
            _managedFanControlKind = LenovoFanControlKind.ThinkPadEcDiscrete;
            _safetyOverride = false;
            _status = $"Manual EC state: {level}, approximately {estimated}% of measured normal range, temperature: {preflight.ControlTemperatureC.Value:0.#} °C";
            _lastOutputChange = DateTimeOffset.UtcNow;
            ClearPendingTransitionLocked();
        }
        SignalControlWake();
        return true;
    }

    internal bool SetManualPercent(int percent, out string? error)
    {
        error = null;
        if (percent is < 0 or > 100)
        {
            error = "Manual fan target must be between 0% and 100%.";
            return false;
        }
        if (!CanEnterManagedCooling(out LenovoHardwareStatus? preflight, out error) || preflight is null)
            return false;

        int? appliedLevel;
        int appliedPercent;
        string? hardwareDetail;

        if (preflight.FanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)
        {
            if (!SetHardwarePercentSerialized(percent, out hardwareDetail, out error))
                return false;
            appliedLevel = null;
            appliedPercent = percent;
        }
        else
        {
            if (!HasCompleteCalibration())
            {
                error = "Measure the supported fan speeds before setting a percentage.";
                return false;
            }
            FanOutputMapping.State output = ResolveOutputState(percent);
            if (!ApplyOutputStateSerialized(output, out hardwareDetail, out error))
                return false;
            appliedLevel = output.HardwareState;
            appliedPercent = output.EstimatedPercent;
        }

        lock (_gate)
        {
            _activeCurve = null;
            _manualLevel = null;
            _manualPercent = percent;
            _appliedLevel = appliedLevel;
            _appliedPercent = appliedPercent;
            _curveTargetPercent = null;
            _smoothedTemperatureC = preflight.ControlTemperatureC!.Value;
            _managedFanControlKind = preflight.FanControlKind;
            _safetyOverride = false;
            _status = $"Manual target: {percent}%, {hardwareDetail}, temperature: {preflight.ControlTemperatureC.Value:0.#} °C";
            _lastOutputChange = DateTimeOffset.UtcNow;
            ClearPendingTransitionLocked();
        }
        SignalControlWake();
        return true;
    }

    internal bool ReturnToAuto(out string? error)
    {
        bool success = ReturnHardwareToAutoSerialized(out error);
        if (!success && !_hardware.Identity.IsVerifiedX9)
        {
            success = true;
            error = null;
        }

        if (success)
        {
            lock (_gate)
            {
                _activeCurve = null;
                _manualLevel = null;
                _manualPercent = null;
                _appliedLevel = null;
                _appliedPercent = null;
                _curveTargetPercent = null;
                _smoothedTemperatureC = null;
                _managedFanControlKind = LenovoFanControlKind.None;
                _safetyOverride = false;
                _status = "Lenovo firmware owns fan control";
                ClearPendingTransitionLocked();
            }
        }
        return success;
    }

    internal bool StartCharacterization(out string? error)
    {
        error = null;
        lock (_gate)
        {
            if (_characterizationRunning)
            {
                error = "Fan calibration is already running.";
                return false;
            }
        }

        if (!_hardware.Identity.IsVerifiedX9)
        {
            error = "Fan calibration is only available for the verified X9 discrete-EC provider.";
            return false;
        }

        LenovoHardwareStatus preflight = _hardware.ReadStatus();
        if (preflight.FanControlKind != LenovoFanControlKind.ThinkPadEcDiscrete)
        {
            error = preflight.FanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm
                ? "The active X9 provider already exposes Lenovo OEM target-RPM control, so seven-step EC calibration is not used."
                : "Fan calibration requires the verified X9 discrete-EC fallback provider.";
            return false;
        }
        if (!preflight.CanFanControl || !preflight.ControlTemperatureC.HasValue ||
            !preflight.CanFanTelemetry || preflight.Fans.Count == 0)
        {
            error = "Calibration needs verified EC fan writes, control-temperature telemetry and a real fan tachometer.";
            return false;
        }
        if (preflight.ControlTemperatureC.Value >= 75)
        {
            error = "Let the laptop cool below 75 °C before calibrating the fan states.";
            return false;
        }

        lock (_gate)
        {
            _characterizationCts?.Dispose();
            _characterizationCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            _activeCurve = null;
            _manualLevel = null;
            _manualPercent = null;
            _appliedLevel = null;
            _appliedPercent = null;
            _curveTargetPercent = null;
            _smoothedTemperatureC = null;
            _managedFanControlKind = LenovoFanControlKind.ThinkPadEcDiscrete;
            _safetyOverride = false;
            ClearPendingTransitionLocked();
            _characterizationRunning = true;
            _characterizationLevel = _hardware.FanCalibrationStates[^1];
            _characterizationCandidate.Clear();
            _characterizationStatus = HasCompleteCalibration()
                ? "Measuring fan speeds. Previous calibration is retained until this measurement succeeds."
                : "Measuring the supported fan speeds.";
            CancellationToken token = _characterizationCts.Token;
            _characterizationTask = Task.Run(() => CharacterizeAsync(token), token);
        }
        SignalControlWake();
        return true;
    }

    internal bool MarkCurrentLevelAudible(out string? error)
    {
        error = null;
        lock (_gate)
        {
            if (!_characterizationRunning || !_characterizationLevel.HasValue || !_hardware.FanCalibrationStates.Contains(_characterizationLevel.Value))
            {
                error = "Start fan calibration first, then mark the first state you clearly hear.";
                return false;
            }

            _audibleFromLevel = _characterizationLevel.Value;
            _characterizationStatus = _characterizationLevel.Value == _hardware.FanCalibrationStates[^1]
                ? "Verified EC maximum marked as clearly audible"
                : $"EC step {_characterizationLevel.Value} marked as clearly audible";
        }
        SaveCalibration();
        return true;
    }

    internal bool StopCharacterization(out string? error)
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _characterizationCts;
            _characterizationLevel = null;
            _characterizationStatus = HasCompleteCalibration()
                ? "Calibration stopped. Previous calibration retained; returning to Auto."
                : "Calibration stopped. No partial results saved; returning to Auto.";
        }
        try { cts?.Cancel(); } catch { }

        bool success = ReturnHardwareToAutoSerialized(out error);
        if (success)
        {
            lock (_gate)
            {
                _activeCurve = null;
                _manualLevel = null;
                _manualPercent = null;
                _appliedLevel = null;
                _appliedPercent = null;
                _curveTargetPercent = null;
                _smoothedTemperatureC = null;
                _managedFanControlKind = LenovoFanControlKind.None;
                _safetyOverride = false;
                ClearPendingTransitionLocked();
            }
        }
        return success;
    }

    internal void WakeForHardwareLease() => SignalControlWake();

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_hardware.CheckFullSpeedSession())
            {
                _log("Cooling stopped: the fan ownership lease expired or hardware readback was lost. Explicit profile selection is required to resume.");
                // Clear the curve so its next tick cannot reacquire expired/lost ownership.
                bool measuring;
                lock (_gate) measuring = _characterizationRunning;
                if (measuring) StopCharacterization(out _);
                else ReturnToAuto(out _);
            }
            bool active;
            lock (_gate)
                active = _activeCurve is not null || _manualLevel.HasValue || _manualPercent.HasValue || _characterizationRunning;
            active |= _hardware.OwnsManagedFan;

            if (!active)
            {
                try { await _controlWake.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await ApplyProfileTickAsync(token).ConfigureAwait(false);
                await Task.Delay(TickInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                try { await Task.Delay(TickInterval, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ApplyProfileTickAsync(CancellationToken token)
    {
        FanCurveDefinition? curve;
        int? manualLevel;
        int? manualPercent;
        bool characterizationRunning;
        LenovoFanControlKind managedKind;
        lock (_gate)
        {
            curve = _activeCurve;
            manualLevel = _manualLevel;
            manualPercent = _manualPercent;
            characterizationRunning = _characterizationRunning;
            managedKind = _managedFanControlKind;
        }

        if (characterizationRunning)
            return;
        if (!manualLevel.HasValue && !manualPercent.HasValue && curve is null)
            return;

        LenovoHardwareStatus status;
        try { status = _hardware.ReadStatus(); }
        catch (Exception ex)
        {
            lock (_gate) _recoverySensorSamples = 0;
            await SafeAutoHandoffAsync($"Control telemetry failed: {ex.GetType().Name}", token, preserveCurve: curve is not null).ConfigureAwait(false);
            return;
        }
        if (!status.CanFanControl || status.FanControlKind == LenovoFanControlKind.None || !status.ControlTemperatureC.HasValue)
        {
            lock (_gate) _recoverySensorSamples = 0;
            await SafeAutoHandoffAsync("Sensor or verified fan-control provider became unavailable", token, preserveCurve: curve is not null).ConfigureAwait(false);
            return;
        }

        if (managedKind != LenovoFanControlKind.None && status.FanControlKind != managedKind)
        {
            await SafeAutoHandoffAsync(
                $"Fan provider changed from {managedKind} to {status.FanControlKind}",
                token,
                preserveCurve: false).ConfigureAwait(false);
            return;
        }

        double raw = status.ControlTemperatureC.Value;
        if (FanCurvePolicy.RequiresFirmwareSafetyHandoff(raw))
        {
            await SafeAutoHandoffAsync($"Safety handoff at {raw:0.#} °C", token, preserveCurve: curve is not null).ConfigureAwait(false);
            return;
        }

        if (manualLevel.HasValue || manualPercent.HasValue)
        {
            lock (_gate)
            {
                _smoothedTemperatureC = raw;
                if (manualPercent.HasValue && _managedFanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)
                {
                    _status = $"Manual target: {manualPercent.Value}%, Lenovo target RPM control, temperature: {raw:0.#} °C";
                }
                else if (manualPercent.HasValue)
                {
                    _status = $"Manual target: {manualPercent.Value}%, measured EC output: {_appliedPercent ?? 0}%, temperature: {raw:0.#} °C";
                }
                else
                {
                    _status = $"Manual EC state: {manualLevel!.Value}, approximately {_appliedPercent ?? 0}% of measured normal range, temperature: {raw:0.#} °C";
                }
            }
            return;
        }

        bool waitingForSafetyResume;
        lock (_gate) waitingForSafetyResume = _safetyOverride;
        if (waitingForSafetyResume)
        {
            bool confirmed;
            lock (_gate) confirmed = _autoHandoffConfirmed;
            if (!confirmed)
            {
                await SafeAutoHandoffAsync("Retrying unconfirmed firmware Auto handoff", token, preserveCurve: true).ConfigureAwait(false);
                return;
            }
            int samples;
            lock (_gate) samples = FanCurvePolicy.CanResumeAfterSafetyHandoff(raw)
                ? ++_recoverySensorSamples : (_recoverySensorSamples = 0);
            if (samples < 2)
            {
                lock (_gate) _status = $"Firmware resumed cooling for safety. Temperature: {raw:0.#} °C";
                return;
            }
            lock (_gate)
            {
                _safetyOverride = false;
                _appliedLevel = null;
                _appliedPercent = null;
                _curveTargetPercent = null;
                _smoothedTemperatureC = raw;
                _managedFanControlKind = status.FanControlKind;
                ClearPendingTransitionLocked();
                _status = $"{curve!.Name} resumed through {DescribeControlKind(status.FanControlKind)}";
            }
        }
        else if (managedKind == LenovoFanControlKind.None)
        {
            lock (_gate) _managedFanControlKind = status.FanControlKind;
        }

        int? currentTarget;
        double smooth;
        lock (_gate)
        {
            _smoothedTemperatureC = _smoothedTemperatureC.HasValue
                ? _smoothedTemperatureC.Value + 0.30 * (raw - _smoothedTemperatureC.Value)
                : raw;
            smooth = _smoothedTemperatureC.Value;
            currentTarget = _curveTargetPercent;
        }

        int requestedPercent = FanCurveGraphPolicy.ResolvePercent(curve!.Points, smooth, currentTarget);
        if (status.FanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)
        {
            await ApplyOemCurveTargetAsync(curve, requestedPercent, smooth, raw, token).ConfigureAwait(false);
            return;
        }

        await ApplyDiscreteCurveTargetAsync(curve, requestedPercent, smooth, raw, token).ConfigureAwait(false);
    }

    private async Task ApplyOemCurveTargetAsync(
        FanCurveDefinition curve,
        int requestedPercent,
        double smooth,
        double raw,
        CancellationToken token)
    {
        int? currentPercent;
        lock (_gate) currentPercent = _appliedPercent;

        if (currentPercent == requestedPercent)
        {
            lock (_gate)
            {
                ClearPendingTransitionLocked();
                _curveTargetPercent = requestedPercent;
                _status = DescribeOemCurveOutput(curve.Name, requestedPercent, smooth);
            }
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool shouldWrite;
        lock (_gate)
        {
            shouldWrite = ShouldCommitOemPercentTransitionLocked(currentPercent, requestedPercent, raw, now);
            _curveTargetPercent = requestedPercent;
            if (!shouldWrite)
                _status = DescribeOemCurveOutput(curve.Name, requestedPercent, smooth) + ". Settling.";
        }
        if (!shouldWrite)
            return;

        bool writeSuccess;
        string? detail;
        string? writeError;
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { writeSuccess = _hardware.SetFanPercent(requestedPercent, out detail, out writeError); }
        finally { _writeGate.Release(); }

        if (!writeSuccess)
        {
            await SafeAutoHandoffAsync(writeError ?? "Lenovo OEM fan target write failed", token).ConfigureAwait(false);
            return;
        }

        lock (_gate)
        {
            _appliedLevel = null;
            _appliedPercent = requestedPercent;
            _curveTargetPercent = requestedPercent;
            _managedFanControlKind = LenovoFanControlKind.LenovoOtherModeTargetRpm;
            _lastOutputChange = now;
            ClearPendingTransitionLocked();
            _status = $"{DescribeOemCurveOutput(curve.Name, requestedPercent, smooth)}. {detail}";
        }
    }

    private async Task ApplyDiscreteCurveTargetAsync(
        FanCurveDefinition curve,
        int requestedPercent,
        double smooth,
        double raw,
        CancellationToken token)
    {
        FanOutputMapping.State desired = ResolveOutputState(requestedPercent);
        int? currentState;
        lock (_gate) currentState = _appliedLevel;
        if (currentState == desired.HardwareState)
        {
            lock (_gate)
            {
                ClearPendingTransitionLocked();
                _curveTargetPercent = requestedPercent;
                _appliedPercent = desired.EstimatedPercent;
                _status = DescribeDiscreteCurveOutput(curve.Name, requestedPercent, desired, smooth);
            }
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool shouldWrite;
        lock (_gate)
        {
            shouldWrite = ShouldCommitDiscreteTransitionLocked(currentState, desired.HardwareState, raw, now);
            if (!shouldWrite)
                _status = $"{DescribeDiscreteCurveOutput(curve.Name, requestedPercent, desired, smooth)}. Settling.";
        }
        if (!shouldWrite)
            return;

        bool writeSuccess;
        string? detail;
        string? writeError;
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { writeSuccess = ApplyOutputStateUnlocked(desired, out detail, out writeError); }
        finally { _writeGate.Release(); }

        if (!writeSuccess)
        {
            await SafeAutoHandoffAsync(writeError ?? "Fan output write failed", token).ConfigureAwait(false);
            return;
        }

        lock (_gate)
        {
            _appliedLevel = desired.HardwareState;
            _appliedPercent = desired.EstimatedPercent;
            _curveTargetPercent = requestedPercent;
            _managedFanControlKind = LenovoFanControlKind.ThinkPadEcDiscrete;
            _lastOutputChange = now;
            ClearPendingTransitionLocked();
            _status = DescribeDiscreteCurveOutput(curve.Name, requestedPercent, desired, smooth);
        }
    }

    private bool ShouldCommitDiscreteTransitionLocked(int? currentState, int desiredState, double rawTemperatureC, DateTimeOffset now)
    {
        if (!currentState.HasValue)
            return true;

        int delta = desiredState - currentState.Value;
        if (delta > 0 && (delta >= 2 || rawTemperatureC >= 82))
            return true;

        if (_pendingLevel != desiredState)
        {
            _pendingLevel = desiredState;
            _pendingLevelSince = now;
            return false;
        }

        TimeSpan dwell = delta > 0 ? MinimumUpshiftDwell : MinimumDownshiftDwell;
        if (now - _pendingLevelSince < dwell)
            return false;

        return delta > 0 || now - _lastOutputChange >= MinimumDownshiftDwell;
    }

    private bool ShouldCommitOemPercentTransitionLocked(int? currentPercent, int desiredPercent, double rawTemperatureC, DateTimeOffset now)
    {
        if (!currentPercent.HasValue)
            return true;

        int delta = desiredPercent - currentPercent.Value;
        if (Math.Abs(delta) < 2)
        {
            _pendingPercent = null;
            _pendingPercentSince = DateTimeOffset.MinValue;
            return false;
        }

        if (delta > 0 && (delta >= 10 || rawTemperatureC >= 82))
            return true;

        if (_pendingPercent != desiredPercent)
        {
            _pendingPercent = desiredPercent;
            _pendingPercentSince = now;
            return false;
        }

        TimeSpan dwell = delta > 0 ? MinimumUpshiftDwell : MinimumDownshiftDwell;
        if (now - _pendingPercentSince < dwell)
            return false;

        return delta > 0 || now - _lastOutputChange >= MinimumDownshiftDwell;
    }

    private void ClearPendingTransitionLocked()
    {
        _pendingLevel = null;
        _pendingPercent = null;
        _pendingLevelSince = DateTimeOffset.MinValue;
        _pendingPercentSince = DateTimeOffset.MinValue;
    }

    private static string DescribeDiscreteCurveOutput(string name, int target, FanOutputMapping.State state, double temperature) =>
        $"{name}: target {target}%, approximately {state.EstimatedPercent}% of measured normal range, EC state {state.HardwareState}, temperature: {temperature:0.#} °C";

    private static string DescribeOemCurveOutput(string name, int target, double temperature) =>
        $"{name}: target {target}%, Lenovo target RPM control, temperature: {temperature:0.#} °C";

    private static string DescribeControlKind(LenovoFanControlKind kind) => kind switch
    {
        LenovoFanControlKind.LenovoOtherModeTargetRpm => "Lenovo OEM target-RPM control",
        LenovoFanControlKind.ThinkPadEcDiscrete => "verified X9 EC fallback",
        _ => "Lenovo firmware control"
    };

    private async Task SafeAutoHandoffAsync(string reason, CancellationToken token, bool preserveCurve = false)
    {
        // A paused curve is already in firmware Auto. Keep observing without
        // repeatedly writing Auto or filling the log while a sensor is missing.
        lock (_gate)
            if (preserveCurve && _safetyOverride && _autoHandoffConfirmed) return;
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        bool restored;
        string? restoreError;
        try { restored = _hardware.ReturnFanToAuto(out restoreError); }
        finally { _writeGate.Release(); }

        _log($"Cooling handoff: {reason}. Auto confirmed: {restored}. {restoreError}");

        lock (_gate)
        {
            _manualLevel = null;
            _manualPercent = null;
            _autoHandoffConfirmed = restored;
            _appliedLevel = null;
            _appliedPercent = null;
            _curveTargetPercent = null;
            if (!preserveCurve) _managedFanControlKind = LenovoFanControlKind.None;
            ClearPendingTransitionLocked();
            if (preserveCurve && _activeCurve is not null)
            {
                _safetyOverride = true;
                _recoverySensorSamples = 0;
                _status = reason + (restored
                    ? ". Firmware is temporarily controlling cooling."
                    : ". Auto handoff is unconfirmed; the curve remains paused.");
            }
            else
            {
                _activeCurve = null;
                _safetyOverride = false;
                _smoothedTemperatureC = null;
                _status = reason + ". Returned to Auto.";
            }
        }
    }

    private async Task CharacterizeAsync(CancellationToken token)
    {
        try
        {
            int[] states = _hardware.FanCalibrationStates.ToArray();
            if (!await SetHardwareLevelSerializedAsync(states[^1], token).ConfigureAwait(false))
                throw new InvalidOperationException("Maximum fan speed could not be verified.");
            await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            _ = ReadCalibrationSampleOrThrow();

            foreach (int state in states)
            {
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (!_characterizationRunning)
                        return;
                    _characterizationLevel = state;
                    _characterizationStatus = $"Measuring fan speed {_characterizationCandidate.Count + 1} of {states.Length}";
                }

                _ = ReadCalibrationSampleOrThrow();
                bool applied = await SetHardwareLevelSerializedAsync(state, token).ConfigureAwait(false);
                if (!applied)
                    throw new InvalidOperationException($"EC step {state} could not be verified.");

                await Task.Delay(CalibrationSettleDelay, token).ConfigureAwait(false);
                var samples = new List<IReadOnlyList<LenovoFanReading>>(CalibrationSampleCount);
                for (int sampleIndex = 0; sampleIndex < CalibrationSampleCount; sampleIndex++)
                {
                    LenovoHardwareStatus sample = ReadCalibrationSampleOrThrow();
                    samples.Add(sample.Fans);
                    if (sampleIndex < CalibrationSampleCount - 1)
                        await Task.Delay(CalibrationSampleSpacing, token).ConfigureAwait(false);
                }

                FanLevelCalibrationSnapshot point = BuildCalibrationPoint(state, samples);
                if (point.Fans.Count == 0)
                    throw new InvalidOperationException($"EC step {state} produced no usable tachometer samples.");

                lock (_gate)
                {
                    _characterizationCandidate.RemoveAll(existing => existing.Level == state);
                    _characterizationCandidate.Add(point);
                    string label = state == states[^1] ? "Max" : $"Speed {_characterizationCandidate.Count}";
                    _characterizationStatus = point.Stable
                        ? $"{label}: measured. {_characterizationCandidate.Count} of {states.Length} speeds checked."
                        : $"{label}: speed varies. {_characterizationCandidate.Count} of {states.Length} speeds checked.";
                }
            }

            FanLevelCalibrationSnapshot[] candidate;
            lock (_gate) candidate = _characterizationCandidate.OrderBy(point => point.Level).ToArray();
            if (!TryValidateCalibration(candidate, out string? validationError))
                throw new InvalidOperationException(validationError ?? "The measured fan states were inconsistent.");

            lock (_gate)
            {
                _calibration.Clear();
                _calibration.AddRange(candidate);
                _unstableLevels.Clear();
                foreach (FanLevelCalibrationSnapshot level in candidate.Where(level => !level.Stable))
                    _unstableLevels.Add(level.Level);
                _characterizationStatus = _unstableLevels.Count == 0
                    ? "Measurement complete. Five readings per speed; curve mapping updated."
                    : $"Calibration verified. {_unstableLevels.Count} variable states recorded; higher states are used when needed.";
            }
            SaveCalibration();
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (!_characterizationStatus.StartsWith("Calibration stopped", StringComparison.Ordinal))
                {
                    _characterizationStatus = HasCompleteCalibration()
                        ? "Calibration cancelled. Previous calibration retained."
                        : "Calibration cancelled. No partial results saved.";
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                string preserved = HasCompleteCalibration()
                    ? ". Previous calibration retained."
                    : ". No partial results saved.";
                _characterizationStatus = $"Calibration stopped: {ex.Message}{preserved}";
            }
        }
        finally
        {
            await ReturnHardwareToAutoSerializedAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_gate)
            {
                _characterizationRunning = false;
                _characterizationLevel = null;
                _characterizationCandidate.Clear();
                _activeCurve = null;
                _manualLevel = null;
                _manualPercent = null;
                _appliedLevel = null;
                _appliedPercent = null;
                _curveTargetPercent = null;
                _smoothedTemperatureC = null;
                _managedFanControlKind = LenovoFanControlKind.None;
                _safetyOverride = false;
                ClearPendingTransitionLocked();
            }
        }
    }

    private LenovoHardwareStatus ReadCalibrationSampleOrThrow()
    {
        LenovoHardwareStatus sample = _hardware.ReadStatus();
        if (!sample.CanFanControl || sample.FanControlKind != LenovoFanControlKind.ThinkPadEcDiscrete || !sample.ControlTemperatureC.HasValue)
            throw new InvalidOperationException("Verified X9 discrete EC control or temperature telemetry disappeared during calibration.");
        if (sample.ControlTemperatureC.Value >= 85)
            throw new InvalidOperationException($"Temperature reached {sample.ControlTemperatureC.Value:0.#} °C; Lenovo firmware takes cooling ownership.");
        if (!sample.CanFanTelemetry || sample.Fans.Count == 0)
            throw new InvalidOperationException("Fan tachometer telemetry disappeared during calibration.");
        return sample;
    }

    private FanOutputMapping.State ResolveOutputState(int targetPercent)
    {
        Dictionary<int, int> rpm = CalibrationRpmByState();
        IReadOnlyList<FanOutputMapping.State> states = FanOutputMapping.BuildStates(rpm, _hardware.FanCalibrationStates);
        FanOutputMapping.State selected = states.First(state => state.EstimatedPercent >= Math.Clamp(targetPercent, 0, 100));

        HashSet<int> unstable;
        lock (_gate) unstable = new HashSet<int>(_unstableLevels);
        int index = states.ToList().FindIndex(state => state.HardwareState == selected.HardwareState);
        while (index < states.Count - 1 && unstable.Contains(states[index].HardwareState))
            index++;
        return states[index];
    }

    private int EstimatePercentForState(int state)
    {
        if (!HasCompleteCalibration()) return 0;
        IReadOnlyList<FanOutputMapping.State> states = FanOutputMapping.BuildStates(CalibrationRpmByState(), _hardware.FanCalibrationStates);
        return states.FirstOrDefault(item => item.HardwareState == state)?.EstimatedPercent ?? 0;
    }

    private Dictionary<int, int> CalibrationRpmByState()
    {
        lock (_gate)
        {
            var result = new Dictionary<int, int>();
            foreach (FanLevelCalibrationSnapshot point in _calibration)
            {
                if (point.Fans.Count == 0)
                    continue;
                int median = (int)Math.Round(point.Fans.Average(fan => fan.MedianRpm));
                if (median >= 0)
                    result[point.Level] = median;
            }
            return result;
        }
    }

    private bool ApplyOutputStateSerialized(FanOutputMapping.State state, out string? detail, out string? error)
    {
        detail = null;
        error = null;
        if (!_writeGate.Wait(SyncWriteTimeout))
        {
            error = "Fan-control writer is busy.";
            return false;
        }
        try { return ApplyOutputStateUnlocked(state, out detail, out error); }
        finally { _writeGate.Release(); }
    }

    private bool ApplyOutputStateUnlocked(FanOutputMapping.State state, out string? detail, out string? error)
    {
        bool levelSuccess = _hardware.SetFanLevel(state.HardwareState, out error);
        detail = levelSuccess
            ? $"approximately {state.EstimatedPercent}% of measured normal range (EC state {state.HardwareState})"
            : null;
        return levelSuccess;
    }

    private bool SetHardwarePercentSerialized(int percent, out string? detail, out string? error)
    {
        detail = null;
        error = null;
        if (!_writeGate.Wait(SyncWriteTimeout))
        {
            error = "Fan-control writer is busy.";
            return false;
        }
        try { return _hardware.SetFanPercent(percent, out detail, out error); }
        finally { _writeGate.Release(); }
    }

    private bool SetHardwareLevelSerialized(int level, out string? error)
    {
        error = null;
        if (!_writeGate.Wait(SyncWriteTimeout))
        {
            error = "Fan-control writer is busy.";
            return false;
        }
        try { return _hardware.SetFanLevel(level, out error); }
        finally { _writeGate.Release(); }
    }

    private async Task<bool> SetHardwareLevelSerializedAsync(int level, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { return _hardware.SetFanLevel(level, out _); }
        finally { _writeGate.Release(); }
    }

    private bool ReturnHardwareToAutoSerialized(out string? error)
    {
        error = null;
        if (!_writeGate.Wait(SyncWriteTimeout))
        {
            error = "Fan-control writer is busy.";
            return false;
        }
        try { return _hardware.ReturnFanToAuto(out error); }
        finally { _writeGate.Release(); }
    }

    private async Task ReturnHardwareToAutoSerializedAsync(CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { _hardware.ReturnFanToAuto(out _); }
        finally { _writeGate.Release(); }
    }

    private static FanLevelCalibrationSnapshot BuildCalibrationPoint(
        int level,
        IReadOnlyList<IReadOnlyList<LenovoFanReading>> samples)
    {
        string[] ids = samples
            .SelectMany(sample => sample.Select(fan => fan.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fans = new List<FanCalibrationFanSnapshot>();
        foreach (string id in ids)
        {
            LenovoFanReading[] readings = samples
                .Select(sample => sample.FirstOrDefault(fan => string.Equals(fan.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(fan => fan is not null)
                .Cast<LenovoFanReading>()
                .ToArray();
            int[] rpms = readings.Select(fan => fan.Rpm).OrderBy(rpm => rpm).ToArray();
            if (rpms.Length == 0)
                continue;

            int median = rpms[rpms.Length / 2];
            int spread = rpms[^1] - rpms[0];
            // Bounded RPM variation is usable for stepped cooling; it is not an
            // acoustic claim. Grossly variable states are skipped by the mapper.
            bool stable = rpms.Length >= CalibrationSampleCount && spread <= Math.Max(250, median * 0.18);
            fans.Add(new FanCalibrationFanSnapshot(id, readings[0].Label, median, spread, stable));
        }

        bool pointStable = fans.Count > 0 && fans.All(fan => fan.Stable);
        return new FanLevelCalibrationSnapshot(level, fans, pointStable);
    }

    private bool HasCompleteCalibration()
    {
        lock (_gate)
            return TryValidateCalibration(_calibration, out _) && _calibration[^1].Stable;
    }

    private bool TryValidateCalibration(
        IReadOnlyList<FanLevelCalibrationSnapshot>? levels,
        out string? error)
        => FanCalibrationPolicy.TryValidate(levels, _hardware.FanCalibrationStates, out error);

    private void LoadCalibration()
    {
        try
        {
            if (!File.Exists(_calibrationPath))
                return;
            PersistedCalibration? stored = JsonSerializer.Deserialize<PersistedCalibration>(File.ReadAllText(_calibrationPath), JsonOptions);
            if (stored is null || !string.Equals(stored.MachineType, _hardware.Identity.MachineType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(stored.CalibrationIdentity, _hardware.FanCalibrationIdentity, StringComparison.Ordinal))
                return;

            _audibleFromLevel = stored.AudibleFromLevel.HasValue && _hardware.FanCalibrationStates.Contains(stored.AudibleFromLevel.Value)
                ? stored.AudibleFromLevel : null;
            FanLevelCalibrationSnapshot[] levels = stored.Levels ?? [];
            if (!TryValidateCalibration(levels, out string? validationError))
            {
                _characterizationStatus = "Stored fan calibration unavailable: " + (validationError ?? "invalid calibration data");
                return;
            }

            _calibration.Clear();
            _calibration.AddRange(levels.OrderBy(level => level.Level));
            _unstableLevels.Clear();
            foreach (FanLevelCalibrationSnapshot level in _calibration.Where(level => !level.Stable))
                _unstableLevels.Add(level.Level);
            _characterizationStatus = $"Loaded {_calibration.Count} measured fan speeds";
        }
        catch
        {
            _characterizationStatus = "Stored fan measurement could not be read. Measure the speeds again to use curves.";
        }
    }

    private void SaveCalibration()
    {
        try
        {
            FanLevelCalibrationSnapshot[] levels;
            int? audible;
            lock (_gate)
            {
                levels = _calibration.OrderBy(point => point.Level).ToArray();
                audible = _audibleFromLevel;
            }

            if (!TryValidateCalibration(levels, out _))
                return;

            string? folder = Path.GetDirectoryName(_calibrationPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(_calibrationPath, JsonSerializer.Serialize(
                new PersistedCalibration(_hardware.Identity.MachineType, audible, levels, _hardware.FanCalibrationIdentity), JsonOptions));
        }
        catch { }
    }

    private void SignalControlWake()
    {
        if (_controlWake.CurrentCount != 0)
            return;
        try { _controlWake.Release(); }
        catch (SemaphoreFullException) { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _characterizationCts?.Cancel(); } catch { }
        try { _runCts?.Cancel(); } catch { }
        _disposeCts.Cancel();
        try { _controlWake.Release(); } catch { }

        try { ReturnHardwareToAutoSerialized(out _); } catch { }
        try { _characterizationTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }

        _characterizationCts?.Dispose();
        _runCts?.Dispose();
        _controlWake.Dispose();
        _writeGate.Dispose();
        _disposeCts.Dispose();
    }

    private sealed record PersistedCalibration(
        string MachineType,
        int? AudibleFromLevel,
        FanLevelCalibrationSnapshot[]? Levels,
        string? CalibrationIdentity = null);
}
