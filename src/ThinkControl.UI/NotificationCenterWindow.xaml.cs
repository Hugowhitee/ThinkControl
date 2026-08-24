using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class NotificationCenterWindow : Window
{
    private readonly App _app;
    private bool _actionBusy;

    internal NotificationCenterWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshMessagesAsync();
    }

    private async Task RefreshMessagesAsync()
    {
        MessagesPanel.Children.Clear();

        HardwareSetupStatus setup;
        try
        {
            setup = await _app.RefreshHardwareSetupStatusAsync();
        }
        catch
        {
            setup = new HardwareSetupStatus(false, false, true, false, false,
                "Could not query ThinkControl hardware service", "Provider status unavailable");
        }

        var messages = new List<InboxMessage>();

        if (!setup.ServiceRunning)
        {
            messages.Add(new(
                "Hardware service needs attention",
                setup.ServiceDetail + ". Repair ThinkControl's own service before retrying hardware providers.",
                "Repair service",
                InboxAction.HardwareSetup,
                true));
        }

        if (setup.LowLevelAccessRelevant && !setup.LowLevelAccessInstalled)
        {
            messages.Add(new(
                "PawnIO is not installed",
                "Windows does not report the PawnIO kernel driver. ThinkControl can download the pinned official installer, verify its SHA-256 and then retry provider discovery.",
                "Install PawnIO",
                InboxAction.HardwareSetup,
                true));
        }

        if (!_app.State.CanSensorTelemetry)
        {
            string detail = setup.LowLevelAccessInstalled
                ? "PawnIO is already installed. ThinkControl will rebuild its LibreHardwareMonitor provider instead of reinstalling the driver."
                : "Sensor discovery is waiting for the low-level provider shown above.";
            messages.Add(new(
                "Sensors are unavailable",
                detail,
                setup.LowLevelAccessInstalled ? "Retry sensors" : string.Empty,
                setup.LowLevelAccessInstalled ? InboxAction.RefreshSensors : InboxAction.None,
                true));
        }

        if (!_app.State.CanFanControl)
        {
            messages.Add(new(
                "Fan control is unavailable",
                _app.State.CanFanTelemetry
                    ? "Fan telemetry is visible, but the verified X9 EC provider has not passed read/write validation. Retry only recycles PawnIO/EC; it never guesses registers."
                    : "Fan telemetry and the verified X9 EC control path are not ready. Retry the provider before changing drivers.",
                "Retry fan provider",
                InboxAction.RefreshFans,
                true));
        }

        if (!_app.State.CanKeyboardBacklight)
        {
            messages.Add(new(
                "Keyboard control is unavailable",
                "ThinkControl has not received a valid readback from the Lenovo PM/EnergyDrv keyboard provider. Retry probes the installed providers again without installing anything.",
                "Retry keyboard",
                InboxAction.RefreshKeyboard,
                true));
        }

        bool reportReady = DeviceSupportReportService.HasUsefulDiscovery(_app.State);
        if (reportReady)
        {
            messages.Add(new(
                "Useful device support data is ready",
                DeviceSupportReportService.DiscoverySummary(_app.State) + ". Review the hardware-only report before sharing it; it helps distinguish missing drivers from ThinkControl provider bugs.",
                "Review sharing",
                InboxAction.Diagnostics,
                false));
        }

        if (messages.Count == 0)
        {
            messages.Add(new(
                "Everything looks ready",
                "The ThinkControl service and detected hardware providers are responding. Compatibility discoveries will appear here only when something actually needs attention.",
                string.Empty,
                InboxAction.None,
                false));
        }

        int attentionCount = messages.Count(message => message.Warning);
        SummaryDot.Fill = (Brush)FindResource(attentionCount > 0 ? "Tc.Warning" : "Tc.Success");
        SummaryText.Text = attentionCount > 0
            ? $"{attentionCount} item{(attentionCount == 1 ? string.Empty : "s")} need attention · {_app.State.DriverStatus}"
            : "Hardware providers ready · no action required";

        foreach (InboxMessage message in messages)
            MessagesPanel.Children.Add(CreateMessageCard(message));
    }

    private FrameworkElement CreateMessageCard(InboxMessage message)
    {
        var section = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 0, 0, 9)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (!string.IsNullOrWhiteSpace(message.Button))
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = (Brush)FindResource(message.Warning ? "Tc.Warning" : "Tc.Success"),
            Margin = new Thickness(0, 5, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(dot);

        var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        text.Children.Add(new TextBlock { Text = message.Title, FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = message.Detail,
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        text.Children.Add(detail);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (!string.IsNullOrWhiteSpace(message.Button))
        {
            var button = new Button
            {
                Content = message.Button,
                Style = TryFindResource("TcButton") as Style,
                MinWidth = 112,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = message.Action
            };
            button.Click += Action_Click;
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);
        }

        section.Child = grid;
        return section;
    }

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_actionBusy || sender is not Button button || button.Tag is not InboxAction action)
            return;

        _actionBusy = true;
        button.IsEnabled = false;
        try
        {
            switch (action)
            {
                case InboxAction.HardwareSetup:
                    _app.OpenHardwareSetup();
                    break;
                case InboxAction.Diagnostics:
                    _app.OpenAdvanced("Settings");
                    Activate();
                    break;
                case InboxAction.RefreshSensors:
                case InboxAction.RefreshFans:
                case InboxAction.RefreshKeyboard:
                    button.Content = "Retrying…";
                    await _app.RefreshHardwareProvidersAsync();
                    await RefreshMessagesAsync();
                    break;
            }
        }
        finally
        {
            _actionBusy = false;
            button.IsEnabled = true;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshMessagesAsync();
    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record InboxMessage(string Title, string Detail, string Button, InboxAction Action, bool Warning);

    private enum InboxAction
    {
        None,
        HardwareSetup,
        Diagnostics,
        RefreshSensors,
        RefreshFans,
        RefreshKeyboard
    }
}
