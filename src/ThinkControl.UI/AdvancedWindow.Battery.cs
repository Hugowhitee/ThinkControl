using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _batteryPageConfigured;

    private void ConfigureBatteryPage()
    {
        if (_batteryPageConfigured || PageBattery?.Content is not StackPanel content)
            return;

        _batteryPageConfigured = true;

        // Keep the XAML-owned telemetry instance so every code/snapshot reference
        // points at the element that is actually rendered. Only the page shell is
        // normalized here.
        content.Children.Clear();
        content.Children.Add(new AdvancedPageHeader
        {
            Title = "Battery",
            Subtitle = "Power, health, history and preservation."
        });
        content.Children.Add(BatteryTelemetryPanelControl);
    }

}
