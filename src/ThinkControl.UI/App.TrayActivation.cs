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
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(ShowThinkControlFromTray));
    }

    public void ShowThinkControlFromTray()
    {
        if (CompactWindow is null)
            return;

        // A tray click always means "show the tray/docked view". If Advanced was
        // previously open, park it instead of unexpectedly restoring the large
        // window from Explorer's hidden-icons flyout.
        if (_advancedWindow is { IsVisible: true } advanced)
            advanced.HideAnimated();

        CompactWindow.BeginAnimation(UIElement.OpacityProperty, null);
        CompactWindow.Opacity = 1;
        CompactWindow.ShowNearTray(animate: !CompactWindow.IsVisible);

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
