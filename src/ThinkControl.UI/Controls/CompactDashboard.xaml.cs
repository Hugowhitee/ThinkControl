using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard : UserControl
{
    private App? _app;
    private bool _syncingCooling;

    public CompactDashboard()
    {
        InitializeComponent();
    }

    internal void Initialize(App app)
    {
        if (!ReferenceEquals(_app, app))
        {
            if (_app is not null)
                _app.State.PropertyChanged -= State_PropertyChanged;
            _app = app;
            app.State.PropertyChanged += State_PropertyChanged;
        }

        EnsureAudioRow();
        EnsureQuickControls();
        EnsureHardwareAlert();
        SyncCoolingProfile();
        SyncQuickControls();
        SyncHardwareAlert();
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.FanStateText) or nameof(AppState.CanFanControl))
            Dispatcher.BeginInvoke(SyncCoolingProfile);

        if (e.PropertyName is nameof(AppState.SelectedMode)
            or nameof(AppState.RefreshAutoEnabled)
            or nameof(AppState.CurrentRefreshHz)
            or nameof(AppState.MaxRefreshHz)
            or nameof(AppState.KeyboardMode)
            or nameof(AppState.KeyboardStatus)
            or nameof(AppState.CanKeyboardBacklight))
        {
            Dispatcher.BeginInvoke(SyncQuickControls);
        }

        if (e.PropertyName is nameof(AppState.DriverStatus)
            or nameof(AppState.CanSensorTelemetry)
            or nameof(AppState.CanFanTelemetry)
            or nameof(AppState.CanKeyboardBacklight))
        {
            Dispatcher.BeginInvoke(SyncHardwareAlert);
        }
    }

    private void SyncCoolingProfile()
    {
        if (_app is null)
            return;

        string profile = _app.State.FanStateText
            .Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "Lenovo Auto";
        if (profile is not ("Silent" or "Normal" or "Cool"))
            profile = "Lenovo Auto";

        _syncingCooling = true;
        try
        {
            CompactFanAuto.IsChecked = profile == "Lenovo Auto";
            CompactFanSilent.IsChecked = profile == "Silent";
            CompactFanNormal.IsChecked = profile == "Normal";
            CompactFanCool.IsChecked = profile == "Cool";
        }
        finally
        {
            _syncingCooling = false;
        }
    }

    private async void CoolingQuick_Click(object sender, RoutedEventArgs e)
    {
        if (_syncingCooling || _app is null || sender is not FrameworkElement { Tag: string profile })
            return;

        CoolingQuickGroup.SetCurrentValue(IsEnabledProperty, false);
        try
        {
            await _app.SetCoolingProfileAsync(profile);
            await _app.RefreshStatusAsync();
        }
        finally
        {
            CoolingQuickGroup.SetCurrentValue(IsEnabledProperty, _app.State.CanFanControl);
            SyncCoolingProfile();
        }
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => _app?.OpenAdvanced("Home");

    private void Hide_Click(object sender, RoutedEventArgs e) => _app?.CompactWindow.HideAnimated();

    private void OpenPage_Click(object sender, RoutedEventArgs e)
    {
        if (_app is not null && sender is FrameworkElement { Tag: string page })
            _app.OpenAdvanced(page);
    }

    private void Battery_Click(object sender, MouseButtonEventArgs e) => _app?.OpenAdvanced("Battery");

    private void Performance_Click(object sender, MouseButtonEventArgs e) => _app?.OpenAdvanced("Performance");

    private void Fans_Click(object sender, MouseButtonEventArgs e) => _app?.OpenAdvanced("Fans");
}
