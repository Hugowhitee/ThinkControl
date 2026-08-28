using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkControl.UI;
using ThinkControl.UI.Services;
using TcThemeMode = ThinkControl.UI.Services.ThemeMode;

namespace ThinkControl.ShellSmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        App? app = null;
        Exception? scenarioFailure = null;
        int exitCode = 1;

        try
        {
            ValidateCrashJournal();
            app = App.CreateForVisualQa();
            app.InitializeComponent();
            ThemeService.Apply(TcThemeMode.Dark);
            SeedState(app);

            // Run the scenario inside a real WPF dispatcher frame. Alpha.23's
            // smoke drove transition methods synchronously without a normal message
            // pump, so routed clicks, activation/deactivation and queued work did
            // not occur in the same ordering as an installed desktop interaction.
            var scenarioFrame = new DispatcherFrame();
            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                try
                {
                    RunScenario(app);
                    exitCode = 0;
                }
                catch (Exception ex)
                {
                    scenarioFailure = ex;
                }
                finally
                {
                    scenarioFrame.Continue = false;
                }
            }));
            Dispatcher.PushFrame(scenarioFrame);

            if (scenarioFailure is not null)
            {
                Console.Error.WriteLine(scenarioFailure);
                return 1;
            }

            Console.WriteLine("Interactive shell lifecycle smoke passed: durable multi-crash journal, rapid tray-open debouncing, preferred app-icon Full/Compact routing, passive-update dismissal on Full transition, diagnostics Ready/Shared/Verified lifecycle, repeated real Compact/Full routing, notification activation/action/dismiss, minimized Touchpad recovery, bounded page-navigation latency, sole-primary-surface and dispatcher-alive assertions.");
            return exitCode;
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

    private static void ValidateCrashJournal()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ThinkControl-crash-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new CrashReportService(folder);
            var state = new ThinkControl.UI.ViewModels.AppState { MachineType = "SMOKE", DeviceName = "Smoke laptop" };
            var recorder = new DiagnosticsRecorder();

            service.CaptureFatal("smoke", new InvalidOperationException("first"), state, recorder);
            service.CaptureFatal("app-domain", new InvalidOperationException("same fatal surfaced twice"), state, recorder);
            if (service.TryGetPending()?.OccurrenceCount != 1)
                throw new InvalidOperationException("One fatal exception was counted twice by multiple process hooks.");
            // A repeated crash occurs in a new process/service instance. Two fatal
            // hooks inside one process are deliberately coalesced by the journal.
            service = new CrashReportService(folder);
            service.CaptureFatal("smoke", new InvalidOperationException("first repeat"), state, recorder);
            CrashReport first = service.TryGetPending() ?? throw new InvalidOperationException("Crash journal did not persist the first crash.");
            if (first.OccurrenceCount != 2)
                throw new InvalidOperationException($"Repeated crash count was {first.OccurrenceCount}, expected 2.");

            service.MarkOpened(first.Id);
            service.CaptureFatal("smoke", new NotSupportedException("second signature"), state, recorder);
            IReadOnlyList<CrashReport> unresolved = service.GetUnresolved();
            if (unresolved.Count != 2 || unresolved.All(item => item.Id != first.Id))
                throw new InvalidOperationException("A newer crash replaced an older unresolved crash.");

            service.Dismiss(unresolved[0].Id);
            if (service.GetUnresolved().Count != 1)
                throw new InvalidOperationException("Dismissing one crash removed more than its selected journal entry.");
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); } catch { }
        }
    }

    private static void SeedState(App app)
    {
        app.State.DeviceName = "ThinkPad X9-15 Gen 1";
        app.State.MachineType = "21Q6";
        app.State.DriverStatus = "Ready";
        app.State.HardwareAccess = "Ready";
        app.State.SelectedMode = "Balanced";
        app.State.BatteryPercent = 72;
        app.State.BatteryStatus = "On battery";
        app.State.CurrentRefreshHz = 120;
        app.State.MaxRefreshHz = 120;
        app.State.CoolingProfile = "Balanced";
    }

    private static void RunScenario(App app)
    {
        app.PrepareInteractiveShellSmoke();
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: true, full: false, "initial Compact");

        // Two impatient tray clicks before the first Input-priority open has painted
        // must still produce exactly one open. Without the activation gate the second
        // queued toggle closes Compact again, which is the real alpha.28 user bug.
        app.ExerciseRapidTrayOpenForShellSmoke();
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: true, full: false, "rapid tray double-click");
        AssertAlive(app, "rapid tray double-click");

        // Regression for alpha.24's hard-coded second-launch behavior. The saved
        // preference must own Start/desktop/taskbar re-activation in both directions.
        app.ApplyPreferredDesktopLaunchForShellSmoke("Advanced");
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: false, full: true, "preferred app-icon Full");
        AssertAlive(app, "preferred app-icon Full");

        app.ApplyPreferredDesktopLaunchForShellSmoke("Compact");
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: true, full: false, "preferred app-icon Compact");
        AssertAlive(app, "preferred app-icon Compact");

        // Alpha.32's successful-update card had no escape hatch and could remain
        // topmost over the newly opened Full surface. Reproduce the real Compact
        // state, show the passive confirmation, then invoke the actual expand button.
        // The transition owns dismissal; actionable attention windows are unaffected.
        app.ShowPassiveAttentionForShellSmoke();
        Pump(app.Dispatcher);
        Window passiveToast = app.AttentionWindowForShellSmoke
            ?? throw new InvalidOperationException("Passive update smoke: toast window was not created.");
        if (!passiveToast.IsVisible)
            throw new InvalidOperationException("Passive update smoke: confirmation was not visible over Compact.");

        InvokeButton(app.CompactWindow.ExpandButtonForShellSmoke);
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: false, full: true, "passive update -> Full");
        if (passiveToast.IsVisible)
            throw new InvalidOperationException("Passive update smoke: confirmation remained visible over Advanced.");
        AssertAlive(app, "passive update -> Full");

        app.SwitchAdvancedToCompact();
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: true, full: false, "after passive update regression");

        // Lifecycle regression for the old forever-ready diagnostics flag. An
        // unknown but fully settled device becomes Ready once, the same semantic
        // fingerprint becomes Shared after handling, while the verified X9 skips
        // compatibility learning entirely.
        app.ResetDeviceSupportLifecycleForShellSmoke();
        app.State.DeviceName = "Shell smoke laptop";
        app.State.MachineType = "SMOKE-UNKNOWN";
        app.State.DriverStatus = "Ready";
        app.State.HardwareAccess = "Provider ready";
        app.State.CanSensorTelemetry = false;
        app.State.CanCpuTemperature = false;
        app.State.CanFanTelemetry = false;
        app.State.CanFanControl = false;
        app.State.CanKeyboardBacklight = false;
        AssertDeviceSupport(app.EvaluateDeviceSupportForShellSmoke(), "ReadyToShare", 5, 5, "unknown device ready");

        app.MarkCurrentDeviceSupportHandledForShellSmoke();
        AssertDeviceSupport(app.EvaluateDeviceSupportForShellSmoke(), "Shared", 5, 5, "same report handled");

        app.State.DeviceName = "ThinkPad X9-15 Gen 1";
        app.State.MachineType = "21Q6";
        AssertDeviceSupport(app.EvaluateDeviceSupportForShellSmoke(), "Verified", 0, 0, "verified X9");

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
        // Turn the hosted-runner suppression OFF here: this sequence must pass
        // because the toast is recognized as a ThinkControl-owned window.
        app.SetExternalAutoHideSuppressedForShellSmoke(false);
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

        // Exercise the other user path that alpha.23 missed: show the same real
        // attention window again, activate it, then click its real Later/dismiss
        // button. Dismissal must restore Compact just like the primary action does.
        app.ShowAttentionForShellSmoke(static () => { });
        Pump(app.Dispatcher);
        if (!toast.IsVisible)
            throw new InvalidOperationException("Dismiss smoke: toast window is not visible.");

        _ = toast.Activate();
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: true, full: false, "after dismiss-toast activation");

        Button dismiss = FindVisualChild<Button>(toast, button =>
                string.Equals(button.Content?.ToString(), "Later", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Dismiss smoke: Later button was not found in the real toast visual tree.");
        InvokeButton(dismiss);
        Pump(app.Dispatcher);

        if (toast.IsVisible)
            throw new InvalidOperationException("Dismiss smoke: toast remained visible after Later click.");
        AssertPrimarySurface(app, compact: true, full: false, "after ThinkControl toast dismiss");
        AssertAlive(app, "after ThinkControl toast dismiss");

        // Return to hosted-runner isolation only after both real notification
        // deactivation paths have completed successfully.
        app.SetExternalAutoHideSuppressedForShellSmoke(true);

        // Finish with one more real click so notification focus cannot leave a
        // latent state that only breaks the next Compact -> Full interaction.
        InvokeButton(app.CompactWindow.ExpandButtonForShellSmoke);
        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: false, full: true, "post-notification real expand click");
        AssertAlive(app, "post-notification Full");

        ValidatePageNavigation(app);
    }

    private static void ValidatePageNavigation(App app)
    {
        AdvancedWindow window = app.AdvancedWindowForShellSmoke
            ?? throw new InvalidOperationException("Page smoke: Advanced window was not available.");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            window.Navigate("Home");
            Pump(app.Dispatcher);
            var elapsed = Stopwatch.StartNew();
            window.NavigateAudio();
            elapsed.Stop();
            if (elapsed.Elapsed > TimeSpan.FromMilliseconds(750))
            {
                throw new InvalidOperationException(
                    $"Audio smoke: navigation attempt {attempt} blocked the WPF dispatcher for {elapsed.ElapsedMilliseconds} ms.");
            }
            Pump(app.Dispatcher);
            AssertAlive(app, $"audio navigation {attempt}");
        }

        // Alpha.32 could appear permanently minimized/invisible when Touchpad page
        // activation synchronously entered raw-input/HID setup. Force the real Full
        // window into the minimized state, then use the same safe open route as tray
        // and gesture callers. The method itself must restore/paint before deferred
        // input discovery executes at ContextIdle.
        window.Navigate("Home");
        window.WindowState = WindowState.Minimized;
        Pump(app.Dispatcher);

        var touchpadElapsed = Stopwatch.StartNew();
        app.OpenAdvancedSafely("Touchpad");
        touchpadElapsed.Stop();
        if (touchpadElapsed.Elapsed > TimeSpan.FromMilliseconds(750))
        {
            throw new InvalidOperationException(
                $"Touchpad smoke: safe open blocked the WPF dispatcher for {touchpadElapsed.ElapsedMilliseconds} ms before returning.");
        }
        if (!window.IsVisible || window.WindowState == WindowState.Minimized)
            throw new InvalidOperationException("Touchpad smoke: Advanced did not recover to a visible non-minimized state.");

        Pump(app.Dispatcher);
        AssertPrimarySurface(app, compact: false, full: true, "Touchpad minimized recovery");
        AssertAlive(app, "Touchpad minimized recovery");

        window.Navigate("Home");
        Pump(app.Dispatcher);
        AssertAlive(app, "Touchpad listener detach after leaving page");
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

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && predicate(typed))
                return typed;

            T? descendant = FindVisualChild(child, predicate);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    private static void Pump(Dispatcher dispatcher)
    {
        // Run a nested frame until ApplicationIdle so queued routed events,
        // Input-priority transitions, layout/render work and deactivation callbacks
        // execute in normal dispatcher order rather than being synchronously forced.
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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

    private static void AssertDeviceSupport(
        (string Phase, int Completed, int Total) actual,
        string phase,
        int completed,
        int total,
        string stage)
    {
        if (!string.Equals(actual.Phase, phase, StringComparison.Ordinal) ||
            actual.Completed != completed || actual.Total != total)
        {
            throw new InvalidOperationException(
                $"{stage}: diagnostics state was {actual.Phase} {actual.Completed}/{actual.Total}, expected {phase} {completed}/{total}.");
        }
    }

    private static void AssertAlive(App app, string stage)
    {
        if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
            throw new InvalidOperationException($"{stage}: WPF dispatcher has begun shutting down.");
    }
}
