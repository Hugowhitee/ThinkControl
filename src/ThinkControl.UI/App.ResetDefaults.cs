using ThinkControl.Core.Touchpad;
using ThinkControl.UI.Services;
using ThinkControl.UI.Services.Touchpad;
using UserThemeMode = ThinkControl.UI.Services.ThemeMode;

namespace ThinkControl.UI;

public partial class App
{
    internal const int DefaultHapticFeedbackIntensity = 50;
    internal const int DefaultHapticClickSensitivity = 50;

    internal bool ResetPerformanceDefaults()
    {
        bool battery = SetPowerPreference(ThinkControlPowerMode.Balanced, onBattery: true);
        bool ac = SetPowerPreference(ThinkControlPowerMode.Balanced, onBattery: false);
        return battery && ac;
    }

    internal async Task<bool> ResetFanDefaultsAsync() =>
        await SetCoolingProfileAsync("Lenovo Auto");

    internal void ResetDisplayDefaults()
    {
        // Brightness and adaptive brightness remain Windows/OEM state. ThinkControl
        // only owns the refresh policy, so its portable default is Auto.
        EnableRefreshAuto();
    }

    internal async Task ResetKeyboardDefaultsAsync()
    {
        const string defaultMode = "Auto";
        const string defaultBaseLevel = "High";
        const string defaultStaticLevel = "High";
        const double defaultEffectSpeed = 1.0;

        KeyboardEffects.SetBaseLevel(defaultBaseLevel);
        KeyboardEffects.SetSpeed(defaultEffectSpeed);
        await KeyboardEffects.SetModeAsync(defaultMode);

        UserSettings.Update(settings => settings with
        {
            KeyboardMode = defaultMode,
            KeyboardBaseLevel = defaultBaseLevel,
            KeyboardStaticLevel = defaultStaticLevel,
            KeyboardEffectSpeed = defaultEffectSpeed
        });
    }

    internal void ResetTouchpadDefaults()
    {
        TouchpadGestureConfiguration defaults =
            TouchpadGestureConfiguration.Default with { Enabled = false };

        UserSettings.Update(settings => settings with
        {
            TouchpadGestures = defaults,
            TouchpadOsdEnabled = true,
            TouchpadOsdOpacity = 0.92,
            TouchpadOsdPosition = "Center"
        });
        TouchpadFeature.UpdateConfiguration(defaults);

        TouchpadHapticStatus status = TouchpadFeature.HapticStatus;
        if (!status.ApiAvailable || !status.TouchpadPresent || !status.FeedbackSupported)
            return;

        _ = TouchpadFeature.SetHapticEnabled(true);
        _ = TouchpadFeature.SetHapticIntensity(DefaultHapticFeedbackIntensity);
        if (status.ClickForceSupported)
            _ = TouchpadFeature.SetClickForceSensitivity(DefaultHapticClickSensitivity);
    }

    internal async Task ResetAudioDefaultsAsync()
    {
        var dolby = new DolbyDirectControlService();
        DolbyProfileResult profile = await dolby.SetProfileAsync("Dynamic");
        UserSettings.Update(settings => settings with
        {
            DolbyProfile = profile.Success ? "Dynamic" : settings.DolbyProfile,
            // Balanced is the portable Music default. Dynamic itself is automatic,
            // so reset-all must not send an unrelated IEQ command while Dynamic is
            // active and must never open Dolby Access as a side effect.
            DolbySubProfile = "Balanced"
        });
    }

    internal void ResetGeneralDefaults()
    {
        ApplyTheme(UserThemeMode.System);
        _ = StartupService.SetEnabled(false);
        UserSettings.Update(settings => settings with { AutomaticUpdates = true });
    }

    internal async Task ResetAllDefaultsAsync()
    {
        ThinkControlUserSettings current = UserSettings.Current;
        TouchpadGestureConfiguration touchpadDefaults =
            TouchpadGestureConfiguration.Default with { Enabled = false };

        // Diagnostics consent and one-time hardware onboarding are trust state,
        // not normal preferences, so reset-all deliberately preserves them.
        UserSettings.Update(_ => new ThinkControlUserSettings(
            Theme: UserThemeMode.System,
            RefreshAuto: true,
            KeyboardMode: "Auto",
            KeyboardBaseLevel: "High",
            KeyboardStaticLevel: "High",
            KeyboardEffectSpeed: 1.0,
            DiagnosticsConsent: current.DiagnosticsConsent,
            TouchpadGestures: touchpadDefaults,
            TouchpadOsdEnabled: true,
            TouchpadOsdOpacity: 0.92,
            TouchpadOsdPosition: "Center",
            HardwareSetupPromptedVersion: current.HardwareSetupPromptedVersion,
            BatteryPowerMode: "Balanced",
            AcPowerMode: "Balanced",
            CoolingProfile: "Lenovo Auto",
            DolbyProfile: "Dynamic",
            DolbySubProfile: "Balanced",
            AutomaticUpdates: true,
            DiagnosticsSharingPrompted: current.DiagnosticsSharingPrompted,
            HardwareIssuePromptedKeys: current.HardwareIssuePromptedKeys));

        ThemeService.Apply(UserThemeMode.System);
        _ = StartupService.SetEnabled(false);
        State.RefreshAutoEnabled = true;

        _ = ResetPerformanceDefaults();
        ResetDisplayDefaults();
        await ResetKeyboardDefaultsAsync();
        _ = await ResetFanDefaultsAsync();
        ResetTouchpadDefaults();
        await ResetAudioDefaultsAsync();

        await RefreshStatusAsync();
    }
}
