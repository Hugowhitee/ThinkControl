using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI;

public partial class App
{
    private bool _trayActivationRecoveryAttached;
    private int _trayToggleScheduled;
    private long _trayToggleGateUntil;
    private static readonly long TrayToggleGateTicks = (long)(Stopwatch.Frequency * 0.55);

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

        long now = Stopwatch.GetTimestamp();
        if (now < Volatile.Read(ref _trayToggleGateUntil) ||
            Interlocked.CompareExchange(ref _trayToggleScheduled, 1, 0) != 0)
        {
            return;
        }

        // NotifyIcon activation should feel immediate even while background startup
        // work is still producing status updates. Queue at Input priority rather
        // than ApplicationIdle, and keep a short post-click gate so an impatient
        // second click cannot close the flyout before the first open has painted.
        NotifyTrayInteractionStarted();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                try
                {
                    ToggleThinkControlFromTray();
                    Volatile.Write(ref _trayToggleGateUntil, Stopwatch.GetTimestamp() + TrayToggleGateTicks);
                }
                finally
                {
                    Interlocked.Exchange(ref _trayToggleScheduled, 0);
                    NotifyTrayInteractionCompleted();
                }
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

    internal void HideThinkControlToTray()
    {
        if (CompactWindow?.IsVisible != true && _advancedWindow?.IsVisible != true)
            return;

        const string operation = "hide-to-tray";
        if (!TryBeginViewTransition(operation))
            return;

        try
        {
            _attentionToast.HidePassive();
            if (CompactWindow?.IsVisible == true)
                CompactWindow.HideForViewTransition();
            if (_advancedWindow?.IsVisible == true)
                _advancedWindow.HideAnimated();

            VerifyPrimarySurfaceState(operation, expectCompact: false, expectAdvanced: false);
            RecordShellEvent("shell.transition.completed", true, operation);
        }
        catch (Exception ex)
        {
            RecordShellException(operation, ex);
        }
        finally
        {
            EndViewTransition();
        }
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
