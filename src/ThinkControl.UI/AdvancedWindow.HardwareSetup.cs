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
            Text = "Hardware setup",
            FontWeight = FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = "Check the ThinkControl service, optional low-level access and detected providers in one guided window. Retry is read-only; repair asks for Windows approval only after you choose it.",
            Foreground = (Brush)FindResource("Tc.TextMuted"),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var button = new WpfButton
        {
            Content = "Open setup",
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
        _hardwareSetupEntryConfigured = true;
    }
}
