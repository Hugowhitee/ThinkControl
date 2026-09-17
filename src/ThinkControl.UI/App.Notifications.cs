using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    public void ToggleNotificationCenter()
    {
        // Notifications live inside the normal full ThinkControl window. If Compact
        // is visible, route through the same paint-before-hide transition used by
        // the explicit expand control rather than maintaining a second shell path.
        bool showSheet = _advancedWindow is null || !_advancedWindow.IsVisible;
        if (showSheet)
            OpenAdvancedSafely("Home");

        // DispatcherPriority must be the first argument. Putting it after a
        // zero-argument Action binds to BeginInvoke(Delegate, params object[]), so
        // WPF later tries to DynamicInvoke the Action with DispatcherPriority as an
        // argument and crashes with TargetParameterCountException.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (showSheet)
                _advancedWindow?.ShowNotificationSheet();
            else
                _advancedWindow?.ToggleNotificationSheet();

            _advancedWindow?.Activate();
        }));
    }
}
