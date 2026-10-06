using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    // Historic destinations are aliases of their canonical grouped pages.
    private ScrollViewer PageFans => PagePerformance;
    private ScrollViewer PageSettings => PageSystem;
    private void HardwareDetails_Click(object sender, RoutedEventArgs e) => SystemHardwareDetails.IsExpanded = !SystemHardwareDetails.IsExpanded;
    private void CompactCustomize_Click(object sender, RoutedEventArgs e) => _app.OpenCompactCustomize(this);
    private void OpenSensors_Click(object sender, RoutedEventArgs e) => _app.OpenSensorDetails(this);
    private void OpenHardwareSetup_Click(object sender, RoutedEventArgs e) => _app.OpenHardwareSetup();
    private string _selectedPage = "Home";
    private bool _selectingGroup;
    private void SelectNavigationGroup(string page)
    {
        RadioButton selected = page switch
        {
            "Performance" or "Fans" => NavPerformance,
            "Battery" => NavBattery,
            "Display" or "Keyboard" or "Touchpad" => NavDisplay,
            "Audio" => NavAudio,
            "Modes" or "Automation" => NavModes,
            "System" or "Settings" or "Updates" or "Diagnostics" => NavSystem,
            _ => NavHome
        };
        _selectingGroup = true;
        try { selected.IsChecked = true; }
        finally { _selectingGroup = false; }
    }
    private void SidebarCompact_Click(object sender, RoutedEventArgs e) => _app.SwitchAdvancedToCompact();
    private void SidebarNotifications_Click(object sender, RoutedEventArgs e) => ToggleNotificationSheet();
}
