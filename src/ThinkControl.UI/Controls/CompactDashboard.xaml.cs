using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard : UserControl
{
    private App? _app;

    public CompactDashboard()
    {
        InitializeComponent();
    }

    internal void Initialize(App app)
    {
        if (!ReferenceEquals(_app, app))
        {
            if (_app is not null)
            {
                _app.State.PropertyChanged -= State_PropertyChanged;
                _app.UpdateAvailabilityChanged -= App_UpdateAvailabilityChanged;
            }

            _app = app;
            app.State.PropertyChanged += State_PropertyChanged;
            app.UpdateAvailabilityChanged += App_UpdateAvailabilityChanged;
        }

        EnsureAudioRow();
        EnsureQuickControls();
        EnsureHardwareAlert();
        SyncQuickControls();
        SyncHardwareAlert();
    }

    private void App_UpdateAvailabilityChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(SyncHardwareAlert);

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.SelectedMode)
            or nameof(AppState.CoolingProfile)
            or nameof(AppState.CanFanControl)
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
