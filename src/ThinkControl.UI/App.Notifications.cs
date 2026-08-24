using System.Windows;

namespace ThinkControl.UI;

public partial class App
{
    private NotificationCenterWindow? _notificationCenterWindow;

    public void OpenNotificationCenter()
    {
        if (_notificationCenterWindow is null)
        {
            _notificationCenterWindow = new NotificationCenterWindow(this);
            _notificationCenterWindow.Closed += (_, _) => _notificationCenterWindow = null;
        }

        Window? owner = _advancedWindow?.IsVisible == true ? _advancedWindow : CompactWindow;
        if (owner?.IsVisible == true)
            _notificationCenterWindow.Owner = owner;

        if (!_notificationCenterWindow.IsVisible)
            _notificationCenterWindow.Show();
        if (_notificationCenterWindow.WindowState == WindowState.Minimized)
            _notificationCenterWindow.WindowState = WindowState.Normal;
        _notificationCenterWindow.Activate();
    }
}
