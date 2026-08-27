using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _batteryPageConfigured;

    private void ConfigureBatteryPage()
    {
        if (_batteryPageConfigured || PageBattery is null)
            return;

        _batteryPageConfigured = true;
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Battery",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = "Live Windows/ACPI battery data plus local charging history and the Windows power controls that belong with it.",
            Foreground = (System.Windows.Media.Brush)FindResource("Tc.TextMuted"),
            Margin = new Thickness(0, 6, 0, 18),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new BatteryTelemetryPanel());
        content.Children.Add(CreatePowerAndSleepCard());
        PageBattery.Content = content;
    }

    private Border CreatePowerAndSleepCard()
    {
        var copy = new StackPanel();
        copy.Children.Add(new TextBlock
        {
            Text = "Screen & sleep",
            FontWeight = FontWeights.SemiBold
        });
        var detail = new TextBlock
        {
            Text = "Screen and sleep time-outs, wake-on-approach and away detection are owned by Windows. Open the exact Power & battery page without hunting through Settings.",
            Margin = new Thickness(0, 5, 170, 0),
            TextWrapping = TextWrapping.Wrap
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);

        var button = new Button
        {
            Content = "Open Power & battery  ↗",
            Style = (Style)FindResource("TcButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 6, 12, 6)
        };
        button.Click += (_, _) => OpenPowerAndSleepSettings();

        var row = new Grid();
        row.Children.Add(copy);
        row.Children.Add(button);

        return new Border
        {
            Style = (Style)FindResource("TcSection"),
            Margin = new Thickness(0, 14, 0, 0),
            Child = row
        };
    }

    private static void OpenPowerAndSleepSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:powersleep") { UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:batterysaver") { UseShellExecute = true }); }
            catch { }
        }
    }
}
