using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkControl.UI.Controls;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal static class AdvancedWindowEnhancer
{
    private const string EnhancedKey = "ThinkControl.Advanced.Enhanced";
    private const string HomePolishedKey = "ThinkControl.Advanced.HomePolished";
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
        PolishHome(window);
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
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var panel = new TouchpadPanel
        {
            MaxWidth = 1040,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var panel = new SensorsPanel
        {
            MaxWidth = 1040,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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

    private static void PolishHome(AdvancedWindow window)
    {
        if (window.Resources.Contains(HomePolishedKey) ||
            window.FindName("PageHome") is not ScrollViewer { Content: StackPanel stack })
        {
            return;
        }

        window.Resources[HomePolishedKey] = true;

        Grid? oldTelemetry = stack.Children.OfType<Grid>().FirstOrDefault();
        if (oldTelemetry is not null)
            stack.Children.Remove(oldTelemetry);

        var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border battery = Section(window, new Thickness(0, 0, 7, 0));
        var batteryGrid = new Grid();
        batteryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        batteryGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var gauge = new BatteryGauge { Width = 142, Height = 52, VerticalAlignment = VerticalAlignment.Center };
        gauge.SetBinding(BatteryGauge.PercentProperty, new Binding(nameof(AppState.BatteryPercent)));
        gauge.SetBinding(BatteryGauge.IsChargingProperty, new Binding(nameof(AppState.BatteryCharging)));
        batteryGrid.Children.Add(gauge);
        var batteryText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        AddBoundText(batteryText, nameof(AppState.BatteryPercentText), 27, null, FontWeights.Light);
        AddBoundText(batteryText, nameof(AppState.BatteryStatus), 10.5, "Tc.TextMuted");
        AddBoundText(batteryText, nameof(AppState.BatteryCompactLine), 9.5, "Tc.TextFaint");
        Grid.SetColumn(batteryText, 1);
        batteryGrid.Children.Add(batteryText);
        battery.Child = batteryGrid;
        row.Children.Add(battery);

        Border cpu = Section(window, new Thickness(7, 0, 7, 0));
        var cpuText = new StackPanel();
        AddText(cpuText, "CPU", 9.5, "Tc.TextMuted", FontWeights.SemiBold);
        AddBoundText(cpuText, nameof(AppState.CpuTemperatureText), 28, null, FontWeights.Light, new Thickness(0, 7, 0, 0));
        AddBoundText(cpuText, nameof(AppState.SelectedMode), 10, "Tc.TextMuted", null, new Thickness(0, 4, 0, 0));
        cpu.Child = cpuText;
        Grid.SetColumn(cpu, 1);
        row.Children.Add(cpu);

        Border fan = Section(window, new Thickness(7, 0, 0, 0));
        var fanText = new StackPanel();
        AddText(fanText, "FANS", 9.5, "Tc.TextMuted", FontWeights.SemiBold);
        AddBoundText(fanText, nameof(AppState.FanRpmText), 23, null, FontWeights.Light, new Thickness(0, 8, 0, 0));
        AddBoundText(fanText, nameof(AppState.FanCountText), 9.5, "Tc.TextFaint", null, new Thickness(0, 3, 0, 0));
        AddBoundText(fanText, nameof(AppState.FanStateText), 10, "Tc.TextMuted", null, new Thickness(0, 3, 0, 0));
        fan.Child = fanText;
        Grid.SetColumn(fan, 2);
        row.Children.Add(fan);

        stack.Children.Insert(0, row);
    }

    private static Border Section(FrameworkElement owner, Thickness margin) => new()
    {
        Style = owner.TryFindResource("TcSection") as Style,
        Margin = margin,
        MinHeight = 120
    };

    private static void AddText(StackPanel panel, string text, double size, string? brushKey = null, FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, FontSize = size };
        if (weight.HasValue) block.FontWeight = weight.Value;
        if (brushKey is not null && panel.TryFindResource(brushKey) is Brush brush) block.Foreground = brush;
        panel.Children.Add(block);
    }

    private static void AddBoundText(StackPanel panel, string property, double size, string? brushKey = null, FontWeight? weight = null, Thickness? margin = null)
    {
        var block = new TextBlock
        {
            FontSize = size,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = margin ?? new Thickness(0, 2, 0, 0)
        };
        if (weight.HasValue) block.FontWeight = weight.Value;
        if (brushKey is not null && panel.TryFindResource(brushKey) is Brush brush) block.Foreground = brush;
        block.SetBinding(TextBlock.TextProperty, new Binding(property));
        panel.Children.Add(block);
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