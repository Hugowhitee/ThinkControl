using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

/// <summary>
/// Makes TypographyScale an app-wide rule rather than a page-specific polish pass.
/// Windows cover the initial visual tree; UserControls cover pages created lazily
/// after the shell has already loaded (Audio, Touchpad, diagnostics, etc.).
/// </summary>
internal static class TypographyBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSurfaceLoaded));

        EventManager.RegisterClassHandler(
            typeof(UserControl),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSurfaceLoaded));

        EventManager.RegisterClassHandler(
            typeof(System.Windows.Controls.ToolTip),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSurfaceLoaded));
    }

    private static void OnSurfaceLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject root)
            return;

        // Loaded can be raised more than once after a control is reparented. Applying
        // the deterministic type normalization again is cheap and keeps late-created
        // template content on the same scale.
        ReadableTypography.Apply(root);
    }
}
