using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private void ConfigureShellUtilitySizing()
    {
        if (NavHome.Parent is not StackPanel navStack)
            return;

        Grid? utilityRow = navStack.Children.OfType<Grid>()
            .FirstOrDefault(grid => Equals(grid.Tag, "ThinkControl.UtilityRow"));
        if (utilityRow is null)
            return;

        utilityRow.Height = 46;
        Button[] buttons = utilityRow.Children.OfType<Button>().ToArray();
        foreach (Button button in buttons)
        {
            button.Width = 38;
            button.Height = 38;
            button.Padding = new Thickness(0);
        }

        Button? compact = buttons.FirstOrDefault(button =>
            button.Content is PackIconLucide icon && icon.Kind == "CompactView");
        if (compact?.Content is PackIconLucide compactIcon)
        {
            compactIcon.Width = 19;
            compactIcon.Height = 19;
            compact.Margin = new Thickness(0, 0, 4, 0);
        }

        Button? notification = buttons.FirstOrDefault(button => !ReferenceEquals(button, compact));
        if (notification?.Content is Grid notificationContent)
        {
            notificationContent.Width = 24;
            notificationContent.Height = 24;
            foreach (FrameworkElement glyph in notificationContent.Children.OfType<FrameworkElement>())
            {
                if (glyph.Width >= 17)
                {
                    glyph.Width = 19;
                    glyph.Height = 19;
                }
            }
        }
    }
}
