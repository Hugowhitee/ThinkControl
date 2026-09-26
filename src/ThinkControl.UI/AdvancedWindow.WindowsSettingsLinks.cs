using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string WindowsLinksKey = "ThinkControl.Advanced.WindowsSettingsLinks";
    private const string WindowsSettingsMenuTag = "ThinkControl.Header.WindowsSettingsMenu";

    private void ConfigureWindowsSettingsLinks()
    {
        if (!Resources.Contains(WindowsLinksKey))
            Resources[WindowsLinksKey] = true;

        AddWindowsSettingsMenu(
            PageDisplay,
            [
                ("Display settings", "ms-settings:display"),
                ("Night light", "ms-settings:nightlight")
            ]);

        AddWindowsSettingsMenu(
            PageBattery,
            [
                ("Power & battery", "ms-settings:powersleep"),
                ("Battery usage", "ms-settings:batterysaver-usagedetails")
            ]);
    }

    private void AddWindowsSettingsMenu(
        ScrollViewer page,
        IReadOnlyList<(string Label, string Uri)> destinations)
    {
        if (page.Content is not StackPanel stack)
            return;

        AdvancedPageHeader? header = stack.Children
            .OfType<AdvancedPageHeader>()
            .FirstOrDefault();
        if (header is null)
            return;

        StackPanel rail = header.EnsureActionStack();
        if (rail.Children.OfType<Button>().Any(button => Equals(button.Tag, WindowsSettingsMenuTag)))
            return;

        var button = new Button
        {
            Tag = WindowsSettingsMenuTag,
            Content = "Windows settings ▾",
            Style = TryFindResource("TcButton") as Style,
            Padding = new Thickness(9, 4, 9, 4),
            FontSize = TypographyScale.Caption,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (rail.Children.Count > 0)
            button.Margin = new Thickness(PageHeaderActionGap, 0, 0, 0);

        button.Click += (_, _) =>
        {
            var menu = new ContextMenu
            {
                PlacementTarget = button,
                Placement = PlacementMode.Bottom
            };

            foreach ((string label, string uri) in destinations)
            {
                var item = new MenuItem { Header = label };
                item.Click += (_, _) => OpenWindowsSettings(uri);
                menu.Items.Add(item);
            }

            button.ContextMenu = menu;
            menu.IsOpen = true;
        };

        rail.Children.Add(button);
    }

    private static void OpenWindowsSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
