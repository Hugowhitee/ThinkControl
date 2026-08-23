using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ThinkControl.UI.Controls;
using WpfButton = System.Windows.Controls.Button;

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

        // BrandWordmark contains a little intentional source-canvas whitespace.
        // Let that be the only inset so the visible lettering sits closer to the
        // sidebar edge than the navigation glyphs and does not look accidentally
        // indented.
        dockRow.Height = 44;
        dockRow.Margin = new Thickness(0, 2, 8, 2);
        FrameworkElement? oldLabel = dockRow.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 0);
        if (oldLabel is not null)
            dockRow.Children.Remove(oldLabel);

        var wordmark = new BrandWordmark
        {
            Width = 120,
            Height = 38,
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(wordmark, 0);
        dockRow.Children.Add(wordmark);

        WpfButton? compactButton = dockRow.Children
            .OfType<WpfButton>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 1);
        if (compactButton is not null)
        {
            compactButton.Width = 30;
            compactButton.Height = 30;
            compactButton.Padding = new Thickness(0);
            compactButton.BorderThickness = new Thickness(0);
            compactButton.Background = Brushes.Transparent;
            compactButton.ToolTip = "Pop out compact view";

            // Compact -> Advanced uses ↗. Advanced -> Compact points ↘ toward the
            // tray side instead of left, making the relationship spatially obvious.
            var path = new Path
            {
                Stroke = (Brush)FindResource("Tc.TextMuted"),
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Data = Geometry.Parse("M3,3 L13,13 M7,13 L13,13 L13,7")
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
