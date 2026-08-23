using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _advancedBrandingConfigured;

    private void ConfigureAdvancedBranding()
    {
        if (_advancedBrandingConfigured)
            return;

        // Advanced uses the native Windows title bar at runtime, so the fallback
        // XAML caption row is intentionally collapsed. Keep branding at a stable
        // top-left anchor by replacing the small "Advanced" label in the sidebar
        // dock row with the same BrandWordmark used by Compact.
        if (NavHome.Parent is not StackPanel navStack ||
            navStack.Children.OfType<Grid>().FirstOrDefault() is not Grid dockRow)
        {
            return;
        }

        FrameworkElement? oldLabel = dockRow.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 0);
        if (oldLabel is not null)
            dockRow.Children.Remove(oldLabel);

        var wordmark = new BrandWordmark
        {
            Width = 103,
            Height = 30,
            Margin = new Thickness(4, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(wordmark, 0);
        dockRow.Children.Add(wordmark);

        // The wordmark now owns the app identity in this surface. Keep only the
        // version in the sidebar footer instead of repeating "ThinkControl" twice.
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
