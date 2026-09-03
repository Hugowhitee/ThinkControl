using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string MoreFanProfilesLabel = "Auto / custom…";
    private bool _homeQuickControlsConfigured;

    private void ConfigureHomeQuickControls()
    {
        if (!_homeQuickControlsConfigured)
        {
            _homeQuickControlsConfigured = true;

            // Home has one deliberately simple power control, so it always edits the
            // battery preference. The full Performance page remains the source for
            // independent AC/DC configuration. Detach the generic current-source
            // handler that XAML wires for these three Home buttons.
            HomeQuiet.Click -= Mode_Click;
            HomeBalanced.Click -= Mode_Click;
            HomePerformance.Click -= Mode_Click;
            HomeQuiet.Click += HomeBatteryMode_Click;
            HomeBalanced.Click += HomeBatteryMode_Click;
            HomePerformance.Click += HomeBatteryMode_Click;

            _app.State.PropertyChanged += HomeQuickState_PropertyChanged;
            Closed += (_, _) => _app.State.PropertyChanged -= HomeQuickState_PropertyChanged;
        }

        SyncHomeBatteryMode();
        RefreshHomeFanProfiles();
    }

    private void HomeQuickState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.AppState.CoolingProfile))
        {
            // AdvancedWindow.SyncControls is subscribed earlier and historically
            // fell back to Auto when its fixed item list did not contain a manual
            // state. Rebuild the Home fan choices on the next dispatcher turn so a
            // manual target is shown truthfully and Auto remains a distinct action.
            Dispatcher.BeginInvoke(new Action(RefreshHomeFanProfiles));
            return;
        }

        if (e.PropertyName != nameof(ViewModels.AppState.SelectedMode))
            return;

        // AdvancedWindow.SyncControls is subscribed earlier and mirrors the current
        // source into both sets of controls. Re-apply Home's battery-only meaning on
        // the next dispatcher turn so AC state can never make this card lie.
        Dispatcher.BeginInvoke(new Action(SyncHomeBatteryMode));
    }

    private void HomeBatteryMode_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string tag } ||
            !Enum.TryParse(tag, out ThinkControlPowerMode mode))
        {
            return;
        }

        if (!_app.SetPowerPreference(mode, onBattery: true))
        {
            SyncHomeBatteryMode();
            return;
        }

        SyncHomeBatteryMode();
    }

    private void SyncHomeBatteryMode()
    {
        if (HomeQuiet is null || HomeBalanced is null || HomePerformance is null)
            return;

        ThinkControlPowerMode battery = _app.GetPowerPreference(onBattery: true);
        _syncing = true;
        try
        {
            HomeQuiet.IsChecked = battery == ThinkControlPowerMode.Quiet;
            HomeBalanced.IsChecked = battery == ThinkControlPowerMode.Balanced;
            HomePerformance.IsChecked = battery == ThinkControlPowerMode.Performance;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RefreshHomeFanProfiles()
    {
        if (HomeFanProfileCombo is null)
            return;

        string selected = _app.State.CoolingProfileDisplay;
        bool manual = IsManualHomeFanState(selected);
        _syncing = true;
        try
        {
            HomeFanQuickGrid.IsEnabled = _app.State.CanFanControl;
            HomeFanQuiet.IsChecked = selected.Equals("Quiet", StringComparison.OrdinalIgnoreCase);
            HomeFanBalanced.IsChecked = selected.Equals("Balanced", StringComparison.OrdinalIgnoreCase);
            HomeFanMax.IsChecked = selected.Equals("Max cooling", StringComparison.OrdinalIgnoreCase);

            var values = new List<string>();
            if (manual)
                values.Add(selected);
            values.Add(MoreFanProfilesLabel);
            values.Add("Auto");
            values.AddRange(_app.FanProfiles.GetProfiles()
                .Where(profile => !_app.FanProfiles.IsBuiltIn(profile.Id))
                .Select(profile => profile.Name));
            HomeFanProfileCombo.ItemsSource = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            HomeFanProfileCombo.SelectedItem = values.Contains(selected, StringComparer.OrdinalIgnoreCase)
                ? selected
                : MoreFanProfilesLabel;
            if (HomeFanProfileCombo.SelectedItem is null)
                HomeFanProfileCombo.SelectedItem = MoreFanProfilesLabel;
            HomeFanProfileCombo.IsEnabled = _app.State.CanFanControl;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static bool IsManualHomeFanState(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("Manual ", StringComparison.OrdinalIgnoreCase);

    private void HomeFanProfile_DropDownOpened(object sender, EventArgs e) => RefreshHomeFanProfiles();

    private async void HomeFanProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || HomeFanProfileCombo.SelectedItem is not string profile ||
            profile.Equals(MoreFanProfilesLabel, StringComparison.OrdinalIgnoreCase) ||
            IsManualHomeFanState(profile))
            return;

        HomeFanProfileCombo.IsEnabled = false;
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

        HomeFanQuickGrid.IsEnabled = false;
        HomeFanProfileCombo.IsEnabled = false;
        try { await _app.SetCoolingProfileAsync(profile); }
        finally { RefreshHomeFanProfiles(); }
    }

    private void HomeBattery_Click(object sender, MouseButtonEventArgs e) => Navigate("Battery");
}