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

    private void ConfigureHomeQuickControls()
    {
        if (!_homeQuickControlsConfigured)
        {
            _homeQuickControlsConfigured = true;
            _app.State.PropertyChanged += HomeQuickState_PropertyChanged;
            _app.Modes.Changed += HomeModes_Changed;
            _app.ModeAutomation.Changed += HomeModes_Changed;
            Closed += (_, _) =>
            {
                _app.State.PropertyChanged -= HomeQuickState_PropertyChanged;
                _app.Modes.Changed -= HomeModes_Changed;
                _app.ModeAutomation.Changed -= HomeModes_Changed;
            };
        }

        SyncHomePowerModes();
        RefreshHomeCoolingSummary();
        RefreshHomeMode();
    }

    private void HomeQuickState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModels.AppState.CoolingProfile) or
            nameof(ViewModels.AppState.CanFanControl) or
            nameof(ViewModels.AppState.FanControlKind))
        {
            Dispatcher.BeginInvoke(new Action(RefreshHomeCoolingSummary));
            return;
        }

        if (e.PropertyName is nameof(ViewModels.AppState.SelectedPowerMode) or nameof(ViewModels.AppState.BatteryStatus))
            Dispatcher.BeginInvoke(new Action(SyncHomePowerModes));
    }

    private void HomePowerMode_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string tag })
            return;

        string[] parts = tag.Split(':', 2);
        if (parts.Length != 2 || !Enum.TryParse(parts[1], true, out ThinkControlPowerMode mode))
            return;

        bool onBattery = parts[0] == "Current" ? HomeUsesBattery : parts[0].Equals("Battery", StringComparison.OrdinalIgnoreCase);
        _ = _app.SetPowerPreference(mode, onBattery);
        SyncHomePowerModes();
    }

    private bool HomeUsesBattery => _snapshotUiPrepared
        ? !_app.State.BatteryStatus.Contains("Plugged", StringComparison.OrdinalIgnoreCase) && !_app.State.BatteryCharging
        : System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;

    private void SyncHomePowerModes()
    {
        if (HomeQuiet is null) return;
        bool onBattery = HomeUsesBattery;
        ThinkControlPowerMode mode = _app.GetPowerPreference(onBattery);
        _syncing = true;
        try
        {
            HomeQuiet.IsChecked = mode == ThinkControlPowerMode.Quiet;
            HomeBalanced.IsChecked = mode == ThinkControlPowerMode.Balanced;
            HomePerformance.IsChecked = mode == ThinkControlPowerMode.Performance;
            HomePowerSourceText.Text = onBattery ? "On battery" : "Plugged in";
            HomePowerOtherText.Text = $"{(onBattery ? "Plugged-in" : "Battery")} profile: {App.PowerPreferenceDisplayName(_app.GetPowerPreference(!onBattery))}";
        }
        finally { _syncing = false; }
    }

    private void RefreshHomeCoolingSummary()
    {
        if (HomeCurveAvailabilityText is null) return;
        HomeCurveAvailabilityText.Text = !_app.State.CanFanControl ||
            _app.State.FanControlKind is FanControlKinds.FullSpeedOnly or FanControlKinds.FirmwarePolicy
            ? "Unavailable" : _app.FanCalibrationState.Required ? "Calibration required" : "Available";
    }

    private void HomeModes_Changed() =>
        Dispatcher.BeginInvoke(new Action(RefreshHomeMode));

    private void RefreshHomeMode()
    {
        if (HomeModeCombo is null || HomeModeModifiedText is null)
            return;

        IReadOnlyList<ThinkControlModeDefinition> modes = _app.Modes.GetModes();
        ThinkControlModeDefinition? active = modes.FirstOrDefault(mode =>
            mode.Id.Equals(_app.Modes.VisibleModeId, StringComparison.OrdinalIgnoreCase));

        _syncing = true;
        try
        {
            HomeModeCombo.ItemsSource = modes;
            HomeModeCombo.IsEnabled = HomeAutomationSwitch.IsEnabled = !_homeModeBusy && !_app.Modes.IsTransitioning;
            HomeModeOffButton.IsEnabled = !_homeModeBusy && !_app.Modes.IsTransitioning && (_app.Modes.ActiveModeId != ThinkControlModeCatalog.NormalId || _app.Modes.LastTransitionError is not null);
            HomeModeCombo.SelectedItem = active;
            var presentation = ModeStatusPresentation.From(_app);
            HomeModeTitle.Text = "Mode";
            HomeModeTitle.ToolTip = presentation.Title;
            HomeModeModifiedText.Text = _app.Modes.SettingsNeedChecking ? presentation.Detail :
                _app.Modes.FailureSummary;
            HomeModeModifiedText.ToolTip = _app.Modes.LastTransitionError ?? presentation.Detail;
            HomeAutomationSwitch.IsChecked = !_app.ModeAutomation.Paused;
            HomeModeModifiedText.SetResourceReference(TextBlock.ForegroundProperty,
                presentation.Failed ? "Tc.Error" : "Tc.TextMuted");
            HomeModeModifiedText.Visibility = _app.Modes.SettingsNeedChecking || !string.IsNullOrEmpty(_app.Modes.LastTransitionError)
                ? Visibility.Visible : Visibility.Collapsed;
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
            HomeModeTitle.Text = "Mode";
            HomeModeModifiedText.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void HomeAutomationSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        if (HomeAutomationSwitch.IsChecked == true) _app.ModeAutomation.Resume();
        else _app.ModeAutomation.Pause();
        RefreshHomeMode();
    }

    private async void HomeModeOff_Click(object sender, RoutedEventArgs e)
    {
        if (_homeModeBusy || _app.Modes.IsTransitioning) return;
        _homeModeBusy = true;
        RefreshHomeMode();
        try { await _app.Modes.ActivateAsync(ThinkControlModeCatalog.NormalId); }
        finally { _homeModeBusy = false; RefreshHomeMode(); }
    }

    private async void HomeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _homeModeBusy ||
            HomeModeCombo.SelectedItem is not ThinkControlModeDefinition mode ||
            mode.Id.Equals(_app.Modes.VisibleModeId, StringComparison.OrdinalIgnoreCase))
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
