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
    private const double AdvancedContentChromeReserve = 224;
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

            // Feature-page helpers subscribe before this method, so reapplying our
            // rail after a resize intentionally wins over their older centered widths.
            SizeChanged += (_, _) => ApplyConsistentPageRail();
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

        // The dock/compact control is a non-nav child at the top of this stack.
        // Remove only navigation buttons, then append them in one stable task flow.
        // This keeps dynamic Audio/Sensors/Touchpad pages from landing in different
        // positions depending on which feature helper happened to run first.
        foreach (RadioButton button in navByTag.Values)
            navStack.Children.Remove(button);

        foreach (string tag in NavigationOrder)
        {
            if (navByTag.TryGetValue(tag, out RadioButton? button))
                navStack.Children.Add(button);
        }

        // Preserve any future tagged pages instead of hiding them accidentally.
        foreach (RadioButton button in navByTag.Values.Where(button =>
                     button.Tag is string tag && !NavigationOrder.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            navStack.Children.Add(button);
        }
    }

    private void ApplySidebarPalette()
    {
        // Navigation and page canvas share one base surface. Cards, hover and the
        // selected-nav indicator provide hierarchy instead of another near-black slab.
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
        double windowWidth = ActualWidth > 1 ? ActualWidth : Width;
        double pageWidth = Math.Min(
            AdvancedContentMaxWidth,
            Math.Max(520, windowWidth - AdvancedContentChromeReserve));

        foreach (string pageName in ConsistentPageNames)
        {
            if (FindName(pageName) is ScrollViewer scroll)
                ApplyPageRail(scroll, pageWidth);
        }

        foreach (string resourceName in DynamicPageResourceNames)
        {
            if (Resources.Contains(resourceName) && Resources[resourceName] is ScrollViewer scroll)
                ApplyPageRail(scroll, pageWidth);
        }
    }

    private static void ApplyPageRail(ScrollViewer scroll, double pageWidth)
    {
        // One literal left rail on every viewport. The explicit width prevents old
        // per-page MaxWidth + Center combinations from making pages jump sideways.
        // A 12px right gutter keeps header actions clear of the themed scrollbar.
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.HorizontalContentAlignment = HorizontalAlignment.Left;

        if (scroll.Content is not FrameworkElement content)
            return;

        content.Width = pageWidth;
        content.MinWidth = 0;
        content.MaxWidth = AdvancedContentMaxWidth;
        content.HorizontalAlignment = HorizontalAlignment.Left;
        content.Margin = new Thickness(0, 0, 12, 0);
    }

    private static void ApplySliderAvailability(Slider slider)
    {
        slider.Opacity = slider.IsEnabled ? 1.0 : 0.42;
    }

    private void NeutralizeHorizontalPageMotion()
    {
        // Keep the short opacity transition, but remove sideways movement. Stable
        // geometry is more important than decorative motion when switching settings.
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
            // Caption color is cosmetic and should never block the app.
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
