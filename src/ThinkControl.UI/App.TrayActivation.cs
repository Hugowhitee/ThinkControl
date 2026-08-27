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
        menu.Items.Add("Open compact view", null, (_, _) => Dispatcher.Invoke(ShowThinkControlFromTray));
        menu.Items.Add("Open full view", null, (_, _) => Dispatcher.Invoke(() => OpenAdvancedSafely("Home")));
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

        // Clicking a WinForms NotifyIcon momentarily moves foreground ownership to
        // Explorer before the WPF dispatcher receives our queued toggle. Claim the
        // interaction immediately so Compact's flyout deactivation does not race
        // ahead and turn an intended hide into a hide-then-show (or vice versa).
        NotifyTrayInteractionStarted();
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                try { ToggleThinkControlFromTray(); }
                finally { NotifyTrayInteractionCompleted(); }
            }));
    }

    private void ToggleThinkControlFromTray()
    {
        // The tray icon represents the quick/compact surface. If the full window is
        // open, one tray click switches safely to Compact rather than hiding one
        // surface before the other one has painted.
        if (_advancedWindow is { IsVisible: true })
        {
            SwitchAdvancedToCompact();
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

        if (_advancedWindow is { IsVisible: true })
        {
            SwitchAdvancedToCompact();
            return;
        }

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
