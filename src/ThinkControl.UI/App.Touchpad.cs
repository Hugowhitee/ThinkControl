using System.Windows;
using System.Windows.Threading;
using ThinkControl.UI.Services.Touchpad;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI;

public partial class App
{
    private TouchpadFeatureHost? _touchpadFeature;
    private bool _trayActivationHooked;

    internal TouchpadFeatureHost TouchpadFeature =>
        _touchpadFeature ??= new TouchpadFeatureHost(this);

    private void OnTouchpadApplicationActivated(object? sender, EventArgs e)
    {
        // BootstrapWindow can activate before the normal app runtime exists. Do not
        // construct the touchpad host until preflight has populated the machine type
        // and the real tray/compact surface has been created.
        if (_trayIcon is null || CompactWindow is null)
            return;

        EnsureTrayActivationBehavior();
        NormalizeCompactTopmostAfterActivation();

        if (UserSettings.Current.TouchpadGestures?.Enabled == true)
            TouchpadFeature.EnsureInputStarted();
    }

    private void EnsureTrayActivationBehavior()
    {
        if (_trayActivationHooked || _trayIcon is null)
            return;

        _trayActivationHooked = true;
        _trayIcon.MouseUp += TrayIcon_ReopenAfterExistingHandler;

        if (_trayIcon.ContextMenuStrip?.Items.Count > 0)
            _trayIcon.ContextMenuStrip.Items[0].Click += (_, _) => QueueTrayActivation();
    }

    private void TrayIcon_ReopenAfterExistingHandler(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            QueueTrayActivation();
    }

    private void QueueTrayActivation()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(BringThinkControlToFrontFromTray));
    }

    private void BringThinkControlToFrontFromTray()
    {
        if (_advancedWindow?.IsVisible == true)
        {
            _advancedWindow.ShowAdvanced(animate: false);
            return;
        }

        if (CompactWindow is null)
            return;

        bool wasVisible = CompactWindow.IsVisible;

        // A tray click can deactivate the WPF flyout while Explorer's hidden-icons
        // surface is still closing. Cancel any pending opacity animation before
        // restoring so the popup cannot finish a stale hide animation afterwards.
        CompactWindow.BeginAnimation(UIElement.OpacityProperty, null);
        CompactWindow.Opacity = 1;
        CompactWindow.ShowNearTray(animate: !wasVisible);

        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (!CompactWindow.IsVisible)
                return;

            CompactWindow.Topmost = false;
            CompactWindow.Activate();
        }));
    }

    private void NormalizeCompactTopmostAfterActivation()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (CompactWindow?.IsVisible == true)
                CompactWindow.Topmost = false;
        }));
    }

    private void OnTouchpadApplicationExit(object? sender, ExitEventArgs e)
    {
        try { _touchpadFeature?.Dispose(); }
        catch { }
    }
}
