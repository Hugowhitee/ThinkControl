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
    private Grid? _notificationIndicator;
    private Ellipse? _notificationDot;

    private void ConfigureNotificationButton()
    {
        if (_notificationButtonConfigured)
        {
            SyncNotificationIndicator();
            return;
        }

        if (Content is not Border { Child: Grid root } ||
            root.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 1) is not Grid body ||
            body.Children.OfType<Border>().FirstOrDefault(border => Grid.GetColumn(border) == 0) is not Border sidebar ||
            sidebar.Child is not Grid sidebarGrid)
        {
            return;
        }

        _notificationButtonConfigured = true;

        // Keep notifications available from every Advanced page without floating a
        // button over Home content. The sidebar footer owns a quiet icon-only target;
        // the red dot is the status signal, not another framed toolbar button.
        var icon = new Grid
        {
            Width = 28,
            Height = 28,
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Opacity = 0.78
        };

        var bell = new Path
        {
            Data = Geometry.Parse("M10,2.1 C6.5,2.1 5,4.7 5,7.7 V10.6 L3.3,13.2 H16.7 L15,10.6 V7.7 C15,4.7 13.5,2.1 10,2.1 Z M7.7,15 C8.1,16.1 8.9,16.6 10,16.6 C11.1,16.6 11.9,16.1 12.3,15"),
            StrokeThickness = 1.75,
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
        icon.Children.Add(bell);

        _notificationDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 3, 0),
            StrokeThickness = 1
        };
        _notificationDot.SetResourceReference(Shape.FillProperty, "Tc.Accent");
        _notificationDot.SetResourceReference(Shape.StrokeProperty, "Tc.Window");
        icon.Children.Add(_notificationDot);

        icon.MouseEnter += (_, _) => icon.Opacity = 1.0;
        icon.MouseLeave += (_, _) => icon.Opacity = 0.78;
        icon.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _app.OpenNotificationCenter();
        };

        _notificationIndicator = icon;
        Grid.SetRow(icon, 1);
        Panel.SetZIndex(icon, 20);
        sidebarGrid.Children.Add(icon);

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
        if (_notificationDot is null || _notificationIndicator is null || DataContext is not AppState state)
            return;

        bool attention = !state.DriverStatus.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
                         !state.CanSensorTelemetry ||
                         !state.CanFanTelemetry ||
                         !state.CanKeyboardBacklight;
        _notificationDot.Visibility = attention ? Visibility.Visible : Visibility.Collapsed;
        _notificationIndicator.ToolTip = attention
            ? "Notifications · hardware setup needs attention"
            : "Notifications";
    }
}
