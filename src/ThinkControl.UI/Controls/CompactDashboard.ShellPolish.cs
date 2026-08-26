using System.Windows;
using System.Windows.Controls;

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

        // The full-window action is a compact layout toggle, not an arrow. Giving
        // it a small framed application glyph makes that destination obvious and
        // keeps it visually grounded beside the quiet hide control.
        Button expand = buttons[0];
        expand.Width = 32;
        expand.Height = 32;
        expand.Margin = new Thickness(0);
        expand.Padding = new Thickness(0);
        expand.BorderThickness = new Thickness(1);
        expand.SetResourceReference(BackgroundProperty, "Tc.SurfaceAlt");
        expand.SetResourceReference(BorderBrushProperty, "Tc.BorderStrong");
        expand.ToolTip = "Open full ThinkControl window";

        var icon = new PackIconLucide
        {
            Kind = "ViewSidebar",
            Width = 17,
            Height = 17
        };
        icon.SetResourceReference(ForegroundProperty, "Tc.TextMuted");
        expand.Content = icon;

        Button close = buttons[1];
        close.Width = 34;
        close.Height = 34;
        close.Margin = new Thickness(4, 0, 0, 0);
        close.Padding = new Thickness(0);
    }
}
