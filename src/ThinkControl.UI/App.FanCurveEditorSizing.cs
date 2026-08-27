using System.Runtime.CompilerServices;
using System.Windows;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class App
{
    // Register once per process. Keeping the sizing/history policy at the Window
    // class boundary means every fan-curve entry point receives the same geometry,
    // edit history and typography behavior.
    private readonly bool _fanCurveEditorSizingRegistered = FanCurveEditorSizingPolicy.Register();
}

internal static class FanCurveEditorSizingPolicy
{
    private static int _registered;
    private static readonly ConditionalWeakTable<FanCurveEditorWindow, object> EnhancedEditors = new();

    internal static bool Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return true;

        EventManager.RegisterClassHandler(
            typeof(FanCurveEditorWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnEditorLoaded));

        // The visual-QA renderer constructs the real FanCurveEditorWindow but does
        // not Show() it, so Loaded never fires. It does set the requested snapshot
        // Width after construction. Hook that dependency-property change on this
        // window type so QA receives the same production enhancements without a
        // separate fake toolbar. Constructor-time Width=900 is ignored because the
        // content has not been built yet; the post-construction snapshot Width is not.
        FrameworkElement.WidthProperty.OverrideMetadata(
            typeof(FanCurveEditorWindow),
            new FrameworkPropertyMetadata(double.NaN, OnEditorWidthChanged));
        return true;
    }

    private static void OnEditorWidthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FanCurveEditorWindow { Content: not null } window)
            EnsureEditorEnhancements(window);
    }

    private static void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FanCurveEditorWindow window)
            return;

        Rect workArea = SystemParameters.WorkArea;
        double maxWidth = Math.Max(760, workArea.Width - 32);
        double maxHeight = Math.Max(520, workArea.Height - 32);
        double targetWidth = Math.Min(980, maxWidth);
        double targetHeight = Math.Min(760, maxHeight);

        window.SizeToContent = SizeToContent.Manual;
        window.MaxWidth = maxWidth;
        window.MaxHeight = maxHeight;
        window.MinWidth = Math.Min(800, targetWidth);
        window.MinHeight = Math.Min(620, targetHeight);
        window.Width = Math.Max(window.MinWidth, targetWidth);
        window.Height = Math.Max(window.MinHeight, targetHeight);

        if (window.Owner is { IsVisible: true } owner)
        {
            window.Left = Math.Clamp(
                owner.Left + (owner.ActualWidth - window.Width) / 2,
                workArea.Left,
                Math.Max(workArea.Left, workArea.Right - window.Width));
            window.Top = Math.Clamp(
                owner.Top + (owner.ActualHeight - window.Height) / 2,
                workArea.Top,
                Math.Max(workArea.Top, workArea.Bottom - window.Height));
        }

        EnsureEditorEnhancements(window);
    }

    private static void EnsureEditorEnhancements(FanCurveEditorWindow window)
    {
        if (EnhancedEditors.TryGetValue(window, out _))
            return;

        FanCurveEditorHistory.Attach(window);
        ReadableTypography.Apply(window);
        EnhancedEditors.Add(window, new object());
    }
}
