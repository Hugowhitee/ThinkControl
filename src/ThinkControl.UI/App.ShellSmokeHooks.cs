using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    internal void PrepareInteractiveShellSmoke()
    {
        if (CompactWindow is null)
            CompactWindow = new MainWindow(this) { DataContext = State };

        CompactWindow.ShowNearTray(animate: false);
        CompactWindow.UpdateLayout();
        Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
    }

    internal void CleanupInteractiveShellSmoke()
    {
        try { _attentionToast.Hide(); } catch { }
        try { _advancedWindow?.ForceClose(); } catch { }
        try { CompactWindow?.ForceClose(); } catch { }
    }
}
