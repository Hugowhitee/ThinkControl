using System.Windows;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private BootstrapWindow? _bootstrapWindow;
    private bool _postStartupShellPolishQueued;

    private void PresentInitialShell(Task initialRefresh, TimeSpan synchronousStartupTime)
    {
        if (IsTrayOnlyLaunch())
        {
            CompactWindow.ShowNearTray(animate: false);
            QueuePostStartupShellPolish();
            return;
        }

        // Fast manual starts go directly to the selected surface. A slower
        // preflight gets a quiet, real loading state while the first hardware
        // refresh is still in progress.
        if (synchronousStartupTime < TimeSpan.FromMilliseconds(180))
        {
            CompactWindow.ShowNearTray(animate: true);
            QueuePostStartupShellPolish();
            return;
        }

        try
        {
            _bootstrapWindow = new BootstrapWindow();
            _bootstrapWindow.Show();
            _bootstrapWindow.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
            CompleteInitialShellPresentationAsync(initialRefresh);
        }
        catch
        {
            _bootstrapWindow = null;
            CompactWindow.ShowNearTray(animate: true);
            QueuePostStartupShellPolish();
        }
    }

    private async void CompleteInitialShellPresentationAsync(Task initialRefresh)
    {
        await Task.WhenAny(initialRefresh, Task.Delay(300));
        CloseBootstrap(() =>
        {
            CompactWindow.ShowNearTray(animate: true);
            QueuePostStartupShellPolish();
        });
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

        // A Windows-startup launch is intentionally silent: no compact popup,
        // advanced window, splash screen or hardware onboarding prompt.
        if (!IsTrayOnlyLaunch())
        {
            // Do not depend solely on a later Application.Activated event for hardware
            // onboarding. The first real window can already be active by the time the
            // bootstrap closes, which previously left the System page on "Checking…".
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
                    window.Close();
                    afterClosed?.Invoke();
                };
                window.BeginAnimation(Window.OpacityProperty, animation);
            }
            else
            {
                window.Close();
                afterClosed?.Invoke();
            }
        }
        catch
        {
            try { window.Close(); } catch { }
            afterClosed?.Invoke();
        }
    }
}
