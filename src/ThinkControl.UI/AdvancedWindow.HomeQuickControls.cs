using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ThinkControl.Core.Audio;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _homeQuickControlsConfigured;
    private bool _homeAudioSafetyBusy;

    private void ConfigureHomeQuickControls()
    {
        if (!_homeQuickControlsConfigured)
        {
            _homeQuickControlsConfigured = true;
            _app.State.PropertyChanged += HomeQuickState_PropertyChanged;
            _app.AudioSafety.ModeChanged += HomeAudioSafety_ModeChanged;
            Closed += (_, _) =>
            {
                _app.State.PropertyChanged -= HomeQuickState_PropertyChanged;
                _app.AudioSafety.ModeChanged -= HomeAudioSafety_ModeChanged;
            };
        }

        SyncHomePowerModes();
        RefreshHomeFanProfiles();
        RefreshHomeAudioSafety();
    }

    private void HomeQuickState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModels.AppState.CoolingProfile) or
            nameof(ViewModels.AppState.CanFanControl) or
            nameof(ViewModels.AppState.FanControlKind))
        {
            Dispatcher.BeginInvoke(new Action(RefreshHomeFanProfiles));
            return;
        }

        if (e.PropertyName == nameof(ViewModels.AppState.SelectedMode))
            Dispatcher.BeginInvoke(new Action(SyncHomePowerModes));
    }

    private void HomePowerMode_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string tag })
            return;

        string[] parts = tag.Split(':', 2);
        if (parts.Length != 2 || !Enum.TryParse(parts[1], true, out ThinkControlPowerMode mode))
            return;

        bool onBattery = parts[0].Equals("Battery", StringComparison.OrdinalIgnoreCase);
        _ = _app.SetPowerPreference(mode, onBattery);
        SyncHomePowerModes();
    }

    private void SyncHomePowerModes()
    {
        if (HomeQuiet is null || HomeBalanced is null || HomePerformance is null ||
            HomeAcQuiet is null || HomeAcBalanced is null || HomeAcPerformance is null)
        {
            return;
        }

        ThinkControlPowerMode battery = _app.GetPowerPreference(onBattery: true);
        ThinkControlPowerMode ac = _app.GetPowerPreference(onBattery: false);
        _syncing = true;
        try
        {
            HomeQuiet.IsChecked = battery == ThinkControlPowerMode.Quiet;
            HomeBalanced.IsChecked = battery == ThinkControlPowerMode.Balanced;
            HomePerformance.IsChecked = battery == ThinkControlPowerMode.Performance;
            HomeAcQuiet.IsChecked = ac == ThinkControlPowerMode.Quiet;
            HomeAcBalanced.IsChecked = ac == ThinkControlPowerMode.Balanced;
            HomeAcPerformance.IsChecked = ac == ThinkControlPowerMode.Performance;
            if (HomePowerSummary is not null)
                HomePowerSummary.Text = $"Battery {PowerShortName(battery)} · AC {PowerShortName(ac)}";
        }
        finally
        {
            _syncing = false;
        }
    }

    private static string PowerShortName(ThinkControlPowerMode mode) => mode switch
    {
        ThinkControlPowerMode.Quiet => "Efficiency",
        ThinkControlPowerMode.Performance => "Fast",
        _ => "Balanced"
    };

    private void RefreshHomeFanProfiles()
    {
        if (HomeFanMoreButton is null || HomeFanAutoSwitch is null)
            return;

        string selected = _app.State.CoolingProfileDisplay;
        bool firmwarePolicy = string.Equals(
            _app.State.FanControlKind,
            FanControlKinds.FirmwarePolicy,
            StringComparison.Ordinal);
        string[] extraProfiles = BuildHomeFanExtraProfiles(selected, firmwarePolicy);
        bool enabled = _app.State.CanFanControl;
        bool autoActive =
            selected.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
            selected.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase);

        _syncing = true;
        try
        {
            HomeFanQuickGrid.IsEnabled = enabled && !autoActive;
            HomeFanQuiet.IsChecked = selected.Equals("Quiet", StringComparison.OrdinalIgnoreCase);
            HomeFanBalanced.IsChecked = selected.Equals("Balanced", StringComparison.OrdinalIgnoreCase);
            HomeFanMax.IsChecked = selected.Equals("Max cooling", StringComparison.OrdinalIgnoreCase);

            HomeFanAutoSwitch.IsChecked = autoActive;
            HomeFanAutoSwitch.IsEnabled = enabled;

            int selectableExtraCount = extraProfiles.Count(profile => !IsManualHomeFanState(profile));
            bool currentUsesMore = extraProfiles.Contains(selected, StringComparer.OrdinalIgnoreCase);
            HomeFanMoreButton.IsEnabled = enabled && !autoActive && selectableExtraCount > 0;
            HomeFanMoreButton.Content = currentUsesMore
                ? $"{selected}  ▾"
                : selectableExtraCount switch
                {
                    0 => "No extra profiles",
                    1 => "More profile  ▾",
                    _ => $"More profiles ({selectableExtraCount})  ▾"
                };
            HomeFanMoreButton.ToolTip = currentUsesMore && IsManualHomeFanState(selected)
                ? selectableExtraCount > 0
                    ? "Current manual fan output · choose a saved profile from this menu"
                    : "Current manual fan output · no additional saved profiles are available"
                : selectableExtraCount > 0
                    ? "Show additional saved fan profiles without leaving Home"
                    : "No additional saved fan profiles are available for the current fan provider";
        }
        finally
        {
            _syncing = false;
        }
    }

    private string[] BuildHomeFanExtraProfiles(string selected, bool firmwarePolicy)
    {
        var values = new List<string>();
        if (IsManualHomeFanState(selected) && !firmwarePolicy)
            values.Add(selected);

        if (!firmwarePolicy)
        {
            values.AddRange(_app.FanProfiles.GetProfiles()
                .Where(profile => !_app.FanProfiles.IsBuiltIn(profile.Id))
                .Select(profile => profile.Name));
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsManualHomeFanState(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("Manual ", StringComparison.OrdinalIgnoreCase);

    private void HomeFanMore_Click(object sender, RoutedEventArgs e)
    {
        string selected = _app.State.CoolingProfileDisplay;
        bool firmwarePolicy = string.Equals(
            _app.State.FanControlKind,
            FanControlKinds.FirmwarePolicy,
            StringComparison.Ordinal);
        string[] profiles = BuildHomeFanExtraProfiles(selected, firmwarePolicy);
        if (profiles.Length == 0)
        {
            RefreshHomeFanProfiles();
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = HomeFanMoreButton,
            Placement = PlacementMode.Bottom
        };

        foreach (string profile in profiles)
        {
            var item = new MenuItem
            {
                Header = profile,
                Tag = profile,
                IsCheckable = true,
                IsChecked = profile.Equals(selected, StringComparison.OrdinalIgnoreCase),
                IsEnabled = !IsManualHomeFanState(profile)
            };
            item.Click += HomeFanMoreProfile_Click;
            menu.Items.Add(item);
        }

        HomeFanMoreButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private async void HomeFanMoreProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string profile })
            return;

        SetHomeFanControlsEnabled(false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            RefreshHomeFanProfiles();
        }
    }

    private async void HomeFanAuto_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;

        // Like Adaptive brightness, Auto is a real on/off control. Leaving Auto
        // returns to the neutral Balanced preset rather than silently doing nothing.
        string profile = HomeFanAutoSwitch.IsChecked == true ? "Auto" : "Balanced";
        SetHomeFanControlsEnabled(false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            RefreshHomeFanProfiles();
        }
    }

    private async void HomeFanQuick_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string profile })
            return;

        SetHomeFanControlsEnabled(false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            RefreshHomeFanProfiles();
        }
    }

    private void SetHomeFanControlsEnabled(bool enabled)
    {
        HomeFanQuickGrid.IsEnabled = enabled;
        HomeFanMoreButton.IsEnabled = enabled;
        HomeFanAutoSwitch.IsEnabled = enabled;
    }

    private void HomeAudioSafety_ModeChanged(AudioSafetyMode mode) =>
        Dispatcher.BeginInvoke(new Action(RefreshHomeAudioSafety));

    private void RefreshHomeAudioSafety()
    {
        if (HomeAudioSafetyNormal is null || HomeAudioSafetyMediaLock is null ||
            HomeAudioSafetySilent is null || HomeAudioSafetyStatus is null)
        {
            return;
        }

        AudioSafetyMode mode = _app.AudioSafety.Mode;
        HomeAudioSafetyNormal.IsChecked = mode == AudioSafetyMode.Normal;
        HomeAudioSafetyMediaLock.IsChecked = mode == AudioSafetyMode.MediaLock;
        HomeAudioSafetySilent.IsChecked = mode == AudioSafetyMode.Silent;
        HomeAudioSafetyStatus.Text = mode switch
        {
            AudioSafetyMode.MediaLock => "Gesture lock · touchpad volume/track/seek actions are blocked; keyboard and Windows/app audio still work.",
            AudioSafetyMode.Silent => "Silent · touchpad media actions are blocked and Windows output is kept muted, including after keyboard/app unmute attempts.",
            _ => "Normal · ThinkControl touchpad media and volume actions are available."
        };
    }

    internal void PrepareHomeAudioSafetyForSnapshot(AudioSafetyMode mode)
    {
        HomeAudioSafetyNormal.IsChecked = mode == AudioSafetyMode.Normal;
        HomeAudioSafetyMediaLock.IsChecked = mode == AudioSafetyMode.MediaLock;
        HomeAudioSafetySilent.IsChecked = mode == AudioSafetyMode.Silent;
        HomeAudioSafetyStatus.Text = mode switch
        {
            AudioSafetyMode.MediaLock => "Media lock active · Windows/app audio still works; ThinkControl media and volume gestures are locked.",
            AudioSafetyMode.Silent => "Silent active · output is muted and ThinkControl media/output actions are locked.",
            _ => "Normal · ThinkControl media and volume controls are available."
        };
    }

    private async void HomeAudioSafety_Click(object sender, RoutedEventArgs e)
    {
        if (_homeAudioSafetyBusy || sender is not FrameworkElement { Tag: string raw })
            return;

        AudioSafetyMode mode = raw switch
        {
            "MediaLock" => AudioSafetyMode.MediaLock,
            "Silent" => AudioSafetyMode.Silent,
            _ => AudioSafetyMode.Normal
        };

        _homeAudioSafetyBusy = true;
        SetHomeAudioSafetyEnabled(false);
        try
        {
            await _app.SetAudioSafetyModeAsync(mode);
        }
        finally
        {
            _homeAudioSafetyBusy = false;
            SetHomeAudioSafetyEnabled(true);
            RefreshHomeAudioSafety();
        }
    }

    private void SetHomeAudioSafetyEnabled(bool enabled)
    {
        HomeAudioSafetyNormal.IsEnabled = enabled;
        HomeAudioSafetyMediaLock.IsEnabled = enabled;
        HomeAudioSafetySilent.IsEnabled = enabled;
    }

    private void BatteryProtectionJump_Click(object sender, RoutedEventArgs e)
    {
        Navigate("Battery");
        Dispatcher.BeginInvoke(new Action(() => BatteryTelemetryPanelControl?.BringPreservationIntoView()),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void HomeBattery_Click(object sender, MouseButtonEventArgs e) => Navigate("Battery");
}
