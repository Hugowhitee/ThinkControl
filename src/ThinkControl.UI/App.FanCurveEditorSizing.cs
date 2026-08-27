using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class App
{
    // Register once per process. Keeping the sizing/history policy at the Window
    // class-event boundary means every fan-curve entry point receives the same
    // screen-aware geometry and edit-history behavior.
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

        // Snapshot windows deliberately never enter the normal WPF Loaded lifecycle.
        // Their graph is still measured/arranged, though, so the direct SizeChanged
        // event gives visual QA the exact same Undo/Redo and typography enhancements
        // without inventing a separate snapshot-only toolbar.
        EventManager.RegisterClassHandler(
            typeof(FanCurveGraph),
            FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler(OnGraphSizeChanged));
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

    private static void OnGraphSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FanCurveGraph graph && FindEditor(graph) is { } window)
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

    private static FanCurveEditorWindow? FindEditor(DependencyObject node)
    {
        if (Window.GetWindow(node) is FanCurveEditorWindow direct)
            return direct;

        DependencyObject? current = node;
        while (current is not null)
        {
            if (current is FanCurveEditorWindow editor)
                return editor;

            DependencyObject? parent = null;
            try
            {
                parent = VisualTreeHelper.GetParent(current);
            }
            catch (InvalidOperationException)
            {
            }

            parent ??= LogicalTreeHelper.GetParent(current);
            current = parent;
        }
        return null;
    }
}
