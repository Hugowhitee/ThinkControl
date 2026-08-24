using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class NotificationCenterWindow : Window
{
    private readonly App _app;

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
            setup = new HardwareSetupStatus(false, false, true, false,
                "Could not query ThinkControl hardware service", "Provider status unavailable");
        }

        var messages = new List<InboxMessage>();

        if (!setup.ServiceRunning)
        {
            messages.Add(new(
                "Hardware service needs attention",
                setup.ServiceDetail + ". Fan, sensor and keyboard controls depend on this service connection.",
                "Open Hardware setup",
                InboxAction.HardwareSetup,
                true));
        }

        if (setup.LowLevelAccessRelevant && !setup.LowLevelAccessInstalled)
        {
            messages.Add(new(
                "Install PawnIO hardware access",
                "PawnIO is the signed low-level provider used by LibreHardwareMonitor and ThinkControl's verified X9 EC path. ThinkControl verifies its installer before starting it.",
                "Install / repair",
                InboxAction.HardwareSetup,
                true));
        }

        if (!_app.State.CanSensorTelemetry)
        {
            messages.Add(new(
                "Sensors are still being detected",
                "Temperature and hardware telemetry are unavailable right now. Retry provider discovery; if PawnIO is missing ThinkControl will offer it there.",
                "Fix sensors",
                InboxAction.HardwareSetup,
                true));
        }

        if (!_app.State.CanFanControl)
        {
            messages.Add(new(
                "Fan control is unavailable",
                _app.State.CanFanTelemetry
                    ? "Fan telemetry is visible, but direct control has not passed the verified EC/write checks yet. Open setup to see the exact provider state."
                    : "Neither fan telemetry nor the verified fan-control path is ready. Hardware setup shows exactly what is missing instead of leaving this control disabled without explanation.",
                "Diagnose fan control",
                InboxAction.HardwareSetup,
                true));
        }

        if (!_app.State.CanKeyboardBacklight)
        {
            messages.Add(new(
                "Keyboard control is unavailable",
                "No Lenovo keyboard-backlight provider has passed readback yet. Hardware setup can retry the Lenovo PM/Energy/Vantage provider paths after driver updates.",
                "Fix keyboard control",
                InboxAction.HardwareSetup,
                true));
        }

        bool reportReady = DeviceSupportReportService.HasUsefulDiscovery(_app.State);
        if (reportReady)
        {
            messages.Add(new(
                "Device support data is ready",
                DeviceSupportReportService.DiscoverySummary(_app.State) + ". Sharing a reviewed hardware-only report can help make this provider/device profile reliable for other ThinkControl users.",
                "Review sharing",
                InboxAction.Diagnostics,
                false));
        }

        if (messages.Count == 0)
        {
            messages.Add(new(
                "Everything looks ready",
                "The ThinkControl service and detected hardware providers are responding. New compatibility discoveries will appear here when something needs your attention.",
                string.Empty,
                InboxAction.None,
                false));
        }

        int attentionCount = messages.Count(message => message.Warning);
        SummaryDot.Fill = (Brush)FindResource(attentionCount > 0 ? "Tc.Warning" : "Tc.Success");
        SummaryText.Text = attentionCount > 0
            ? $"{attentionCount} hardware item{(attentionCount == 1 ? string.Empty : "s")} need attention · {_app.State.DriverStatus}"
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

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InboxAction action })
            return;

        switch (action)
        {
            case InboxAction.HardwareSetup:
                _app.OpenHardwareSetup();
                break;
            case InboxAction.Diagnostics:
                _app.OpenAdvanced("Settings");
                Activate();
                break;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshMessagesAsync();
    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record InboxMessage(string Title, string Detail, string Button, InboxAction Action, bool Warning);

    private enum InboxAction
    {
        None,
        HardwareSetup,
        Diagnostics
    }
}