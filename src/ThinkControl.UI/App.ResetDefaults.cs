using ThinkControl.Core.Touchpad;
using ThinkControl.UI.Services;
using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI;

public partial class App
{
    internal const int DefaultHapticFeedbackIntensity = 50;
    internal const int DefaultHapticClickSensitivity = 50;

    internal bool ResetPerformanceDefaults() =>
        SetPowerMode(ThinkControlPowerMode.Balanced);

    internal async Task<bool> ResetFanDefaultsAsync()
    {
        var response = await HardwareClient.ReturnFanToAutoAsync();
        if (response?.Success != true)
        {
            State.HardwareAccess = response?.Error ?? "Fan control unavailable";
            return false;
        }

        return true;
    }

    internal void ResetDisplayDefaults()
    {
        // Brightness and adaptive-brightness defaults are OEM/device policy, not a
        // portable ThinkControl value. Reset only the preference ThinkControl owns.
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

        TouchpadFeature.UpdateConfiguration(defaults);

        TouchpadHapticStatus status = TouchpadFeature.HapticStatus;
        if (!status.ApiAvailable || !status.TouchpadPresent || !status.FeedbackSupported)
            return;

        _ = TouchpadFeature.SetHapticEnabled(true);
        _ = TouchpadFeature.SetHapticIntensity(DefaultHapticFeedbackIntensity);
        if (status.ClickForceSupported)
            _ = TouchpadFeature.SetClickForceSensitivity(DefaultHapticClickSensitivity);
    }

    internal void ResetGeneralDefaults()
    {
        ApplyTheme(ThemeMode.System);
        _ = StartupService.SetEnabled(false);
    }

    internal async Task ResetAllDefaultsAsync()
    {
        ThinkControlUserSettings current = UserSettings.Current;
        TouchpadGestureConfiguration touchpadDefaults =
            TouchpadGestureConfiguration.Default with { Enabled = false };

        // Diagnostics consent and the one-time hardware-setup acknowledgement are
        // trust/onboarding state, not appearance or control preferences. Preserve
        // them while every user-adjustable ThinkControl preference is reset.
        UserSettings.Update(_ => new ThinkControlUserSettings(
            Theme: ThemeMode.System,
            RefreshAuto: true,
            KeyboardMode: "Auto",
            KeyboardBaseLevel: "High",
            KeyboardStaticLevel: "High",
            KeyboardEffectSpeed: 1.0,
            DiagnosticsConsent: current.DiagnosticsConsent,
            TouchpadGestures: touchpadDefaults,
            HardwareSetupPromptedVersion: current.HardwareSetupPromptedVersion));

        ThemeService.Apply(ThemeMode.System);
        _ = StartupService.SetEnabled(false);
        State.RefreshAutoEnabled = true;

        _ = ResetPerformanceDefaults();
        ResetDisplayDefaults();
        await ResetKeyboardDefaultsAsync();
        _ = await ResetFanDefaultsAsync();
        ResetTouchpadDefaults();

        await RefreshStatusAsync();
    }
}
