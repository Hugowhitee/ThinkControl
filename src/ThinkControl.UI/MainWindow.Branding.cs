using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class MainWindow
{
    private bool _compactBrandingConfigured;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ConfigureCompactBranding();
    }

    private void ConfigureCompactBranding()
    {
        if (_compactBrandingConfigured)
            return;

        if (Content is not Border root || root.Child is not Grid layout)
            return;

        Grid? header = layout.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 0);
        if (header is null)
            return;

        StackPanel? titleStack = header.Children
            .OfType<StackPanel>()
            .FirstOrDefault(child => Grid.GetColumn(child) == 0);
        if (titleStack is null || titleStack.Children.Count == 0)
            return;

        titleStack.Children.RemoveAt(0);
        titleStack.Children.Insert(0, new BrandWordmark
        {
            Height = 27,
            Margin = new Thickness(3, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });
        _compactBrandingConfigured = true;
    }
}
