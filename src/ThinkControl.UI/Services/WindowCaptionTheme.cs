using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ThinkControl.UI.Services;

/// <summary>One native caption palette for every WPF window, including dialogs.</summary>
internal static class WindowCaptionTheme
{
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, args) =>
            {
                if (ReferenceEquals(sender, args.OriginalSource) && sender is Window window)
                    Apply(window);
            }));
    }

    internal static void Apply(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = ThemeService.IsLightEffective ? 0 : 1;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        SetColor(window, handle, 35, "Tc.Window");
        SetColor(window, handle, 36, "Tc.Text");
        SetColor(window, handle, 34, "Tc.Border");
    }

    private static void SetColor(Window window, IntPtr handle, int attribute, string resource)
    {
        if (window.TryFindResource(resource) is not SolidColorBrush brush) return;
        int color = brush.Color.R | (brush.Color.G << 8) | (brush.Color.B << 16);
        _ = DwmSetWindowAttribute(handle, attribute, ref color, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
