using System.Windows;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private BootstrapWindow? _bootstrapWindow;
    private bool _postStartupShellPolishQueued;

    /// <summary>
    /// Show the real ThinkControl loading surface before App.OnStartup performs its
    /// synchronous device/WMI preflight. Application.Startup is raised from
    /// base.OnStartup, so this runs early enough that Windows never has to display
    /// an unpainted/black application surface while the preflight is busy.
    /// Tray-only Windows startup intentionally remains silent.
    /// </summary>
    internal void ShowStartupBootstrapEarly()
    {
        if (IsTrayOnlyLaunch() || _bootstrapWindow is not null)
            return;

        try
        {
            _bootstrapWindow = new BootstrapWindow
            {
                // Keep the small painted loading surface above a destination window
                // until that destination has completed at least one WPF render pass.
                // This is preferable to ever exposing an empty native window frame.
                Topmost = true
            };
            _bootstrapWindow.Show();
            _bootstrapWindow.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
        }
        catch
        {
            try { _bootstrapWindow?.Close(); } catch { }
            _bootstrapWindow = null;
        }
    }

    private void PresentInitialShell(Task initialRefresh, TimeSpan synchronousStartupTime)
    {
        if (IsTrayOnlyLaunch())
        {
            QueuePostStartupShellPolish();
            return;
        }

        if (_bootstrapWindow is not null)
        {
            CompleteInitialShellPresentationAsync(initialRefresh);
            return;
        }

        // Fallback only: normally the early loader already owns this path. If its
        // creation failed, show the configured destination directly.
        if (synchronousStartupTime < TimeSpan.FromMilliseconds(180))
        {
            ShowConfiguredInitialView();
            QueuePostStartupShellPolish();
            return;
        }

        try
        {
            _bootstrapWindow = new BootstrapWindow { Topmost = true };
            _bootstrapWindow.Show();
            _bootstrapWindow.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
            CompleteInitialShellPresentationAsync(initialRefresh);
        }
        catch
        {
            _bootstrapWindow = null;
            ShowConfiguredInitialView();
            QueuePostStartupShellPolish();
        }
    }

    private async void CompleteInitialShellPresentationAsync(Task initialRefresh)
    {
        await Task.WhenAny(initialRefresh, Task.Delay(300));

        // Paint the real destination while the bootstrap is still above it. The
        // two dispatcher yields let layout/render and OnContentRendered work finish
        // before the loader fades, eliminating the black-frame gap seen in alpha.22.
        ShowConfiguredInitialView();
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);

        CloseBootstrap();
        QueuePostStartupShellPolish();
    }

    private void ShowConfiguredInitialView()
    {
        if (IsAdvancedOpeningPreferred())
            OpenAdvanced("Home");
        else
            CompactWindow.ShowNearTray(animate: true);
    }

    private void QueuePostStartupShellPolish()
    {
        if (_postStartupShellPolishQueued)
            return;

        _postStartupShellPolishQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyPostStartupShellPolish));
    }

    private void ApplyPostStartupShellPolish()
    {
        AttachTrayActivationRecovery();
        ApplyTrayIconPolish();
        StartAutomaticUpdateChecks();
        EvaluatePreviousUpdateHandoff();

        // The startup refresh intentionally performs one complete discovery pass.
        // After that, switch to the direct Windows/service runtime scheduler so WMI,
        // powercfg and display-capability discovery never run on a fixed background
        // cadence. This removes the periodic whole-laptop hitch seen during discharge
        // sessions instead of merely moving the hitch to a longer interval.
        StartRuntimeStatusScheduler();

        if (!IsTrayOnlyLaunch())
        {
            OnHardwareSetupActivated(this, EventArgs.Empty);
            ApplyPreferredLaunchViewAfterStartup();
        }
    }

    private void CloseBootstrap(Action? afterClosed = null)
    {
        BootstrapWindow? window = _bootstrapWindow;
        _bootstrapWindow = null;
        if (window is null)
        {
            afterClosed?.Invoke();
            return;
        }

        try
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation(
                    window.Opacity,
                    0,
                    TimeSpan.FromMilliseconds(110));
                animation.Completed += (_, _) =>
                {
                    window.Topmost = false;
                    window.Close();
                    afterClosed?.Invoke();
                };
                window.BeginAnimation(Window.OpacityProperty, animation);
            }
            else
            {
                window.Topmost = false;
                window.Close();
                afterClosed?.Invoke();
            }
        }
        catch
        {
            try
            {
                window.Topmost = false;
                window.Close();
            }
            catch { }
            afterClosed?.Invoke();
        }
    }
}
