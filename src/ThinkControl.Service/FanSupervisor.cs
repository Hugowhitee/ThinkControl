using System.Text.Json;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.Hardware.Lenovo;

namespace ThinkControl.Service;

internal sealed record CoolingSupervisorSnapshot(
    string Profile,
    int? AppliedLevel,
    double? SmoothedTemperatureC,
    string Status,
    bool SafetyOverride,
    FanCharacterizationSnapshot Characterization);

/// <summary>
/// Sole owner of ThinkControl manual fan writes. UI requests, custom curves and
/// characterization are serialized here so two callers can never fight over EC.
/// Firmware Auto is the fail-safe state for missing telemetry, unsafe heat,
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
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly string _calibrationPath;

    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _characterizationCts;
    private Task? _loopTask;
    private Task? _characterizationTask;

    private CoolingProfile _profile = CoolingProfile.LenovoAuto;
    private int? _manualLevel;
    private int? _appliedLevel;
    private double? _smoothedTemperatureC;
    private bool _safetyOverride;
    private string _status = "Lenovo firmware owns fan control";
    private DateTimeOffset _lastLevelChange = DateTimeOffset.MinValue;

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
            string profile = _manualLevel.HasValue ? $"Manual level {_manualLevel.Value}" : DisplayName(_profile);
            return new CoolingSupervisorSnapshot(
                profile,
                _appliedLevel,
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
        if (!TryParseProfile(raw, out CoolingProfile requested))
        {
            error = "Cooling profile must be Lenovo Auto, Silent, Normal or Cool.";
            return false;
        }
        if (requested == CoolingProfile.LenovoAuto)
            return ReturnToAuto(out error);

        lock (_gate)
        {
            if (_characterizationRunning)
            {
                error = "Fan characterization is running. Stop or finish it before selecting a cooling profile.";
                return false;
            }
        }

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (!status.CanFanControl || !status.ControlTemperatureC.HasValue)
        {
            error = "Custom cooling requires the verified fan-control provider and a valid control-temperature sensor.";
            return false;
        }
        if (FanCurvePolicy.RequiresFirmwareSafetyHandoff(status.ControlTemperatureC.Value))
        {
            ReturnHardwareToAutoSerialized(out _);
            error = "The system is too hot to enter custom cooling. Lenovo firmware keeps control until temperature falls.";
            return false;
        }

        lock (_gate)
        {
            _profile = requested;
            _manualLevel = null;
            _appliedLevel = null;
            _smoothedTemperatureC = status.ControlTemperatureC.Value;
            _safetyOverride = false;
            _status = $"{DisplayName(requested)} cooling active";
            _lastLevelChange = DateTimeOffset.MinValue;
        }
        return true;
    }

    internal bool SetManualLevel(int level, out string? error)
    {
        error = null;
        if (level is < 1 or > 7)
        {
            error = "Fan level must be between 1 and 7.";
            return false;
        }
        lock (_gate)
        {
            if (_characterizationRunning)
            {
                error = "Fan characterization is running.";
                return false;
            }
        }

        if (!SetHardwareLevelSerialized(level, out error))
            return false;

        lock (_gate)
        {
            _profile = CoolingProfile.LenovoAuto;
            _manualLevel = level;
            _appliedLevel = level;
            _smoothedTemperatureC = null;
            _safetyOverride = false;
            _status = $"Manual fan level {level}";
            _lastLevelChange = DateTimeOffset.UtcNow;
        }
        return true;
    }

    internal bool ReturnToAuto(out string? error)
    {
        bool success = ReturnHardwareToAutoSerialized(out error);
        if (!success && !_hardware.Identity.IsVerifiedX9)
        {
            // No ThinkControl manual provider means firmware already owns cooling.
            success = true;
            error = null;
        }

        if (success)
        {
            lock (_gate)
            {
                _profile = CoolingProfile.LenovoAuto;
                _manualLevel = null;
                _appliedLevel = null;
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
            error = "Let the laptop cool below 75 °C before characterizing the fan levels.";
            return false;
        }

        lock (_gate)
        {
            _characterizationCts?.Dispose();
            _characterizationCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            _profile = CoolingProfile.LenovoAuto;
            _manualLevel = null;
            _appliedLevel = null;
            _smoothedTemperatureC = null;
            _safetyOverride = false;
            _characterizationRunning = true;
            _characterizationLevel = 7;
            _characterizationStatus = "Safety spin-up · level 7";
            _calibration.Clear();
            _unstableLevels.Clear();
            CancellationToken token = _characterizationCts.Token;
            _characterizationTask = Task.Run(() => CharacterizeAsync(token), token);
        }
        return true;
    }

    internal bool MarkCurrentLevelAudible(out string? error)
    {
        error = null;
        lock (_gate)
        {
            if (!_characterizationRunning || !_characterizationLevel.HasValue || _characterizationLevel.Value is < 1 or > 7)
            {
                error = "Start fan characterization first, then mark the first level you clearly hear.";
                return false;
            }

            _audibleFromLevel = _characterizationLevel.Value;
            _characterizationStatus = $"Level {_characterizationLevel.Value} marked as clearly audible";
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
                _profile = CoolingProfile.LenovoAuto;
                _manualLevel = null;
                _appliedLevel = null;
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
        CoolingProfile profile;
        lock (_gate)
        {
            profile = _profile;
            if (_manualLevel.HasValue || _characterizationRunning || profile == CoolingProfile.LenovoAuto)
                return;
        }

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (!status.CanFanControl || !status.ControlTemperatureC.HasValue)
        {
            await SafeAutoHandoffAsync("Sensor or verified fan-control provider became unavailable", token).ConfigureAwait(false);
            return;
        }

        double raw = status.ControlTemperatureC.Value;
        bool waitingForSafetyResume;
        lock (_gate)
            waitingForSafetyResume = _safetyOverride;

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
                _smoothedTemperatureC = raw;
                _status = $"{DisplayName(profile)} cooling resumed after safety handoff";
            }
        }

        if (FanCurvePolicy.RequiresFirmwareSafetyHandoff(raw))
        {
            await SafeAutoHandoffAsync($"Safety handoff at {raw:0.#} °C", token, preserveProfile: true).ConfigureAwait(false);
            return;
        }

        int? current;
        int? audible;
        HashSet<int> unstable;
        double smooth;
        lock (_gate)
        {
            _smoothedTemperatureC = _smoothedTemperatureC.HasValue
                ? _smoothedTemperatureC.Value + 0.18 * (raw - _smoothedTemperatureC.Value)
                : raw;
            smooth = _smoothedTemperatureC.Value;
            current = _appliedLevel;
            audible = _audibleFromLevel;
            unstable = new HashSet<int>(_unstableLevels);
        }

        int requested = FanCurvePolicy.ResolveLevel(profile, smooth, current);
        requested = FanCurvePolicy.PreferStableLevel(requested, unstable);

        // Acoustic calibration is preference-only: it may suppress an audible
        // level at low temperature, but above 72 °C the thermal curve always wins.
        if (profile == CoolingProfile.Silent && audible is >= 2 and <= 7 && smooth < 72 && requested >= audible.Value)
            requested = Math.Max(1, audible.Value - 1);

        bool shouldWrite;
        lock (_gate)
        {
            shouldWrite = !_appliedLevel.HasValue || requested > _appliedLevel.Value ||
                (requested < _appliedLevel.Value && DateTimeOffset.UtcNow - _lastLevelChange >= MinimumDownshiftDwell);
        }
        if (!shouldWrite)
            return;

        bool writeSuccess;
        string? writeError;
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            writeSuccess = _hardware.SetFanLevel(requested, out writeError);
        }
        finally
        {
            _writeGate.Release();
        }

        if (!writeSuccess)
        {
            await SafeAutoHandoffAsync(writeError ?? "Fan write failed", token).ConfigureAwait(false);
            return;
        }

        lock (_gate)
        {
            _appliedLevel = requested;
            _lastLevelChange = DateTimeOffset.UtcNow;
            _status = $"{DisplayName(profile)} · level {requested} · {smooth:0.#} °C control temperature";
        }
    }

    private async Task SafeAutoHandoffAsync(string reason, CancellationToken token, bool preserveProfile = false)
    {
        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try { _hardware.ReturnFanToAuto(out _); }
        finally { _writeGate.Release(); }

        lock (_gate)
        {
            _manualLevel = null;
            _appliedLevel = null;
            if (preserveProfile)
            {
                _safetyOverride = true;
                _status = reason + " · Lenovo firmware owns cooling temporarily";
            }
            else
            {
                _profile = CoolingProfile.LenovoAuto;
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
                throw new InvalidOperationException("Level 7 safety spin-up could not be verified.");
            await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);

            for (int level = 1; level <= 7; level++)
            {
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (!_characterizationRunning)
                        return;
                    _characterizationLevel = level;
                    _characterizationStatus = $"Testing level {level} of 7 · listen and mark it if clearly audible";
                }

                LenovoHardwareStatus thermal = _hardware.ReadStatus();
                if (!thermal.ControlTemperatureC.HasValue || thermal.ControlTemperatureC.Value >= 94)
                    throw new InvalidOperationException("Temperature safety check handed control back to Lenovo firmware.");

                if (!await SetHardwareLevelSerializedAsync(level, token).ConfigureAwait(false))
                    throw new InvalidOperationException($"Level {level} could not be verified.");

                // The X9 EC tachometer is deliberately rate-limited. Two readings
                // separated by >10 s avoid hammering EC while still detecting the
                // pulsing/variable levels reported by other X9 fan tools.
                await Task.Delay(TimeSpan.FromMilliseconds(4500), token).ConfigureAwait(false);
                LenovoHardwareStatus first = _hardware.ReadStatus();
                await Task.Delay(TimeSpan.FromMilliseconds(10200), token).ConfigureAwait(false);
                LenovoHardwareStatus second = _hardware.ReadStatus();

                FanLevelCalibrationSnapshot point = BuildCalibrationPoint(level, first.Fans, second.Fans);
                lock (_gate)
                {
                    _calibration.RemoveAll(existing => existing.Level == level);
                    _calibration.Add(point);
                    if (point.Stable) _unstableLevels.Remove(level); else _unstableLevels.Add(level);
                    _characterizationStatus = point.Stable
                        ? $"Level {level}: stable · {_calibration.Count}/7 complete"
                        : $"Level {level}: RPM varies noticeably · {_calibration.Count}/7 complete";
                }
                SaveCalibration();
            }

            lock (_gate)
                _characterizationStatus = _unstableLevels.Count == 0
                    ? "Characterization complete · all measured levels stable"
                    : $"Characterization complete · {_unstableLevels.Count} unstable level(s) will be avoided when thermally safe";
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
                _profile = CoolingProfile.LenovoAuto;
                _manualLevel = null;
                _appliedLevel = null;
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

            _audibleFromLevel = stored.AudibleFromLevel is >= 1 and <= 7 ? stored.AudibleFromLevel : null;
            _calibration.Clear();
            _calibration.AddRange(stored.Levels ?? []);
            _unstableLevels.Clear();
            foreach (FanLevelCalibrationSnapshot level in _calibration.Where(level => !level.Stable))
                _unstableLevels.Add(level.Level);
            if (_calibration.Count > 0)
                _characterizationStatus = $"Loaded {_calibration.Count}/7 characterized fan levels";
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

    private static bool TryParseProfile(string? raw, out CoolingProfile profile)
    {
        string normalized = raw?.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() ?? string.Empty;
        profile = normalized switch
        {
            "silent" => CoolingProfile.Silent,
            "normal" => CoolingProfile.Normal,
            "cool" => CoolingProfile.Cool,
            "lenovoauto" or "auto" => CoolingProfile.LenovoAuto,
            _ => CoolingProfile.LenovoAuto
        };
        return normalized is "silent" or "normal" or "cool" or "lenovoauto" or "auto";
    }

    private static string DisplayName(CoolingProfile profile) => profile switch
    {
        CoolingProfile.Silent => "Silent",
        CoolingProfile.Normal => "Normal",
        CoolingProfile.Cool => "Cool",
        _ => "Lenovo Auto"
    };

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _characterizationCts?.Cancel(); } catch { }
        try { _runCts?.Cancel(); } catch { }
        _disposeCts.Cancel();

        // The shutdown path gets one serialized chance to return ownership to
        // firmware; the hardware controller has a second defensive Auto fallback.
        try { ReturnHardwareToAutoSerialized(out _); } catch { }
        try { _characterizationTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }

        _characterizationCts?.Dispose();
        _runCts?.Dispose();
        _writeGate.Dispose();
        _disposeCts.Dispose();
    }

    private sealed record PersistedCalibration(
        string MachineType,
        int? AudibleFromLevel,
        FanLevelCalibrationSnapshot[]? Levels);
}
