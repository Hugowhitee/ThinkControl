using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI;

public partial class App
{
    internal void PrepareInteractiveShellSmoke()
    {
        if (CompactWindow is null)
            CompactWindow = new MainWindow(this) { DataContext = State };

        // Hosted GitHub runners do not grant the console test process ownership of
        // the interactive desktop foreground. Keep the legacy host suppression for
        // activation tests, but explicitly exercise the production deactivation
        // coordinator below so external focus loss can never regress into auto-hide.
        CompactWindow.SuppressExternalAutoHideForShellSmoke = true;
        CompactWindow.ShowNearTray(animate: false);
        CompactWindow.UpdateLayout();
        Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

        OnCompactDeactivated();
        Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(static () => { }));
        if (!CompactWindow.IsVisible)
            throw new InvalidOperationException("Compact focus-loss regression: deactivation hid the persistent utility window.");
    }

    internal void ExerciseRapidTrayOpenForShellSmoke()
    {
        CompactWindow.HideForViewTransition();
        _trayToggleGateUntil = 0;
        _trayToggleScheduled = 0;
        var click = new Forms.MouseEventArgs(Forms.MouseButtons.Left, 1, 0, 0, 0);
        TrayIcon_Toggle(_trayIcon, click);
        TrayIcon_Toggle(_trayIcon, click);
    }

    internal void ApplyPreferredDesktopLaunchForShellSmoke(string view)
    {
        UserSettings.Update(settings => settings with { DefaultOpeningView = view });
        ShowPreferredDesktopLaunchView();
    }

    internal (string Phase, int Completed, int Total) EvaluateDeviceSupportForShellSmoke()
    {
        var status = EvaluateDeviceSupportLifecycle(showAttention: false);
        return (status.Phase.ToString(), status.CompletedChecks, status.TotalChecks);
    }

    internal void MarkCurrentDeviceSupportHandledForShellSmoke()
    {
        var status = EvaluateDeviceSupportLifecycle(showAttention: false);
        if (status.Report is null)
            throw new InvalidOperationException("Shell smoke device-support state has no report fingerprint to handle.");
        _diagnosticLifecycleStore.MarkHandled(status.Report.Fingerprint);
        _ = EvaluateDeviceSupportLifecycle(showAttention: false);
    }

    internal void ResetDeviceSupportLifecycleForShellSmoke()
    {
        _diagnosticLifecycleStore.Clear();
        _deviceSupportStatus = null;
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
