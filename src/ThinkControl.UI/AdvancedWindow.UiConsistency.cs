using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const double AdvancedContentMaxWidth = 1040;
    private const double PageRightGutter = 10;
    private bool _uiConsistencyConfigured;

    private static readonly string[] ConsistentPageNames =
    [
        "PageHome",
        "PagePerformance",
        "PageFans",
        "PageDisplay",
        "PageKeyboard",
        "PageBattery",
        "PageSystem",
        "PageUpdates",
        "PageSettings"
    ];

    private static readonly string[] DynamicPageResourceNames =
    [
        "ThinkControl.Dynamic.PageTouchpad",
        "ThinkControl.Dynamic.PageSensors",
        "ThinkControl.Dynamic.PageAudio"
    ];

    private static readonly string[] NavigationOrder =
    [
        "Home",
        "Performance",
        "Fans",
        "Sensors",
        "Battery",
        "Display",
        "Audio",
        "Keyboard",
        "Touchpad",
        "System",
        "Updates",
        "Settings"
    ];

    private void ConfigureAdvancedUiConsistency()
    {
        if (!_uiConsistencyConfigured)
        {
            _uiConsistencyConfigured = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            SizeChanged += (_, _) => Dispatcher.BeginInvoke(ApplyConsistentPageRail);
            Activated += (_, _) => ApplyConsistentCaptionPalette();

            foreach (RadioButton theme in FindVisualChildren<RadioButton>(this)
                         .Where(button => string.Equals(button.GroupName, "Theme", StringComparison.Ordinal)))
            {
                theme.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplySidebarPalette();
                    ApplyConsistentCaptionPalette();
                }));
            }

            foreach (RadioButton nav in FindVisualChildren<RadioButton>(this)
                         .Where(button => string.Equals(button.GroupName, "Nav", StringComparison.Ordinal)))
            {
                nav.Checked += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyConsistentPageRail();
                    NeutralizeHorizontalPageMotion();
                }));
            }

            foreach (Slider slider in FindVisualChildren<Slider>(this))
            {
                ApplySliderAvailability(slider);
                slider.IsEnabledChanged += (_, _) => ApplySliderAvailability(slider);
            }
        }

        ApplyNavigationOrder();
        ApplySidebarPalette();
        ApplyConsistentPageRail();
        ApplyConsistentCaptionPalette();
        NeutralizeHorizontalPageMotion();
    }

    private void ApplyNavigationOrder()
    {
        if (NavHome.Parent is not StackPanel navStack)
            return;

        Dictionary<string, RadioButton> navByTag = navStack.Children
            .OfType<RadioButton>()
            .Where(button => string.Equals(button.GroupName, "Nav", StringComparison.Ordinal))
            .Where(button => button.Tag is string)
            .ToDictionary(button => (string)button.Tag, StringComparer.OrdinalIgnoreCase);

        foreach (RadioButton button in navByTag.Values)
            navStack.Children.Remove(button);

        foreach (string tag in NavigationOrder)
        {
            if (navByTag.TryGetValue(tag, out RadioButton? button))
                navStack.Children.Add(button);
        }

        foreach (RadioButton button in navByTag.Values.Where(button =>
                     button.Tag is string tag && !NavigationOrder.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            navStack.Children.Add(button);
        }
    }

    private void ApplySidebarPalette()
    {
        if (Content is not Border { Child: Grid rootGrid })
            return;

        Grid? body = rootGrid.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 1);
        Border? sidebar = body?.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);

        sidebar?.SetResourceReference(Border.BackgroundProperty, "Tc.Window");
    }

    private void ApplyConsistentPageRail()
    {
        foreach (string pageName in ConsistentPageNames)
        {
            if (FindName(pageName) is ScrollViewer scroll)
                ApplyPageRail(scroll);
        }

        foreach (string resourceName in DynamicPageResourceNames)
        {
            if (Resources.Contains(resourceName) && Resources[resourceName] is ScrollViewer scroll)
                ApplyPageRail(scroll);
        }
    }

    private static void ApplyPageRail(ScrollViewer scroll)
    {
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;

        if (scroll.Content is not FrameworkElement content)
            return;

        // Never calculate a content width from the outer Window. During a resize the
        // vertical scrollbar owns part of the ScrollViewer viewport; an explicit
        // window-derived width can therefore sit underneath it. Stretching lets WPF
        // measure against the actual viewport and the right margin becomes an inside
        // gutter. MaxWidth keeps the intentionally narrow ThinkControl rail on large
        // displays without creating a minimum width on small windows.
        content.ClearValue(FrameworkElement.WidthProperty);
        content.MinWidth = 0;
        content.MaxWidth = AdvancedContentMaxWidth;
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Margin = new Thickness(0, 0, PageRightGutter, 0);
    }

    private static void ApplySliderAvailability(Slider slider)
    {
        slider.Opacity = slider.IsEnabled ? 1.0 : 0.42;
    }

    private void NeutralizeHorizontalPageMotion()
    {
        foreach (ScrollViewer page in FindVisualChildren<ScrollViewer>(this))
        {
            if (page.RenderTransform is not TranslateTransform transform)
                continue;

            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;
        }
    }

    private void ApplyConsistentCaptionPalette()
    {
        if (!IsSourceInitialized)
            return;

        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetDwmColor(hwnd, DwmwaCaptionColor, "Tc.Window");
            SetDwmColor(hwnd, DwmwaTextColor, "Tc.Text");
            SetDwmColor(hwnd, DwmwaBorderColor, "Tc.Border");
        }
        catch
        {
        }
    }

    private void SetDwmColor(IntPtr hwnd, int attribute, string resourceKey)
    {
        if (TryFindResource(resourceKey) is not SolidColorBrush brush)
            return;

        int colorRef = brush.Color.R | (brush.Color.G << 8) | (brush.Color.B << 16);
        _ = DwmSetWindowAttribute(hwnd, attribute, ref colorRef, sizeof(int));
    }
}