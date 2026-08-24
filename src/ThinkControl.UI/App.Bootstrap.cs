using System.Windows;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class App
{
    private BootstrapWindow? _bootstrapWindow;

    private void OnBootstrapStartup(object? sender, StartupEventArgs e)
    {
        if (e.Args.Any(argument => string.Equals(argument, "--tray", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyPostStartupShellPolish));
            return;
        }

        try
        {
            _bootstrapWindow = new BootstrapWindow();
            _bootstrapWindow.Show();
            _bootstrapWindow.UpdateLayout();

            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(CloseBootstrap));
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyPostStartupShellPolish));
        }
        catch
        {
            _bootstrapWindow = null;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyPostStartupShellPolish));
        }
    }

    private void ApplyPostStartupShellPolish()
    {
        AttachTrayActivationRecovery();
        ApplyTrayIconPolish();
        StartAutomaticUpdateChecks();

        // The original 2-second all-in-one status cadence was too aggressive for a
        // laptop companion that also talks to WMI, display APIs and a privileged
        // hardware service. Keep the UI responsive without making Windows do a
        // multi-provider refresh every two seconds. Live gesture/slider interactions
        // remain event-driven and are therefore unaffected by this cadence.
        if (_statusTimer is not null)
            _statusTimer.Interval = TimeSpan.FromSeconds(4);

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

    private void CloseBootstrap()
    {
        BootstrapWindow? window = _bootstrapWindow;
        _bootstrapWindow = null;
        if (window is null)
            return;

        try
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation(
                    window.Opacity,
                    0,
                    TimeSpan.FromMilliseconds(110));
                animation.Completed += (_, _) => window.Close();
                window.BeginAnimation(Window.OpacityProperty, animation);
            }
            else
            {
                window.Close();
            }
        }
        catch
        {
            try { window.Close(); } catch { }
        }
    }
}
