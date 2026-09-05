using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private static readonly TimeSpan CoolingAutoRestoreRetryInterval = TimeSpan.FromSeconds(15);

    private bool _coolingPreferenceRestoreAttempted;
    private bool _coolingPreferenceRestoreInFlight;
    private DateTimeOffset _coolingPreferenceRetryAfter = DateTimeOffset.MinValue;
    private FanProfileCatalog? _fanProfiles;

    public FanProfileCatalog FanProfiles => _fanProfiles ??= new FanProfileCatalog(UserSettings);

    private void InitializeCoolingCoordinator()
    {
        Exit += (_, _) =>
        {
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
            return true;
        State.HardwareAccess = response?.Error ?? "Fan characterization unavailable";
        return false;
    }

    internal async Task<bool> StopFanCharacterizationAsync()
    {
        ServiceResponse? response = await HardwareClient.StopFanCharacterizationAsync();
        if (response?.Success == true)
            return true;
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
