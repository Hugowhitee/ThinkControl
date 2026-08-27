using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.UI;
using ThinkControl.UI.Services;

namespace ThinkControl.ShellSmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        App? app = null;
        try
        {
            app = App.CreateForVisualQa();
            app.InitializeComponent();
            ThemeService.Apply(ThemeMode.Dark);
            app.State.DeviceName = "ThinkPad X9-15 Gen 1";
            app.State.SelectedMode = "Balanced";
            app.State.BatteryPercent = 72;
            app.State.BatteryStatus = "On battery";
            app.State.CurrentRefreshHz = 120;
            app.State.MaxRefreshHz = 120;
            app.State.CoolingProfile = "Balanced";
            app.PrepareInteractiveShellSmoke();

            AssertPrimarySurface(app, compact: true, full: false, "initial Compact");

            // The alpha.23 gate called App.SwitchCompactToAdvanced directly. That
            // skipped the routed Button.Click + Dispatcher.BeginInvoke path used by
            // a real person. Invoke the actual rendered expand button instead.
            for (int cycle = 1; cycle <= 5; cycle++)
            {
                InvokeButton(app.CompactWindow.ExpandButtonForShellSmoke);
                Pump(app.Dispatcher);
                AssertPrimarySurface(app, compact: false, full: true, $"cycle {cycle} after real expand click");
                AssertAlive(app, $"cycle {cycle} Full");

                app.SwitchAdvancedToCompact();
                Pump(app.Dispatcher);
                AssertPrimarySurface(app, compact: true, full: false, $"cycle {cycle} after return");
                AssertAlive(app, $"cycle {cycle} Compact");
            }

            // Reproduce the other missing alpha.23 sequence: Compact is active,
            // ThinkControl shows another top-level window, the user activates and
            // clicks that notification, and Compact must remain the primary surface.
            bool attentionActionInvoked = false;
            app.ShowAttentionForShellSmoke(() => attentionActionInvoked = true);
            Pump(app.Dispatcher);

            Window toast = app.AttentionWindowForShellSmoke
                ?? throw new InvalidOperationException("Attention smoke: toast window was not created.");
            if (!toast.IsVisible)
                throw new InvalidOperationException("Attention smoke: toast window is not visible.");

            _ = toast.Activate();
            Pump(app.Dispatcher);
            AssertPrimarySurface(app, compact: true, full: false, "after ThinkControl toast activation");
            AssertAlive(app, "after ThinkControl toast activation");

            Button action = app.AttentionActionForShellSmoke
                ?? throw new InvalidOperationException("Attention smoke: action button was not created.");
            InvokeButton(action);
            Pump(app.Dispatcher);

            if (!attentionActionInvoked)
                throw new InvalidOperationException("Attention smoke: real action click did not invoke its callback.");
            if (toast.IsVisible)
                throw new InvalidOperationException("Attention smoke: toast remained visible after action click.");
            AssertPrimarySurface(app, compact: true, full: false, "after ThinkControl toast action");
            AssertAlive(app, "after ThinkControl toast action");

            // Finish with one more real click so notification focus cannot leave a
            // latent state that only breaks the next Compact -> Full interaction.
            InvokeButton(app.CompactWindow.ExpandButtonForShellSmoke);
            Pump(app.Dispatcher);
            AssertPrimarySurface(app, compact: false, full: true, "post-notification real expand click");
            AssertAlive(app, "post-notification Full");

            Console.WriteLine("Interactive shell lifecycle smoke passed: 6 real Compact expand clicks, 5 return transitions, notification activation/action, sole-primary-surface and dispatcher-alive assertions.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { app?.CleanupInteractiveShellSmoke(); } catch { }
        }
    }

    private static void InvokeButton(Button button)
    {
        if (!button.IsVisible || !button.IsEnabled)
            throw new InvalidOperationException($"Cannot invoke hidden or disabled button '{button.Name}'.");

        var peer = new ButtonAutomationPeer(button);
        if (peer.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
            throw new InvalidOperationException($"Button '{button.Name}' does not expose Invoke automation.");

        invoke.Invoke();
    }

    private static void Pump(Dispatcher dispatcher)
    {
        dispatcher.Invoke(DispatcherPriority.Input, new Action(static () => { }));
        dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));
        dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(static () => { }));
    }

    private static void AssertPrimarySurface(App app, bool compact, bool full, string stage)
    {
        bool actualCompact = app.CompactWindow.IsVisible;
        bool actualFull = app.AdvancedWindowForShellSmoke?.IsVisible == true;
        int visiblePrimarySurfaces = (actualCompact ? 1 : 0) + (actualFull ? 1 : 0);

        if (actualCompact != compact || actualFull != full || visiblePrimarySurfaces != 1)
        {
            throw new InvalidOperationException(
                $"{stage}: unexpected shell state (Compact={actualCompact}, Full={actualFull}, primaryCount={visiblePrimarySurfaces}).");
        }
    }

    private static void AssertAlive(App app, string stage)
    {
        if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
            throw new InvalidOperationException($"{stage}: WPF dispatcher has begun shutting down.");
    }
}
