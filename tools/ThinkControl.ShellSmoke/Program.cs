using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
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
            ValidateBatteryHistoryGaps();
            ValidateBatteryEtaLabels();
            ValidateModeAutomationPolicy();
            app = App.CreateForVisualQa();
            app.InitializeComponent();
            ThemeService.Apply(TcThemeMode.Dark);
            SeedState(app);

            // Run the scenario inside a real WPF dispatcher frame. Alpha.23's
            // smoke drove transition methods synchronously without a normal message
            // pump, so routed clicks, activation/deactivation and queued work did
            // not occur in the same ordering as an installed desktop interaction.
            var scenarioFrame = new DispatcherFrame();
            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
            {
                try
                {
                    await ValidateAutomationTransitions(app);
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

            Console.WriteLine("Interactive shell lifecycle smoke passed: durable multi-crash journal, rapid tray-open debouncing, preferred app-icon Advanced/Compact routing, passive-update dismissal on Full transition, diagnostics Ready/Shared/Verified lifecycle, repeated real Compact/Full routing, notification activation/action/dismiss, minimized Touchpad recovery, bounded page-navigation latency, sole-primary-surface and dispatcher-alive assertions.");
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

    private static void ValidateModeAutomationPolicy()
    {
        var context = new ModeAutomationSnapshot("School", true, 20,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "editor" }, DateTimeOffset.Now);
        var school = new ThinkControlModeDefinition("school", "School", AutomationEnabled: true,
            Triggers: [new("Wifi", "School")]);
        var appMode = new ThinkControlModeDefinition("app", "App", AutomationEnabled: true,
            Triggers: [new("Process", "editor.exe")]);
        if (ThinkControlModeAutomationPolicy.MatchScore(appMode, context) != ThinkControlModeAutomationPolicy.MatchScore(school, context))
            throw new InvalidOperationException("Equal-priority app/Wi-Fi arbitration disagrees with the UI.");
        if (ThinkControlModeAutomationPolicy.MatchScore(school with { AutomationPriority = 1 }, context) <= ThinkControlModeAutomationPolicy.MatchScore(appMode, context))
            throw new InvalidOperationException("An explicit user priority failed to outrank a lower-priority rule.");
        if (ThinkControlModeAutomationPolicy.MatchScore(school, context with { WifiSsid = "Home" }) != 0)
            throw new InvalidOperationException("School Wi-Fi remained matched after leaving the network.");
        var all = school with { MatchAllTriggers = true, Triggers = [new("Wifi", "School"), new("Power", "AC")] };
        if (ThinkControlModeAutomationPolicy.MatchScore(all, context) != 0 ||
            ThinkControlModeAutomationPolicy.MatchScore(all with { MatchAllTriggers = false }, context) == 0)
            throw new InvalidOperationException("Any/all rule semantics are inconsistent.");
        var overnight = new ThinkControlModeTrigger("Schedule", StartTime: "22:00", EndTime: "06:00", DaysMask: 1 << (int)DayOfWeek.Monday);
        DateTimeOffset tuesday = new(new DateTime(2026, 10, 6, 1, 0, 0), TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 6, 1, 0, 0)));
        if (!ThinkControlModeAutomationPolicy.Matches(overnight, context with { Now = tuesday }) ||
            ThinkControlModeAutomationPolicy.Matches(overnight, context with { Now = tuesday.AddHours(5) }))
            throw new InvalidOperationException("Overnight schedule did not use the originating day or exclusive end boundary.");
    }

    private static async Task ValidateAutomationTransitions(App app)
    {
        var original = app.UserSettings.Current;
        var manual = new ThinkControlModeDefinition("custom:qa-manual", "Manual", TouchpadGesturesEnabled: false);
        var school = new ThinkControlModeDefinition("custom:qa-school", "School", TouchpadGesturesEnabled: true);
        var blocked = new ThinkControlModeDefinition("custom:qa-blocked", "Blocked", CoolingProfile: "Quiet");
        var wifi = new ThinkControlAutomationRule("rule:qa-wifi", "School network", school.Id, [new("Wifi", "School")]);
        var process = new ThinkControlAutomationRule("rule:qa-process", "School app", school.Id, [new("Process", "editor")], Priority: 1);
        var environment = new ModeAutomationSnapshot("Home", true, 50, new HashSet<string>(), DateTimeOffset.Now);
        var engine = app.ModeAutomation;
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Automation lifecycle: " + message); }
        async Task At(int seconds, string? ssid, bool editor = false) => await engine.EvaluateSnapshotAsync(
            environment with { Now = environment.Now.AddSeconds(seconds), WifiSsid = ssid,
                RunningProcesses = editor ? new HashSet<string> { "editor" } : new HashSet<string>() });
        try
        {
            app.UserSettings.Update(settings => settings with { CustomModes = [manual, school, blocked], AutomationRules = [wifi, process] });
            Require(await app.Modes.ActivateAsync(manual.Id), "manual sparse mode failed");
            await At(0, "School");
            Require(!app.Modes.ActiveModeAutomatic, "dwell did not protect a brief Wi-Fi change");
            await At(6, "School");
            Require(app.Modes.ActiveModeAutomatic && app.GetEffectiveTouchpadGesturesEnabled(), "school entry did not apply");
            Require(engine.Status.Contains("School network") && engine.RestoreTarget == "Manual", "winner/restoration explanation missing");
            await At(7, null);
            await At(9, "School");
            await At(15, "School");
            Require(app.Modes.ActiveModeAutomatic, "short disconnect restored prematurely");
            await At(20, null);
            await At(26, null);
            Require(app.Modes.ActiveModeId == manual.Id && !app.Modes.ActiveModeAutomatic && !app.GetEffectiveTouchpadGesturesEnabled(), "leaving school did not restore the prior manual mode");

            await At(30, "School");
            Require(await app.Modes.ActivateAsync(manual.Id), "manual selection during dwell failed");
            await At(36, "School");
            Require(engine.Paused && !app.Modes.ActiveModeAutomatic, "pending context overwrote a manual selection");
            await At(42, "Home"); await At(48, "Home");
            await At(54, "School"); await At(60, "School");
            app.TouchpadFeature.UpdateConfiguration(app.TouchpadFeature.Configuration with { Enabled = false }, releaseGestureModeOwnership: true);
            Require(app.Modes.IsModified && !app.Modes.OwnsFacet(ThinkControlModeFacet.TouchpadGestures), "manual facet ownership was not released");
            await At(66, "School", true); await At(72, "School", true);
            Require(!app.GetEffectiveTouchpadGesturesEnabled() && !app.Modes.OwnsFacet(ThinkControlModeFacet.TouchpadGestures), "same-mode rule handoff reclaimed a manual override");
            Require(engine.Matches.Count(match => match.Matches) == 2 && engine.Status.StartsWith("School app"), "overlap arbitration explanation disagrees with the winner");
            await At(78, "Home"); await At(84, "Home");
            Require(app.Modes.ActiveModeId == manual.Id && app.Modes.IsModified && !app.GetEffectiveTouchpadGesturesEnabled(), "restoration undid a manual change made during automation");

            app.UserSettings.Update(settings => settings with { AutomationRules = [wifi with { ModeId = blocked.Id }] });
            app.State.CanFanControl = false;
            await At(90, "School"); await At(96, "School");
            Require(app.Modes.ActiveModeId == manual.Id && engine.Status.Contains("Could not apply"), "blocked hardware caused partial activation or false success");
            string error = engine.Status;
            await At(102, "School");
            Require(engine.Status == error, "failed automation did not back off");

            var migrated = ThinkControlAutomationRules.Migrate([school with { Triggers = [new("Wifi", "School")], AutomationEnabled = false }]);
            Require(migrated.Length == 1 && !migrated[0].Enabled && migrated[0].ModeId == school.Id, "legacy disabled rule was lost or enabled");
            Require(ThinkControlAutomationRules.Sanitize([wifi, process]).Length == 2, "multiple rules for one mode were collapsed");
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<ThinkControlUserSettings>(
                System.Text.Json.JsonSerializer.Serialize(app.UserSettings.Current));
            Require(roundTrip?.AutomationRules?.Length == 1 && roundTrip.CustomModes?.Length == 3, "settings migration did not survive serialization");

            await app.Modes.ActivateAsync(ThinkControlModeCatalog.NormalId);
            engine.Resume();
            var identical = wifi with { Id = "rule:qa-identical", Name = "Second school rule", ModeId = manual.Id };
            app.UserSettings.Update(settings => settings with { AutomationRules = [wifi, process with { Priority = -1 }, identical] });
            await At(120, "School", true); await At(126, "School", true);
            Require(app.Modes.ActiveModeId == school.Id && engine.Matches.Single(match => match.Id == identical.Id).State.Contains("comes first"),
                "first rule did not win identical conditions or explain the tie");
            var reordered = ThinkControlAutomationRules.MoveWithinPriority(app.UserSettings.Current.AutomationRules!, identical.Id, -1);
            Require(reordered[0].Id == identical.Id && reordered[1].Id == process.Id && reordered[2].Id == wifi.Id,
                "reordering changed an unrelated priority group");
            app.UserSettings.Update(settings => settings with { AutomationRules = reordered });
            await At(132, "School", true);
            Require(app.Modes.ActiveModeId == school.Id, "reordering skipped transition dwell");
            await At(138, "School", true);
            Require(app.Modes.ActiveModeId == manual.Id && engine.Status.StartsWith(identical.Name),
                "active rule incorrectly kept precedence over the new list order");
            app.UserSettings.Update(settings => settings with { AutomationRules = reordered.Select(rule => rule.Id == process.Id ? rule with { Priority = 1 } : rule).ToArray() });
            await At(144, "School", true); await At(150, "School", true);
            Require(app.Modes.ActiveModeId == school.Id && engine.Matches.Single(match => match.Id == identical.Id).State.Contains("higher priority"),
                "explicit priority did not outrank list order or explain the result");
        }
        finally
        {
            await app.Modes.ActivateAsync(ThinkControlModeCatalog.NormalId);
            app.UserSettings.Update(_ => original);
            engine.Stop();
        }
    }

    private static void ValidateBatteryEtaLabels()
    {
        var state = new ThinkControl.UI.ViewModels.AppState
        {
            BatteryCharging = true,
            BatteryPercent = 84,
            BatteryProtectionEnabled = true,
            BatteryProtectionStartPercent = 80,
            BatteryProtectionStopPercent = 85,
            BatteryEtaToChargeTarget = TimeSpan.FromMinutes(12)
        };
        if (!state.BatteryEtaText.EndsWith("to 85%", StringComparison.Ordinal))
            throw new InvalidOperationException("Battery ETA must name the verified stop threshold.");
        state.BatteryPercent = 85;
        if (state.BatteryEtaText != "85% target reached")
            throw new InvalidOperationException("A reached target must override a stale nonzero ETA.");
        state.BatteryCharging = false;
        state.BatteryStatus = "Plugged in";
        if (state.BatteryEtaText != "Charge limit 85%")
            throw new InvalidOperationException("Stopped charging at the threshold must display the limit.");
        state.BatteryPercent = 82;
        if (state.BatteryEtaText != "Charge hold, resumes below 80%")
            throw new InvalidOperationException("Charge-window hysteresis must name the resume threshold.");
        state.BatteryCharging = true;
        state.BatteryEtaToChargeTarget = null;
        state.BatteryProtectionStopPercent = 90;
        if (state.BatteryEtaText != "Estimating to 90%…")
            throw new InvalidOperationException("A changed target must not display an old-target duration.");
    }

    private static void ValidateBatteryHistoryGaps()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThinkControl-history-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-2);
            foreach (bool initialCharging in new[] { true, false })
            {
                var history = new BatteryHistoryService(Path.Combine(directory, initialCharging + ".json"));
                history.Record(initialCharging, !initialCharging, start, 60, 20, 45, 75, 80);
                history.Record(initialCharging, !initialCharging, start.AddMinutes(10), initialCharging ? 65 : 55, 20, initialCharging ? 49 : 41, 75, 80);
                history.Record(!initialCharging, initialCharging, start.AddDays(1), 50, 20, 38, 75, 80);
                BatterySessionDetail finished = history.GetRecentSessionDetails().Single(session => !session.IsActive);
                if (finished.Duration != TimeSpan.FromMinutes(10) || finished.EndPercent != (initialCharging ? 65 : 55))
                    throw new InvalidOperationException("A battery source change included an unobserved sleep gap in the previous session.");
            }
            var shortHistory = new BatteryHistoryService(Path.Combine(directory, "short.json"));
            shortHistory.Record(false, true, start, 85, 10, 63, 75, 80);
            shortHistory.Record(false, true, start.AddMinutes(1), 83, 10, 62, 75, 80);
            shortHistory.Record(false, false, start.AddMinutes(1), 83, null, 62, 75, 80);
            BatterySessionDetail shortSession = shortHistory.GetRecentSessionDetails().Single();
            if (shortSession.PercentPerHour is not null || shortSession.Summary.Contains("%/h", StringComparison.Ordinal))
                throw new InvalidOperationException("A one-minute battery percentage change was extrapolated into a misleading hourly rate.");

            DateTime localMidnight = DateTime.Today.AddDays(-1);
            var midnight = new DateTimeOffset(localMidnight, TimeZoneInfo.Local.GetUtcOffset(localMidnight));
            foreach (bool charge in new[] { true, false })
            {
                var overnight = new BatteryHistoryService(Path.Combine(directory, "midnight-" + charge + ".json"));
                overnight.Record(charge, !charge, midnight.AddMinutes(-10), 60, 10, 45, 75, 80);
                overnight.Record(charge, !charge, midnight.AddMinutes(-5), charge ? 62 : 58, 10, 45, 75, 80);
                overnight.Record(charge, !charge, midnight.AddMinutes(5), charge ? 65 : 55, 10, 45, 75, 80);
                overnight.Record(false, false, midnight.AddMinutes(10), charge ? 65 : 55, null, 45, 75, 80);
                BatteryDaySummary[] days = overnight.GetRecentDays().OrderBy(day => day.Day).ToArray();
                if (days.Length != 2 ||
                    (charge ? days[0].ChargingTime : days[0].UsageTime) != TimeSpan.FromMinutes(10) ||
                    (charge ? days[1].ChargingTime : days[1].UsageTime) != TimeSpan.FromMinutes(10) ||
                    (charge ? days[0].ChargedPercent : days[0].DischargedPercent) != 2 ||
                    (charge ? days[1].ChargedPercent : days[1].DischargedPercent) != 3)
                    throw new InvalidOperationException("An overnight battery session was attributed entirely to its starting day.");
            }
            var busy = new BatteryHistoryService(Path.Combine(directory, "many-sessions.json"));
            DateTimeOffset busyStart = midnight.AddDays(-1);
            for (int index = 0; index < 50; index++)
            {
                DateTimeOffset at = busyStart.AddMinutes(index * 15);
                busy.Record(true, false, at, 60, 10, 45, 75, 80);
                busy.Record(true, false, at.AddMinutes(5), 61, 10, 46, 75, 80);
                busy.Record(false, false, at.AddMinutes(5), 61, null, 46, 75, 80);
            }
            BatteryDaySummary busyDay = busy.GetRecentDays().Single();
            if (busyDay.ChargedPercent != 50 || busyDay.ChargingTime != TimeSpan.FromMinutes(250))
                throw new InvalidOperationException("The daily battery total was truncated to the forty most recent sessions.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
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
        app.State.SelectedPowerMode = "Balanced";
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
        AssertPrimarySurface(app, compact: false, full: true, "preferred app-icon Advanced");
        AssertAlive(app, "preferred app-icon Advanced");

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
        AssertPrimarySurface(app, compact: false, full: true, "passive update -> Advanced");
        if (passiveToast.IsVisible)
            throw new InvalidOperationException("Passive update smoke: confirmation remained visible over Advanced.");
        AssertAlive(app, "passive update -> Advanced");

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
        AssertAlive(app, "post-notification Advanced");

        ValidatePageNavigation(app);
        ValidateReadOnlyCoolingState(app);
    }

    private static void ValidateReadOnlyCoolingState(App app)
    {
        bool previousControl = app.State.CanFanControl;
        string previousProfile = app.State.CoolingProfile;
        try
        {
            app.State.CanFanControl = false;
            app.State.CoolingProfile = "Reported full-speed (read-only)";
            var panel = new ThinkControl.UI.Controls.FansPanel();
            panel.Initialize(app);
            var telemetry = new ThinkControl.Core.Ipc.TelemetrySnapshot(
                65, "Smoke fixture", 4800, "Smoke fixture", "Read-only", "Read-only firmware state", "Off",
                CoolingProfile: app.State.CoolingProfile);
            var capabilities = new ThinkControl.Core.Ipc.HardwareCapabilitySnapshot(
                true, false, true, true, FanAutoRecoverySupported: false);
            var response = new ThinkControl.Core.Ipc.ServiceResponse(1, true, Telemetry: telemetry, Capabilities: capabilities);
            typeof(ThinkControl.UI.Controls.FansPanel)
                .GetMethod("ApplyStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(panel, [response]);
            var applied = (TextBlock)panel.FindName("AppliedLevelText");
            var profile = (ComboBox)panel.FindName("ProfileComboBox");
            var recovery = (Button)panel.FindName("RecoverAutoButton");
            var card = (FrameworkElement)panel.FindName("ProfileCard");
            if (applied.Text != "Not confirmed" || profile.IsEnabled || profile.Text != "Read-only" ||
                recovery.Visibility != Visibility.Collapsed || card.Opacity != 1)
                throw new InvalidOperationException("Read-only cooling was presented as Auto, writable, blank or recoverable.");
            var recoverable = response with { Capabilities = capabilities with { FanAutoRecoverySupported = true } };
            typeof(ThinkControl.UI.Controls.FansPanel)
                .GetMethod("ApplyStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(panel, [recoverable]);
            if (profile.IsEnabled || recovery.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Independent Auto recovery incorrectly enabled profiles or remained hidden.");
            typeof(ThinkControl.UI.Controls.FansPanel)
                .GetMethod("ConfirmAutoRecovery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(panel, []);
            typeof(ThinkControl.UI.Controls.FansPanel)
                .GetMethod("ApplyStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(panel, [recoverable with { Telemetry = telemetry with { FanState = "Lenovo Auto" } }]);
            var result = (TextBlock)panel.FindName("AutoRecoveryResultText");
            if (result.Visibility != Visibility.Visible || result.Text != "Last recovery: Lenovo Auto confirmed." ||
                recovery.Visibility != Visibility.Collapsed || applied.Text != "Auto confirmed")
                throw new InvalidOperationException("Telemetry erased confirmed Auto or reoffered a completed recovery.");
            typeof(ThinkControl.UI.Controls.FansPanel)
                .GetMethod("ApplyStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(panel, [recoverable with { Telemetry = telemetry with { FanState = "Full speed" } }]);
            if (recovery.Visibility != Visibility.Visible || applied.Text != "Not confirmed")
                throw new InvalidOperationException("A later non-Auto observation failed to restore the recovery action.");
        }
        finally
        {
            app.State.CanFanControl = previousControl;
            app.State.CoolingProfile = previousProfile;
        }
    }

    private static void ValidatePageNavigation(App app)
    {
        AdvancedWindow window = app.AdvancedWindowForShellSmoke
            ?? throw new InvalidOperationException("Page smoke: Advanced window was not available.");

        double oldWidth = window.Width, oldHeight = window.Height;
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        Pump(app.Dispatcher);
        window.Navigate("Settings");
        Pump(app.Dispatcher);
        var navigation = (ScrollViewer)window.FindName("SidebarNavigationScroll");
        var settings = (RadioButton)window.FindName("NavSettings");
        Point position = settings.TranslatePoint(new Point(), navigation);
        if (position.Y < -1 || position.Y + settings.ActualHeight > navigation.ActualHeight + 1)
            throw new InvalidOperationException("Settings navigation remained outside the minimum-window scroll viewport after selecting it.");
        navigation.ScrollToTop();
        Pump(app.Dispatcher);
        navigation.ScrollToEnd();
        Pump(app.Dispatcher);
        position = settings.TranslatePoint(new Point(), navigation);
        if (position.Y < -1 || position.Y + settings.ActualHeight > navigation.ActualHeight + 1)
            throw new InvalidOperationException("Settings cannot be reached by scrolling the minimum-window sidebar.");
        window.Width = oldWidth;
        window.Height = oldHeight;
        ValidatePrecisionScrolling(app, window);
        window.Navigate("Home");
        Pump(app.Dispatcher);

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

    private static void ValidatePrecisionScrolling(App app, AdvancedWindow window)
    {
        window.Navigate("Battery");
        Pump(app.Dispatcher);
        var page = (ScrollViewer)window.FindName("PageBattery");
        page.ScrollToTop();
        Pump(app.Dispatcher);
        int lines = SystemParameters.WheelScrollLines;
        double expected = Math.Min(page.ScrollableHeight, lines < 0 ? page.ViewportHeight : lines * 16d);
        for (int tick = 0; tick < 12; tick++)
            page.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -10)
                { RoutedEvent = Mouse.MouseWheelEvent });
        Pump(app.Dispatcher);
        if (Math.Abs(page.VerticalOffset - expected) > 1)
            throw new InvalidOperationException($"Precision scroll expanded or lost small deltas: expected {expected}, got {page.VerticalOffset}.");

        page.ScrollToTop();
        Pump(app.Dispatcher);
        page.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            { RoutedEvent = Mouse.MouseWheelEvent });
        Pump(app.Dispatcher);
        if (Math.Abs(page.VerticalOffset - expected) > 1)
            throw new InvalidOperationException("Ordinary mouse wheel behavior changed during precision-scroll normalization.");
        page.ScrollToTop();
        Pump(app.Dispatcher);
        page.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(page), Environment.TickCount, Key.PageDown)
            { RoutedEvent = Keyboard.KeyDownEvent });
        Pump(app.Dispatcher);
        if (page.ScrollableHeight > 0 && page.VerticalOffset <= 0)
            throw new InvalidOperationException("Keyboard PageDown stopped scrolling after precision-scroll normalization.");
        page.ScrollToTop();
        Pump(app.Dispatcher);
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
