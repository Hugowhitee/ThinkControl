using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
        catch { }

        if (NavHome.Parent is not StackPanel navStack ||
            navStack.Children.OfType<Grid>()
                .FirstOrDefault(grid => Equals(grid.Tag, "ThinkControl.UtilityRow")) is not Grid utilityRow)
        {
            return;
        }

        navStack.Margin = new Thickness(0);

        var brandRow = new Grid
        {
            Tag = "ThinkControl.BrandRow",
            Height = 64,
            Margin = new Thickness(14, 5, 10, 0)
        };
        brandRow.Children.Add(new BrandWordmark
        {
            Width = 150,
            Height = 48,
            Margin = new Thickness(-4, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });

        navStack.Children.Insert(0, brandRow);
        navStack.Children.Insert(1, CreateSidebarDivider("ThinkControl.BrandDivider"));

        int utilityIndex = navStack.Children.IndexOf(utilityRow);
        navStack.Children.Insert(
            Math.Min(navStack.Children.Count, utilityIndex + 1),
            CreateSidebarDivider("ThinkControl.NavigationDivider"));

        if (navStack.Parent is Grid sidebarGrid)
        {
            StackPanel? footer = sidebarGrid.Children
                .OfType<StackPanel>()
                .FirstOrDefault(child => Grid.GetRow(child) == 1);
            if (footer is not null)
            {
                footer.Margin = new Thickness(17, 0, 12, 10);
                footer.Children.OfType<TextBlock>().FirstOrDefault()?.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            }
        }

        _advancedBrandingConfigured = true;
    }

    private Border CreateSidebarDivider(string tag)
    {
        var divider = new Border
        {
            Tag = tag,
            Height = 1,
            Margin = new Thickness(17, 0, 17, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        divider.SetResourceReference(Border.BackgroundProperty, "Tc.Border");
        return divider;
    }
}
