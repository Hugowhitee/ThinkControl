using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _notificationButtonConfigured;
    private Button? _notificationIndicator;
    private Ellipse? _notificationDot;

    private void ConfigureNotificationButton()
    {
        if (_notificationButtonConfigured)
        {
            SyncNotificationIndicator();
            return;
        }

        _notificationButtonConfigured = true;
        _notificationIndicator = SidebarNotificationsButton;
        _notificationDot = SidebarNotificationDot;

        if (DataContext is AppState state)
            state.PropertyChanged += NotificationState_PropertyChanged;
        _app.UpdateAvailabilityChanged += App_UpdateNotificationAvailabilityChanged;
        _app.FanCalibrationStateChanged += App_FanCalibrationStateChanged;
        Closed += (_, _) =>
        {
            if (DataContext is AppState closingState)
                closingState.PropertyChanged -= NotificationState_PropertyChanged;
            _app.UpdateAvailabilityChanged -= App_UpdateNotificationAvailabilityChanged;
            _app.FanCalibrationStateChanged -= App_FanCalibrationStateChanged;
        };

        SyncNotificationIndicator();
    }

    private void App_UpdateNotificationAvailabilityChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(SyncNotificationIndicator);

    private void App_FanCalibrationStateChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() => { SyncNotificationIndicator(); RefreshHomeCoolingSummary(); }));

    private void NotificationState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.DriverStatus)
            or nameof(AppState.MachineType)
            or nameof(AppState.CanSensorTelemetry)
            or nameof(AppState.CanFanTelemetry)
            or nameof(AppState.CanFanControl)
            or nameof(AppState.CanKeyboardBacklight))
        {
            Dispatcher.BeginInvoke(SyncNotificationIndicator);
        }
    }

    private void SyncNotificationIndicator()
    {
        if (_notificationDot is null || _notificationIndicator is null || DataContext is not AppState state)
            return;

        string status = state.DriverStatus ?? string.Empty;
        bool stableHardwareProblem = !string.IsNullOrWhiteSpace(status) &&
                                     !status.Equals("Ready", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Checking", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Refreshing", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Verifying", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Starting", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Restarting", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Installing", StringComparison.OrdinalIgnoreCase) &&
                                     !status.StartsWith("Repairing", StringComparison.OrdinalIgnoreCase);
        bool hardwareAttention = stableHardwareProblem &&
                                 (!state.CanSensorTelemetry ||
                                  (DeviceCapabilityExpectations.ExpectsFanTelemetry(state) && !state.CanFanTelemetry) ||
                                  (DeviceCapabilityExpectations.ExpectsWritableFanControl(state) && !state.CanFanControl) ||
                                  (DeviceCapabilityExpectations.ExpectsKeyboardBacklight(state) && !state.CanKeyboardBacklight));
        bool updateAttention = _app.LatestUpdateResult?.Available == true;
        bool calibrationAttention = _app.FanCalibrationState.Required && !_app.FanCalibrationState.Running;
        bool attention = hardwareAttention || updateAttention || calibrationAttention;

        _notificationDot.Visibility = attention ? Visibility.Visible : Visibility.Collapsed;
        string label = calibrationAttention
            ? updateAttention || hardwareAttention
                ? "Notifications: setup attention"
                : "Notifications: fan calibration required"
            : updateAttention && hardwareAttention
                ? "Notifications: update and hardware attention"
                : updateAttention
                    ? "Notifications: update available"
                    : hardwareAttention
                        ? "Notifications: hardware attention"
                        : "Notifications";
        TcToolTip.Apply(_notificationIndicator, label);
    }
}
