using ThinkControl.Core.Cooling;
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
        string internalProfile = NormalizeCoolingProfile(profile);
        ServiceResponse? response = internalProfile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase)
            ? await HardwareClient.ReturnFanToAutoAsync()
            : await HardwareClient.SetCoolingProfileAsync(internalProfile);

        if (response?.Success != true)
        {
            State.HardwareAccess = response?.Error ?? "Cooling profile unavailable";
            return false;
        }

        UserSettings.Update(settings => settings with { CoolingProfile = internalProfile });
        _coolingPreferenceRestoreAttempted = true;
        return true;
    }

    internal async Task<bool> SetCustomCoolingCurveAsync(IReadOnlyList<double> thresholds)
    {
        if (!FanCurvePolicy.TryValidateCustomThresholds(thresholds, out double[] normalized, out string? error))
        {
            State.HardwareAccess = error ?? "Custom cooling curve is invalid";
            return false;
        }

        ServiceResponse? response = await HardwareClient.SetCustomCoolingCurveAsync(normalized);
        if (response?.Success != true)
        {
            State.HardwareAccess = response?.Error ?? "Custom cooling curve unavailable";
            return false;
        }

        UserSettings.Update(settings => settings with
        {
            CoolingProfile = "Custom",
            CustomFanThresholds = normalized
        });
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
        ThinkControlUserSettings settings = UserSettings.Current;
        if (settings.CoolingProfile == "Custom")
        {
            double[] curve = settings.CustomFanThresholds ?? FanCurvePolicy.DefaultCustomThresholds.ToArray();
            ServiceResponse? custom = await HardwareClient.SetCustomCoolingCurveAsync(curve);
            if (custom?.Success != true)
                State.HardwareAccess = custom?.Error ?? "Saved custom cooling curve could not be restored";
            return;
        }

        if (settings.CoolingProfile is not ("Silent" or "Normal" or "Cool"))
            return;

        ServiceResponse? applied = await HardwareClient.SetCoolingProfileAsync(settings.CoolingProfile);
        if (applied?.Success != true)
            State.HardwareAccess = applied?.Error ?? "Saved cooling profile could not be restored";
    }

    private static string NormalizeCoolingProfile(string profile) => profile.Trim() switch
    {
        "Quiet" or "Silent" => "Silent",
        "Balanced" or "Normal" => "Normal",
        "Max cooling" or "Cool" => "Cool",
        "Custom" => "Custom",
        _ => "Lenovo Auto"
    };
}
