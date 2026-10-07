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

        var icon = new Grid { Width = 19, Height = 19 };
        var bellPath = new PackIconLucide { Kind = "Notifications", Width = 19, Height = 19 };
        bellPath.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Tc.TextMuted");
        icon.Children.Add(bellPath);
        _hardwareAlertDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 1, 0)
        };
        _hardwareAlertDot.SetResourceReference(Shape.FillProperty, "Tc.Accent");
        _hardwareAlertDot.SetResourceReference(Shape.StrokeProperty, "Tc.Window");
        icon.Children.Add(_hardwareAlertDot);

        _hardwareAlertButton = new WpfButton
        {
            Style = TryFindResource("CompactCaptionButton") as Style,
            Tag = ShellUtilityOrder.NotificationTag,
            ToolTip = "Notifications",
            Content = icon,
            Visibility = Visibility.Visible
        };
        _hardwareAlertButton.Click += (_, _) => _app?.ToggleNotificationCenter();

        WpfButton? hideButton = actions.Children.OfType<WpfButton>()
            .FirstOrDefault(button => !ReferenceEquals(button, CompactExpandButton));
        ShellUtilityOrder.ConfigureModeButton(
            CompactExpandButton,
            "Advanced",
            "FullView");
        ShellUtilityOrder.Apply(
            actions,
            _hardwareAlertButton,
            CompactExpandButton,
            hideButton is null ? [] : [hideButton]);
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
            ? $"Notifications: {_app.State.DriverStatus}"
            : "Notifications";
    }
}
