using System.Windows;
using System.Windows.Controls;
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

        if (Content is not Border root || root.Child is not Grid rootGrid)
            return;

        Grid? header = rootGrid.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 0);
        if (header is null)
            return;

        StackPanel? brandStack = header.Children
            .OfType<StackPanel>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 0);
        if (brandStack is null || brandStack.Children.Count < 2)
            return;

        brandStack.Children.RemoveAt(0);
        if (brandStack.Children.Count > 0 && brandStack.Children[0] is Ellipse)
            brandStack.Children.RemoveAt(0);

        brandStack.Children.Insert(0, new BrandWordmark
        {
            Height = 28,
            Width = 89,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });

        _advancedBrandingConfigured = true;
    }
}
