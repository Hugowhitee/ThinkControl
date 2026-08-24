using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _notificationButtonConfigured;
    private Button? _notificationButton;
    private Ellipse? _notificationDot;

    private void ConfigureNotificationButton()
    {
        if (_notificationButtonConfigured)
        {
            SyncNotificationIndicator();
            return;
        }

        if (Content is not Border { Child: Grid root } ||
            root.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 1) is not Grid body)
        {
            return;
        }

        _notificationButtonConfigured = true;

        var icon = new Grid { Width = 20, Height = 20 };
        var bell = new Path
        {
            Data = Geometry.Parse("M10,2.1 C6.5,2.1 5,4.7 5,7.7 V10.6 L3.3,13.2 H16.7 L15,10.6 V7.7 C15,4.7 13.5,2.1 10,2.1 Z M7.7,15 C8.1,16.1 8.9,16.6 10,16.6 C11.1,16.6 11.9,16.1 12.3,15"),
            StrokeThickness = 1.65,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent
        };
        bell.SetResourceReference(Shape.StrokeProperty, "Tc.TextMuted");
        icon.Children.Add(bell);

        _notificationDot = new Ellipse
        {
            Width = 7,
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -1, -1, 0),
            StrokeThickness = 1
        };
        _notificationDot.SetResourceReference(Shape.FillProperty, "Tc.Accent");
        _notificationDot.SetResourceReference(Shape.StrokeProperty, "Tc.Window");
        icon.Children.Add(_notificationDot);

        _notificationButton = new Button
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            Style = TryFindResource("TcIconButton") as Style,
            Content = icon,
            ToolTip = "Notifications",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 20, 0)
        };
        _notificationButton.Click += (_, _) => _app.OpenNotificationCenter();
        Grid.SetColumn(_notificationButton, 1);
        Panel.SetZIndex(_notificationButton, 50);
        body.Children.Add(_notificationButton);

        if (DataContext is AppState state)
            state.PropertyChanged += NotificationState_PropertyChanged;
        Closed += (_, _) =>
        {
            if (DataContext is AppState closingState)
                closingState.PropertyChanged -= NotificationState_PropertyChanged;
        };

        SyncNotificationIndicator();
    }

    private void NotificationState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.DriverStatus)
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
        if (_notificationDot is null || _notificationButton is null || DataContext is not AppState state)
            return;

        bool attention = !state.DriverStatus.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
                         !state.CanSensorTelemetry ||
                         !state.CanFanTelemetry ||
                         !state.CanKeyboardBacklight;
        _notificationDot.Visibility = attention ? Visibility.Visible : Visibility.Collapsed;
        _notificationButton.ToolTip = attention
            ? "Notifications · hardware setup needs attention"
            : "Notifications";
    }
}
