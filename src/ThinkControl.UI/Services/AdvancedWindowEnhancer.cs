using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI.Services;

internal static class AdvancedWindowEnhancer
{
    private const string EnhancedKey = "ThinkControl.Advanced.Enhanced";
    private const string TouchpadNavName = "ThinkControl.Dynamic.NavTouchpad";
    private const string TouchpadPageName = "ThinkControl.Dynamic.PageTouchpad";

    internal static void Ensure(AdvancedWindow window, App app)
    {
        if (window.Resources.Contains(EnhancedKey))
        {
            ApplyResponsiveLayout(window);
            return;
        }

        window.Resources[EnhancedKey] = true;
        AddTouchpadPage(window, app);
        window.SizeChanged += (_, _) => ApplyResponsiveLayout(window);
        AttachPageMotion(window);
        ApplyResponsiveLayout(window);
    }

    internal static void SelectTouchpad(AdvancedWindow window)
    {
        if (window.Resources[TouchpadNavName] is RadioButton nav)
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
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var panel = new TouchpadPanel
        {
            MaxWidth = 1260,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 4, 0)
        };
        panel.Initialize(app);
        scroll.Content = panel;
        pageHost.Children.Add(scroll);

        window.Resources[TouchpadNavName] = touchpadNav;
        window.Resources[TouchpadPageName] = scroll;

        touchpadNav.Checked += (_, _) =>
        {
            CollapseKnownPages(window);
            scroll.Visibility = Visibility.Visible;
            panel.Initialize(app);
            AnimateElement(scroll);
        };

        foreach (RadioButton button in KnownNav(window))
            button.Checked += (_, _) => scroll.Visibility = Visibility.Collapsed;
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

        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = transform;
        element.Opacity = 0;
        transform.X = 9;

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(165)) { EasingFunction = ease });
        transform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
    }

    private static void ApplyResponsiveLayout(AdvancedWindow window)
    {
        // Headless snapshot windows have Width set but ActualWidth remains zero until
        // the visual tree is arranged. Falling back to Width makes snapshot rendering
        // exercise the same wide/maximized layout that a real SizeChanged event uses.
        double windowWidth = window.ActualWidth > 1 ? window.ActualWidth : window.Width;
        double available = Math.Max(620, windowWidth - 198);
        bool wide = available >= 1200;

        SetContentWidth(window, "PageDisplay", wide ? 1120 : Math.Min(960, available));
        SetContentWidth(window, "PageKeyboard", wide ? 1160 : Math.Min(960, available));
        SetContentWidth(window, "PageBattery", wide ? 1260 : Math.Min(980, available));
        SetContentWidth(window, "PageSystem", wide ? 1280 : Math.Min(1000, available));
        SetContentWidth(window, "PageUpdates", wide ? 980 : Math.Min(820, available));
        SetContentWidth(window, "PageSettings", wide ? 1000 : Math.Min(840, available));

        if (window.Resources[TouchpadPageName] is ScrollViewer touchpad && touchpad.Content is FrameworkElement content)
        {
            touchpad.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            content.MaxWidth = wide ? 1260 : Math.Min(1040, available);
            content.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }

    private static void SetContentWidth(AdvancedWindow window, string scrollName, double maxWidth)
    {
        if (window.FindName(scrollName) is not ScrollViewer scroll || scroll.Content is not FrameworkElement content)
            return;

        scroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        content.MaxWidth = Math.Max(620, maxWidth);
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private static void CollapseKnownPages(AdvancedWindow window)
    {
        foreach (string name in KnownPageNames())
        {
            if (window.FindName(name) is FrameworkElement element)
                element.Visibility = Visibility.Collapsed;
        }
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
