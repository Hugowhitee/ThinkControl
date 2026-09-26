using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _homeQuickControlsConfigured;
    private bool _homeModeBusy;
    private bool _homeFanBusy;

    private void ConfigureHomeQuickControls()
    {
        if (!_homeQuickControlsConfigured)
        {
            _homeQuickControlsConfigured = true;
            _app.State.PropertyChanged += HomeQuickState_PropertyChanged;
            _app.Modes.Changed += HomeModes_Changed;
            Closed += (_, _) =>
            {
                _app.State.PropertyChanged -= HomeQuickState_PropertyChanged;
                _app.Modes.Changed -= HomeModes_Changed;
            };
        }

        SyncHomePowerModes();
        RefreshHomeFanProfiles();
        RefreshHomeMode();
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

        if (e.PropertyName == nameof(ViewModels.AppState.SelectedPowerMode))
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
            HomeFanQuickGrid.IsEnabled = enabled && !autoActive && !_homeFanBusy;
            HomeFanQuiet.IsChecked = selected.Equals("Quiet", StringComparison.OrdinalIgnoreCase);
            HomeFanBalanced.IsChecked = selected.Equals("Balanced", StringComparison.OrdinalIgnoreCase);
            HomeFanMax.IsChecked = selected.Equals("Max cooling", StringComparison.OrdinalIgnoreCase);

            HomeFanAutoSwitch.IsChecked = autoActive;
            HomeFanAutoSwitch.IsEnabled = enabled && !_homeFanBusy;

            int selectableExtraCount = extraProfiles.Count(profile => !IsManualHomeFanState(profile));
            bool currentUsesMore = extraProfiles.Contains(selected, StringComparer.OrdinalIgnoreCase);
            HomeFanMoreButton.IsEnabled = enabled && !autoActive && !_homeFanBusy && selectableExtraCount > 0;
            HomeFanMoreButton.Opacity = HomeFanMoreButton.IsEnabled ? 1.0 : 0.42;
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
        if (_homeFanBusy || sender is not FrameworkElement { Tag: string profile })
            return;

        _homeFanBusy = true;
        SetHomeFanControlsEnabled(false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            _homeFanBusy = false;
            RefreshHomeFanProfiles();
        }
    }

    private async void HomeFanAuto_Click(object sender, RoutedEventArgs e)
    {
        // IsChecked assignments performed by RefreshHomeFanProfiles do not raise
        // Click. Do not discard a real user click merely because another Home
        // control is being synchronized on the dispatcher at the same moment.
        if (_homeFanBusy)
            return;

        // Like Adaptive brightness, Auto is a real on/off control. Leaving Auto
        // returns to the neutral Balanced preset rather than silently doing nothing.
        string profile = HomeFanAutoSwitch.IsChecked == true ? "Auto" : "Balanced";
        _homeFanBusy = true;
        SetHomeFanControlsEnabled(false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            _homeFanBusy = false;
            RefreshHomeFanProfiles();
        }
    }

    private async void HomeFanQuick_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _homeFanBusy || sender is not FrameworkElement { Tag: string profile })
            return;

        _homeFanBusy = true;
        SetHomeFanControlsEnabled(false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            _homeFanBusy = false;
            RefreshHomeFanProfiles();
        }
    }

    private void SetHomeFanControlsEnabled(bool enabled)
    {
        HomeFanQuickGrid.IsEnabled = enabled;
        HomeFanMoreButton.IsEnabled = enabled;
        HomeFanAutoSwitch.IsEnabled = enabled;
    }

    private void HomeModes_Changed() =>
        Dispatcher.BeginInvoke(new Action(RefreshHomeMode));

    private void RefreshHomeMode()
    {
        if (HomeModeCombo is null || HomeModeName is null || HomeModeModifiedText is null)
            return;

        IReadOnlyList<ThinkControlModeDefinition> modes = _app.Modes.GetModes();
        ThinkControlModeDefinition? active = modes.FirstOrDefault(mode =>
            mode.Id.Equals(_app.Modes.ActiveModeId, StringComparison.OrdinalIgnoreCase));

        _syncing = true;
        try
        {
            HomeModeCombo.ItemsSource = modes;
            HomeModeCombo.SelectedItem = active;
            HomeModeName.Text = _app.Modes.ActiveModeName;
            HomeModeModifiedText.Visibility = _app.Modes.IsModified
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void PrepareHomeModeForSnapshot(string id, bool modified = false)
    {
        IReadOnlyList<ThinkControlModeDefinition> modes = _app.Modes.GetModes();
        ThinkControlModeDefinition? mode = modes.FirstOrDefault(item =>
            item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
            ThinkControlModeCatalog.BuiltIns.First();

        _syncing = true;
        try
        {
            HomeModeCombo.ItemsSource = modes;
            HomeModeCombo.SelectedItem = mode;
            HomeModeName.Text = mode.Name;
            HomeModeModifiedText.Visibility = modified ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void HomeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _homeModeBusy ||
            HomeModeCombo.SelectedItem is not ThinkControlModeDefinition mode ||
            mode.Id.Equals(_app.Modes.ActiveModeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _homeModeBusy = true;
        HomeModeCombo.IsEnabled = false;
        try
        {
            await _app.Modes.ActivateAsync(mode.Id);
        }
        finally
        {
            _homeModeBusy = false;
            HomeModeCombo.IsEnabled = true;
            RefreshHomeMode();
        }
    }

    private void BatteryProtectionJump_Click(object sender, RoutedEventArgs e)
    {
        Navigate("Battery");
        Dispatcher.BeginInvoke(new Action(() => BatteryTelemetryPanelControl?.BringPreservationIntoView()),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void HomeBattery_Click(object sender, MouseButtonEventArgs e) => Navigate("Battery");
}
