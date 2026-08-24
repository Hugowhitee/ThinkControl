using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private WpfButton? _hardwareAlertButton;
    private Ellipse? _hardwareAlertDot;

    private void EnsureHardwareAlert()
    {
        if (_hardwareAlertButton is not null || Content is not Border { Child: Grid root })
            return;

        Grid? header = root.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        StackPanel? actions = header?.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 1);
        if (header is null || actions is null)
            return;

        if (header.ColumnDefinitions.Count > 1)
            header.ColumnDefinitions[1].Width = new GridLength(108);

        var icon = new Grid { Width = 19, Height = 19 };
        icon.Children.Add(new Path
        {
            Data = Geometry.Parse("M9.5,2.2 C6.2,2.2 4.7,4.7 4.7,7.5 V10.2 L3.1,12.7 H15.9 L14.3,10.2 V7.5 C14.3,4.7 12.8,2.2 9.5,2.2 Z M7.4,14.5 C7.8,15.6 8.5,16.1 9.5,16.1 C10.5,16.1 11.2,15.6 11.6,14.5"),
            Stroke = (Brush)FindResource("Tc.TextMuted"),
            StrokeThickness = 1.55,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent
        });
        _hardwareAlertDot = new Ellipse
        {
            Width = 6.5,
            Height = 6.5,
            Fill = (Brush)FindResource("Tc.Accent"),
            Stroke = (Brush)FindResource("Tc.Window"),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -1, -1, 0)
        };
        icon.Children.Add(_hardwareAlertDot);

        _hardwareAlertButton = new WpfButton
        {
            Style = TryFindResource("CompactCaptionButton") as Style,
            ToolTip = "Notifications",
            Content = icon,
            Visibility = Visibility.Visible
        };
        _hardwareAlertButton.Click += (_, _) => _app?.OpenNotificationCenter();
        actions.Children.Insert(0, _hardwareAlertButton);
    }

    private void SyncHardwareAlert()
    {
        if (_app is null || _hardwareAlertButton is null || _hardwareAlertDot is null)
            return;

        bool statusAttention = !_app.State.DriverStatus.Equals("Ready", StringComparison.OrdinalIgnoreCase);
        bool providerAttention = !_app.State.CanSensorTelemetry ||
                                 !_app.State.CanFanTelemetry ||
                                 !_app.State.CanKeyboardBacklight;
        bool showDot = statusAttention || providerAttention;

        // The inbox itself is always available; only the red dot is conditional.
        // This means users can review successful discoveries/device-report messages
        // without ThinkControl pretending there is an error.
        _hardwareAlertButton.Visibility = Visibility.Visible;
        _hardwareAlertDot.Visibility = showDot ? Visibility.Visible : Visibility.Collapsed;
        _hardwareAlertButton.ToolTip = showDot
            ? $"Notifications · hardware attention · {_app.State.DriverStatus}"
            : "Notifications";
    }
}
