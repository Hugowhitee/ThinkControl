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
/// Sole owner of ThinkControl fan writes. Graph curves, manual requests and
/// characterization are serialized here so two callers can never fight over EC.
/// Lenovo Auto is always the fail-safe for missing telemetry, unsafe heat,
/// cancellation and service shutdown.
/// </summary>
internal sealed class FanSupervisor : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MinimumDownshiftDwell = TimeSpan.FromSeconds(8);
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
    private double? _smoothedTemperatureC;
    private bool _safetyOverride;
    private string _status = "Lenovo firmware owns fan control";
    private DateTimeOffset _lastOutputChange = DateTimeOffset.MinValue;

    private bool _characterizationRunning;
    private int? _characterizationLevel;
    private string _characterizationStatus = "Not characterized yet";
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
                    8,
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
            error = "Fan profile must be Lenovo Auto, Quiet, Balanced, Max cooling or a validated graph profile.";
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
            _smoothedTemperatureC = status.ControlTemperatureC!.Value;
            _safetyOverride = false;
            _status = $"{name} fan curve active · 0–100% target mapped to verified X9 states";
            _lastOutputChange = DateTimeOffset.MinValue;
        }
        SignalControlWake();
        return true;
    }

    // Compatibility for alpha.16 development builds that stored six temperature
    // thresholds. Convert once to an 8-point graph rather than maintaining a second
    // runtime fan-curve implementation.
    internal bool SetCustomCurve(IReadOnlyList<double>? thresholds, out string? error)
    {
        error = null;
        if (!FanCurvePolicy.TryValidateCustomThresholds(thresholds, out double[] normalized, out error))
            return false;

        FanCurvePoint[] points =
        [
            new(35, 0),
            new(normalized[0], 16),
            new(normalized[1], 32),
            new(normalized[2], 48),
            new(normalized[3], 64),
            new(normalized[4], 80),
            new(normalized[5], 94),
            new(92, 100)
        ];
        Array.Sort(points, (a, b) => a.TemperatureC.CompareTo(b.TemperatureC));
        // The migration input can place its old final threshold too close to 92 °C.
        // When that happens use the known-good factory Balanced graph instead.
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
                error = "Fan characterization is running. Stop or finish it before selecting a fan profile.";
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

        lock (_gate)
        {
            _activeCurve = null;
            _manualLevel = level;
            _manualPercent = null;
            _appliedLevel = level;
            _appliedPercent = NominalPercentForStep(level);
            _smoothedTemperatureC = preflight.ControlTemperatureC!.Value;
            _safetyOverride = false;
            _status = $"Manual EC step {level} · {preflight.ControlTemperatureC.Value:0.#} °C control temperature";
            _lastOutputChange = DateTimeOffset.UtcNow;
        }
        SignalControlWake();
        return true;
    }

    internal bool SetManualPercent(int percent, out string? error)
    {
        error = null;
        if (percent is < 0 or > 100)
        {
            error = "Manual fan output must be between 0% and 100%.";
            return false;
        }
        if (!CanEnterManagedCooling(out LenovoHardwareStatus? preflight, out error) || preflight is null)
            return false;

        if (!SetHardwarePercentSerialized(percent, out int step, out bool fullSpeed, out string? detail, out error))
            return false;

        lock (_gate)
        {
            _activeCurve = null;
            _manualLevel = null;
            _manualPercent = percent;
            _appliedLevel = fullSpeed ? 8 : step;
            _appliedPercent = percent;
            _smoothedTemperatureC = preflight.ControlTemperatureC!.Value;
            _safetyOverride = false;
            _status = $"Manual {percent}% · {detail} · {preflight.ControlTemperatureC.Value:0.#} °C";
            _lastOutputChange = DateTimeOffset.UtcNow;
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
                _smoothedTemperatureC = null;
                _safetyOverride = false;
                _status = "Lenovo firmware owns fan control";
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
                error = "Fan characterization is already running.";
                return false;
            }
        }

        LenovoHardwareStatus preflight = _hardware.ReadStatus();
        if (!preflight.CanFanControl || !preflight.ControlTemperatureC.HasValue)
        {
            error = "Characterization needs the verified fan-control provider and temperature telemetry.";
            return false;
        }
        if (preflight.ControlTemperatureC.Value >= 75)
        {
            error = "Let the laptop cool below 75 °C before characterizing the fan states.";
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
            _smoothedTemperatureC = null;
            _safetyOverride = false;
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
            if (!_characterizationRunning || !_characterizationLevel.HasValue || _characterizationLevel.Value is < 1 or > 8)
            {
                error = "Start fan characterization first, then mark the first state you clearly hear.";
                return false;
            }

            _audibleFromLevel = _characterizationLevel.Value;
            _characterizationStatus = _characterizationLevel.Value == 8
                ? "Full speed marked as clearly audible"
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
            _characterizationStatus = "Characterization stopped · returning to Lenovo Auto";
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
                _smoothedTemperatureC = null;
                _safetyOverride = false;
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
                // Auto/firmware mode is event driven: do not wake every two seconds
                // merely to discover that ThinkControl does not own the fan.
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
                    ? $"Manual {manualPercent.Value}% · {raw:0.#} °C control temperature"
                    : $"Manual EC step {manualLevel!.Value} · {raw:0.#} °C control temperature";
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
                _smoothedTemperatureC = raw;
                _status = $"{curve!.Name} resumed after safety handoff";
            }
        }

        int? currentPercent;
        double smooth;
        lock (_gate)
        {
            _smoothedTemperatureC = _smoothedTemperatureC.HasValue
                ? _smoothedTemperatureC.Value + 0.18 * (raw - _smoothedTemperatureC.Value)
                : raw;
            smooth = _smoothedTemperatureC.Value;
            currentPercent = _appliedPercent;
        }

        int requestedPercent = FanCurveGraphPolicy.ResolvePercent(curve!.Points, smooth, currentPercent);
        bool shouldWrite;
        lock (_gate)
        {
            shouldWrite = !_appliedPercent.HasValue || requestedPercent > _appliedPercent.Value ||
                (requestedPercent < _appliedPercent.Value && DateTimeOffset.UtcNow - _lastOutputChange >= MinimumDownshiftDwell);
        }
        if (!shouldWrite)
            return;

        bool writeSuccess;
        int appliedStep;
        bool fullSpeed;
        string? detail;
        string? writeError;
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            writeSuccess = _hardware.SetFanPercent(requestedPercent, out appliedStep, out fullSpeed, out detail, out writeError);
        }
        finally
        {
            _writeGate.Release();
        }

        if (!writeSuccess)
        {
            await SafeAutoHandoffAsync(writeError ?? "Fan output write failed", token, preserveCurve: true).ConfigureAwait(false);
            return;
        }

        lock (_gate)
        {
            _appliedLevel = fullSpeed ? 8 : appliedStep;
            _appliedPercent = requestedPercent;
            _lastOutputChange = DateTimeOffset.UtcNow;
            _status = $"{curve.Name} · {requestedPercent}% · {(fullSpeed ? "full speed" : $"EC step {appliedStep}")} · {smooth:0.#} °C";
        }
    }

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

            for (int state = 1; state <= 8; state++)
            {
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (!_characterizationRunning)
                        return;
                    _characterizationLevel = state;
                    _characterizationStatus = state == 8
                        ? "Testing full speed · 8/8"
                        : $"Testing EC step {state} of 7 · {state}/8";
                }

                LenovoHardwareStatus thermal = _hardware.ReadStatus();
                if (!thermal.ControlTemperatureC.HasValue || FanCurvePolicy.RequiresFirmwareSafetyHandoff(thermal.ControlTemperatureC.Value))
                    throw new InvalidOperationException("Temperature safety check handed control back to Lenovo firmware.");
                if (state == 8 && thermal.ControlTemperatureC.Value >= 85)
                    throw new InvalidOperationException("Full-speed calibration was skipped because the system is already hot; Lenovo Auto restored.");

                bool applied = state == 8
                    ? await SetHardwarePercentSerializedAsync(100, token).ConfigureAwait(false)
                    : await SetHardwareLevelSerializedAsync(state, token).ConfigureAwait(false);
                if (!applied)
                    throw new InvalidOperationException(state == 8 ? "Full-speed state could not be verified." : $"EC step {state} could not be verified.");

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
                    string label = state == 8 ? "Full speed" : $"EC step {state}";
                    _characterizationStatus = point.Stable
                        ? $"{label}: stable · {_calibration.Count}/8 complete"
                        : $"{label}: RPM varies noticeably · {_calibration.Count}/8 complete";
                }
                SaveCalibration();
            }

            lock (_gate)
                _characterizationStatus = _unstableLevels.Count == 0
                    ? "Characterization complete · all measured fan states stable"
                    : $"Characterization complete · {_unstableLevels.Count} variable state(s) recorded";
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (!_characterizationStatus.StartsWith("Characterization stopped", StringComparison.Ordinal))
                    _characterizationStatus = "Characterization cancelled";
            }
        }
        catch (Exception ex)
        {
            lock (_gate) _characterizationStatus = $"Characterization stopped safely · {ex.Message}";
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
                _smoothedTemperatureC = null;
                _safetyOverride = false;
            }
            SaveCalibration();
        }
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

    private bool SetHardwarePercentSerialized(int percent, out int step, out bool fullSpeed, out string? detail, out string? error)
    {
        step = 0;
        fullSpeed = false;
        detail = null;
        error = null;
        if (!_writeGate.Wait(SyncWriteTimeout))
        {
            error = "Fan-control writer is busy.";
            return false;
        }
        try { return _hardware.SetFanPercent(percent, out step, out fullSpeed, out detail, out error); }
        finally { _writeGate.Release(); }
    }

    private async Task<bool> SetHardwareLevelSerializedAsync(int level, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { return _hardware.SetFanLevel(level, out _); }
        finally { _writeGate.Release(); }
    }

    private async Task<bool> SetHardwarePercentSerializedAsync(int percent, CancellationToken token)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { return _hardware.SetFanPercent(percent, out _, out _, out _, out _); }
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
        string[] ids = first.Select(f => f.Id)
            .Concat(second.Select(f => f.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fans = new List<FanCalibrationFanSnapshot>();
        foreach (string id in ids)
        {
            LenovoFanReading? a = first.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
            LenovoFanReading? b = second.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
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
                _characterizationStatus = $"Loaded {_calibration.Count}/8 characterized fan states";
        }
        catch
        {
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

            string? folder = Path.GetDirectoryName(_calibrationPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(_calibrationPath, JsonSerializer.Serialize(
                new PersistedCalibration(_hardware.Identity.MachineType, audible, levels), JsonOptions));
        }
        catch
        {
        }
    }

    private static int NominalPercentForStep(int level) => level switch
    {
        <= 1 => 0,
        2 => 16,
        3 => 32,
        4 => 48,
        5 => 64,
        6 => 80,
        _ => 99
    };

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
