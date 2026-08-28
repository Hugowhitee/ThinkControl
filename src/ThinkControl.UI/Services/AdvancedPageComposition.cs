using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI.Services;

/// <summary>
/// Single owner for Advanced-page composition that cannot yet live directly in
/// static XAML. Performance/Fans are canonical panels; Audio/Touchpad are dynamic
/// capability pages. Navigation visibility and page-entry motion are centralized
/// here so multiple enhancer layers cannot race each other.
/// </summary>
internal static class AdvancedPageComposition
{
    private const string InitializedKey = "ThinkControl.Advanced.PageComposition";
    internal const string AudioNavKey = "ThinkControl.Dynamic.NavAudio";
    internal const string AudioPageKey = "ThinkControl.Dynamic.PageAudio";
    internal const string TouchpadNavKey = "ThinkControl.Dynamic.NavTouchpad";
    internal const string TouchpadPageKey = "ThinkControl.Dynamic.PageTouchpad";

    private static readonly string[] StaticNavNames =
    [
        "NavHome", "NavPerformance", "NavFans", "NavDisplay", "NavKeyboard",
        "NavBattery", "NavSystem", "NavUpdates", "NavSettings"
    ];

    private static readonly string[] StaticPageNames =
    [
        "PageHome", "PagePerformance", "PageFans", "PageDisplay", "PageKeyboard",
        "PageBattery", "PageSystem", "PageUpdates", "PageSettings"
    ];

    internal static void Ensure(AdvancedWindow window, App app)
    {
        if (window.Resources.Contains(InitializedKey))
            return;

        window.Resources[InitializedKey] = true;
        ReplacePerformance(window, app);
        ReplaceFans(window, app);
        AddAudio(window, app);
        AddTouchpad(window, app);
        AttachStaticNavigation(window);
    }

    internal static void SelectAudio(AdvancedWindow window) =>
        SelectDynamicNavigation(window, AudioNavKey);

    internal static void SelectTouchpad(AdvancedWindow window) =>
        SelectDynamicNavigation(window, TouchpadNavKey);

