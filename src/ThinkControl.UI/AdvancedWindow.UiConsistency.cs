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
        "ThinkControl.Dynamic.PageSensors"
    ];

    private void ConfigureAdvancedUiConsistency()
    {
        if (!_uiConsistencyConfigured)
        {
            _uiConsistencyConfigured = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // AdvancedWindowEnhancer subscribes first. Running this handler after it
            // means our fixed left rail wins whenever the window is resized.
            SizeChanged += (_, _) => ApplyConsistentPageRail();
            Activated += (_, _) => ApplyConsistentCaptionPalette();

            foreach (RadioButton theme in FindVisualChildren<RadioButton>(this)
                         .Where(button => string.Equals(button.GroupName, "Theme", StringComparison.Ordinal)))
            {
                theme.Click += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyConsistentCaptionPalette));
            }

            foreach (RadioButton nav in FindVisualChildren<RadioButton>(this)
                         .Where(button => string.Equals(button.GroupName, "Nav", StringComparison.Ordinal)))
            {
                nav.Checked += (_, _) => Dispatcher.BeginInvoke(new Action(NeutralizeHorizontalPageMotion));
            }

            foreach (Slider slider in FindVisualChildren<Slider>(this))
            {
                ApplySliderAvailability(slider);
                slider.IsEnabledChanged += (_, _) => ApplySliderAvailability(slider);
            }
        }

        ApplySidebarPalette();
        ApplyConsistentPageRail();
        ApplyConsistentCaptionPalette();
        NeutralizeHorizontalPageMotion();
    }

    private void ApplySidebarPalette()
    {
        // Keep navigation and page canvas on one base surface. Selected items and
        // cards provide the hierarchy; the whole left rail no longer needs a third
        // near-black background color. Keep a dynamic resource reference so a live
        // Dark/Light/System switch updates the sidebar immediately.
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
        double pageWidth = Math.Min(1040, Math.Max(520, windowWidth - 204));

        foreach (string pageName in ConsistentPageNames)
        {
            if (FindName(pageName) is ScrollViewer scroll)
                ApplyPageRail(scroll, pageWidth);
        }

        foreach (string resourceName in DynamicPageResourceNames)
        {
            if (Resources[resourceName] is ScrollViewer scroll)
                ApplyPageRail(scroll, pageWidth);
        }
    }

    private static void ApplyPageRail(ScrollViewer scroll, double pageWidth)
    {
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        scroll.HorizontalContentAlignment = HorizontalAlignment.Left;

        if (scroll.Content is not FrameworkElement content)
            return;

        content.Width = pageWidth;
        content.MinWidth = 0;
        content.MaxWidth = 1040;
        content.HorizontalAlignment = HorizontalAlignment.Left;
        content.Margin = new Thickness(0, 0, 4, 0);
    }

    private static void ApplySliderAvailability(Slider slider)
    {
        slider.Opacity = slider.IsEnabled ? 1.0 : 0.42;
    }

    private void NeutralizeHorizontalPageMotion()
    {
        // Keep the existing short opacity transition, but remove the 9 px sideways
        // entrance. Switching pages now feels stable because the content never slides
        // away from its shared left rail.
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
