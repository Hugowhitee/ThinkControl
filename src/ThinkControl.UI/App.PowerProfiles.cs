using Microsoft.Win32;
using System.Windows.Threading;
using ThinkControl.UI.Services;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI;

public partial class App
{
    private bool? _lastObservedOnAc;
    private bool _powerEventsAttached;

    private void InitializePowerProfileCoordinator()
    {
        Startup += (_, _) =>
        {
            if (!_powerEventsAttached)
            {
                SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
                _powerEventsAttached = true;
            }

            // Read once at startup, then react to Windows' AC/DC notifications. A
            // periodic WPF timer is unnecessary for a state that Windows already
            // publishes as an event and only adds needless wakeups while tray-only.
            ObservePowerSource(force: true);
        };
        Exit += (_, _) =>
        {
            if (!_powerEventsAttached)
                return;
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            _powerEventsAttached = false;
        };
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is not PowerModes.StatusChange and not PowerModes.Resume)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => ObservePowerSource(force: e.Mode == PowerModes.Resume)));
    }

    internal ThinkControlPowerMode GetPowerPreference(bool onBattery)
    {
        string stored = onBattery ? UserSettings.Current.BatteryPowerMode : UserSettings.Current.AcPowerMode;
        if (TryParsePowerPreference(stored, out ThinkControlPowerMode parsed))
            return parsed;
        return PowerModeService.GetConfigured(onBattery) ?? ThinkControlPowerMode.Balanced;
    }

    internal bool SetPowerPreference(ThinkControlPowerMode mode, bool onBattery)
    {
        bool configured = PowerModeService.Configure(mode, onBattery);
        if (!configured)
            return false;

        UserSettings.Update(settings => onBattery
            ? settings with { BatteryPowerMode = mode.ToString() }
            : settings with { AcPowerMode = mode.ToString() });

        bool currentSource = IsCurrentlyOnBattery() == onBattery;
        if (currentSource)
        {
            bool applied = PowerModeService.SetForSource(mode, onBattery, makeEffective: true);
            if (applied)
                State.SelectedMode = mode.ToString();
            return applied;
        }

        return true;
    }

    internal bool IsCurrentlyOnBattery()
    {
        if (_lastObservedOnAc.HasValue)
            return !_lastObservedOnAc.Value;

        try
        {
            return Forms.SystemInformation.PowerStatus.PowerLineStatus != Forms.PowerLineStatus.Online;
        }
        catch
        {
            return false;
        }
    }

    internal static string PowerPreferenceDisplayName(ThinkControlPowerMode mode) =>
        PowerModeService.DisplayName(mode);

    private void ObservePowerSource(bool force = false)
    {
        bool onAc;
        try
        {
            onAc = Forms.SystemInformation.PowerStatus.PowerLineStatus == Forms.PowerLineStatus.Online;
        }
        catch
        {
            return;
        }

        if (!force && _lastObservedOnAc == onAc)
            return;
        _lastObservedOnAc = onAc;

        bool onBattery = !onAc;
        ThinkControlPowerMode mode = GetPowerPreference(onBattery);

        // If this is the first ThinkControl version that stores separate source
        // preferences, seed the setting with the user's existing Windows choice
        // rather than silently inventing a new one.
        ThinkControlUserSettings settings = UserSettings.Current;
        string stored = onBattery ? settings.BatteryPowerMode : settings.AcPowerMode;
        if (string.IsNullOrWhiteSpace(stored))
        {
            UserSettings.Update(current => onBattery
                ? current with { BatteryPowerMode = mode.ToString() }
                : current with { AcPowerMode = mode.ToString() });
        }

        if (PowerModeService.SetForSource(mode, onBattery, makeEffective: true))
            State.SelectedMode = mode.ToString();
    }

    private static bool TryParsePowerPreference(string? value, out ThinkControlPowerMode mode)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Equals("Efficiency", StringComparison.OrdinalIgnoreCase))
            normalized = "Quiet";
        return Enum.TryParse(normalized, true, out mode);
    }
}
