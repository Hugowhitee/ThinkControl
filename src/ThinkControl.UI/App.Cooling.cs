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
        string raw = profile?.Trim() ?? string.Empty;
        if (raw.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();
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

        return await ApplyFanCurveAsync(definition, persistSelection: true);
    }

    internal async Task<bool> ApplyFanCurveAsync(FanCurveDefinition definition, bool persistSelection)
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

        // The generic preference path follows the advertised provider capability.
        // The exact-X9 exception below is retained only as a safety/recovery guard
        // for an older ThinkControl-owned target that may survive a transient loss
        // of the writer capability; it is not the product-wide calibration rule.
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
                ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();
                if (auto?.Success == true)
                    State.CoolingProfile = "Lenovo Auto";
                return;
            }

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
                    State.HardwareAccess = auto?.Error ?? "Firmware Auto fallback could not be reasserted";
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
