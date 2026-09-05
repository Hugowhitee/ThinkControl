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
    internal static FanCalibrationUiState None { get; } = new(false, false, false, 0, 7, string.Empty);
    internal bool Required => Relevant && !Ready;
}

public partial class App
{
    private static readonly TimeSpan CoolingAutoRestoreRetryInterval = TimeSpan.FromSeconds(15);

    private bool _coolingPreferenceRestoreAttempted;
    private bool _coolingPreferenceRestoreInFlight;
    private DateTimeOffset _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
    private FanProfileCatalog? _fanProfiles;
    private FanCalibrationUiState _fanCalibrationState = FanCalibrationUiState.None;

    public FanProfileCatalog FanProfiles => _fanProfiles ??= new FanProfileCatalog(UserSettings);
    internal FanCalibrationUiState FanCalibrationState => _fanCalibrationState;
    internal event EventHandler? FanCalibrationStateChanged;

    private void InitializeCoolingCoordinator()
    {
        HardwareClient.StatusObserved += CoolingStatusObserved;
        Exit += (_, _) =>
        {
            HardwareClient.StatusObserved -= CoolingStatusObserved;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(900));
                HardwareClient.ReturnFanToAutoAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch
            {
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

    private FanCalibrationUiState ResolveFanCalibrationState(ServiceResponse? response)
    {
        if (response?.Success != true || response.Capabilities is not HardwareCapabilitySnapshot capabilities)
            return FanCalibrationUiState.None;

        bool relevant = DeviceCapabilityExpectations.IsVerifiedX9(State.MachineType) &&
                        capabilities.FanControl &&
                        capabilities.FanTelemetry &&
                        string.Equals(capabilities.FanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        if (!relevant)
            return FanCalibrationUiState.None;

        FanCharacterizationSnapshot? characterization = response.Telemetry?.FanCharacterization;
        bool running = characterization?.Running == true;
        int completed = characterization?.CompletedLevels ?? 0;
        int total = Math.Max(1, characterization?.TotalLevels ?? 7);
        bool ready = !running && characterization?.Levels.Count == total;
        string status = characterization?.Status ?? "Fan calibration is required before percentage targets and curves are enabled.";
        return new FanCalibrationUiState(true, running, ready, completed, total, status);
    }

    internal async Task<bool> SetCoolingProfileAsync(string profile)
    {
        string raw = profile?.Trim() ?? string.Empty;
        if (raw.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();
            if (auto?.Success != true)
            {
                State.HardwareAccess = auto?.Error ?? "Lenovo Auto unavailable";
                return false;
            }
            UserSettings.Update(settings => settings with { CoolingProfile = "Lenovo Auto" });
            // Keep Compact and Advanced on one immediate source of truth. Hardware
            // telemetry will confirm this on the next status snapshot, but the UI
            // must not remain visually stuck on the previous profile meanwhile.
            State.CoolingProfile = "Lenovo Auto";
            _coolingPreferenceRestoreAttempted = true;
            _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
            return true;
        }

        if (FanCalibrationState.Required)
        {
            State.HardwareAccess = FanCalibrationState.Running
                ? "Fan calibration currently owns the discrete EC fan provider. Finish or stop calibration before selecting a profile."
                : "Calibrate the verified X9 EC fan states before using percentage-based fan profiles.";
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

        return await ApplyFanCurveAsync(definition, persistSelection: true);
    }

    internal async Task<bool> ApplyFanCurveAsync(FanCurveDefinition definition, bool persistSelection)
    {
        if (FanCalibrationState.Required)
        {
            State.HardwareAccess = FanCalibrationState.Running
                ? "Fan calibration currently owns the discrete EC fan provider. Finish or stop calibration before applying a curve."
                : "Calibrate the verified X9 EC fan states before applying percentage-based curves.";
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

        // The service owns hardware truth, while AppState owns current UI truth.
        // Publish the friendly profile name synchronously so Compact reflects a
        // selection made in Advanced (and vice versa) without waiting for polling.
        State.CoolingProfile = normalized.Name;
        _coolingPreferenceRestoreAttempted = true;
        _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
        return true;
    }

    internal async Task<bool> SetManualFanPercentAsync(int percent)
    {
        if (FanCalibrationState.Required)
        {
            State.HardwareAccess = FanCalibrationState.Running
                ? "Fan calibration currently owns the discrete EC fan provider."
                : "Calibrate the verified X9 EC fan states before using percentage-based manual targets.";
            return false;
        }

        ServiceResponse? response = await HardwareClient.SetFanPercentAsync(percent);
        if (response?.Success == true)
            return true;
        State.HardwareAccess = response?.Error ?? "Manual fan output unavailable";
        return false;
    }

    internal async Task<bool> StartFanCharacterizationAsync()
    {
        ServiceResponse? response = await HardwareClient.StartFanCharacterizationAsync();
        if (response?.Success == true)
        {
            _ = HardwareClient.GetStatusAsync();
            return true;
        }
        State.HardwareAccess = response?.Error ?? "Fan characterization unavailable";
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
        State.HardwareAccess = response?.Error ?? "Fan characterization could not stop";
        return false;
    }

    private async Task TryRestoreCoolingPreferenceAsync(ServiceResponse response)
    {
        if (_coolingPreferenceRestoreAttempted || _coolingPreferenceRestoreInFlight ||
            DateTimeOffset.UtcNow < _coolingPreferenceRetryAfter)
        {
            return;
        }

        string selected = UserSettings.Current.CoolingProfile;
        bool wantsAuto = selected.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
                         selected.Equals("Auto", StringComparison.OrdinalIgnoreCase);
        bool verifiedX9 = DeviceCapabilityExpectations.IsVerifiedX9(State.MachineType);

        // A saved Auto preference is itself an explicit request to give the X9
        // firmware ownership. Reassert it even when fan-control capability is not
        // currently advertised: an earlier crashed process can leave an OEM/EC
        // target manual while this UI has lost the in-memory ownership marker.
        // A transient startup failure is retried with bounded backoff; we mark the
        // restore complete only after Lenovo Auto actually succeeds.
        if (response.Capabilities?.FanControl != true && !(wantsAuto && verifiedX9))
            return;

        _coolingPreferenceRestoreInFlight = true;
        try
        {
            if (wantsAuto)
            {
                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();
                if (auto?.Success != true)
                {
                    _coolingPreferenceRetryAfter = DateTimeOffset.UtcNow + CoolingAutoRestoreRetryInterval;
                    State.HardwareAccess = auto?.Error ?? "Saved Lenovo Auto preference could not be reasserted";
                    return;
                }

                State.CoolingProfile = "Lenovo Auto";
                _coolingPreferenceRestoreAttempted = true;
                _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
                return;
            }

            // Percentage-based curves on the discrete EC fallback are meaningful
            // only after the seven real fan states have been measured. Keep Lenovo
            // Auto in charge rather than restoring a guessed mapping at startup.
            if (FanCalibrationState.Required)
            {
                _coolingPreferenceRestoreAttempted = true;
                State.HardwareAccess = "Fan calibration is required before the saved percentage-based fan profile can be restored.";
                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();
                if (auto?.Success == true)
                    State.CoolingProfile = "Lenovo Auto";
                return;
            }

            // Non-Auto saved profiles still get one startup restore attempt. Their
            // normal UI action remains available if a provider rejects the profile.
            _coolingPreferenceRestoreAttempted = true;
            FanCurveDefinition? definition = FanProfiles.Find(selected);
            if (definition is null)
            {
                UserSettings.Update(settings => settings with { CoolingProfile = "Lenovo Auto" });
                if (!verifiedX9)
                {
                    State.CoolingProfile = "Lenovo Auto";
                    return;
                }

                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();
                if (auto?.Success != true)
                {
                    _coolingPreferenceRestoreAttempted = false;
                    _coolingPreferenceRetryAfter = DateTimeOffset.UtcNow + CoolingAutoRestoreRetryInterval;
                    State.HardwareAccess = auto?.Error ?? "Lenovo Auto fallback could not be reasserted";
                    return;
                }

                State.CoolingProfile = "Lenovo Auto";
                return;
            }

            ServiceResponse? applied = await HardwareClient.SetCoolingCurveAsync(definition);
            if (applied?.Success != true)
                State.HardwareAccess = applied?.Error ?? "Saved fan profile could not be restored";
            else
                State.CoolingProfile = definition.Name;
        }
        finally
        {
            _coolingPreferenceRestoreInFlight = false;
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
