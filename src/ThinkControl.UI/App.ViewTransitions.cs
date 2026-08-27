using System.Diagnostics;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private bool _viewTransitionBusy;

    internal void SwitchCompactToAdvanced(string page = "Home")
    {
        if (_viewTransitionBusy)
            return;

        _viewTransitionBusy = true;
        try
        {
            CompactWindow.BeginExplicitViewSwitch();

            if (_advancedWindow is null)
            {
                _advancedWindow = new AdvancedWindow(this) { DataContext = State };
                _advancedWindow.Closed += (_, _) => _advancedWindow = null;
            }

            _advancedWindow.Navigate(page);

            // Paint the destination before removing Compact. Compact is topmost, so
            // the user keeps seeing a real ThinkControl surface while Advanced does
            // its first layout/ContentRendered pass instead of a blank desktop/frame.
            _advancedWindow.ShowAdvanced(animate: false);
            _advancedWindow.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

            CompactWindow.HideAnimated();
            _advancedWindow.Activate();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"ThinkControl Compact -> Advanced transition failed: {ex}");
            try { CompactWindow.ShowNearTray(animate: false); } catch { }
        }
        finally
        {
            _viewTransitionBusy = false;
        }
    }

    internal void SwitchAdvancedToCompact()
    {
        if (_viewTransitionBusy)
            return;

        _viewTransitionBusy = true;
        try
        {
            // Reverse order for the return trip: get Compact painted first, then
            // hide Advanced. There should always be at least one real app surface.
            CompactWindow.ShowNearTray(animate: false);
            CompactWindow.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

            _advancedWindow?.HideAnimated();
            CompactWindow.Activate();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"ThinkControl Advanced -> Compact transition failed: {ex}");
            try { _advancedWindow?.ShowAdvanced(animate: false); } catch { }
        }
        finally
        {
            _viewTransitionBusy = false;
        }
    }
}
