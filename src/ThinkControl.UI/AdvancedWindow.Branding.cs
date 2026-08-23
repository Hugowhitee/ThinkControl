using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _advancedBrandingConfigured;

    private void ConfigureAdvancedBranding()
    {
        if (_advancedBrandingConfigured)
            return;

        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/ThinkControl.ico", UriKind.Absolute));
        }
        catch
        {
            // The executable icon remains the fallback if WPF cannot decode it.
        }

        if (NavHome.Parent is not StackPanel navStack ||
            navStack.Children.OfType<Grid>().FirstOrDefault() is not Grid dockRow)
        {
            return;
        }

        dockRow.Height = 48;
        FrameworkElement? oldLabel = dockRow.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 0);
        if (oldLabel is not null)
            dockRow.Children.Remove(oldLabel);

        var wordmark = new BrandWordmark
        {
            Width = 132,
            Height = 38,
            Margin = new Thickness(2, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(wordmark, 0);
        dockRow.Children.Add(wordmark);

        Button? compactButton = dockRow.Children
            .OfType<Button>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 1);
        if (compactButton is not null)
        {
            compactButton.Width = 30;
            compactButton.Height = 30;
            compactButton.Padding = new Thickness(0);
            compactButton.BorderThickness = new Thickness(0);
            compactButton.Background = Brushes.Transparent;
            compactButton.ToolTip = "Open compact tray view";

            var path = new Path
            {
                Stroke = (Brush)FindResource("Tc.TextMuted"),
                StrokeThickness = 1.35,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = Geometry.Parse("M2,2 L7,7 M2,2 L6,2 M2,2 L2,6 M14,14 L9,9 M14,14 L10,14 M14,14 L14,10")
            };
            compactButton.Content = new Viewbox { Width = 13, Height = 13, Child = path };
        }

        if (navStack.Parent is Grid sidebarGrid)
        {
            StackPanel? footer = sidebarGrid.Children
                .OfType<StackPanel>()
                .FirstOrDefault(child => Grid.GetRow(child) == 1);
            if (footer?.Children.Count > 0 && footer.Children[0] is TextBlock appName)
                appName.Visibility = Visibility.Collapsed;
        }

        _advancedBrandingConfigured = true;
    }
}
