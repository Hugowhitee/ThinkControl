using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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

        // ConfigureNativeWindow creates one compact-arrow button beside the
        // ThinkControl wordmark. Branding used to leave that arrow there and this
        // class inserted a whole new Notifications row below it, which pushed the
        // logo/navigation down and left two separate view-switch controls. Reuse
        // that single top-right slot for the bell instead.
        Grid? brandRow = navStack.Children.OfType<Grid>().FirstOrDefault(grid =>
            grid.Children.OfType<Button>().Any(button => button.Tag as string == "ThinkControl.NotificationSlot"));
        Button? button = brandRow?.Children.OfType<Button>()
            .FirstOrDefault(child => child.Tag as string == "ThinkControl.NotificationSlot");
        if (button is null)
            return;

        _notificationButtonConfigured = true;
        button.Width = 30;
        button.Height = 30;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0);
        button.BorderThickness = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.ToolTip = "Inbox";

        var bell = new Path
        {
            Data = Geometry.Parse("M10,2.1 C6.5,2.1 5,4.7 5,7.7 V10.6 L3.3,13.2 H16.7 L15,10.6 V7.7 C15,4.7 13.5,2.1 10,2.1 Z M7.7,15 C8.1,16.1 8.9,16.6 10,16.6 C11.1,16.6 11.9,16.1 12.3,15"),
            StrokeThickness = 1.45,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Width = 17,
            Height = 17,
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

        var content = new Grid { Width = 21, Height = 21 };
        content.Children.Add(bell);
        content.Children.Add(_notificationDot);
        button.Content = content;
        button.Click += (_, _) => ToggleNotificationSheet();
        _notificationIndicator = button;

        if (DataContext is AppState state)
            state.PropertyChanged += NotificationState_PropertyChanged;
        _app.UpdateAvailabilityChanged += App_UpdateNotificationAvailabilityChanged;
        Closed += (_, _) =>
        {
            if (DataContext is AppState closingState)
                closingState.PropertyChanged -= NotificationState_PropertyChanged;
            _app.UpdateAvailabilityChanged -= App_UpdateNotificationAvailabilityChanged;
        };

        SyncNotificationIndicator();
    }

    private void App_UpdateNotificationAvailabilityChanged(object? sender, EventArgs e) =>
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
        bool attention = hardwareAttention || updateAttention;

        _notificationDot.Visibility = attention ? Visibility.Visible : Visibility.Collapsed;
        _notificationIndicator.ToolTip = updateAttention && hardwareAttention
            ? "Inbox · update and hardware attention"
            : updateAttention
                ? "Inbox · update available"
                : hardwareAttention
                    ? "Inbox · a required component needs attention"
                    : "Inbox";
    }
}
