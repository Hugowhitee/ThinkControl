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
        ConfigureShellChromePolish();

        if (_notificationButtonConfigured)
        {
            SyncNotificationIndicator();
            return;
        }

        if (NavHome.Parent is not StackPanel navStack)
            return;

        Grid? utilityRow = navStack.Children.OfType<Grid>().FirstOrDefault(grid =>
            grid.Tag as string == "ThinkControl.UtilityRow");
        Button? notificationButton = utilityRow?.Children.OfType<Button>()
            .FirstOrDefault(child => Equals(child.Tag, ShellUtilityOrder.NotificationTag));
        Button? compactButton = utilityRow?.Children.OfType<Button>()
            .FirstOrDefault(child => Equals(child.Tag, ShellUtilityOrder.ViewModeTag));
        if (utilityRow is null || notificationButton is null || compactButton is null)
            return;

        _notificationButtonConfigured = true;

        compactButton.Margin = new Thickness(0, 0, 4, 0);
        compactButton.Background = Brushes.Transparent;
        compactButton.BorderBrush = Brushes.Transparent;
        compactButton.BorderThickness = new Thickness(0);
        ShellUtilityOrder.ConfigureModeButton(
            compactButton,
            "Compact",
            "CompactView",
            (Brush)FindResource("Tc.TextMuted"));
        TcToolTip.Apply(compactButton, "Compact view");

        notificationButton.Width = 34;
        notificationButton.Height = 34;
        notificationButton.Padding = new Thickness(0);
        notificationButton.Margin = new Thickness(0);
        notificationButton.BorderThickness = new Thickness(0);
        notificationButton.BorderBrush = Brushes.Transparent;
        notificationButton.Background = Brushes.Transparent;

        var bell = new Path
        {
            Data = Geometry.Parse("M10,2.1 C6.5,2.1 5,4.7 5,7.7 V10.6 L3.3,13.2 H16.7 L15,10.6 V7.7 C15,4.7 13.5,2.1 10,2.1 Z M7.7,15 C8.1,16.1 8.9,16.6 10,16.6 C11.1,16.6 11.9,16.1 12.3,15"),
            StrokeThickness = 1.55,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Width = 18,
            Height = 18,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        bell.SetResourceReference(Shape.StrokeProperty, "Tc.TextMuted");

        _notificationDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 1, 0),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        _notificationDot.SetResourceReference(Shape.FillProperty, "Tc.Accent");
        _notificationDot.SetResourceReference(Shape.StrokeProperty, "Tc.Surface");

        var content = new Grid { Width = 22, Height = 22 };
        content.Children.Add(bell);
        content.Children.Add(_notificationDot);
        notificationButton.Content = content;
        notificationButton.Tag = ShellUtilityOrder.NotificationTag;
        notificationButton.Click += (_, _) => ToggleNotificationSheet();
        _notificationIndicator = notificationButton;
        ShellUtilityOrder.Apply(utilityRow, notificationButton, compactButton);

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
        Dispatcher.BeginInvoke(SyncNotificationIndicator);

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
                ? "Notifications · setup attention"
                : "Notifications · fan calibration required"
            : updateAttention && hardwareAttention
                ? "Notifications · update + hardware"
                : updateAttention
                    ? "Notifications · update available"
                    : hardwareAttention
                        ? "Notifications · hardware attention"
                        : "Notifications";
        TcToolTip.Apply(_notificationIndicator, label);
    }
}
