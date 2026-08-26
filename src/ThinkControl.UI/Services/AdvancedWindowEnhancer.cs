using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.UI.Controls;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal static class AdvancedWindowEnhancer
{
    private const string EnhancedKey = "ThinkControl.Advanced.Enhanced";
    private const string TouchpadNavName = "ThinkControl.Dynamic.NavTouchpad";
    private const string TouchpadPageName = "ThinkControl.Dynamic.PageTouchpad";
    private const string SensorsNavName = "ThinkControl.Dynamic.NavSensors";
    private const string SensorsPageName = "ThinkControl.Dynamic.PageSensors";

    internal static void Ensure(AdvancedWindow window, App app)
    {
        if (window.Resources.Contains(EnhancedKey))
            return;

        window.Resources[EnhancedKey] = true;
        AddTouchpadPage(window, app);
        AddSensorsPage(window);
        AttachPageMotion(window);
    }

    internal static void SelectTouchpad(AdvancedWindow window)
    {
        if (window.Resources[TouchpadNavName] is RadioButton nav)
            nav.IsChecked = true;
    }

    internal static void SelectSensors(AdvancedWindow window)
    {
        if (window.Resources[SensorsNavName] is RadioButton nav)
            nav.IsChecked = true;
    }

    private static void AddTouchpadPage(AdvancedWindow window, App app)
    {
        if (window.FindName("NavKeyboard") is not RadioButton keyboard ||
            window.FindName("NavBattery") is not RadioButton battery ||
            keyboard.Parent is not Panel navPanel ||
            window.FindName("PageHome") is not FrameworkElement home ||
            home.Parent is not Grid pageHost)
        {
            return;
        }

        var touchpadNav = new RadioButton
        {
            GroupName = "Nav",
            Tag = "Touchpad",
            Style = window.TryFindResource("TcNav") as Style
        };
        var navContent = new StackPanel { Orientation = Orientation.Horizontal };
        navContent.Children.Add(new PackIconLucide
        {
            Kind = "Touchpad",
            Width = 15,
            Height = 15,
            Margin = new Thickness(0, 0, 12, 0)
        });
        navContent.Children.Add(new TextBlock { Text = "Touchpad" });
        touchpadNav.Content = navContent;

        int batteryIndex = navPanel.Children.IndexOf(battery);
        navPanel.Children.Insert(Math.Max(0, batteryIndex), touchpadNav);

        var scroll = new ScrollViewer
        {
            Tag = "Touchpad",
            Visibility = Visibility.Collapsed,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var panel = new TouchpadPanel
        {
            Margin = new Thickness(0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        pageHost.Children.Add(scroll);

        window.Resources[TouchpadNavName] = touchpadNav;
        window.Resources[TouchpadPageName] = scroll;

        touchpadNav.Checked += (_, _) =>
        {
            CollapseKnownPages(window);
            HideDynamicPage(window, SensorsPageName);
            scroll.Visibility = Visibility.Visible;
            scroll.ScrollToTop();
            panel.Initialize(app);
            AnimateElement(scroll);
        };

        foreach (RadioButton button in KnownNav(window))
            button.Checked += (_, _) => scroll.Visibility = Visibility.Collapsed;
    }

    private static void AddSensorsPage(AdvancedWindow window)
    {
        if (window.FindName("NavFans") is not RadioButton fans ||
            window.FindName("NavDisplay") is not RadioButton display ||
            fans.Parent is not Panel navPanel ||
            window.FindName("PageHome") is not FrameworkElement home ||
            home.Parent is not Grid pageHost)
        {
            return;
        }

        var sensorsNav = new RadioButton
        {
            GroupName = "Nav",
            Tag = "Sensors",
            Style = window.TryFindResource("TcNav") as Style
        };
        var navContent = new StackPanel { Orientation = Orientation.Horizontal };
        navContent.Children.Add(new PackIconLucide
        {
            Kind = "Sensors",
            Width = 15,
            Height = 15,
            Margin = new Thickness(0, 0, 12, 0)
        });
        navContent.Children.Add(new TextBlock { Text = "Sensors" });
        sensorsNav.Content = navContent;

        int displayIndex = navPanel.Children.IndexOf(display);
        navPanel.Children.Insert(Math.Max(0, displayIndex), sensorsNav);

        var scroll = new ScrollViewer
        {
            Tag = "Sensors",
            Visibility = Visibility.Collapsed,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var panel = new SensorsPanel
        {
            Margin = new Thickness(0)
        };
        scroll.Content = panel;
        pageHost.Children.Add(scroll);

        window.Resources[SensorsNavName] = sensorsNav;
        window.Resources[SensorsPageName] = scroll;

        sensorsNav.Checked += (_, _) =>
        {
            CollapseKnownPages(window);
            HideDynamicPage(window, TouchpadPageName);
            scroll.Visibility = Visibility.Visible;
            scroll.ScrollToTop();
            AnimateElement(scroll);
        };

        foreach (RadioButton button in KnownNav(window))
            button.Checked += (_, _) => scroll.Visibility = Visibility.Collapsed;
        if (window.Resources[TouchpadNavName] is RadioButton touchpadNav)
            touchpadNav.Checked += (_, _) => scroll.Visibility = Visibility.Collapsed;
    }

    private static void AttachPageMotion(AdvancedWindow window)
    {
        foreach (RadioButton nav in KnownNav(window))
        {
            nav.Checked += (_, _) => window.Dispatcher.BeginInvoke(() =>
            {
                if (nav.Tag is string page)
                    AnimateVisiblePage(window, page);
            });
        }
    }

    private static void AnimateVisiblePage(AdvancedWindow window, string page)
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
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(135)) { EasingFunction = ease });
    }

    private static void CollapseKnownPages(AdvancedWindow window)
    {
        foreach (string name in KnownPageNames())
        {
            if (window.FindName(name) is FrameworkElement element)
                element.Visibility = Visibility.Collapsed;
        }
    }

    private static void HideDynamicPage(AdvancedWindow window, string resourceName)
    {
        if (window.Resources[resourceName] is FrameworkElement element)
            element.Visibility = Visibility.Collapsed;
    }

    private static IEnumerable<RadioButton> KnownNav(AdvancedWindow window)
    {
        foreach (string name in KnownNavNames())
        {
            if (window.FindName(name) is RadioButton nav)
                yield return nav;
        }
    }

    private static string[] KnownNavNames() =>
    ["NavHome", "NavPerformance", "NavFans", "NavDisplay", "NavKeyboard", "NavBattery", "NavSystem", "NavUpdates", "NavSettings"];

    private static string[] KnownPageNames() =>
    ["PageHome", "PagePerformance", "PageFans", "PageDisplay", "PageKeyboard", "PageBattery", "PageSystem", "PageUpdates", "PageSettings"];
}
