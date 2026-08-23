using ThinkControl.Core.Ipc;

namespace ThinkControl.UI;

public partial class App
{
    private bool _coolingPreferenceRestoreAttempted;

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
        ServiceResponse? response = profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase)
            ? await HardwareClient.ReturnFanToAutoAsync()
            : await HardwareClient.SetCoolingProfileAsync(profile);

        if (response?.Success != true)
        {
            State.HardwareAccess = response?.Error ?? "Cooling profile unavailable";
            return false;
        }

        string normalized = profile switch
        {
            "Silent" => "Silent",
            "Normal" => "Normal",
            "Cool" => "Cool",
            _ => "Lenovo Auto"
        };
        UserSettings.Update(settings => settings with { CoolingProfile = normalized });
        _coolingPreferenceRestoreAttempted = true;
        return true;
    }

    internal async Task<bool> StartFanCharacterizationAsync()
    {
        ServiceResponse? response = await HardwareClient.StartFanCharacterizationAsync();
        if (response?.Success == true)
            return true;
        State.HardwareAccess = response?.Error ?? "Fan characterization unavailable";
        return false;
    }

    internal async Task<bool> MarkCurrentFanLevelAudibleAsync()
    {
        ServiceResponse? response = await HardwareClient.MarkFanLevelAudibleAsync();
        if (response?.Success == true)
            return true;
        State.HardwareAccess = response?.Error ?? "Audibility marker unavailable";
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
        string profile = UserSettings.Current.CoolingProfile;
        if (profile is not ("Silent" or "Normal" or "Cool"))
            return;

        ServiceResponse? applied = await HardwareClient.SetCoolingProfileAsync(profile);
        if (applied?.Success != true)
        {
            // Do not retry every 2 seconds. A repair/restart or the next app launch
            // provides a fresh attempt while Lenovo firmware remains the safe owner.
            State.HardwareAccess = applied?.Error ?? "Saved cooling profile could not be restored";
        }
    }
}
