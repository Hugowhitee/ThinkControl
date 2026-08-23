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
        var content = new StackPanel
        {
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        content.Children.Add(new TextBlock
        {
            Text = "Battery",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = "Live Windows/ACPI battery data plus a small local charging history. ThinkControl learns from prior sessions without inventing unsupported firmware controls.",
            Foreground = (System.Windows.Media.Brush)FindResource("Tc.TextMuted"),
            Margin = new Thickness(0, 6, 0, 18),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new BatteryTelemetryPanel());
        PageBattery.Content = content;
    }
}
