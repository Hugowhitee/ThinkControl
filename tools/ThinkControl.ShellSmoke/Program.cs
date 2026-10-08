using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkControl.UI;
using ThinkControl.UI.Services;
using TcThemeMode = ThinkControl.UI.Services.ThemeMode;

namespace ThinkControl.ShellSmoke;

internal static partial class Program
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
            ValidateCompactLayoutMigration();
            ValidateSettingsWriteFailure();
            if (!ThinkControl.UI.Controls.TimeSeriesChart.AxisTicks(9.8, 12.2, "0").SequenceEqual(new[] { 10d, 11d, 12d }))
                throw new InvalidOperationException("Cycle chart ticks must use exact integer values at their actual positions.");
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
                    await ValidateFanSupervisorRecovery();
                    ValidateCurveInspection(app);
                    Console.WriteLine("Curve inspection passed.");
                    await ValidateAutomationTransitions(app);
                    Console.WriteLine("Automation transitions passed.");
                    ValidateModeSwitchControls(app);
                    await ValidateKeyboardTransitions();
                    await ValidateKeyboardOsdVisibilityLease();
                    await ValidateBatteryMotion();
                    Console.WriteLine("Keyboard and battery motion passed.");
                    ValidateCompactDragFeedback(app);
                    await ValidateContextTabsAndAudioReadback();
                    Console.WriteLine("Drag feedback, context tabs and audio readback passed.");
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

            Console.WriteLine("Interactive shell lifecycle smoke passed: deterministic rule precedence/restoration, keyboard latest-selection/delayed-write/disposal, durable multi-crash journal, rapid tray-open debouncing, preferred app-icon Advanced/Compact routing, passive-update dismissal on Full transition, diagnostics Ready/Shared/Verified lifecycle, repeated real Compact/Full routing, notification activation/action/dismiss, minimized Touchpad recovery, bounded page-navigation latency, sole-primary-surface and dispatcher-alive assertions.");
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

    private static async Task ValidateContextTabsAndAudioReadback()
    {
        var tabs = new ThinkControl.UI.Controls.ContextTabs { Section = "System", SelectedPage = "System" };
        var audio = new ThinkControl.UI.Controls.AudioPanel();
        audio.PrepareForSnapshot(true); // Isolate this regression from the actual Windows endpoint.
        var content = new StackPanel();
        content.Children.Add(tabs);
        content.Children.Add(audio);
        var host = new Window { Content = content, Width = 700, Height = 600, ShowInTaskbar = false, ShowActivated = false };
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            host.Show();
            host.UpdateLayout();
            int navigations = 0;
            tabs.AddHandler(ThinkControl.UI.Controls.ContextTabs.NavigationRequestedEvent,
                new RoutedEventHandler((_, _) => navigations++));
            foreach (string destination in new[] { "Updates", "Diagnostics", "Updates" })
            {
                var button = FindVisualChild<RadioButton>(tabs, b => (string?)b.Tag == destination)!;
                button.IsChecked = true;
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                tabs.Visibility = Visibility.Collapsed;
                tabs.Visibility = Visibility.Visible;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                host.UpdateLayout();
                foreach (string page in new[] { "System", "Updates", "Diagnostics" })
                    if (FindVisualChild<RadioButton>(tabs, b => (string?)b.Tag == page)!.IsChecked != (page == "System"))
                        throw new InvalidOperationException("Context tabs retained a second selection after navigation/revisit.");
            }
            if (navigations != 3) throw new InvalidOperationException("Context tab navigation was lost.");

            var output = (Slider)audio.FindName("VolumeSlider");
            var input = (Slider)audio.FindName("MicrophoneSlider");
            output.Value = 49.42;
            input.Value = 63.37;
            typeof(ThinkControl.UI.Controls.AudioPanel).GetField("_snapshotMode", flags)!.SetValue(audio, false);
            var apply = typeof(ThinkControl.UI.Controls.AudioPanel).GetMethod("ApplyVolumeStatus", flags)!;
            apply.Invoke(audio, [new WindowsVolumeStatus(true, 49, false, "QA output"), new WindowsVolumeStatus(true, 63, false, "QA input")]);
            if (output.Value != 49.42 || input.Value != 63.37)
                throw new InvalidOperationException("Confirmed audio readback quantized the continuous thumb position.");
            apply.Invoke(audio, [new WindowsVolumeStatus(true, 70, false, "QA output"), new WindowsVolumeStatus(true, 25, false, "QA input")]);
            if (output.Value != 70 || input.Value != 25)
                throw new InvalidOperationException("Real external audio changes did not update sliders.");
            var generation = typeof(ThinkControl.UI.Controls.AudioPanel).GetField("_volumeProbeGeneration", flags)!;
            int previous = (int)generation.GetValue(audio)!;
            typeof(ThinkControl.UI.Controls.AudioPanel).GetMethod("VolumeSlider_MouseDown", flags)!.Invoke(audio, [output, null]);
            apply.Invoke(audio, [new WindowsVolumeStatus(true, 5, false, "QA output"), new WindowsVolumeStatus(true, 25, false, "QA input")]);
            if ((int)generation.GetValue(audio)! <= previous || output.Value != 70)
                throw new InvalidOperationException("Audio interaction did not invalidate older reads/protect the active thumb.");
        }
        finally { host.Close(); }
    }

    private static void ValidateCompactLayoutMigration()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThinkControl-Layout-Smoke-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "layout.json");
        try
        {
            File.WriteAllText(path, "[\"cpu\",\"battery\",\"power\"]");
            var service = new CompactMetricLayoutService(path);
            File.WriteAllText(path, "{\"Schema\":2,\"Metrics\":[\"CPU\",\"Battery\",\"Power\"],\"Controls\":[\"Keyboard\",\"Display\",\"Fans\",\"Performance\"]}");
            if (!service.LoadControls().SequenceEqual(new[] { "Mode", "Display", "Fans", "Performance" })) throw new InvalidOperationException("Schema 2 did not replace Keyboard with Mode in place.");
            service.SaveControls(["Keyboard", "Display", "Fans", "Performance"]);
            if (!service.Load().SequenceEqual(new[] { "CPU", "Battery", "Power" })) throw new InvalidOperationException("Quick controls lost the legacy status layout.");
            service.Save(["Fans", "CPU", "Battery"]);
            if (!service.LoadControls().SequenceEqual(new[] { "Keyboard", "Display", "Fans", "Performance" })) throw new InvalidOperationException("Status layout lost the saved quick-control order.");
            service.SaveControls(["Automation", "Mode", "Fans", "Keyboard"]);
            if (!service.LoadControls().SequenceEqual(new[] { "Automation", "Mode", "Fans", "Keyboard" })) throw new InvalidOperationException("Available controls did not round-trip.");
            service.SaveControls(["Keyboard", "Keyboard", "Fans", "Performance"]);
            if (!service.LoadControls().SequenceEqual(new[] { "Performance", "Fans", "Display", "Mode" })) throw new InvalidOperationException("Duplicate control slots were retained.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void ValidateSettingsWriteFailure()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ThinkControl-settings-qa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "settings.json");
            var store = new UserSettingsService(settingsPath: path);
            if (!store.TryUpdate(settings => settings with { DefaultOpeningView = "Advanced" }))
                throw new InvalidOperationException("Settings atomic commit failed at a writable destination.");
            // Block atomic replacement after the temp file has been flushed.
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (store.TryUpdate(settings => settings with { DefaultOpeningView = "Compact" }) || store.Current.DefaultOpeningView != "Advanced")
                throw new InvalidOperationException("Failed settings persistence was reported as a committed selection.");
            if (new UserSettingsService(settingsPath: path).Current.DefaultOpeningView != "Advanced")
                throw new InvalidOperationException("Rejected settings were recovered as a successful selection at startup.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void ValidateModeSwitchControls(App app)
    {
        var original = app.UserSettings.Current;
        var mode = new ThinkControlModeDefinition("custom:switch-qa", "Switch QA", TouchpadGesturesEnabled: false);
        var rule = new ThinkControlAutomationRule("rule:switch-qa", "Campus rule", mode.Id, [new("Wifi", "Campus")]);
        var panel = new ThinkControl.UI.Controls.ModesPanel();
        var host = new Window { Content = panel, Width = 800, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            app.UserSettings.Update(settings => settings with { CustomModes = [mode], AutomationRules = [rule] });
            panel.Initialize(app, automationSurface: true);
            host.Show(); Pump(app.Dispatcher);
            var rows = (StackPanel)panel.FindName("RuleRows");
            var toggle = VisualDescendants<CheckBox>(rows).Single();
            if (toggle.ActualHeight < 40 || toggle.ActualWidth < 48 ||
                VisualTreeHelper.HitTest(toggle, new Point(toggle.ActualWidth / 2, 2)) is null)
                throw new InvalidOperationException("Shared switch does not expose its full 40-unit hit area.");
            toggle.IsChecked = false;
            toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if (app.UserSettings.Current.AutomationRules!.Single().Enabled || app.UserSettings.Current.AutomationRules!.Single().Conditions.Single().Value != "Campus")
                throw new InvalidOperationException("Rule switch failed to persist independently of its condition values.");
            var edit = VisualDescendants<Button>(rows).Single(button => Equals(button.Content, "Edit rule"));
            edit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var triggerRows = (StackPanel)panel.FindName("EditorTriggers");
            var condition = VisualDescendants<CheckBox>(triggerRows).Single();
            condition.IsChecked = false;
            condition.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if (!app.UserSettings.Current.AutomationRules!.Single().Conditions.Single().Enabled)
                throw new InvalidOperationException("Condition switch committed before Save.");
            VisualDescendants<Button>(panel).Single(button => Equals(button.Content, "Save"))
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var saved = app.UserSettings.Current.AutomationRules!.Single();
            if (saved.Conditions.Single().Enabled || saved.Conditions.Single().Value != "Campus" || saved.Enabled)
                throw new InvalidOperationException("Saving a disabled condition lost its value or changed rule enablement.");
        }
        finally { host.Close(); app.UserSettings.Update(_ => original); }
    }

    private static void ValidateCompactDragFeedback(App app)
    {
        var preview = new MainWindow(app) { ShowActivated = false, Topmost = false };
        var editor = preview.CreateLayoutEditorForSnapshot();
        try
        {
            editor.UpdateLayout();
            System.Windows.DragEventArgs DragArgs(System.Windows.IDataObject data, DragDropKeyStates keys, System.Windows.DragDropEffects effects, DependencyObject target, System.Windows.Point point, RoutedEvent routedEvent)
            {
                var args = (System.Windows.DragEventArgs)Activator.CreateInstance(typeof(System.Windows.DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, [data, keys, effects, target, point], null)!;
                args.RoutedEvent = routedEvent;
                return args;
            }
            Button Target(string id) => VisualDescendants<Button>(editor).First(b => b.AllowDrop && Equals(b.Tag, id));
            var mode = Target("Mode");
            var payload = new System.Windows.DataObject("ThinkControl.CompactLayout", "C:Fans");
            var over = DragArgs(payload, DragDropKeyStates.LeftMouseButton, System.Windows.DragDropEffects.Move, mode, new System.Windows.Point(5, 5), System.Windows.DragDrop.DragOverEvent);
            mode.RaiseEvent(over);
            if (over.Effects != System.Windows.DragDropEffects.Move || mode.BorderThickness.Left != 2)
                throw new InvalidOperationException("Compact drag target did not highlight a valid swap.");
            mode.RaiseEvent(DragArgs(payload, 0, System.Windows.DragDropEffects.Move, mode, new System.Windows.Point(), System.Windows.DragDrop.DragLeaveEvent));
            if (mode.ReadLocalValue(Control.BorderThicknessProperty) != DependencyProperty.UnsetValue)
                throw new InvalidOperationException("Compact drag target retained its highlight after leaving.");
            var invalid = DragArgs(new System.Windows.DataObject("ThinkControl.CompactLayout", "M:CPU"), 0, System.Windows.DragDropEffects.Move, mode, new System.Windows.Point(), System.Windows.DragDrop.DragOverEvent);
            mode.RaiseEvent(invalid);
            if (invalid.Effects != System.Windows.DragDropEffects.None) throw new InvalidOperationException("Compact accepted a cross-family drag.");
            mode.RaiseEvent(DragArgs(payload, 0, System.Windows.DragDropEffects.Move, mode, new System.Windows.Point(), System.Windows.DragDrop.DropEvent));
            editor.UpdateLayout();
            var dashboard = (ThinkControl.UI.Controls.CompactDashboard)preview.FindName("Dashboard");
            var slots = (string[])typeof(ThinkControl.UI.Controls.CompactDashboard).GetField("_compactControlSlots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dashboard)!;
            if (!slots.SequenceEqual(new[] { "Performance", "Mode", "Display", "Fans" })) throw new InvalidOperationException("Compact drop did not swap slots.");
        }
        finally { editor.Close(); preview.ForceClose(); }
    }

    private static async Task ValidateBatteryMotion()
    {
        var gauge = new ThinkControl.UI.Controls.BatteryGauge { Width = 100, Height = 40, Percent = 78, IsCharging = true, MotionPreference = "On" };
        var host = new Window { Content = gauge, Width = 140, Height = 100, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            host.Show();
            double initial = gauge.MotionPhase;
            await Task.Delay(350);
            if ((!gauge.MotionActive || gauge.MotionPhase == initial))
                throw new InvalidOperationException("Charging gauge did not move in a real WPF rendering loop.");
            gauge.IsCharging = false;
            gauge.IsDischarging = true;
            await Task.Delay(450);
            if (!gauge.MotionActive)
                throw new InvalidOperationException("Discharge flow stopped while discharging.");
            gauge.MotionEnabled = false;
            if (gauge.MotionActive) throw new InvalidOperationException("Compact still animates with motion disabled.");
            gauge.MotionEnabled = true;
            gauge.MotionPreference = "System";
            if (!SystemParameters.ClientAreaAnimation && gauge.MotionActive) throw new InvalidOperationException("System motion preference ignored Windows reduced motion.");
            gauge.MotionPreference = "On";
            gauge.IsDischarging = false;
            await Task.Delay(1300);
            if (gauge.MotionActive)
                throw new InvalidOperationException("Paused battery kept a permanent animation callback.");
        }
        finally { host.Close(); }
        if (gauge.MotionActive) throw new InvalidOperationException("Battery animation survived window unload.");
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
        var disabled = school with { MatchAllTriggers = true, Triggers = [new("Wifi", "School", Enabled: false)] };
        if (ThinkControlModeAutomationPolicy.MatchScore(disabled, context) != 0 ||
            ThinkControlModeAutomationPolicy.MatchScore(all with { Triggers = [new("Wifi", "School"), new("Power", "AC", Enabled: false)] }, context) == 0)
            throw new InvalidOperationException("Disabled conditions must be excluded; zero enabled conditions must never match.");
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
            Require(engine.ActiveRuleId == wifi.Id && ModeStatusPresentation.From(app).Detail.Contains("School network"), "active rule was not identified in the shared mode presentation");
            Require(await app.Modes.ActivateAsync(ThinkControlModeCatalog.NormalId), "No mode did not restore regular settings");
            await At(7, "School");
            Require(engine.Paused && app.Modes.ActiveModeId == ThinkControlModeCatalog.NormalId, "No mode was immediately replaced by the current trigger");
            Require(await app.Modes.ActivateAsync(ThinkControlModeCatalog.NormalId) && engine.Paused, "reselecting No mode failed");
            Require(await app.Modes.ActivateAsync(manual.Id), "manual baseline could not be restored for Resume");
            engine.Resume();
            await At(7, "School"); await At(13, "School");
            Require(app.Modes.ActiveModeAutomatic, "Resume did not re-evaluate the enabled rule");
            engine.Pause();
            await At(14, "Home", editor: true); await At(20, "Home", editor: true);
            Require(engine.Paused && engine.ActiveRuleId == wifi.Id, "explicit Off did not hold across a different winning context");
            Require(app.UserSettings.Current.AutomationRules!.All(rule => rule.Enabled), "pausing disabled saved rules");
            engine.Resume();
            await At(7, "School"); await At(13, "School");
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
            Require(engine.ActiveRuleId is null && ModeStatusPresentation.From(app).Detail.Contains("paused"), "manual override still appeared as an active automatic rule");
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

    private static async Task ValidateKeyboardTransitions()
    {
        var state = new ThinkControl.UI.ViewModels.AppState { CanKeyboardBacklight = true, CanKeyboardEffects = true,
            KeyboardBaseLevel = "Low", KeyboardEffectSpeed = 2 };
        var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new List<string>();
        string heldLevel = "High";
        async Task<ThinkControl.Core.Ipc.ServiceResponse?> Write(string level, CancellationToken token)
        {
            lock (writes) writes.Add(level);
            if (level == heldLevel)
            {
                writeStarted.TrySetResult();
                // A provider may finish an already accepted request after cancellation.
                await releaseWrite.Task;
            }
            return new(ThinkControl.Core.Ipc.ThinkControlProtocol.Version, true);
        }
        using var effects = new KeyboardEffectService(Write, state);
        try
        {
            await effects.SetModeAsync("Breathing");
            await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var olderChoice = effects.SetModeAsync("Breathing");
            await Task.Delay(15);
            var latestChoice = effects.SetStaticLevelAsync("Low");
            await Task.Delay(600);
            releaseWrite.TrySetResult();
            await Task.WhenAll(olderChoice, latestChoice).WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(550);
            if (state.KeyboardMode != "Static" || state.KeyboardStatus != "Low" || writes[^1] != "Low")
                throw new InvalidOperationException("Keyboard lifecycle: an older effect selection replaced the latest static choice after a delayed write.");

            heldLevel = "Off";
            writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            releaseWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (writes) writes.Clear();
            var oldStatic = effects.SetStaticLevelAsync("Off");
            await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var supersededStatic = effects.SetStaticLevelAsync("High");
            var finalStatic = effects.SetStaticLevelAsync("Low");
            releaseWrite.TrySetResult();
            var results = await Task.WhenAll(oldStatic, supersededStatic, finalStatic);
            if (!results.SequenceEqual(new[] { false, false, true }) || !writes.SequenceEqual(new[] { "Off", "Low" }) || state.KeyboardStatus != "Low")
                throw new InvalidOperationException("Keyboard lifecycle: superseded static commands wrote hardware or reported success.");

            heldLevel = "High";
            writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            releaseWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var closingWrite = effects.SetStaticLevelAsync("High");
            await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            effects.Dispose();
            releaseWrite.TrySetResult();
            if (await closingWrite || state.KeyboardStatus != "Low")
                throw new InvalidOperationException("Keyboard lifecycle: a late request updated a disposed owner.");
        }
        finally
        {
            releaseWrite.TrySetResult();
            await effects.SetStaticLevelAsync("Low");
        }
    }

    private static async Task ValidateKeyboardOsdVisibilityLease()
    {
        object gate = new();
        bool popup = false, visible = true;
        var changed = new List<bool>();
        using var suppressor = new LenovoKeyboardOsdSuppressor(() => 42,
            _ => { lock (gate) return popup && visible ? new[] { new IntPtr(1) } : []; },
            (_, show) => { lock (gate) { visible = show; changed.Add(show); } },
            (_, pid) => pid == 42, TimeSpan.FromMilliseconds(100));
        suppressor.Arm();
        lock (gate) popup = true;
        await Task.Delay(160);
        lock (gate)
            if (!changed.SequenceEqual(new[] { false, true }) || !visible)
                throw new InvalidOperationException("Keyboard OSD: burst expiry left the reusable Lenovo popup hidden.");
        // A window visible before an effect must remain untouched (e.g. Fn+Space).
        suppressor.Arm();
        await Task.Delay(160);
        lock (gate)
            if (changed.Count != 2) throw new InvalidOperationException("Keyboard OSD: an existing popup was hidden.");
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
            string cyclePath = Path.Combine(directory, "cycles.json");
            var cycleHistory = new BatteryHistoryService(cyclePath);
            cycleHistory.Record(false, false, start, 60, null, 45, 75, 80, 100);
            cycleHistory.Record(false, false, start.AddMinutes(1), 60, null, 45, 75, 80, 100);
            cycleHistory.Record(false, false, start.AddMinutes(2), 60, null, 45, 75, 80, null);
            cycleHistory.Record(false, false, start.AddDays(1), 60, null, 45, 75, 80, 102);
            BatteryHistoryView cycleView = new BatteryHistoryService(cyclePath).GetView();
            if (cycleView.CycleCountTimeline.Count != 2 || cycleView.CycleCountTimeline[1].Value != 102 ||
                !cycleView.CycleTrendText.Contains("2 cycles added", StringComparison.Ordinal))
                throw new InvalidOperationException("Cycle history failed persistence, de-duplication or unknown-reading handling.");
            cycleHistory.Record(false, false, start.AddDays(1).AddHours(1), 60, null, 45, 75, 80, 3);
            cycleView = cycleHistory.GetView();
            if (cycleView.CycleCountTimeline[^1].Value != 3 || !cycleView.CycleTrendText.Contains("counter decreased", StringComparison.Ordinal))
                throw new InvalidOperationException("A firmware cycle-counter reset was reported as negative battery wear.");
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
        var limitedState = new ThinkControl.UI.ViewModels.AppState
        {
            CanFanControl = true,
            FanControlKind = ThinkControl.Core.Ipc.FanControlKinds.FullSpeedOnly,
            CoolingProfile = "Max cooling",
            HardwareAccess = "Internal failure · InvalidClass · driver error"
        };
        var limitedFans = new ThinkControl.UI.Controls.FansPanel();
        limitedFans.Initialize(app);
        limitedFans.PrepareForSnapshot(limitedState);
        var limitedSelector = (ComboBox)limitedFans.FindName("ProfileComboBox");
        string[] limitedNames = limitedSelector.Items.Cast<object>()
            .Select(item => (string)item.GetType().GetProperty("Name")!.GetValue(item)!).ToArray();
        if (!limitedNames.SequenceEqual(new[] { "Auto", "Max cooling" }) || !limitedSelector.IsEnabled ||
            ((FrameworkElement)limitedFans.FindName("AdvancedFanControlsExpander")).Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Limited fan capability exposed unsupported choices or hid its working selector.");

        AdvancedWindow window = app.AdvancedWindowForShellSmoke
            ?? throw new InvalidOperationException("Page smoke: Advanced window was not available.");
        foreach (var (metric, destination) in new[] { ("CPU", "Diagnostics"), ("Sensors", "Diagnostics"), ("Fans", "Fans"), ("Battery", "Battery"), ("Power", "Battery"), ("Display", "Display"), ("Keyboard", "Keyboard"), ("Performance", "Performance") })
        {
            app.SwitchAdvancedToCompact();
            Pump(app.Dispatcher);
            var dashboard = (ThinkControl.UI.Controls.CompactDashboard)app.CompactWindow.FindName("Dashboard");
            var slots = typeof(ThinkControl.UI.Controls.CompactDashboard).GetField("_compactMetricSlots", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var original = (string[])slots.GetValue(dashboard)!;
            slots.SetValue(dashboard, new[] { metric, "Battery", "Fans" });
            try { ((Button)dashboard.FindName("CompactMetricSlot0")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            finally { slots.SetValue(dashboard, original); }
            Pump(app.Dispatcher);
            string? actual = (string?)typeof(AdvancedWindow).GetField("_selectedPage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
            if (actual != destination || !window.IsVisible)
                throw new InvalidOperationException($"Compact {metric} opened {actual} instead of {destination}.");
        }
        ValidateHistoryManagement(app, window);

        double oldWidth = window.Width, oldHeight = window.Height;
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        Pump(app.Dispatcher);
        window.Navigate("Settings");
        Pump(app.Dispatcher);
        var navigation = (ScrollViewer)window.FindName("SidebarNavigationScroll");
        var settings = (RadioButton)window.FindName("NavSystem");
        Point position = settings.TranslatePoint(new Point(), navigation);
        if (position.Y < -1 || position.Y + settings.ActualHeight > navigation.ActualHeight + 1)
            throw new InvalidOperationException("Settings navigation remained outside the minimum-window scroll viewport after selecting it.");
        navigation.ScrollToTop();
        Pump(app.Dispatcher);
        var scrollFade = (Border)window.FindName("SidebarScrollFade");
        if (scrollFade.Visibility != Visibility.Visible || scrollFade.IsHitTestVisible)
            throw new InvalidOperationException("Sidebar scroll hint did not appear without blocking input while more navigation exists.");
        navigation.ScrollToEnd();
        Pump(app.Dispatcher);
        if (scrollFade.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Sidebar scroll hint remained visible after reaching the end.");
        position = settings.TranslatePoint(new Point(), navigation);
        if (position.Y < -1 || position.Y + settings.ActualHeight > navigation.ActualHeight + 1)
            throw new InvalidOperationException("Settings cannot be reached by scrolling the minimum-window sidebar.");
        window.Width = oldWidth;
        window.Height = oldHeight;
        ValidatePrecisionScrolling(app, window);
        ValidateSharedNavigationReset(app, window);
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

    private static void ValidateSharedNavigationReset(App app, AdvancedWindow window)
    {
        foreach (string name in new[] { "Home", "Modes", "Automation", "Performance", "Battery", "Display", "Audio", "Keyboard", "Touchpad", "System", "Updates", "Diagnostics" })
        {
            window.Navigate(name);
            Pump(app.Dispatcher);
            var page = (ScrollViewer)window.FindName("Page" + name);
            ThinkControl.UI.Controls.ModesPanel? modes = page.Content as ThinkControl.UI.Controls.ModesPanel;
            if (modes is not null)
            {
                if (name == "Automation") modes.PrepareRuleEditorForSnapshot();
                else modes.PrepareEditorForSnapshot();
            }
            Expander? expander = VisualDescendants<Expander>(page).FirstOrDefault();
            if (expander is not null) expander.IsExpanded = true;
            page.ScrollToEnd();
            Pump(app.Dispatcher);
            // Grouped destinations are selected through their visible context tab.
            // Hidden historical navigation fields are not interactive controls.
            var nav = VisualDescendants<RadioButton>(page).FirstOrDefault(button =>
                button.IsVisible && button.Tag is string destination && destination == name)
                ?? (RadioButton)window.FindName("Nav" + name);
            nav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, nav));
            Pump(app.Dispatcher);
            if (page.VerticalOffset > 0.1 || VisualDescendants<Expander>(page).Any(item => item.IsExpanded))
                throw new InvalidOperationException($"Navigation: {name} did not reset its scroll/disclosures on reselect.");
            if (modes is not null)
            {
                string sibling = name == "Modes" ? "Automation" : "Modes";
                var draft = (TextBox)modes.FindName("ModeNameTextBox");
                draft.Text = "Keep this draft";
                window.Navigate(sibling); Pump(app.Dispatcher);
                window.Navigate(name); Pump(app.Dispatcher);
                if (((FrameworkElement)modes.FindName("EditorView")).Visibility != Visibility.Visible || draft.Text != "Keep this draft")
                    throw new InvalidOperationException($"Navigation: {name} lost its draft across context tabs.");
                window.Navigate("Home"); Pump(app.Dispatcher);
                window.Navigate(name); Pump(app.Dispatcher);
                if (((FrameworkElement)modes.FindName("EditorView")).Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException($"Navigation: {name} did not reset after leaving Modes.");
            }
        }
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T matching) yield return matching;
            foreach (T nested in VisualDescendants<T>(child)) yield return nested;
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
