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
/// characterization are serialized here so two callers can never fight over EC.
/// Lenovo Auto is the fail-safe for missing telemetry, unsafe heat, cancellation
/// and service shutdown.
/// </summary>
internal sealed class FanSupervisor : IDisposable
{
    // Managed curves intentionally run at a low cadence. EC/telemetry access is not
    // a real-time servo API; polling it aggressively only adds low-level I/O and can
    // make adjacent graph states hunt audibly. Safety still evaluates raw temperature
    // on every tick and immediately hands cooling back to Lenovo at the safety limit.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MinimumUpshiftDwell = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan MinimumDownshiftDwell = TimeSpan.FromSeconds(14);
    private static readonly TimeSpan SyncWriteTimeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly LenovoHardwareController _hardware;
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
    private bool _safetyOverride;
    private string _status = "Lenovo firmware owns fan control";
    private DateTimeOffset _lastOutputChange = DateTimeOffset.MinValue;
    private int? _pendingLevel;
    private DateTimeOffset _pendingLevelSince = DateTimeOffset.MinValue;

    private bool _characterizationRunning;
    private int? _characterizationLevel;
    private string _characterizationStatus = "Not calibrated yet";
    private readonly List<FanLevelCalibrationSnapshot> _calibration = [];
    private readonly HashSet<int> _unstableLevels = [];
    private int? _audibleFromLevel;
    private bool _disposed;

