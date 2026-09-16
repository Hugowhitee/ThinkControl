using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

internal sealed record FanCalibrationUiState(
    bool Relevant,
    bool Running,
    bool Ready,
    int CompletedLevels,
    int TotalLevels,
    string Status)
{
    internal static FanCalibrationUiState None { get; } = new(false, false, false, 0, 0, string.Empty);
    internal bool Required => Relevant && !Ready;
}

public partial class App
{
    private static readonly TimeSpan CoolingAutoRestoreRetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CoolingStartupSettleDelay = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan[] CoolingColdStartProbeDelays =
    [
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(650),
        TimeSpan.FromSeconds(1.1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3.5),
        TimeSpan.FromSeconds(5)
    ];

    private readonly CancellationTokenSource _coolingLifetimeCts = new();
    private readonly SemaphoreSlim _coolingWriteGate = new(1, 1);
    private bool _coolingPreferenceRestoreAttempted;
    private bool _coolingPreferenceRestoreInFlight;
    private int _coolingThermalBaselineReady;
    private int _coolingShutdownPrepared;
    private DateTimeOffset _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
    private int _coolingSelectionGeneration;
    private FanProfileCatalog? _fanProfiles;
    private FanCalibrationUiState _fanCalibrationState = FanCalibrationUiState.None;

    public FanProfileCatalog FanProfiles => _fanProfiles ??= new FanProfileCatalog(UserSettings);
    internal FanCalibrationUiState FanCalibrationState => _fanCalibrationState;
    internal event EventHandler? FanCalibrationStateChanged;

    internal void MarkCoolingThermalBaselineReady() =>
        Volatile.Write(ref _coolingThermalBaselineReady, 1);

    private bool CoolingThermalBaselineReady =>
        Volatile.Read(ref _coolingThermalBaselineReady) != 0;

    private bool UsesFirmwareCoolingPolicy =>
        State.CanFanControl && string.Equals(State.FanControlKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal);

