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

internal sealed class FanSupervisor : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MinimumDownshiftDwell = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly LenovoHardwareController _hardware;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly string _calibrationPath;

    private CoolingProfile _profile = CoolingProfile.LenovoAuto;
    private int? _manualLevel;
    private int? _appliedLevel;
    private double? _smoothedTemperatureC;
    private bool _safetyOverride;
    private string _status = "Lenovo firmware owns fan control";
    private DateTimeOffset _lastLevelChange = DateTimeOffset.MinValue;
    private Task? _loopTask;
    private Task? _characterizationTask;
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
        if (_loopTask is not null)
            return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        CancellationToken token = linked.Token;
        _loopTask = Task.Run(() => LoopAsync(token), token);
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

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (!status.CanFanControl || !status.ControlTemperatureC.HasValue)
        {
            error = "Custom cooling requires the verified fan-control provider and a valid control-temperature sensor.";
            return false;
        }

        if (FanCurvePolicy.RequiresFirmwareSafetyHandoff(status.ControlTemperatureC.Value))
        {
            _hardware.ReturnFanToAuto(out _);
            error = "The system is too hot to enter custom cooling. Lenovo firmware keeps control until temperature falls.";
            return false;
        }

        lock (_gate)
        {
            if (_characterizationRunning)
            {
                error = "Fan characterization is running. Stop or finish it before selecting a cooling profile.";
                return false;
            }

            _profile = requested;
            _manualLevel = null;
            _appliedLevel = null;
            _smoothedTemperatureC = status.ControlTemperatureC;
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

        if (!_hardware.SetFanLevel(level, out error))
            return false;

        lock (_gate)
        {
            _profile = CoolingProfile.LenovoAuto;
            _manualLevel = level;
            _appliedLevel = level;
            _safetyOverride = false;
            _status = $"Manual fan level {level}";
            _lastLevelChange = DateTimeOffset.UtcNow;
        }
        return true;
    }

    internal bool ReturnToAuto(out string? error)
    {
        bool success = _hardware.ReturnFanToAuto(out error);
        if (!success && !_hardware.Identity.IsVerifiedX9)
        {
            // On unsupported direct-control devices firmware already owns the fan.
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
            _profile = CoolingProfile.LenovoAuto;
            _manualLevel = null;
            _appliedLevel = null;
            _safetyOverride = false;
            _characterizationRunning = true;
            _characterizationLevel = 7;
            _characterizationStatus = "Safety spin-up · level 7";
            _calibration.Clear();
            _unstableLevels.Clear();
        }

        _characterizationTask = Task.Run(() => CharacterizeAsync(_disposeCts.Token));
        return true;
    }

    internal bool MarkCurrentLevelAudible(out string? error)
    {
        error = null;
        lock (_gate)
        {
            if (!_characterizationRunning || _characterizationLevel is not >= 1 or > 7)
            {
                error = "Start fan characterization first, then mark the first level you clearly hear.";
                return false;
            }

            _audibleFromLevel = _characterizationLevel;
            _characterizationStatus = $"Level {_characterizationLevel} marked as clearly audible";
        }
        SaveCalibration();
        return true;
    }

    internal bool StopCharacterization(out string? error)
    {
        lock (_gate)
        {
            if (!_characterizationRunning)
            {
                error = null;
                return true;
            }
            _characterizationRunning = false;
            _characterizationLevel = null;
            _characterizationStatus = "Characterization stopped";
        }
        return ReturnToAuto(out error);
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
        bool characterization;
        lock (_gate)
        {
            profile = _profile;
            characterization = _characterizationRunning;
            if (_manualLevel.HasValue || characterization || profile == CoolingProfile.LenovoAuto)
                return;
        }

        LenovoHardwareStatus status = _hardware.ReadStatus();
        if (!status.CanFanControl || !status.ControlTemperatureC.HasValue)
        {
            await SafeAutoHandoffAsync("Sensor or verified fan-control provider became unavailable", token).ConfigureAwait(false);
            return;
        }

        double raw = status.ControlTemperatureC.Value;
        lock (_gate)
        {
            if (_safetyOverride)
            {
                if (!FanCurvePolicy.CanResumeAfterSafetyHandoff(raw))
                {
                    _status = $"Safety handoff · Lenovo firmware control · {raw:0.#} °C";
                    return;
                }
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
        double smooth;
        int? audible;
        HashSet<int> unstable;
        lock (_gate)
        {
            // 2 s ticks with roughly a 10 s time constant: rises are responsive
            // enough for a laptop, but noisy one-sample spikes do not flap levels.
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

        // An acoustic preference may hold Silent below the first clearly audible
        // step only while thermals are comfortably low. Above 72 °C the thermal
        // curve always wins; user preference can never become a safety cap.
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

        await _writeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!_hardware.SetFanLevel(requested, out string? error))
            {
                await SafeAutoHandoffAsync(error ?? "Fan write failed", token).ConfigureAwait(false);
                return;
            }

            lock (_gate)
            {
                _appliedLevel = requested;
                _lastLevelChange = DateTimeOffset.UtcNow;
                _status = $"{DisplayName(profile)} · level {requested} · {smooth:0.#} °C control temperature";
            }
        }
        finally
        {
            _writeGate.Release();
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
            if (!_hardware.SetFanLevel(7, out string? spinError))
                throw new InvalidOperationException(spinError ?? "Level 7 safety spin-up failed.");
            await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);

            for (int level = 1; level <= 7; level++)
            {
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

                if (!_hardware.SetFanLevel(level, out string? setError))
                    throw new InvalidOperationException(setError ?? $"Level {level} could not be applied.");

                // The verified EC tachometer is deliberately rate-limited in the
                // normal status path. Two readings separated by >10 s give us a
                // low-disturbance stability sample rather than hammering EC ports.
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
            lock (_gate) _characterizationStatus = "Characterization cancelled";
        }
        catch (Exception ex)
        {
            lock (_gate) _characterizationStatus = $"Characterization stopped safely · {ex.Message}";
        }
        finally
        {
            _hardware.ReturnFanToAuto(out _);
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

    private static FanLevelCalibrationSnapshot BuildCalibrationPoint(
        int level,
        IReadOnlyList<LenovoFanReading> first,
        IReadOnlyList<LenovoFanReading> second)
    {
        string[] ids = first.Select(f => f.Id).Concat(second.Select(f => f.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var fans = new List<FanCalibrationFanSnapshot>();
        foreach (string id in ids)
        {
            LenovoFanReading? a = first.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
            LenovoFanReading? b = second.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
            int[] rpms = new[] { a?.Rpm, b?.Rpm }.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            if (rpms.Length == 0)
                continue;

            int median = (int)Math.Round(rpms.Average());
            int spread = rpms.Max() - rpms.Min();
            bool stable = spread <= Math.Max(250, median * 0.12);
            fans.Add(new FanCalibrationFanSnapshot(id, a?.Label ?? b?.Label ?? "Fan", median, spread, stable));
        }

        bool pointStable = fans.Count > 0 && fans.All(f => f.Stable);
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
                levels = _calibration.OrderBy(p => p.Level).ToArray();
                audible = _audibleFromLevel;
            }
            string? folder = Path.GetDirectoryName(_calibrationPath);
            if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
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
        _disposeCts.Cancel();
        try { _hardware.ReturnFanToAuto(out _); } catch { }
        try { _characterizationTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _writeGate.Dispose();
        _disposeCts.Dispose();
    }

    private sealed record PersistedCalibration(
        string MachineType,
        int? AudibleFromLevel,
        FanLevelCalibrationSnapshot[]? Levels);
}