    internal FanSupervisor(LenovoHardwareController hardware)
    {
        _hardware = hardware;
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
                    _calibration.Count,
                    7,
                    _characterizationStatus,
                    _audibleFromLevel,
                    _calibration.OrderBy(point => point.Level).ToArray()));
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
        if (!CanEnterManagedCooling(out LenovoHardwareStatus? status, out error) || status is null)
            return false;

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
            _safetyOverride = false;
            _status = $"{name} fan curve active · targets map to verified X9 fan states";
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

    private bool CanEnterManagedCooling(out LenovoHardwareStatus? status, out string? error)
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
        if (!status.CanFanControl || !status.ControlTemperatureC.HasValue)
        {
            error = "Managed cooling requires the verified fan-control provider and a valid control-temperature sensor.";
            return false;
        }
        if (FanCurvePolicy.RequiresFirmwareSafetyHandoff(status.ControlTemperatureC.Value))
        {
            ReturnHardwareToAutoSerialized(out _);
            error = "The system is too hot to enter managed cooling. Lenovo firmware keeps control until temperature falls.";
            return false;
        }
        return true;
    }

    internal bool SetManualLevel(int level, out string? error)
    {
        error = null;
        if (level is < 1 or > 7)
        {
            error = "Manual EC step must be between 1 and 7.";
            return false;
        }
        if (!CanEnterManagedCooling(out LenovoHardwareStatus? preflight, out error) || preflight is null)
            return false;
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
            _safetyOverride = false;
            _status = $"Manual EC step {level} · ~{estimated}% of verified maximum · {preflight.ControlTemperatureC.Value:0.#} °C";
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

        FanOutputMapping.State output = ResolveOutputState(percent);
        if (!ApplyOutputStateSerialized(output, out string? hardwareDetail, out error))
            return false;

        lock (_gate)
        {
            _activeCurve = null;
            _manualLevel = null;
            _manualPercent = percent;
            _appliedLevel = output.HardwareState;
            _appliedPercent = output.EstimatedPercent;
            _curveTargetPercent = null;
            _smoothedTemperatureC = preflight.ControlTemperatureC!.Value;
            _safetyOverride = false;
            _status = $"Manual {percent}% target · {hardwareDetail} · {preflight.ControlTemperatureC.Value:0.#} °C";
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

        LenovoHardwareStatus preflight = _hardware.ReadStatus();
        if (!preflight.CanFanControl || !preflight.ControlTemperatureC.HasValue)
        {
            error = "Calibration needs the verified fan-control provider and temperature telemetry.";
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
            _safetyOverride = false;
            ClearPendingTransitionLocked();
            _characterizationRunning = true;
            _characterizationLevel = 7;
            _characterizationStatus = "Safety spin-up · EC step 7";
            _calibration.Clear();
            _unstableLevels.Clear();
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
            if (!_characterizationRunning || !_characterizationLevel.HasValue || _characterizationLevel.Value is < 1 or > 7)
            {
                error = "Start fan calibration first, then mark the first state you clearly hear.";
                return false;
            }

            _audibleFromLevel = _characterizationLevel.Value;
            _characterizationStatus = _characterizationLevel.Value == 7
                ? "Verified maximum marked as clearly audible"
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
            _characterizationRunning = false;
            _characterizationLevel = null;
            _characterizationStatus = "Calibration stopped · returning to Lenovo Auto";
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
                _safetyOverride = false;
                ClearPendingTransitionLocked();
            }
        }
        return success;
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool active;
            lock (_gate)
                active = _activeCurve is not null || _manualLevel.HasValue || _manualPercent.HasValue || _characterizationRunning;

            if (!active)
            {
                // Firmware Auto stays truly idle. ThinkControl wakes this loop only
                // while it actually owns a manual/curve state.
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
        lock (_gate)
        {
            curve = _activeCurve;
            manualLevel = _manualLevel;
            manualPercent = _manualPercent;
            characterizationRunning = _characterizationRunning;
        }

        if (characterizationRunning)
            return;
        if (!manualLevel.HasValue && !manualPercent.HasValue && curve is null)
            return;

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (!status.CanFanControl || !status.ControlTemperatureC.HasValue)
        {
            await SafeAutoHandoffAsync("Sensor or verified fan-control provider became unavailable", token).ConfigureAwait(false);
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
                _status = manualPercent.HasValue
                    ? $"Manual {manualPercent.Value}% target · {_appliedPercent ?? 0}% calibrated output · {raw:0.#} °C"
                    : $"Manual EC step {manualLevel!.Value} · ~{_appliedPercent ?? 0}% of verified maximum · {raw:0.#} °C";
            }
            return;
        }

        bool waitingForSafetyResume;
        lock (_gate) waitingForSafetyResume = _safetyOverride;
        if (waitingForSafetyResume)
        {
            if (!FanCurvePolicy.CanResumeAfterSafetyHandoff(raw))
            {
                lock (_gate) _status = $"Safety handoff · Lenovo firmware control · {raw:0.#} °C";
                return;
            }
            lock (_gate)
            {
                _safetyOverride = false;
                _appliedLevel = null;
                _appliedPercent = null;
                _curveTargetPercent = null;
                _smoothedTemperatureC = raw;
                ClearPendingTransitionLocked();
                _status = $"{curve!.Name} resumed after safety handoff";
            }
        }

        int? currentTarget;
        double smooth;
        lock (_gate)
        {
            // Preserve approximately the same thermal time constant as alpha.19's
            // 0.18/2s EMA while sampling only every four seconds.
            _smoothedTemperatureC = _smoothedTemperatureC.HasValue
                ? _smoothedTemperatureC.Value + 0.30 * (raw - _smoothedTemperatureC.Value)
                : raw;
            smooth = _smoothedTemperatureC.Value;
            currentTarget = _curveTargetPercent;
        }

        int requestedPercent = FanCurveGraphPolicy.ResolvePercent(curve!.Points, smooth, currentTarget);
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
                _status = DescribeCurveOutput(curve.Name, requestedPercent, desired, smooth);
            }
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool shouldWrite;
        lock (_gate)
        {
            shouldWrite = ShouldCommitTransitionLocked(currentState, desired.HardwareState, raw, now);
            if (!shouldWrite)
            {
                _status = $"{DescribeCurveOutput(curve.Name, requestedPercent, desired, smooth)} · stabilizing";
            }
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
            await SafeAutoHandoffAsync(writeError ?? "Fan output write failed", token, preserveCurve: true).ConfigureAwait(false);
            return;
        }

        lock (_gate)
        {
            _appliedLevel = desired.HardwareState;
            _appliedPercent = desired.EstimatedPercent;
            _curveTargetPercent = requestedPercent;
            _lastOutputChange = now;
            ClearPendingTransitionLocked();
            _status = DescribeCurveOutput(curve.Name, requestedPercent, desired, smooth);
        }
    }

    private bool ShouldCommitTransitionLocked(int? currentState, int desiredState, double rawTemperatureC, DateTimeOffset now)
    {
        if (!currentState.HasValue)
            return true;

        int delta = desiredState - currentState.Value;
        if (delta > 0 && (delta >= 2 || rawTemperatureC >= 82))
        {
            // Never make a meaningful cooling increase wait behind comfort-oriented
            // anti-hunting logic. Raw temperature, not the EMA, controls this escape.
            return true;
        }

        if (_pendingLevel != desiredState)
        {
            _pendingLevel = desiredState;
            _pendingLevelSince = now;
            return false;
        }

        TimeSpan dwell = delta > 0 ? MinimumUpshiftDwell : MinimumDownshiftDwell;
        if (now - _pendingLevelSince < dwell)
            return false;

        // The transition target survived the full dwell. _lastOutputChange is still
        // checked for downshifts so two cooling reductions cannot occur back-to-back.
        return delta > 0 || now - _lastOutputChange >= MinimumDownshiftDwell;
    }

    private void ClearPendingTransitionLocked()
    {
        _pendingLevel = null;
        _pendingLevelSince = DateTimeOffset.MinValue;
    }

    private static string DescribeCurveOutput(string name, int target, FanOutputMapping.State state, double temperature) =>
        $"{name} · {target}% target · ~{state.EstimatedPercent}% verified output · EC step {state.HardwareState} · {temperature:0.#} °C";

    private async Task SafeAutoHandoffAsync(string reason, CancellationToken token, bool preserveCurve = false)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { _hardware.ReturnFanToAuto(out _); }
        finally { _writeGate.Release(); }

        lock (_gate)
        {
            _manualLevel = null;
            _manualPercent = null;
            _appliedLevel = null;
            _appliedPercent = null;
            _curveTargetPercent = null;
            ClearPendingTransitionLocked();
            if (preserveCurve && _activeCurve is not null)
            {
                _safetyOverride = true;
                _status = reason + " · Lenovo firmware owns cooling temporarily";
            }
            else
            {
                _activeCurve = null;
                _safetyOverride = false;
                _smoothedTemperatureC = null;
                _status = reason + " · returned to Lenovo Auto";
            }
        }
    }

    private async Task CharacterizeAsync(CancellationToken token)
    {
        try
        {
            if (!await SetHardwareLevelSerializedAsync(7, token).ConfigureAwait(false))
                throw new InvalidOperationException("EC step 7 safety spin-up could not be verified.");
            await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);

            for (int state = 1; state <= 7; state++)
            {
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (!_characterizationRunning)
                        return;
                    _characterizationLevel = state;
                    _characterizationStatus = $"Testing EC step {state} of 7 · {state}/7";
                }

                LenovoHardwareStatus thermal = _hardware.ReadStatus();
                if (!thermal.ControlTemperatureC.HasValue || FanCurvePolicy.RequiresFirmwareSafetyHandoff(thermal.ControlTemperatureC.Value))
                    throw new InvalidOperationException("Temperature safety check handed control back to Lenovo firmware.");
                bool applied = await SetHardwareLevelSerializedAsync(state, token).ConfigureAwait(false);
                if (!applied)
                    throw new InvalidOperationException($"EC step {state} could not be verified.");

                // EC tachometer reads are deliberately sparse. The two samples are
                // separated enough to detect pulsing/unstable states without turning
                // calibration into a continuous low-level polling loop.
                await Task.Delay(TimeSpan.FromMilliseconds(4500), token).ConfigureAwait(false);
                LenovoHardwareStatus first = _hardware.ReadStatus();
                await Task.Delay(TimeSpan.FromMilliseconds(10200), token).ConfigureAwait(false);
                LenovoHardwareStatus second = _hardware.ReadStatus();

                FanLevelCalibrationSnapshot point = BuildCalibrationPoint(state, first.Fans, second.Fans);
                lock (_gate)
                {
                    _calibration.RemoveAll(existing => existing.Level == state);
                    _calibration.Add(point);
                    if (point.Stable) _unstableLevels.Remove(state); else _unstableLevels.Add(state);
                    string label = $"EC step {state}";
                    _characterizationStatus = point.Stable
                        ? $"{label}: stable · {_calibration.Count}/7 complete"
                        : $"{label}: RPM varies noticeably · {_calibration.Count}/7 complete";
                }
                SaveCalibration();
            }

            lock (_gate)
                _characterizationStatus = _unstableLevels.Count == 0
                    ? "Calibration complete · fan percentages now use measured RPM relative to verified EC step 7"
                    : $"Calibration complete · {_unstableLevels.Count} variable state(s) recorded and skipped when a safer higher state is available";
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (!_characterizationStatus.StartsWith("Calibration stopped", StringComparison.Ordinal))
                    _characterizationStatus = "Calibration cancelled";
            }
        }
        catch (Exception ex)
        {
            lock (_gate) _characterizationStatus = $"Calibration stopped safely · {ex.Message}";
        }
        finally
        {
            await ReturnHardwareToAutoSerializedAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_gate)
            {
                _characterizationRunning = false;
                _characterizationLevel = null;
                _activeCurve = null;
                _manualLevel = null;
                _manualPercent = null;
                _appliedLevel = null;
                _appliedPercent = null;
                _curveTargetPercent = null;
                _smoothedTemperatureC = null;
                _safetyOverride = false;
                ClearPendingTransitionLocked();
            }
            SaveCalibration();
        }
    }

    private FanOutputMapping.State ResolveOutputState(int targetPercent)
    {
        Dictionary<int, int> rpm = CalibrationRpmByState();
        IReadOnlyList<FanOutputMapping.State> states = FanOutputMapping.BuildStates(rpm);
        FanOutputMapping.State selected = states.First(state => state.EstimatedPercent >= Math.Clamp(targetPercent, 0, 100));

        // A variable normal state is avoided by moving upward, never downward. This
        // preserves the requested cooling floor while avoiding fan pulsing where the
        // characterization run proved a step unstable.
        HashSet<int> unstable;
        lock (_gate) unstable = new HashSet<int>(_unstableLevels);
        int index = selected.HardwareState - 1;
        while (index < states.Count - 1 && unstable.Contains(states[index].HardwareState))
            index++;
        selected = states[index];
        return selected;
    }

    private int EstimatePercentForState(int state)
    {
        IReadOnlyList<FanOutputMapping.State> states = FanOutputMapping.BuildStates(CalibrationRpmByState());
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
            ? $"~{state.EstimatedPercent}% calibrated output · EC step {state.HardwareState}"
            : null;
        return levelSuccess;
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
        IReadOnlyList<LenovoFanReading> first,
        IReadOnlyList<LenovoFanReading> second)
    {
        string[] ids = first.Select(fan => fan.Id)
            .Concat(second.Select(fan => fan.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fans = new List<FanCalibrationFanSnapshot>();
        foreach (string id in ids)
        {
            LenovoFanReading? a = first.FirstOrDefault(fan => string.Equals(fan.Id, id, StringComparison.OrdinalIgnoreCase));
            LenovoFanReading? b = second.FirstOrDefault(fan => string.Equals(fan.Id, id, StringComparison.OrdinalIgnoreCase));
            int[] rpms = new int?[] { a?.Rpm, b?.Rpm }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
            if (rpms.Length == 0)
                continue;

            int median = (int)Math.Round(rpms.Average());
            int spread = rpms.Max() - rpms.Min();
            bool stable = spread <= Math.Max(250, median * 0.12);
            fans.Add(new FanCalibrationFanSnapshot(id, a?.Label ?? b?.Label ?? "Fan", median, spread, stable));
        }

        bool pointStable = fans.Count > 0 && fans.All(fan => fan.Stable);
        return new FanLevelCalibrationSnapshot(level, fans, pointStable);
    }

    private void LoadCalibration()
    {
        try
        {
            if (!File.Exists(_calibrationPath))
                return;
            PersistedCalibration? stored = JsonSerializer.Deserialize<PersistedCalibration>(File.ReadAllText(_calibrationPath), JsonOptions);
            if (stored is null || !string.Equals(stored.MachineType, _hardware.Identity.MachineType, StringComparison.OrdinalIgnoreCase))
                return;

            _audibleFromLevel = stored.AudibleFromLevel is >= 1 and <= 8 ? stored.AudibleFromLevel : null;
            _calibration.Clear();
            _calibration.AddRange(stored.Levels ?? []);
            _unstableLevels.Clear();
            foreach (FanLevelCalibrationSnapshot level in _calibration.Where(level => !level.Stable))
                _unstableLevels.Add(level.Level);
            if (_calibration.Count > 0)
                _characterizationStatus = $"Loaded {_calibration.Count}/8 calibrated fan states";
        }
        catch { }
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

            string? folder = Path.GetDirectoryName(_calibrationPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(_calibrationPath, JsonSerializer.Serialize(
                new PersistedCalibration(_hardware.Identity.MachineType, audible, levels), JsonOptions));
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
        FanLevelCalibrationSnapshot[]? Levels);
}
