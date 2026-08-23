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

        // Alpha.2 already has a left-click handler that toggles Compact. Keep it for
        // compatibility with the existing tray lifecycle, but queue this handler
        // afterwards so an explicit tray click always ends with ThinkControl visible.
        _trayIcon.MouseUp += TrayIcon_EnsureForeground;

        if (_trayIcon.ContextMenuStrip is { Items.Count: > 0 } menu)
        {
            Forms.ToolStripItem oldOpen = menu.Items[0];
            int index = menu.Items.IndexOf(oldOpen);
            menu.Items.Remove(oldOpen);
            oldOpen.Dispose();

            var open = new Forms.ToolStripMenuItem("Open ThinkControl");
            open.Click += (_, _) => QueueTrayActivation();
            menu.Items.Insert(Math.Max(0, index), open);
        }
    }

    private void TrayIcon_EnsureForeground(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            QueueTrayActivation();
    }

    private void QueueTrayActivation()
    {
        // Explorer's hidden-icons flyout can still own focus during MouseUp. Waiting
        // for ApplicationIdle prevents Compact.Window_Deactivated from immediately
        // hiding the popup we just opened.
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
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

        if (CompactWindow is null)
            return;

        // Cancel a stale fade-out started by Window_Deactivated while Explorer was
        // closing its hidden-icons surface, then restore the existing popup instance.
        CompactWindow.BeginAnimation(UIElement.OpacityProperty, null);
        CompactWindow.Opacity = 1;
        CompactWindow.ShowNearTray(animate: !CompactWindow.IsVisible);

        // ShowNearTray uses Topmost to win foreground placement. Drop it again once
        // focus has settled so ThinkControl behaves like a normal tray utility.
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (!CompactWindow.IsVisible)
            {
                CompactWindow.BeginAnimation(UIElement.OpacityProperty, null);
                CompactWindow.Opacity = 1;
                CompactWindow.ShowNearTray(animate: false);
            }

            CompactWindow.Activate();
            CompactWindow.Topmost = false;
        }));
    }
}
