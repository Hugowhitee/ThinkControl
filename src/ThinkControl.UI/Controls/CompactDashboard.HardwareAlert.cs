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

        var icon = new Grid { Width = 18, Height = 18 };
        icon.Children.Add(new Path
        {
            Data = Geometry.Parse("M9,2 C5.7,2 4.2,4.4 4.2,7.2 V10.1 L2.8,12.2 H15.2 L13.8,10.1 V7.2 C13.8,4.4 12.3,2 9,2 Z M7.2,14.1 C7.6,15.1 8.2,15.6 9,15.6 C9.8,15.6 10.4,15.1 10.8,14.1"),
            Stroke = (Brush)FindResource("Tc.TextMuted"),
            StrokeThickness = 1.35,
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
            ToolTip = "Hardware setup",
            Content = icon,
            Visibility = Visibility.Collapsed
        };
        _hardwareAlertButton.Click += (_, _) => _app?.OpenHardwareAttention();
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
        bool show = statusAttention || providerAttention;

        _hardwareAlertButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _hardwareAlertDot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _hardwareAlertButton.ToolTip = show
            ? $"Hardware attention · {_app.State.DriverStatus}"
            : "Hardware ready";
    }
}