    private void InitializeCoolingCoordinator()
    {
        HardwareClient.StatusObserved += CoolingStatusObserved;
        Exit += (_, _) =>
        {
            HardwareClient.StatusObserved -= CoolingStatusObserved;
            _coolingLifetimeCts.Cancel();

            // Normal explicit Quit calls PrepareCoolingForApplicationExitAsync before
            // WPF shutdown. This Exit hook is only a last-chance path for external/
            // session shutdown. Never block the UI thread waiting for an async restore
            // continuation: take the gate only if it is immediately available.
            if (Volatile.Read(ref _coolingShutdownPrepared) != 0 || UsesFirmwareCoolingPolicy)
                return;
            if (!_coolingWriteGate.Wait(0))
                return;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(900));
                HardwareClient.ReturnFanToAutoAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch
            {
            }
            finally
            {
                _coolingWriteGate.Release();
            }
        };
    }

    private void CoolingStatusObserved(object? sender, ServiceResponse? response)
    {
        void Apply()
        {
            FanCalibrationUiState next = ResolveFanCalibrationState(response);
            if (Equals(next, _fanCalibrationState))
                return;
            _fanCalibrationState = next;
            FanCalibrationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }

    internal async Task PrepareCoolingForApplicationExitAsync()
    {
        if (Interlocked.Exchange(ref _coolingShutdownPrepared, 1) != 0)
            return;

        HardwareClient.StatusObserved -= CoolingStatusObserved;
        _coolingLifetimeCts.Cancel();

        // Firmware-policy profiles are durable preferences owned by the service.
        // Direct/manual writers must hand ownership back to Lenovo Auto before the
        // UI actually shuts down.
        if (UsesFirmwareCoolingPolicy)
            return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        bool gateHeld = false;
        try
        {
            await _coolingWriteGate.WaitAsync(timeout.Token);
            gateHeld = true;
            await HardwareClient.ReturnFanToAutoAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (gateHeld)
                _coolingWriteGate.Release();
        }
    }

    private static FanCalibrationUiState ResolveFanCalibrationState(ServiceResponse? response)
    {
        if (response?.Success != true || response.Capabilities is not HardwareCapabilitySnapshot capabilities ||
            !capabilities.FanCalibrationSupported)
        {
            return FanCalibrationUiState.None;
        }

        FanCharacterizationSnapshot? characterization = response.Telemetry?.FanCharacterization;
        bool running = characterization?.Running == true;
        int completed = characterization?.CompletedLevels ?? 0;
        int total = Math.Max(1, characterization?.TotalLevels ?? 1);
        bool ready = !running && !capabilities.FanCalibrationRequired;
        string status = characterization?.Status ?? (ready
            ? "Fan calibration is ready."
            : "The active fan provider requires calibration before percentage targets and curves are enabled.");
        return new FanCalibrationUiState(true, running, ready, completed, total, status);
    }

    internal async Task<bool> SetCoolingProfileAsync(string profile)
    {
        // Increment before waiting: this immediately invalidates any older startup
        // restore/reassert that may currently own the serialized write gate. If the
        // older write already started, this user request runs immediately after it
        // and therefore becomes the final physical state.
        int generation = Interlocked.Increment(ref _coolingSelectionGeneration);
        await _coolingWriteGate.WaitAsync();
        try
        {
            return await SetCoolingProfileCoreAsync(profile, generation);
        }
        finally
        {
            _coolingWriteGate.Release();
        }
    }

    private async Task<bool> SetCoolingProfileCoreAsync(string profile, int generation)
    {
        string raw = profile?.Trim() ?? string.Empty;
        if (raw.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync(_coolingLifetimeCts.Token);
            if (auto?.Success != true)
            {
                State.HardwareAccess = auto?.Error ?? "Firmware Auto unavailable";
                return false;
            }
            UserSettings.Update(settings => settings with { CoolingProfile = "Lenovo Auto" });
            State.CoolingProfile = "Lenovo Auto";
            _coolingPreferenceRestoreAttempted = true;
            _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
            return true;
        }

        if (FanCalibrationState.Required)
        {
            State.HardwareAccess = FanCalibrationState.Running
                ? "Fan calibration currently owns the active fan provider. Finish or stop calibration before selecting a profile."
                : "The active fan provider requires calibration before percentage-based fan profiles can be used.";
            return false;
        }

        string id = NormalizeProfileId(raw);
        FanCurveDefinition? definition = FanProfiles.Find(id) ??
            FanProfiles.GetProfiles().FirstOrDefault(candidate =>
                string.Equals(candidate.Name, raw, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            State.HardwareAccess = $"Fan profile '{raw}' no longer exists.";
            return false;
        }

        if (UsesFirmwareCoolingPolicy)
        {
            if (!FanProfiles.IsBuiltIn(definition.Id))
            {
                State.HardwareAccess = "Custom fan curves require a physically accepted direct fan writer. Quiet, Balanced and Max cooling remain available through Lenovo firmware.";
                return false;
            }

            // The service coordinator needs the current Windows performance mode as
            // the restore baseline before a cooling profile temporarily overrides
            // Lenovo's thermal policy. While the profile is active, later performance
            // and AC/DC changes reassert this cooling override for the current source.
            ServiceResponse? baseline = await HardwareClient.SetThermalModeAsync(State.SelectedMode, _coolingLifetimeCts.Token);
            if (baseline?.Success != true)
            {
                State.HardwareAccess = baseline?.Error ?? "Lenovo thermal-policy baseline unavailable";
                return false;
            }

            ServiceResponse? applied = await HardwareClient.SetCoolingProfileAsync(definition.Name, _coolingLifetimeCts.Token);
            if (applied?.Success != true)
            {
                State.HardwareAccess = applied?.Error ?? "Lenovo firmware cooling profile unavailable";
                return false;
            }

            UserSettings.Update(settings => settings with { CoolingProfile = definition.Id });
            State.CoolingProfile = definition.Name;
            _coolingPreferenceRestoreAttempted = true;
            _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
            ScheduleFirmwareCoolingSettleReassert(definition.Id, generation);
            return true;
        }

        return await ApplyFanCurveCoreAsync(definition, persistSelection: true);
    }

    internal async Task<bool> ApplyFanCurveAsync(FanCurveDefinition definition, bool persistSelection)
    {
        int generation = Interlocked.Increment(ref _coolingSelectionGeneration);
        await _coolingWriteGate.WaitAsync();
        try
        {
            if (UsesFirmwareCoolingPolicy)
            {
                if (FanProfiles.IsBuiltIn(definition.Id))
                    return await SetCoolingProfileCoreAsync(definition.Id, generation);
                State.HardwareAccess = "Custom fan curves are unavailable until a physically accepted direct fan writer is active. The built-in Lenovo firmware profiles still work.";
                return false;
            }

            return await ApplyFanCurveCoreAsync(definition, persistSelection);
        }
        finally
        {
            _coolingWriteGate.Release();
        }
    }

    private async Task<bool> ApplyFanCurveCoreAsync(FanCurveDefinition definition, bool persistSelection)
    {
        if (FanCalibrationState.Required)
        {
            State.HardwareAccess = FanCalibrationState.Running
                ? "Fan calibration currently owns the active fan provider. Finish or stop calibration before applying a curve."
                : "The active fan provider requires calibration before percentage-based curves can be used.";
            return false;
        }

        if (!FanCurveGraphPolicy.TryNormalize(definition.Points, out FanCurvePoint[] points, out string? validation))
        {
            State.HardwareAccess = validation ?? "Fan curve is invalid";
            return false;
        }

        var normalized = definition with { Points = points };
        ServiceResponse? response = await HardwareClient.SetCoolingCurveAsync(normalized);
        if (response?.Success != true)
        {
            State.HardwareAccess = response?.Error ?? "Fan curve unavailable";
            return false;
        }

        if (persistSelection)
            UserSettings.Update(settings => settings with { CoolingProfile = normalized.Id });

        State.CoolingProfile = normalized.Name;
        _coolingPreferenceRestoreAttempted = true;
        _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
        return true;
    }

    internal async Task<bool> SetManualFanPercentAsync(int percent)
    {
        Interlocked.Increment(ref _coolingSelectionGeneration);
        await _coolingWriteGate.WaitAsync();
        try
        {
            if (UsesFirmwareCoolingPolicy)
            {
                State.HardwareAccess = "Temporary percentage targets require a physically accepted direct fan writer. Use Quiet, Balanced or Max cooling for Lenovo firmware-controlled cooling.";
                return false;
            }

            if (FanCalibrationState.Required)
            {
                State.HardwareAccess = FanCalibrationState.Running
                    ? "Fan calibration currently owns the active fan provider."
                    : "The active fan provider requires calibration before percentage-based manual targets can be used.";
                return false;
            }

            ServiceResponse? response = await HardwareClient.SetFanPercentAsync(percent);
            if (response?.Success == true)
                return true;
            State.HardwareAccess = response?.Error ?? "Manual fan output unavailable";
            return false;
        }
        finally
        {
            _coolingWriteGate.Release();
        }
    }

    internal async Task<bool> StartFanCharacterizationAsync()
    {
        if (!FanCalibrationState.Relevant)
        {
            State.HardwareAccess = "The active fan provider does not expose a calibration workflow.";
            return false;
        }

        ServiceResponse? response = await HardwareClient.StartFanCharacterizationAsync();
        if (response?.Success == true)
        {
            _ = HardwareClient.GetStatusAsync();
            return true;
        }
        State.HardwareAccess = response?.Error ?? "Fan calibration unavailable";
        return false;
    }

    internal async Task<bool> StopFanCharacterizationAsync()
    {
        ServiceResponse? response = await HardwareClient.StopFanCharacterizationAsync();
        if (response?.Success == true)
        {
            _ = HardwareClient.GetStatusAsync();
            return true;
        }
        State.HardwareAccess = response?.Error ?? "Fan calibration could not stop";
        return false;
    }

    internal void StartCoolingColdStartConvergence()
    {
        int generation = Volatile.Read(ref _coolingSelectionGeneration);
        _ = ConvergeCoolingPreferenceAfterColdStartAsync(generation, _coolingLifetimeCts.Token);
    }

    private async Task ConvergeCoolingPreferenceAfterColdStartAsync(
        int generation,
        CancellationToken cancellationToken)
    {
        // A silent Windows login can race the auto-start hardware service. The normal
        // tray runtime intentionally avoids hardware polling while no window is open,
        // so one failed first status request used to leave the saved cooling preference
        // unapplied until a later activation/resume. Keep the fix bounded and lifecycle-
        // owned: retry only during the cold-start window, bypassing the client's normal
        // offline backoff, then return to the low-impact runtime scheduler.
        foreach (TimeSpan delay in CoolingColdStartProbeDelays)
        {
            if (cancellationToken.IsCancellationRequested ||
                generation != Volatile.Read(ref _coolingSelectionGeneration) ||
                _coolingPreferenceRestoreAttempted)
            {
                return;
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            ServiceResponse? response = await HardwareClient.GetStatusAsync(
                cancellationToken,
                bypassOfflineBackoff: true);
            if (cancellationToken.IsCancellationRequested ||
                generation != Volatile.Read(ref _coolingSelectionGeneration))
            {
                return;
            }

            if (response?.Success != true || response.Telemetry is null)
                continue;

            await TryRestoreCoolingPreferenceAsync(response, bypassRetryBackoff: true);
            if (_coolingPreferenceRestoreAttempted)
                return;
        }
    }

    private async Task TryRestoreCoolingPreferenceAsync(
        ServiceResponse response,
        bool bypassRetryBackoff = false)
    {
        if (_coolingPreferenceRestoreAttempted || _coolingPreferenceRestoreInFlight ||
            (!bypassRetryBackoff && DateTimeOffset.UtcNow < _coolingPreferenceRetryAfter))
        {
            return;
        }

        int generation = Volatile.Read(ref _coolingSelectionGeneration);
        string selected = UserSettings.Current.CoolingProfile;
        bool wantsAuto = selected.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
                         selected.Equals("Auto", StringComparison.OrdinalIgnoreCase);
        bool verifiedX9 = DeviceCapabilityExpectations.IsVerifiedX9(State.MachineType);
        bool firmwarePolicy = string.Equals(
            response.Capabilities?.FanControlKind,
            FanControlKinds.FirmwarePolicy,
            StringComparison.Ordinal);

        // A non-Auto firmware profile needs the real current Windows overlay as its
        // restore baseline. State.SelectedMode starts as a UI placeholder, so never
        // seed Lenovo thermal policy from it before RefreshStatusAsync has confirmed
        // the actual Windows mode. Auto/direct-provider restores do not depend on it.
        if (!wantsAuto && firmwarePolicy && !CoolingThermalBaselineReady)
            return;

        // Generic restoration follows the advertised cooling capability. The exact-X9
        // Auto exception remains only as a safety/recovery guard for a stale target
        // that may survive a transient provider-capability miss.
        if (response.Capabilities?.FanControl != true && !(wantsAuto && verifiedX9))
            return;

        _coolingPreferenceRestoreInFlight = true;
        bool writeGateHeld = false;
        try
        {
            await _coolingWriteGate.WaitAsync(_coolingLifetimeCts.Token);
            writeGateHeld = true;

            // A manual profile selection increments generation before it waits for
            // this gate. Revalidate only after owning the gate so an older startup
            // task can never start another hardware write after the user's choice.
            if (generation != Volatile.Read(ref _coolingSelectionGeneration) ||
                !string.Equals(UserSettings.Current.CoolingProfile, selected, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (wantsAuto)
            {
                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync(_coolingLifetimeCts.Token);
                if (auto?.Success != true)
                {
                    _coolingPreferenceRetryAfter = DateTimeOffset.UtcNow + CoolingAutoRestoreRetryInterval;
                    State.HardwareAccess = auto?.Error ?? "Saved firmware Auto preference could not be reasserted";
                    return;
                }

                State.CoolingProfile = "Lenovo Auto";
                _coolingPreferenceRestoreAttempted = true;
                _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
                return;
            }

            if (FanCalibrationState.Required)
            {
                _coolingPreferenceRestoreAttempted = true;
                State.HardwareAccess = "The active fan provider requires calibration before the saved percentage-based profile can be restored.";
                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync(_coolingLifetimeCts.Token);
                if (auto?.Success == true)
                    State.CoolingProfile = "Lenovo Auto";
                return;
            }

            FanCurveDefinition? definition = FanProfiles.Find(selected);
            if (definition is null)
            {
                UserSettings.Update(settings => settings with { CoolingProfile = "Lenovo Auto" });
                if (!verifiedX9)
                {
                    State.CoolingProfile = "Lenovo Auto";
                    _coolingPreferenceRestoreAttempted = true;
                    return;
                }

                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync(_coolingLifetimeCts.Token);
                if (auto?.Success != true)
                {
                    _coolingPreferenceRetryAfter = DateTimeOffset.UtcNow + CoolingAutoRestoreRetryInterval;
                    State.HardwareAccess = auto?.Error ?? "Firmware Auto fallback could not be reasserted";
                    return;
                }

                State.CoolingProfile = "Lenovo Auto";
                _coolingPreferenceRestoreAttempted = true;
                return;
            }

            if (UsesFirmwareCoolingPolicy)
            {
                if (!FanProfiles.IsBuiltIn(definition.Id))
                {
                    UserSettings.Update(settings => settings with { CoolingProfile = "Lenovo Auto" });
                    ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync(_coolingLifetimeCts.Token);
                    State.CoolingProfile = "Lenovo Auto";
                    State.HardwareAccess = auto?.Success == true
                        ? "The saved custom fan curve needs a direct fan writer, so Lenovo Auto was restored. Built-in firmware profiles remain available."
                        : auto?.Error ?? "Saved custom fan curve cannot be restored and Lenovo Auto reassertion failed.";
                    _coolingPreferenceRestoreAttempted = auto?.Success == true;
                    if (!_coolingPreferenceRestoreAttempted)
                        _coolingPreferenceRetryAfter = DateTimeOffset.UtcNow + CoolingAutoRestoreRetryInterval;
                    return;
                }

                ServiceResponse? baseline = await HardwareClient.SetThermalModeAsync(State.SelectedMode, _coolingLifetimeCts.Token);
                ServiceResponse? applied = baseline?.Success == true
                    ? await HardwareClient.SetCoolingProfileAsync(definition.Name, _coolingLifetimeCts.Token)
                    : null;
                if (baseline?.Success != true || applied?.Success != true)
                {
                    _coolingPreferenceRetryAfter = DateTimeOffset.UtcNow + CoolingAutoRestoreRetryInterval;
                    State.HardwareAccess = baseline?.Error ?? applied?.Error ?? "Saved Lenovo firmware cooling profile could not be restored";
                    return;
                }

                // If the user selected a different profile while startup restoration
                // was in flight, do not relabel or schedule a stale saved preference.
                if (generation != Volatile.Read(ref _coolingSelectionGeneration) ||
                    !string.Equals(UserSettings.Current.CoolingProfile, selected, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                State.CoolingProfile = definition.Name;
                _coolingPreferenceRestoreAttempted = true;
                _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
                ScheduleFirmwareCoolingSettleReassert(definition.Id, generation);
                return;
            }

            ServiceResponse? directApplied = await HardwareClient.SetCoolingCurveAsync(definition, _coolingLifetimeCts.Token);
            if (directApplied?.Success != true)
            {
                State.HardwareAccess = directApplied?.Error ?? "Saved fan profile could not be restored";
                return;
            }

            State.CoolingProfile = definition.Name;
            _coolingPreferenceRestoreAttempted = true;
            _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (writeGateHeld)
                _coolingWriteGate.Release();
            _coolingPreferenceRestoreInFlight = false;
        }
    }

    private void ScheduleFirmwareCoolingSettleReassert(string profileId, int generation)
    {
        _ = ReassertFirmwareCoolingAfterStartupSettleAsync(profileId, generation, _coolingLifetimeCts.Token);
    }

    private async Task ReassertFirmwareCoolingAfterStartupSettleAsync(
        string profileId,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CoolingStartupSettleDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (generation != Volatile.Read(ref _coolingSelectionGeneration) ||
            !UsesFirmwareCoolingPolicy ||
            !string.Equals(UserSettings.Current.CoolingProfile, profileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FanCurveDefinition? definition = FanProfiles.Find(profileId);
        if (definition is null || !FanProfiles.IsBuiltIn(definition.Id))
            return;

        bool writeGateHeld = false;
        try
        {
            await _coolingWriteGate.WaitAsync(cancellationToken);
            writeGateHeld = true;

            if (generation != Volatile.Read(ref _coolingSelectionGeneration) ||
                !string.Equals(UserSettings.Current.CoolingProfile, profileId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Lenovo services can finish their own login/resume policy work shortly after
            // ThinkControl first becomes available. Reassert once after startup settles so
            // a successful early Quiet/Balanced/Max restore cannot be silently overwritten
            // while our UI continues displaying the saved preference. This is deliberately
            // one bounded retry, not a polling loop or a fight with firmware.
            ServiceResponse? baseline = await HardwareClient.SetThermalModeAsync(State.SelectedMode, cancellationToken);
            ServiceResponse? applied = baseline?.Success == true
                ? await HardwareClient.SetCoolingProfileAsync(definition.Name, cancellationToken)
                : null;

            if (generation != Volatile.Read(ref _coolingSelectionGeneration) ||
                !string.Equals(UserSettings.Current.CoolingProfile, profileId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (baseline?.Success == true && applied?.Success == true)
            {
                State.CoolingProfile = definition.Name;
                return;
            }

            State.HardwareAccess = baseline?.Error ?? applied?.Error ??
                                   $"Saved {definition.Name} cooling could not be reasserted after startup settled.";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (writeGateHeld)
                _coolingWriteGate.Release();
        }
    }

    private static string NormalizeProfileId(string profile) => profile switch
    {
        "Quiet" or "Silent" => FanCurveDefaults.QuietId,
        "Balanced" or "Normal" => FanCurveDefaults.BalancedId,
        "Max cooling" or "Cool" => FanCurveDefaults.MaxCoolingId,
        _ => profile
    };
}
