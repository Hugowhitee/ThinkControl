using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _hardwareSetupEntryConfigured;

    private void ConfigureHardwareSetupEntry()
    {
        if (_hardwareSetupEntryConfigured || FindName("PageSystem") is not ScrollViewer systemPage ||
            systemPage.Content is not StackPanel systemStack)
        {
            return;
        }

        var section = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        text.Children.Add(new TextBlock
        {
            Text = "Required components",
            FontWeight = FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = "Inbox lists only the service or provider that currently needs attention. Open the current item to review its one focused install, repair or retry action.",
            Foreground = (Brush)FindResource("Tc.TextMuted"),
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var button = new WpfButton
        {
            Content = "Open current item",
            Style = TryFindResource("TcButton") as Style,
            MinWidth = 96,
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => _app.OpenHardwareSetup();

        grid.Children.Add(text);
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);
        section.Child = grid;
        systemStack.Children.Insert(Math.Min(1, systemStack.Children.Count), section);

        var sensorSection = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var sensorGrid = new Grid();
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition());
        sensorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var sensorText = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        sensorText.Children.Add(new TextBlock { Text = "Sensors & telemetry", FontWeight = FontWeights.SemiBold });
        sensorText.Children.Add(new TextBlock
        {
            Text = "Live control temperature, fan tachometers and provider-reported hardware readings. Details stay read-only and missing sensors remain unavailable.",
            Foreground = (Brush)FindResource("Tc.TextMuted"),
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        var sensorButton = new WpfButton
        {
            Content = "View details",
            Style = TryFindResource("TcButton") as Style,
            MinWidth = 96,
            VerticalAlignment = VerticalAlignment.Center
        };
        sensorButton.Click += (_, _) => _app.OpenSensorDetails(this);
        sensorGrid.Children.Add(sensorText);
        Grid.SetColumn(sensorButton, 1);
        sensorGrid.Children.Add(sensorButton);
        sensorSection.Child = sensorGrid;
        systemStack.Children.Insert(Math.Min(2, systemStack.Children.Count), sensorSection);
        _hardwareSetupEntryConfigured = true;
    }
}
