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

        MoveExpandButtonIntoHeader(header);
        _compactBrandingConfigured = true;
    }

    private void MoveExpandButtonIntoHeader(Grid header)
    {
        if (FindName("ExpandButton") is not Button expand ||
            expand.Parent is not Grid footer)
        {
            return;
        }

        Button? hide = header.Children.OfType<Button>().FirstOrDefault();
        if (hide is null)
            return;

        footer.Children.Remove(expand);
        if (footer.ColumnDefinitions.Count >= 4)
            footer.ColumnDefinitions[3].Width = new GridLength(0);

        if (header.ColumnDefinitions.Count == 2)
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        Grid.SetColumn(hide, 2);
        Grid.SetColumn(expand, 1);
        expand.Width = 30;
        expand.Height = 30;
        expand.Margin = new Thickness(0);
        if (TryFindResource("TcIconButton") is Style iconStyle)
            expand.Style = iconStyle;
        expand.ToolTip = "Open Advanced";
        header.Children.Add(expand);
    }
}
