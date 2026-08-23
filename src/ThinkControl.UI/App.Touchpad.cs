using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI;

public partial class App
{
    private TouchpadFeatureHost? _touchpadFeature;

    public App()
    {
        Activated += OnTouchpadApplicationActivated;
        Exit += (_, _) =>
        {
            try { _touchpadFeature?.Dispose(); }
            catch { }
        };
    }

    internal TouchpadFeatureHost TouchpadFeature =>
        _touchpadFeature ??= new TouchpadFeatureHost(this);

    private void OnTouchpadApplicationActivated(object? sender, EventArgs e)
    {
        if (UserSettings.Current.TouchpadGestures?.Enabled == true)
            TouchpadFeature.EnsureInputStarted();
    }
}
