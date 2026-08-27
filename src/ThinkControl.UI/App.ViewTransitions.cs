using System.Diagnostics;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private bool _viewTransitionBusy;

    /// <summary>
    /// Single entry point for commands that want the full surface. If Compact is
    /// visible, use the paint-before-hide transition. Otherwise the normal full
    /// window path is safe because there is no visible Compact surface to replace.
    /// </summary>
    internal void OpenAdvancedSafely(string page = "Home")
    {
        if (CompactWindow is { IsVisible: true })
            SwitchCompactToAdvanced(page);
        else
            OpenAdvanced(page);
    }

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
            try { _advancedWindow?.HideAnimated(); } catch { }
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
            try { CompactWindow.HideAnimated(); } catch { }
            try { _advancedWindow?.ShowAdvanced(animate: false); } catch { }
        }
        finally
        {
            _viewTransitionBusy = false;
        }
    }

    /// <summary>
    /// Release-CI regression gate for the exact shell path that failed in alpha.22.
    /// Unlike screenshot-only coverage this really shows both WPF windows and runs
    /// Compact -> Full -> Compact repeatedly, so a constructor, activation or hide
    /// regression fails the visual-QA executable before packaging can pass.
    /// </summary>
    internal void RunViewTransitionSmokeForVisualQa(int cycles = 3)
    {
        if (cycles < 1)
            throw new ArgumentOutOfRangeException(nameof(cycles));

        if (CompactWindow is null)
            CompactWindow = new MainWindow(this) { DataContext = State };

        try
        {
            CompactWindow.ShowNearTray(animate: false);
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

            for (int i = 0; i < cycles; i++)
            {
                SwitchCompactToAdvanced("Home");
                if (_advancedWindow?.IsVisible != true || CompactWindow.IsVisible)
                    throw new InvalidOperationException($"View transition cycle {i + 1}: Full view did not become the sole visible surface.");

                SwitchAdvancedToCompact();
                if (!CompactWindow.IsVisible || _advancedWindow?.IsVisible == true)
                    throw new InvalidOperationException($"View transition cycle {i + 1}: Compact view did not become the sole visible surface.");
            }
        }
        finally
        {
            try { _advancedWindow?.ForceClose(); } catch { }
            try { CompactWindow.ForceClose(); } catch { }
        }
    }
}
