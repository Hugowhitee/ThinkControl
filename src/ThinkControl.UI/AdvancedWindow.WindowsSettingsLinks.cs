using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string WindowsLinksKey = "ThinkControl.Advanced.WindowsSettingsLinks";
    private const string WindowsSettingsLinkTag = "ThinkControl.Header.WindowsSettingsLink";

    private void ConfigureWindowsSettingsLinks()
    {
        if (!Resources.Contains(WindowsLinksKey))
            Resources[WindowsLinksKey] = true;

        AddWindowsSettingsLink(PageDisplay, "Windows display ↗", "ms-settings:display");
        AddWindowsSettingsLink(PageBattery, "Power & battery ↗", "ms-settings:powersleep");
    }

    private void AddWindowsSettingsLink(ScrollViewer page, string label, string uri)
    {
        if (page.Content is not StackPanel stack)
            return;

        AdvancedPageHeader? header = stack.Children
            .OfType<AdvancedPageHeader>()
            .FirstOrDefault();
        if (header is null)
            return;

        StackPanel rail = header.EnsureActionStack();
        if (rail.Children.OfType<Button>().Any(button =>
                Equals(button.Tag, WindowsSettingsLinkTag) &&
                string.Equals(button.Content?.ToString(), label, StringComparison.Ordinal)))
        {
            return;
        }

        var button = new Button
        {
            Tag = WindowsSettingsLinkTag,
            Content = label,
            Style = TryFindResource("TcPageHeaderExternalAction") as Style,
            ToolTip = "Open the matching Windows Settings page"
        };
        button.Click += (_, _) => OpenWindowsSettings(uri);
        header.AddAction(button, PageHeaderActionRole.External);
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
