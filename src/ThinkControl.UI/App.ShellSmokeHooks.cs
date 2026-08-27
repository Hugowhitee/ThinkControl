using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    internal void PrepareInteractiveShellSmoke()
    {
        if (CompactWindow is null)
            CompactWindow = new MainWindow(this) { DataContext = State };

        // Hosted GitHub runners do not grant the console test process ownership of
        // the interactive desktop foreground. Suppress only that external-host
        // auto-hide artifact while transition clicks are exercised. The smoke turns
        // this off for the owned-notification activation sequence.
        CompactWindow.SuppressExternalAutoHideForShellSmoke = true;
        CompactWindow.ShowNearTray(animate: false);
        CompactWindow.UpdateLayout();
        Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
    }

    internal void SetExternalAutoHideSuppressedForShellSmoke(bool suppressed)
    {
        if (CompactWindow is not null)
            CompactWindow.SuppressExternalAutoHideForShellSmoke = suppressed;
    }

    internal void CleanupInteractiveShellSmoke()
    {
        try { CompactWindow.SuppressExternalAutoHideForShellSmoke = false; } catch { }
        try { _attentionToast.Hide(); } catch { }
        try { _advancedWindow?.ForceClose(); } catch { }
        try { CompactWindow?.ForceClose(); } catch { }
    }
}
