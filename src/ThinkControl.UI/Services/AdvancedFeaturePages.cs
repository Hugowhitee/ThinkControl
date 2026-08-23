using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI.Services;

internal static class AdvancedFeaturePages
{
    private const string EnhancedKey = "ThinkControl.Advanced.FeaturePages";
    private const string AudioNavKey = "ThinkControl.Dynamic.NavAudio";
    private const string AudioPageKey = "ThinkControl.Dynamic.PageAudio";
    private const string TouchpadPageKey = "ThinkControl.Dynamic.PageTouchpad";
    private const string SensorsPageKey = "ThinkControl.Dynamic.PageSensors";

    internal static void Ensure(AdvancedWindow window, App app)
    {
        if (window.Resources.Contains(EnhancedKey))
        {
            Resize(window);
            return;
        }
        window.Resources[EnhancedKey] = true;

        ReplacePerformance(window, app);
        ReplaceFans(window, app);
        AddAudio(window, app);
        window.SizeChanged += (_, _) => Resize(window);
        Resize(window);
    }

    internal static void SelectAudio(AdvancedWindow window)
    {
        if (window.Resources[AudioNavKey] is RadioButton nav)
            nav.IsChecked = true;
    }

    private static void ReplacePerformance(AdvancedWindow window, App app)
    {
        if (window.FindName("PagePerformance") is not ScrollViewer scroll)
            return;
        var panel = new PerformancePanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 940,
            Margin = new Thickness(0, 0, 2, 0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private static void ReplaceFans(AdvancedWindow window, App app)
    {
        if (window.FindName("PageFans") is not ScrollViewer scroll)
            return;
        var panel = new FansPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 1040,
            Margin = new Thickness(0, 0, 2, 0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private static void AddAudio(AdvancedWindow window, App app)
    {
        if (window.FindName("NavDisplay") is not RadioButton display ||
            window.FindName("NavKeyboard") is not RadioButton keyboard ||
            display.Parent is not Panel navPanel ||
            window.FindName("PageHome") is not FrameworkElement home ||
            home.Parent is not Grid pageHost)
        {
            return;
        }

        var nav = new RadioButton
        {
            GroupName = "Nav",
            Tag = "Audio",
            Style = window.TryFindResource("TcNav") as Style
        };
        var navContent = new StackPanel { Orientation = Orientation.Horizontal };
        navContent.Children.Add(new TextBlock
        {
            Text = "◖))",
            Width = 15,
            FontSize = 10,
            Foreground = window.TryFindResource("Tc.TextMuted") as System.Windows.Media.Brush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });
        navContent.Children.Add(new TextBlock { Text = "Audio" });
        nav.Content = navContent;

        int keyboardIndex = navPanel.Children.IndexOf(keyboard);
        navPanel.Children.Insert(Math.Max(0, keyboardIndex), nav);

        var scroll = new ScrollViewer
        {
            Tag = "Audio",
            Visibility = Visibility.Collapsed,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        var panel = new AudioPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 940,
            Margin = new Thickness(0, 0, 2, 0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        pageHost.Children.Add(scroll);

        window.Resources[AudioNavKey] = nav;
        window.Resources[AudioPageKey] = scroll;

        nav.Checked += (_, _) =>
        {
            CollapseKnownPages(window);
            HideDynamic(window, TouchpadPageKey);
            HideDynamic(window, SensorsPageKey);
            scroll.Visibility = Visibility.Visible;
        };

        foreach (RadioButton known in FindSidebarNav(window))
        {
            if (!ReferenceEquals(known, nav))
                known.Checked += (_, _) => scroll.Visibility = Visibility.Collapsed;
        }
    }

    private static void Resize(AdvancedWindow window)
    {
        double width = window.ActualWidth > 1 ? window.ActualWidth : window.Width;
        double available = Math.Max(560, width - 204);
        if (window.FindName("PagePerformance") is ScrollViewer performance && performance.Content is FrameworkElement performanceContent)
            performanceContent.MaxWidth = Math.Min(940, available);
        if (window.FindName("PageFans") is ScrollViewer fans && fans.Content is FrameworkElement fansContent)
            fansContent.MaxWidth = Math.Min(1040, available);
        if (window.Resources[AudioPageKey] is ScrollViewer audio && audio.Content is FrameworkElement audioContent)
            audioContent.MaxWidth = Math.Min(940, available);
    }

    private static void CollapseKnownPages(AdvancedWindow window)
    {
        foreach (string name in new[]
        {
            "PageHome", "PagePerformance", "PageFans", "PageDisplay", "PageKeyboard",
            "PageBattery", "PageSystem", "PageUpdates", "PageSettings"
        })
        {
            if (window.FindName(name) is FrameworkElement element)
                element.Visibility = Visibility.Collapsed;
        }
    }

    private static void HideDynamic(AdvancedWindow window, string key)
    {
        if (window.Resources[key] is FrameworkElement element)
            element.Visibility = Visibility.Collapsed;
    }

    private static IEnumerable<RadioButton> FindSidebarNav(AdvancedWindow window)
    {
        foreach (string name in new[]
        {
            "NavHome", "NavPerformance", "NavFans", "NavDisplay", "NavKeyboard", "NavBattery",
            "NavSystem", "NavUpdates", "NavSettings"
        })
        {
            if (window.FindName(name) is RadioButton button)
                yield return button;
        }

        if (window.Resources["ThinkControl.Dynamic.NavTouchpad"] is RadioButton touchpad)
            yield return touchpad;
        if (window.Resources["ThinkControl.Dynamic.NavSensors"] is RadioButton sensors)
            yield return sensors;
    }
}
