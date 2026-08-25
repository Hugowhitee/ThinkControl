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

        _notificationButtonConfigured = true;

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
            Width = 7,
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeThickness = 1
        };
        _notificationDot.SetResourceReference(Shape.FillProperty, "Tc.Accent");
        _notificationDot.SetResourceReference(Shape.StrokeProperty, "Tc.Surface");

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        content.Children.Add(bell);

        var label = new TextBlock
        {
            Text = "Notifications",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0)
        };
        Grid.SetColumn(label, 1);
        content.Children.Add(label);

        Grid.SetColumn(_notificationDot, 2);
        content.Children.Add(_notificationDot);

        var button = new Button
        {
            Height = 40,
            Margin = new Thickness(10, 2, 8, 5),
            Padding = new Thickness(6, 0, 7, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = content,
            Style = TryFindResource("TcButton") as Style,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            ToolTip = "Notifications"
        };
        button.Click += (_, _) => ShowNotificationSheet();

        _notificationIndicator = button;
        navStack.Children.Insert(0, button);

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

        bool hardwareAttention = !state.DriverStatus.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
                                 !state.CanSensorTelemetry ||
                                 (DeviceCapabilityExpectations.ExpectsFanTelemetry(state) && !state.CanFanTelemetry) ||
                                 (DeviceCapabilityExpectations.ExpectsWritableFanControl(state) && !state.CanFanControl) ||
                                 (DeviceCapabilityExpectations.ExpectsKeyboardBacklight(state) && !state.CanKeyboardBacklight);
        bool updateAttention = _app.LatestUpdateResult?.Available == true;
        bool attention = hardwareAttention || updateAttention;

        _notificationDot.Visibility = attention ? Visibility.Visible : Visibility.Collapsed;
        _notificationIndicator.ToolTip = updateAttention && hardwareAttention
            ? "Notifications · update and hardware attention"
            : updateAttention
                ? "Notifications · update available"
                : hardwareAttention
                    ? "Notifications · hardware setup needs attention"
                    : "Notifications";
    }
}
