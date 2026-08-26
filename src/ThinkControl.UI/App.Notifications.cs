using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    public void OpenNotificationCenter()
    {
        // Notifications live inside the normal undocked ThinkControl window. This
        // avoids a second task/window lifecycle from tray and makes dismissing the
        // sheet return users to exactly the page they were already using.
        if (_advancedWindow is null || !_advancedWindow.IsVisible)
            OpenAdvanced("Home");

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
