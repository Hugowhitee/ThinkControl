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
        if (UserSettings.Current.TouchpadGestures?.Enabled == true)
            TouchpadFeature.EnsureInputStarted();
    }

    private void OnTouchpadApplicationExit(object? sender, ExitEventArgs e)
    {
        try { _touchpadFeature?.Dispose(); }
        catch { }
    }
}
