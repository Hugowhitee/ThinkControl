using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI;

public partial class App
{
    private bool _trayActivationRecoveryAttached;

    private void AttachTrayActivationRecovery()
    {
        if (_trayActivationRecoveryAttached || _trayIcon is null)
            return;

        _trayActivationRecoveryAttached = true;

        // The original alpha.2 handler toggles Compact. Keep compatibility with
        // existing callers, but make a direct tray click end in a visible,
        // foreground ThinkControl surface. This also fixes restoring a minimized
        // Advanced window, where Window.Activate() by itself is insufficient.
        _trayIcon.MouseUp += TrayIcon_EnsureForeground;

        if (_trayIcon.ContextMenuStrip is { Items.Count: > 0 } menu)
        {
            Forms.ToolStripItem oldOpen = menu.Items[0];
            int index = menu.Items.IndexOf(oldOpen);
            menu.Items.Remove(oldOpen);
            oldOpen.Dispose();

            var open = new Forms.ToolStripMenuItem("Open ThinkControl");
            open.Click += (_, _) => Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(ShowThinkControlFromTray));
            menu.Items.Insert(Math.Max(0, index), open);
        }
    }

    private void TrayIcon_EnsureForeground(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
            return;

        // Queue after the legacy MouseUp handler so its toggle cannot leave the
        // application hidden as the final state.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(ShowThinkControlFromTray));
    }

    public void ShowThinkControlFromTray()
    {
        if (_advancedWindow is { IsVisible: true } advanced)
        {
            if (advanced.WindowState == WindowState.Minimized)
                advanced.WindowState = WindowState.Normal;
            advanced.ShowAdvanced(animate: false);
            return;
        }

        CompactWindow.ShowNearTray(animate: !CompactWindow.IsVisible);
        CompactWindow.Activate();
    }
}
