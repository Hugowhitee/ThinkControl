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
        if (captionButtons is null)
            return;

        Button[] buttons = captionButtons.Children.OfType<Button>().ToArray();
        if (buttons.Length < 2)
            return;

        _shellPolished = true;
        header.ClipToBounds = false;
        captionButtons.ClipToBounds = false;
        header.ColumnDefinitions[1].Width = new GridLength(76);

        // Keep both caption actions icon-only. The previous alpha.17 pass widened
        // "Full view" inside a column designed for two 36px caption buttons, so its
        // hover surface could be clipped on the left. One standard caption contract
        // is both cleaner and impossible to clip here.
        Button expand = buttons[0];
        expand.Width = 34;
        expand.Height = 34;
        expand.Margin = new Thickness(0);
        expand.Padding = new Thickness(0);
        expand.ToolTip = "Switch to normal layout";

        var icon = new Viewbox
        {
            Width = 13,
            Height = 13
        };
        var path = new Path
        {
            // Restore the alpha.18 expand arrow: it communicates the transition
            // from this compact surface to the full window more directly.
            Data = Geometry.Parse("M13,13 L3,3 M3,9 L3,3 L9,3"),
            StrokeThickness = 1.45,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        path.SetResourceReference(Shape.StrokeProperty, "Tc.TextMuted");
        icon.Child = path;
        expand.Content = icon;

        Button close = buttons[1];
        close.Width = 34;
        close.Height = 34;
        close.Margin = new Thickness(4, 0, 0, 0);
        close.Padding = new Thickness(0);
    }
}
