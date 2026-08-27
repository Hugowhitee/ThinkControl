using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string WindowsLinksKey = "ThinkControl.Advanced.WindowsSettingsLinks";
    private const string DisplayWindowsLinkTag = "ThinkControl.Display.WindowsSettings";
    private const string NightLightWindowsLinkTag = "ThinkControl.Display.NightLightSettings";
    private const string TouchpadWindowsLinkTag = "ThinkControl.Touchpad.WindowsSettings";
    private const string DisplayHeaderActionsTag = "ThinkControl.Display.HeaderActions";

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

        StackPanel? actions = header.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Equals(panel.Tag, DisplayHeaderActionsTag));

        if (actions is null)
        {
            Button[] existingButtons = header.Children.OfType<Button>().ToArray();
            foreach (Button button in existingButtons)
                header.Children.Remove(button);

            header.ColumnDefinitions.Clear();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            foreach (UIElement child in header.Children)
                Grid.SetColumn(child, 0);

            actions = new StackPanel
            {
                Tag = DisplayHeaderActionsTag,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (Button button in existingButtons)
            {
                button.Margin = new Thickness(8, 0, 0, 0);
                actions.Children.Add(button);
            }

            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
        }

        if (!actions.Children.OfType<Button>().Any(button => Equals(button.Tag, NightLightWindowsLinkTag)))
        {
            Button nightLight = CreateWindowsLink("Night light ↗", "ms-settings:nightlight", NightLightWindowsLinkTag);
            nightLight.Margin = new Thickness(0, 0, 2, 0);
            actions.Children.Insert(0, nightLight);
        }

        if (actions.Children.OfType<Button>().Any(button => Equals(button.Tag, DisplayWindowsLinkTag)))
            return;

        Button link = CreateWindowsLink("Display settings ↗", "ms-settings:display", DisplayWindowsLinkTag);
        link.Margin = new Thickness(0, 0, 2, 0);
        actions.Children.Insert(Math.Min(1, actions.Children.Count), link);
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
            Style = TryFindResource("TcButton") as Style,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(7, 4, 7, 4),
            FontSize = TypographyScale.Secondary,
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
