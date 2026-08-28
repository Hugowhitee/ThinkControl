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

        // Battery uses the same page-header contract as the other Advanced pages:
        // title on the left, page actions on the right, then subtitle/help text.
        // Tag the canonical header so WindowsSettingsLinks augments this row instead
        // of accidentally promoting the subtitle into a second synthetic header.
        var header = new Grid { Tag = BatteryHeaderTag };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = "Battery",
            FontSize = TypographyScale.PageTitle,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Button windowsPower = CreateWindowsLink(
            "Screen & sleep ↗",
            "ms-settings:powersleep",
            "ThinkControl.Battery.PowerSettings");
        actions.Children.Add(windowsPower);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        content.Children.Add(header);

        content.Children.Add(new TextBlock
        {
            Text = "Live Windows/ACPI battery data plus local charging history and Windows-owned power controls.",
            FontSize = TypographyScale.Body,
            Foreground = (System.Windows.Media.Brush)FindResource("Tc.TextMuted"),
            Margin = new Thickness(0, 7, 0, 18),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new BatteryTelemetryPanel());
        PageBattery.Content = content;
    }
}