    private static void ReplacePerformance(AdvancedWindow window, App app)
    {
        if (window.FindName("PagePerformance") is not ScrollViewer scroll)
            return;

        var panel = new PerformancePanel
        {
            DataContext = window.DataContext,
            Margin = new Thickness(0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private static void ReplaceFans(AdvancedWindow window, App app)
    {
        if (window.FindName("PageFans") is not ScrollViewer scroll)
            return;

        var panel = new FansPanel
        {
            DataContext = window.DataContext,
            Margin = new Thickness(0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private static void AddAudio(AdvancedWindow window, App app)
    {
        if (window.FindName("NavKeyboard") is not RadioButton keyboard ||
            keyboard.Parent is not Panel navPanel ||
            FindPageHost(window) is not Grid pageHost)
        {
            return;
        }

        RadioButton nav = CreateNavigation(window, "Audio", "Audio");
        int keyboardIndex = navPanel.Children.IndexOf(keyboard);
        navPanel.Children.Insert(Math.Max(0, keyboardIndex), nav);

        var scroll = CreateDynamicScroll("Audio");
        var panel = new AudioPanel
        {
            DataContext = window.DataContext,
            Margin = new Thickness(0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        pageHost.Children.Add(scroll);

        window.Resources[AudioNavKey] = nav;
        window.Resources[AudioPageKey] = scroll;

        nav.Checked += (_, _) =>
        {
            HideAllPages(window);
            scroll.Visibility = Visibility.Visible;
            scroll.ScrollToTop();
            panel.Initialize(app);
            AnimateElement(scroll);
        };
    }

    private static void AddTouchpad(AdvancedWindow window, App app)
    {
        if (window.FindName("NavBattery") is not RadioButton battery ||
            battery.Parent is not Panel navPanel ||
            FindPageHost(window) is not Grid pageHost)
        {
            return;
        }

        RadioButton nav = CreateNavigation(window, "Touchpad", "Touchpad");
        int batteryIndex = navPanel.Children.IndexOf(battery);
        navPanel.Children.Insert(Math.Max(0, batteryIndex), nav);

        var scroll = CreateDynamicScroll("Touchpad");
        var panel = new TouchpadPanel { Margin = new Thickness(0) };
        panel.Initialize(app);
        scroll.Content = panel;
        pageHost.Children.Add(scroll);

        window.Resources[TouchpadNavKey] = nav;
        window.Resources[TouchpadPageKey] = scroll;

        nav.Checked += (_, _) =>
        {
            HideAllPages(window);
            scroll.Visibility = Visibility.Visible;
            scroll.ScrollToTop();
            panel.Initialize(app);
            AnimateElement(scroll);
        };
    }

    private static RadioButton CreateNavigation(AdvancedWindow window, string page, string iconKind)
    {
        var nav = new RadioButton
        {
            GroupName = "Nav",
            Tag = page,
            Style = window.TryFindResource("TcNav") as Style
        };
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new PackIconLucide
        {
            Kind = iconKind,
            Width = 15,
            Height = 15,
            Margin = new Thickness(0, 0, 12, 0)
        });
        content.Children.Add(new TextBlock { Text = page });
        nav.Content = content;
        return nav;
    }

    private static ScrollViewer CreateDynamicScroll(string tag) => new()
    {
        Tag = tag,
        Visibility = Visibility.Collapsed,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
    };

    private static Grid? FindPageHost(AdvancedWindow window)
    {
        if (window.FindName("PageHome") is FrameworkElement { Parent: Grid host })
            return host;
        return null;
    }

    private static void AttachStaticNavigation(AdvancedWindow window)
    {
        foreach (RadioButton nav in StaticNavigation(window))
        {
            nav.Checked += (_, _) =>
            {
                HideDynamicPages(window);
                window.Dispatcher.BeginInvoke(() =>
                {
                    if (nav.Tag is string page)
                        AnimateVisibleStaticPage(window, page);
                });
            };
        }
    }

    private static void HideAllPages(AdvancedWindow window)
    {
        foreach (string name in StaticPageNames)
        {
            if (window.FindName(name) is FrameworkElement element)
                element.Visibility = Visibility.Collapsed;
        }
        HideDynamicPages(window);
    }

    private static void HideDynamicPages(AdvancedWindow window)
    {
        HideDynamic(window, AudioPageKey);
        HideDynamic(window, TouchpadPageKey);
    }

    private static void HideDynamic(AdvancedWindow window, string key)
    {
        if (window.Resources.Contains(key) && window.Resources[key] is FrameworkElement element)
            element.Visibility = Visibility.Collapsed;
    }

    private static IEnumerable<RadioButton> StaticNavigation(AdvancedWindow window)
    {
        foreach (string name in StaticNavNames)
        {
            if (window.FindName(name) is RadioButton nav)
                yield return nav;
        }
    }

    private static void SelectDynamicNavigation(AdvancedWindow window, string key)
    {
        if (window.Resources.Contains(key) && window.Resources[key] is RadioButton nav)
            nav.IsChecked = true;
    }

    private static void AnimateVisibleStaticPage(AdvancedWindow window, string page)
    {
        FrameworkElement? element = page switch
        {
            "Performance" => window.FindName("PagePerformance") as FrameworkElement,
            "Fans" => window.FindName("PageFans") as FrameworkElement,
            "Display" => window.FindName("PageDisplay") as FrameworkElement,
            "Keyboard" => window.FindName("PageKeyboard") as FrameworkElement,
            "Battery" => window.FindName("PageBattery") as FrameworkElement,
            "System" => window.FindName("PageSystem") as FrameworkElement,
            "Updates" => window.FindName("PageUpdates") as FrameworkElement,
            "Settings" => window.FindName("PageSettings") as FrameworkElement,
            _ => window.FindName("PageHome") as FrameworkElement
        };

        if (element?.Visibility == Visibility.Visible)
            AnimateElement(element);
    }

    private static void AnimateElement(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(135)) { EasingFunction = ease });
    }
}
