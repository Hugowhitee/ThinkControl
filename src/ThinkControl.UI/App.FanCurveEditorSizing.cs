using System.Windows;

namespace ThinkControl.UI;

public partial class App
{
    // Register once per process. Keeping the sizing policy at the Window class-event
    // boundary means the editor itself remains the owner of its layout while every
    // entry point (runtime and visual QA) receives the same screen-aware geometry.
    private readonly bool _fanCurveEditorSizingRegistered = FanCurveEditorSizingPolicy.Register();
}

internal static class FanCurveEditorSizingPolicy
{
    private static int _registered;

    internal static bool Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return true;

        EventManager.RegisterClassHandler(
            typeof(FanCurveEditorWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnEditorLoaded));
        return true;
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

        // WindowStartupLocation used the old fixed dimensions before Loaded. Recenter
        // after applying the real work-area size so the larger graph stays balanced.
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
    }
}