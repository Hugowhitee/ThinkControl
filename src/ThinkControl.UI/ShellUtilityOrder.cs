using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

internal static class ShellUtilityOrder
{
    internal const string ViewModeTag = "ThinkControl.Utility.ViewMode";
    internal const string NotificationTag = "ThinkControl.Utility.Notifications";

    /// <summary>One semantic utility order for every primary shell.</summary>
    internal static void Apply(Panel host, Button notification, Button viewMode, params Button[] trailing)
    {
        Button[] ordered = [notification, viewMode, .. trailing];
        foreach (Button button in ordered)
            host.Children.Remove(button);
        if (host is Grid grid)
        {
            while (grid.ColumnDefinitions.Count < ordered.Length)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (int index = 0; index < ordered.Length; index++)
        {
            Button button = ordered[index];
            if (host is Grid)
                Grid.SetColumn(button, index);
            host.Children.Add(button);
        }
    }

    internal static void ConfigureModeButton(Button button, string label, string iconKind, Brush foreground)
    {
        button.Tag = ViewModeTag;
        button.Width = 96;
        button.Height = 38;
        button.Padding = new Thickness(10, 0, 11, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new PackIconLucide
                {
                    Kind = iconKind,
                    Width = 18,
                    Height = 18,
                    Foreground = foreground,
                    Margin = new Thickness(0, 0, 7, 0)
                },
                new TextBlock
                {
                    Text = label,
                    FontSize = TypographyScale.ControlText,
                    Foreground = foreground,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }
}
