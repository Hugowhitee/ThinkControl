using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private bool _shellPolished;

    private void EnsureShellPolish()
    {
        if (_shellPolished || Content is not Border { Child: Grid root })
            return;

        Grid? header = root.Children.OfType<Grid>().FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        if (header is null || header.ColumnDefinitions.Count < 2)
            return;

        StackPanel? captionButtons = header.Children.OfType<StackPanel>()
            .FirstOrDefault(stack => Grid.GetColumn(stack) == 1 && stack.Orientation == Orientation.Horizontal);
        if (captionButtons is null || captionButtons.Children.OfType<Button>().ToArray() is not { Length: >= 2 } buttons)
            return;

        _shellPolished = true;
        header.ColumnDefinitions[1].Width = new GridLength(120);

        Button expand = buttons[0];
        expand.Width = 82;
        expand.Height = 34;
        expand.ToolTip = "Open full ThinkControl window";

        var icon = new Viewbox
        {
            Width = 13,
            Height = 13,
            Margin = new Thickness(0, 0, 7, 0)
        };
        var path = new Path
        {
            Data = Geometry.Parse("M3,8 V3 H8 M13,8 V13 H8"),
            StrokeThickness = 1.45,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        path.SetResourceReference(Shape.StrokeProperty, "Tc.TextMuted");
        icon.Child = path;

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(icon);
        content.Children.Add(new TextBlock
        {
            Text = "Full view",
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center
        });
        expand.Content = content;

        Button close = buttons[1];
        close.Width = 34;
        close.Margin = new Thickness(4, 0, 0, 0);
    }
}
