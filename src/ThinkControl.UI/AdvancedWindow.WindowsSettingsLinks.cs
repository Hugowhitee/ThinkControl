using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string WindowsLinksKey = "ThinkControl.Advanced.WindowsSettingsLinks";
    private const string DisplayWindowsLinkTag = "ThinkControl.Display.WindowsSettings";
    private const string TouchpadWindowsLinkTag = "ThinkControl.Touchpad.WindowsSettings";

    private void ConfigureWindowsSettingsLinks()
    {
        if (!Resources.Contains(WindowsLinksKey))
            Resources[WindowsLinksKey] = true;

        AddDisplayWindowsSettingsLink();
        AddTouchpadWindowsSettingsLink();
    }

    private void AddDisplayWindowsSettingsLink()
    {
        if (PageDisplay.Content is not StackPanel stack || stack.Children.Count == 0 || stack.Children[0] is not Grid header)
            return;
        if (header.Children.OfType<Button>().Any(button => Equals(button.Tag, DisplayWindowsLinkTag)))
            return;

        Button link = CreateWindowsLink("Windows display settings ↗", "ms-settings:display", DisplayWindowsLinkTag);
        link.HorizontalAlignment = HorizontalAlignment.Right;
        link.Margin = new Thickness(0, 0, 38, 0);
        header.Children.Add(link);
    }

    private void AddTouchpadWindowsSettingsLink()
    {
        const string pageKey = "ThinkControl.Dynamic.PageTouchpad";
        if (!Resources.Contains(pageKey) || Resources[pageKey] is not ScrollViewer { Content: TouchpadPanel panel } || panel.Content is not Grid root)
            return;

        Grid? header = root.Children.OfType<Grid>().FirstOrDefault(child => Grid.GetRow(child) == 0);
        StackPanel? actions = header?.Children.OfType<StackPanel>().FirstOrDefault(child => Grid.GetColumn(child) == 1);
        if (actions is null || actions.Children.OfType<Button>().Any(button => Equals(button.Tag, TouchpadWindowsLinkTag)))
            return;

        Button link = CreateWindowsLink("Windows touchpad ↗", "ms-settings:devices-touchpad", TouchpadWindowsLinkTag);
        link.Margin = new Thickness(0, 0, 10, 0);
        actions.Children.Insert(Math.Min(1, actions.Children.Count), link);
    }

    private Button CreateWindowsLink(string text, string uri, string tag)
    {
        var button = new Button
        {
            Content = text,
            Tag = tag,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(5, 3, 5, 3),
            FontSize = 10,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Open the matching native Windows Settings page",
            VerticalAlignment = VerticalAlignment.Center
        };
        button.SetResourceReference(Button.ForegroundProperty, "Tc.TextMuted");
        button.Click += (_, _) => OpenWindowsSettings(uri);
        return button;
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
