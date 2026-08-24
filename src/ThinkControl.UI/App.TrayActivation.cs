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

        // The original bootstrap icon registered an unconditional show-recovery
        // handler in addition to the normal toggle handler. Recreate it once here
        // so there is exactly one left-click path and no double-click race.
        Forms.NotifyIcon old = _trayIcon;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open ThinkControl", null, (_, _) => Dispatcher.Invoke(ShowThinkControlFromTray));
        menu.Items.Add("Advanced", null, (_, _) => Dispatcher.Invoke(() => OpenAdvanced("Home")));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _ownedTrayIcon ?? old.Icon,
            Text = "ThinkControl",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.MouseUp += TrayIcon_Toggle;

        try { old.Visible = false; } catch { }
        try { old.Dispose(); } catch { }
    }

    private void TrayIcon_Toggle(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(ToggleThinkControlFromTray));
    }

    private void ToggleThinkControlFromTray()
    {
        if (_advancedWindow is { IsVisible: true } advanced)
        {
            advanced.HideAnimated();
            return;
        }

        if (CompactWindow is null)
            return;

        if (CompactWindow.IsVisible)
        {
            CompactWindow.HideAnimated();
            return;
        }

        ShowThinkControlFromTray();
    }

    public void ShowThinkControlFromTray()
    {
        if (CompactWindow is null)
            return;

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
