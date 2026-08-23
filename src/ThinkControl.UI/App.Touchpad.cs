using System.Windows;
using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI;

public partial class App
{
    private TouchpadFeatureHost? _touchpadFeature;

    internal TouchpadFeatureHost TouchpadFeature =>
        _touchpadFeature ??= new TouchpadFeatureHost(this);

    private void OnTouchpadApplicationActivated(object? sender, EventArgs e)
    {
        // BootstrapWindow can activate before the normal app runtime exists. Do not
        // construct the touchpad host until preflight has populated the machine type
        // and the actual tray/compact surface has been created.
        if (_trayIcon is null || CompactWindow is null)
            return;

        if (UserSettings.Current.TouchpadGestures?.Enabled == true)
            TouchpadFeature.EnsureInputStarted();
    }

    private void OnTouchpadApplicationExit(object? sender, ExitEventArgs e)
    {
        try { _touchpadFeature?.Dispose(); }
        catch { }
    }
}
