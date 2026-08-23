using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ThinkControl.UI.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI;

public partial class MainWindow
{
    private bool _compactBrandingConfigured;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ConfigureCompactBranding();
    }

    public void PrepareBrandingForSnapshot() => ConfigureCompactBranding();

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
            // The canonical 455px wordmark canvas has ~26.6px of transparent
            // source space before the first visible letter. At this rendered size
            // that is ~5px, so compensate optically instead of adding another inset.
            Margin = new Thickness(-5, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });

        MoveExpandButtonIntoHeader(header);
        _compactBrandingConfigured = true;
    }

    private void MoveExpandButtonIntoHeader(Grid header)
    {
        if (FindName("ExpandButton") is not WpfButton expand ||
            expand.Parent is not Grid footer)
        {
            return;
        }

        WpfButton? hide = header.Children.OfType<WpfButton>().FirstOrDefault();
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

        // The compact popup lives at the bottom-right tray area. Opening the full
        // window moves visually up and left, so a single ↖ arrow communicates the
        // destination better than the old opposing expand arrows.
        expand.ToolTip = "Open full window";
        expand.Content = new Viewbox
        {
            Width = 13,
            Height = 13,
            Child = new Path
            {
                Stroke = TryFindResource("Tc.TextMuted") as Brush ?? Brushes.Gray,
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Data = Geometry.Parse("M13,13 L3,3 M3,9 L3,3 L9,3")
            }
        };
        header.Children.Add(expand);
    }
}
