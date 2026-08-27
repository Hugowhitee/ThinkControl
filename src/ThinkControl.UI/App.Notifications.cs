using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    public void OpenNotificationCenter()
    {
        // Notifications live inside the normal full ThinkControl window. If Compact
        // is visible, route through the same paint-before-hide transition used by
        // the explicit expand control rather than maintaining a second shell path.
        if (_advancedWindow is null || !_advancedWindow.IsVisible)
            OpenAdvancedSafely("Home");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _advancedWindow?.ShowNotificationSheet();
            _advancedWindow?.Activate();
        }), DispatcherPriority.Background);
    }

    public void ToggleNotificationCenter()
    {
        if (_advancedWindow is null || !_advancedWindow.IsVisible)
        {
            OpenNotificationCenter();
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _advancedWindow?.ToggleNotificationSheet();
            _advancedWindow?.Activate();
        }), DispatcherPriority.Background);
    }
}
