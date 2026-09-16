using System.Windows;
using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI;

public partial class App
{
    private TouchpadFeatureHost? _touchpadFeature;

    internal TouchpadFeatureHost TouchpadFeature =>
        _touchpadFeature ??= new TouchpadFeatureHost(this);

    internal void StartConfiguredTouchpadInputForStartup()
    {
        if (UserSettings.Current.TouchpadGestures?.Enabled != true)
            return;

        // A --tray Windows startup may never activate a WPF window, so relying on
        // Application.Activated leaves edge gestures dormant until the user opens
        // ThinkControl. Start them explicitly once the cheap machine identity and
        // tray/Compact runtime exist. Silent startup prioritizes registration because
        // there is no visible destination window whose first paint could be delayed.
        TouchpadFeature.EnsureInputStarted(startupCritical: IsTrayOnlyLaunch());
    }

    private void OnTouchpadApplicationActivated(object? sender, EventArgs e)
    {
        // Activation remains a recovery path after device/session transitions. The
        // normal Windows --tray startup no longer depends on this event to make
        // configured edge gestures usable.
        if (_trayIcon is null || CompactWindow is null)
            return;

        if (UserSettings.Current.TouchpadGestures?.Enabled == true)
            TouchpadFeature.EnsureInputStarted();
    }

    private void OnTouchpadApplicationExit(object? sender, ExitEventArgs e)
    {
        try { _touchpadFeature?.Dispose(); }
        catch { }
        try { DisposeAudioSafety(); }
        catch { }
    }
}
