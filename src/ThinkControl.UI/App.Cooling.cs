using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private bool _coolingPreferenceRestoreAttempted;
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
        if (_coolingPreferenceRestoreAttempted || response.Capabilities?.FanControl != true)
            return;

        _coolingPreferenceRestoreAttempted = true;
        string selected = UserSettings.Current.CoolingProfile;
        if (selected == "Lenovo Auto")
        {
            State.CoolingProfile = "Lenovo Auto";
            return;
        }

        FanCurveDefinition? definition = FanProfiles.Find(selected);
        if (definition is null)
        {
            UserSettings.Update(settings => settings with { CoolingProfile = "Lenovo Auto" });
            State.CoolingProfile = "Lenovo Auto";
            return;
        }

        ServiceResponse? applied = await HardwareClient.SetCoolingCurveAsync(definition);
        if (applied?.Success != true)
            State.HardwareAccess = applied?.Error ?? "Saved fan profile could not be restored";
        else
            State.CoolingProfile = definition.Name;
    }

    private static string NormalizeProfileId(string profile) => profile switch
    {
        "Quiet" or "Silent" => FanCurveDefaults.QuietId,
        "Balanced" or "Normal" => FanCurveDefaults.BalancedId,
        "Max cooling" or "Cool" => FanCurveDefaults.MaxCoolingId,
        _ => profile
    };
}
