using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkControl.Core.Audio;
using ThinkControl.Core.Ipc;
using ThinkControl.Core.Touchpad;
using ThinkControl.UI;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.Services.Touchpad;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.Snapshots;

internal static class Program
{
    private sealed record SnapshotEntry(string File, string Surface, string State, int Width, int Height);

    private static readonly string[] AdvancedPages =
    [
        "Home", "Modes", "Automation", "Performance", "Fans", "Display", "Audio",
        "Keyboard", "Battery", "Touchpad", "System", "Updates", "Settings"
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        string output = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "snapshots"));
        Directory.CreateDirectory(output);

        var app = App.CreateForVisualQa();
        app.InitializeComponent();
        var snapshots = new List<SnapshotEntry>();

        if (args.Contains("--compact-editor", StringComparer.Ordinal))
        {
            foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                ThemeService.Apply(mode);
                SyncAppState(CreateDemoState(true, true), app.State);
                var preview = new MainWindow(app) { DataContext = app.State, Topmost = false };
                var editor = preview.CreateLayoutEditorForSnapshot();
                RenderWindowContent(editor, Path.Combine(output, $"CompactEditor-{mode}.png"));
                editor.Width = 780; editor.Height = 500;
                RenderWindowContent(editor, Path.Combine(output, $"CompactEditor-Minimum-{mode}.png"));
                editor.Close(); preview.ForceClose();
            }
            return 0;
        }

        if (args.Contains("--inspect-hardware", StringComparer.Ordinal))
        {
            ThemeService.Apply(args.Contains("--light", StringComparer.Ordinal) ? ThemeMode.Light : ThemeMode.Dark);
            SyncAppState(CreateDemoState(charging: true, hardwareReady: false), app.State);
            var setup = new HardwareSetupStatus(true, false, false, false, false,
                "Visual QA service unavailable", "Not applicable", false);
            var dialog = new HardwareSetupWindow(app, new HardwareSetupService(), HardwarePrerequisiteIssue.Service)
            {
                ShowInTaskbar = true
            };
            app.MainWindow = dialog;
            dialog.PrepareForSnapshot(setup, args.Contains("--failure", StringComparer.Ordinal));
            dialog.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) dialog.Close(); };
            dialog.Closed += (_, _) => System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
            dialog.Show();
            System.Windows.Threading.Dispatcher.Run();
            return 0;
        }

        if (args.Contains("--inspect-compact", StringComparer.Ordinal))
        {
            ThemeService.Apply(ThemeMode.Dark);
            SyncAppState(CreateDemoState(true, true), app.State);
            var compact = new MainWindow(app) { DataContext = app.State, Topmost = false, ShowInTaskbar = true, Title = "ThinkControl — Compact visual QA" };
            compact.Closed += (_, _) => System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
            compact.Show();
            System.Windows.Threading.Dispatcher.Run();
            return 0;
        }

        if (args.Contains("--inspect", StringComparer.Ordinal))
        {
            ThemeService.Apply(ThemeMode.Dark);
            SyncAppState(CreateDemoState(charging: true, hardwareReady: true), app.State);
            var native = new AdvancedWindow(app) { DataContext = app.State, Title = "ThinkControl — visual QA", Width = 1200, Height = 814 };
            native.PrepareEnhancedUiForSnapshot();
            if (args.Contains("--battery", StringComparer.Ordinal)) native.Navigate("Battery");
            native.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) native.ForceClose(); };
            native.Closed += (_, _) => System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
            native.Show();
            System.Windows.Threading.Dispatcher.Run();
            return 0;
        }

        if (args.Contains("--compact", StringComparer.Ordinal))
        {
            foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                ThemeService.Apply(theme);
                RenderCompact(app, CreateDemoState(true, true), output, snapshots, $"Compact-{theme}.png", "Charging");
                RenderCompact(app, CreateDemoState(false, true), output, snapshots, $"CompactBattery-{theme}.png", "Discharging");
                RenderCompact(app, CreateDemoState(true, false), output, snapshots, $"CompactUnavailable-{theme}.png", "Unavailable");
            }
            WriteManifest(output, snapshots);
            WriteGallery(output, snapshots);
            return 0;
        }

        if (args.Contains("--overview", StringComparer.Ordinal))
        {
            foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                ThemeService.Apply(theme);
                foreach ((int width, int height) in new[] { (980, 650), (1200, 780), (1600, 900) })
                    RenderAdvanced(app, CreateDemoState(charging: true, hardwareReady: true), "Home", width, height,
                        output, snapshots, $"overview-{theme}-{width}.png", "Figma migration");
            }
            WriteManifest(output, snapshots);
            WriteGallery(output, snapshots);
            return 0;
        }

        if (args.Contains("--battery-history", StringComparer.Ordinal))
        {
            foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                ThemeService.Apply(theme);
                foreach ((int width, int height) in new[] { (980, 650), (1200, 780), (1600, 900) })
                    RenderAdvanced(app, CreateDemoState(true, true), "Battery", width, height,
                        output, snapshots, $"BatteryHistory-{theme}-{width}.png", "Details and recorded sessions", expandBatteryDay: true);
            }
            WriteManifest(output, snapshots);
            WriteGallery(output, snapshots);
            return 0;
        }

        if (args.Contains("--battery", StringComparer.Ordinal))
        {
            foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                ThemeService.Apply(theme);
                foreach ((int width, int height) in new[] { (980, 650), (1200, 780), (1600, 900) })
                {
                    RenderAdvanced(app, CreateDemoState(true, true), "Battery", width, height,
                        output, snapshots, $"Battery-{theme}-{width}.png", "Estimated wear");
                    RenderAdvanced(app, CreateDemoState(true, true), "Battery", width, height,
                        output, snapshots, $"BatteryCycles-{theme}-{width}.png", "Firmware cycle history", batteryCycles: true);
                }
            }
            WriteManifest(output, snapshots);
            WriteGallery(output, snapshots);
            return 0;
        }

        if (args.Contains("--canonical", StringComparer.Ordinal))
        {
            foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
            {
                ThemeService.Apply(theme);
                foreach ((int width, int height) in new[] { (980, 650), (1200, 780), (1600, 900) })
                    foreach (string page in new[] { "Home", "Performance", "Battery", "Display", "Keyboard", "Touchpad", "Audio", "Modes", "Automation", "System", "Updates", "Diagnostics" })
                        RenderAdvanced(app, CreateDemoState(charging: true, hardwareReady: true), page, width, height,
                            output, snapshots, $"{page}-{theme}-{width}.png", "Figma migration", modeList: page == "Modes");
            }
            WriteManifest(output, snapshots);
            WriteGallery(output, snapshots);
            return 0;
        }

        AppState charging = CreateDemoState(charging: true, hardwareReady: true);
        AppState onBattery = CreateDemoState(charging: false, hardwareReady: true);
        AppState serviceOffline = CreateDemoState(charging: true, hardwareReady: false);
        AppState unknownReady = CreateDemoState(charging: true, hardwareReady: true);
        unknownReady.DeviceName = "Visual QA laptop";
        unknownReady.MachineType = "QA-UNKNOWN";
        AppState unknownOffline = CreateDemoState(charging: true, hardwareReady: false);
        unknownOffline.DeviceName = "Visual QA laptop";
        unknownOffline.MachineType = "QA-UNKNOWN";
        AppState batteryDeviceTemperature = CreateDemoState(charging: true, hardwareReady: true);
        batteryDeviceTemperature.BatteryTemperatureC = null;
        AppState batteryProtectionPaused = CreateDemoState(charging: false, hardwareReady: true);
        batteryProtectionPaused.BatteryPercent = 90;
        batteryProtectionPaused.BatteryCharging = false;
        batteryProtectionPaused.BatteryStatus = "Plugged in";
        batteryProtectionPaused.BatteryProtectionEnabled = true;
        batteryProtectionPaused.BatteryProtectionStartPercent = 85;
        batteryProtectionPaused.BatteryProtectionStopPercent = 90;
        batteryProtectionPaused.BatteryProtectionWritable = true;
        AppState keyboardExperimentalFallback = CreateDemoState(charging: true, hardwareReady: true);
        keyboardExperimentalFallback.CanKeyboardEffects = false;
        keyboardExperimentalFallback.ExperimentalKeyboardEffectsEnabled = true;
        keyboardExperimentalFallback.KeyboardBackend = "Lenovo Vantage fallback";
        keyboardExperimentalFallback.KeyboardMode = "Breathing";
        AppState activeFanCurve = CreateDemoState(charging: true, hardwareReady: true);
        activeFanCurve.CoolingProfile = "Balanced";
        AppState homeManualFan = CreateDemoState(charging: true, hardwareReady: true);
        homeManualFan.CoolingProfile = "Manual 55%";
        homeManualFan.FanControlKind = FanControlKinds.DiscreteEc;
        AppState homeFanAuto = CreateDemoState(charging: true, hardwareReady: true);
        homeFanAuto.CoolingProfile = "Lenovo Auto";
        AppState fullSpeedOnly = CreateDemoState(charging: true, hardwareReady: true);
        fullSpeedOnly.FanControlKind = FanControlKinds.FullSpeedOnly;
        fullSpeedOnly.CoolingProfile = "Max cooling";
        AppState batteryLongStatus = CreateDemoState(charging: false, hardwareReady: true);
        batteryLongStatus.BatteryStatus = "Plugged in: charging paused while the battery cools down";
        AppState firmwareFanRecovery = CreateDemoState(charging: true, hardwareReady: false);
        firmwareFanRecovery.MachineType = "21Q6";
        firmwareFanRecovery.CanFanControl = true;
        firmwareFanRecovery.CanFanTelemetry = false;
        firmwareFanRecovery.CanSensorTelemetry = false;
        firmwareFanRecovery.FanControlKind = FanControlKinds.FirmwarePolicy;
        firmwareFanRecovery.CoolingProfile = "Quiet";
        firmwareFanRecovery.DriverStatus = "Hardware service online. Direct fan controls unavailable.";
        firmwareFanRecovery.HardwareAccess = "Lenovo firmware cooling policy available";
        AppState externalFanOwner = CreateDemoState(charging: true, hardwareReady: false);
        externalFanOwner.MachineType = "21Q6";
        externalFanOwner.CanFanControl = true;
        externalFanOwner.CanFanTelemetry = false;
        externalFanOwner.CanSensorTelemetry = false;
        externalFanOwner.FanControlKind = FanControlKinds.FirmwarePolicy;
        externalFanOwner.CoolingProfile = "Max cooling";
        externalFanOwner.HardwareAccess =
            "A Lenovo full-speed override is already active but was not started by this ThinkControl service instance. Return it to Auto explicitly before applying Quiet/Balanced.";
        AppState readOnlyCooling = CreateDemoState(charging: true, hardwareReady: true);
        readOnlyCooling.CanFanControl = false;
        readOnlyCooling.FanAutoRecoverySupported = false;
        readOnlyCooling.FanControlKind = FanControlKinds.None;
        readOnlyCooling.CoolingProfile = "Reported full-speed (read-only)";
        readOnlyCooling.HardwareAccess = "Lenovo reports a readable state, but firmware exposes no SetFeatureValue method. Physical full-speed behavior is unverified.";
        AppState autoRecoveryOnly = CreateDemoState(charging: true, hardwareReady: true);
        autoRecoveryOnly.CanFanControl = false;
        autoRecoveryOnly.FanAutoRecoverySupported = true;
        autoRecoveryOnly.FanControlKind = FanControlKinds.None;
        autoRecoveryOnly.CoolingProfile = "Lenovo Auto";
        autoRecoveryOnly.HardwareAccess = "Telemetry ready · direct fan profiles remain unavailable · verified Auto recovery is available.";
        AppState pawnIoRepair = CreateDemoState(charging: true, hardwareReady: false);
        pawnIoRepair.DriverStatus = "Hardware service online. Some controls need attention.";
        pawnIoRepair.HardwareAccess =
            "Limited · verified X9 · PawnIO is registered but its device is not available. Repair PawnIO in Hardware setup, then retry providers.";
        pawnIoRepair.KeyboardStatus = "High";
        pawnIoRepair.CanKeyboardBacklight = true;

        var readySetup = new HardwareSetupStatus(
            ServiceInstalled: true,
            ServiceRunning: true,
            LowLevelAccessRelevant: true,
            LowLevelAccessInstalled: true,
            LowLevelAccessRunning: true,
            ServiceDetail: "Running and connected to ThinkControl",
            LowLevelAccessDetail: "Installed and verified. Driver active.",
            ServiceReachable: true);

        var pawnIoRepairSetup = new HardwareSetupStatus(
            ServiceInstalled: true,
            ServiceRunning: true,
            LowLevelAccessRelevant: true,
            LowLevelAccessInstalled: true,
            LowLevelAccessRunning: true,
            ServiceDetail: "Running and connected to ThinkControl",
            LowLevelAccessDetail: "Installed, but the connection needs repair.",
            ServiceReachable: true);

        var serviceRepairSetup = new HardwareSetupStatus(
            ServiceInstalled: true,
            ServiceRunning: false,
            LowLevelAccessRelevant: true,
            LowLevelAccessInstalled: true,
            LowLevelAccessRunning: false,
            ServiceDetail: "Installed. Service stopped.",
            LowLevelAccessDetail: "Installed",
            ServiceReachable: false);

        ThemeService.Apply(ThemeMode.Dark);
        RenderBootstrap(output, snapshots);
        RenderUpdateAttention(app, output, snapshots);
        RenderCompact(app, charging, output, snapshots, "compact-dark.png", "charging");
        RenderCompact(app, charging, output, snapshots, "compact-media-lock.png", "Audio safety · Gesture lock", audioSafetyMode: AudioSafetyMode.MediaLock);
        RenderCompact(app, charging, output, snapshots, "compact-silent.png", "Audio safety · Silent", audioSafetyMode: AudioSafetyMode.Silent);
        RenderCompact(app, charging, output, snapshots, "compact-metrics-editor.png", "charging · metric editor", editMetrics: true);
        RenderCompact(app, onBattery, output, snapshots, "compact-on-battery.png", "on battery");

        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 1160, 760, output, snapshots, $"advanced-{page.ToLowerInvariant()}.png", "normal");
        RenderAdvanced(app, charging, "System", 980, 650, output, snapshots,
            "advanced-system-details-min.png", "live hardware details below shortcuts", systemDetails: true);
        RenderAdvanced(app, charging, "Home", 1160, 760, output, snapshots,
            "advanced-home-audio-media-lock.png", "Audio safety · Gesture lock", audioSafetyMode: AudioSafetyMode.MediaLock);
        RenderAdvanced(app, homeFanAuto, "Home", 1160, 760, output, snapshots,
            "advanced-home-fan-auto.png", "firmware Auto · presets disabled");
        RenderAdvanced(app, homeManualFan, "Home", 1160, 760, output, snapshots,
            "advanced-home-fan-manual.png", "manual fan output · Home state clarity");
        RenderAdvanced(app, charging, "Home", 980, 650, output, snapshots,
            "advanced-home-audio-silent-min.png", "Audio safety · Silent · minimum window", audioSafetyMode: AudioSafetyMode.Silent);
        RenderAdvanced(app, charging, "Settings", 1160, 760, output, snapshots,
            "advanced-settings-opening-advanced.png", "app icon opens · Advanced", openingView: "Advanced");
        RenderAdvanced(app, batteryDeviceTemperature, "Battery", 1160, 760, output, snapshots,
            "advanced-battery-device-temperature.png", "battery temperature unavailable · device fallback");
        RenderAdvanced(app, batteryProtectionPaused, "Battery", 1160, 760, output, snapshots,
            "advanced-battery-preservation-paused.png", "90% limit · resume below 85% · charging paused");
        RenderAdvanced(app, charging, "Battery", 1160, 760, output, snapshots,
            "advanced-battery-custom-limits.png", "custom 50–80% · expanded editor", batteryCustom: true);
        RenderAdvanced(app, keyboardExperimentalFallback, "Keyboard", 1160, 760, output, snapshots,
            "advanced-keyboard-experimental-fallback.png", "Experimental fallback · session enabled");
        RenderAdvanced(app, charging, "Battery", 1160, 900, output, snapshots,
            "advanced-battery-day-expanded.png", "expanded daily session detail", expandBatteryDay: true);
        RenderAdvanced(app, charging, "Modes", 1160, 760, output, snapshots,
            "advanced-modes-context.png", "saved context modes", modeList: true);
        RenderAdvanced(app, charging, "Modes", 1160, 980, output, snapshots,
            "advanced-modes-editor.png", "context mode editor", modeEditor: true);
        RenderAdvanced(app, charging, "Automation", 980, 900, output, snapshots,
            "advanced-rule-editor.png", "separate automation rule", ruleEditor: true);
        RenderAdvanced(app, serviceOffline, "Modes", 980, 650, output, snapshots,
            "advanced-modes-apply-failed-min.png", "cooling unavailable · failed mode preflight", modeFailure: true);

        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 980, 650, output, snapshots, $"advanced-{page.ToLowerInvariant()}-min.png", "minimum window");
        RenderAdvanced(app, unknownReady, "Home", 980, 650, output, snapshots,
            "advanced-home-device-learning-min.png", "new-device learning · minimum window", deviceLearning: true);

        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 1720, 980, output, snapshots, $"advanced-{page.ToLowerInvariant()}-wide.png", "wide window");

        RenderAdvanced(app, charging, "Touchpad", 1160, 760, output, snapshots,
            "advanced-touchpad-top-left-selected.png", "top-left corner · selected", touchpadCorner: TouchpadCorner.TopLeft);
        RenderAdvanced(app, charging, "Touchpad", 1160, 760, output, snapshots,
            "advanced-touchpad-top-right-selected.png", "top-right corner · selected", touchpadCorner: TouchpadCorner.TopRight);
        RenderAdvanced(app, charging, "Touchpad", 1160, 760, output, snapshots,
            "advanced-touchpad-top-left-live.png", "top-left corner · live", touchpadCorner: TouchpadCorner.TopLeft, touchpadCornerLive: true);
        RenderAdvanced(app, charging, "Touchpad", 1160, 760, output, snapshots,
            "advanced-touchpad-top-right-live.png", "top-right corner · live", touchpadCorner: TouchpadCorner.TopRight, touchpadCornerLive: true);

        RenderAdvanced(app, serviceOffline, "System", 1160, 760, output, snapshots, "advanced-system-service-offline.png", "hardware service offline");
        RenderAdvanced(app, serviceOffline, "Keyboard", 1160, 760, output, snapshots, "advanced-keyboard-unavailable.png", "hardware service offline");
        RenderAdvanced(app, serviceOffline, "Fans", 1160, 760, output, snapshots, "advanced-fans-unavailable.png", "hardware service offline");
        RenderAdvanced(app, fullSpeedOnly, "Fans", 980, 650, output, snapshots, "advanced-fans-full-speed-min.png", "verified Auto/Max controller");
        RenderAdvanced(app, fullSpeedOnly, "Home", 1160, 760, output, snapshots, "advanced-home-full-speed.png", "verified Auto/Max controller");
        RenderAdvanced(app, batteryLongStatus, "Battery", 980, 650, output, snapshots,
            "advanced-battery-long-status-min.png", "long charge status · minimum");
        RenderAdvanced(app, firmwareFanRecovery, "Fans", 1160, 760, output, snapshots,
            "advanced-fans-firmware-policy-recovery.png", "direct provider unavailable · firmware profiles available");
        RenderAdvanced(app, externalFanOwner, "Fans", 1160, 760, output, snapshots,
            "advanced-fans-external-owner.png", "external full-speed owner · explicit Lenovo Auto recovery");
        RenderAdvanced(app, readOnlyCooling, "Fans", 980, 650, output, snapshots,
            "advanced-fans-read-only-min.png", "read-only firmware · profiles/Auto unavailable");
        RenderAdvanced(app, autoRecoveryOnly, "Fans", 980, 650, output, snapshots,
            "advanced-fans-auto-recovery-min.png", "shared tachometer · independent Auto recovery · profiles unavailable");
        RenderAdvanced(app, autoRecoveryOnly, "Fans", 980, 650, output, snapshots,
            "advanced-fans-auto-recovery-result-min.png", "last Auto recovery confirmed · profiles unavailable", fanRecoveryResult: true);
        RenderAdvanced(app, activeFanCurve, "Fans", 1160, 760, output, snapshots, "advanced-fans-active-curve.png", "Balanced curve · live marker", fanActiveCurve: true);
        foreach (var size in new[] { (980, 650, "min"), (1160, 760, "normal"), (1720, 980, "wide") })
            RenderAdvanced(app, activeFanCurve, "Fans", size.Item1, size.Item2, output, snapshots,
                $"advanced-fans-measured-{size.Item3}.png", "custom curve, shared reading, measured steps", fanMeasuredCurve: true);
        RenderAdvanced(app, activeFanCurve, "Fans", 1160, 760, output, snapshots,
            "advanced-fans-manual-test.png", "temporary 72% target · auto restore", fanManualTest: true);
        RenderAdvanced(app, charging, "Audio", 1160, 760, output, snapshots, "advanced-audio-unavailable.png", "audio/DAX providers unavailable", audioProvidersAvailable: false);

        RenderNotificationSheet(app, pawnIoRepair, 1160, 760, output, snapshots,
            "notifications-hardware-attention.png", "PawnIO + provider attention");
        RenderNotificationSheet(app, charging, 1160, 760, output, snapshots,
            "notifications-update-dismissed.png", "update available · startup prompt dismissed", updateAvailable: true, updateDismissed: true);
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 560, 360, output, snapshots,
            "hardware-setup-pawnio-repair.png", "PawnIO device repair");
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 500, 330, output, snapshots,
            "hardware-setup-pawnio-repair-min.png", "PawnIO device repair · minimum window");
        RenderHardwareSetup(app, charging, readySetup, 560, 360, output, snapshots,
            "hardware-setup-ready.png", "all providers ready");
        RenderHardwareSetup(app, charging, readySetup, 500, 330, output, snapshots,
            "hardware-setup-ready-min.png", "all providers ready · minimum window");
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 560, 360, output, snapshots,
            "hardware-setup-pawnio-error.png", "PawnIO setup unsuccessful", terminalFailure: true);
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 500, 330, output, snapshots,
            "hardware-setup-pawnio-error-min.png", "PawnIO setup unsuccessful · minimum window", terminalFailure: true);
        RenderHardwareSetup(app, serviceOffline, serviceRepairSetup, 560, 360, output, snapshots,
            "required-component-service.png", "service repair", issue: HardwarePrerequisiteIssue.Service);
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 560, 360, output, snapshots,
            "required-component-sensors.png", "sensor retry", issue: HardwarePrerequisiteIssue.Sensors);
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 560, 360, output, snapshots,
            "required-component-fans.png", "fan provider retry", issue: HardwarePrerequisiteIssue.FanControl);
        RenderHardwareSetup(app, serviceOffline, readySetup, 560, 360, output, snapshots,
            "required-component-keyboard.png", "keyboard provider retry", issue: HardwarePrerequisiteIssue.Keyboard);
        RenderDiagnostics(app, charging, 1160, 760, output, snapshots,
            "diagnostics-verified.png", "verified X9 · routine troubleshooting", verifiedDevice: true);
        RenderDiagnostics(app, charging, 1160, 760, output, snapshots,
            "diagnostics-crash-queue.png", "verified X9 · three unresolved crashes", verifiedDevice: true, crashQueue: true);
        RenderDiagnostics(app, unknownReady, 1160, 760, output, snapshots,
            "diagnostics-ready.png", "capabilities detected · report ready");
        RenderDiagnostics(app, unknownOffline, 1160, 760, output, snapshots,
            "diagnostics-discovering.png", "provider data not ready");
        RenderFanCurveEditor(app, charging, output, snapshots);
        RenderTelemetryDetail(output, snapshots);
        RenderSensorDetails(app, charging, 900, 700, output, snapshots, "sensor-details.png", "normal");
        RenderSensorDetails(app, charging, 700, 560, output, snapshots, "sensor-details-min.png", "minimum window");
        RenderGestureOsd(app, output, snapshots, "gesture-osd-brightness.png", "Brightness", 100);
        RenderGestureOsd(app, output, snapshots, "gesture-osd-brightness-min.png", "Brightness", 0);
        RenderGestureOsd(app, output, snapshots, "gesture-osd-next-track.png", "Next track", 0, nextTrack: true);

        ThemeService.Apply(ThemeMode.Light);
        RenderCompact(app, charging, output, snapshots, "compact-light.png", "charging · light");
        RenderCompact(app, charging, output, snapshots, "compact-silent-light.png", "Mode · Silent · light", audioSafetyMode: AudioSafetyMode.Silent);

        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 1160, 760, output, snapshots, $"advanced-{page.ToLowerInvariant()}-light.png", "normal · light");
        RenderAdvanced(app, charging, "System", 980, 650, output, snapshots,
            "advanced-system-details-min-light.png", "live hardware details below shortcuts, light", systemDetails: true);
        foreach (var size in new[] { (980, 650, "min"), (1160, 760, "normal"), (1720, 980, "wide") })
            RenderAdvanced(app, activeFanCurve, "Fans", size.Item1, size.Item2, output, snapshots,
                $"advanced-fans-measured-{size.Item3}-light.png", "custom curve, shared reading, measured steps, light", fanMeasuredCurve: true);
        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 980, 650, output, snapshots, $"advanced-{page.ToLowerInvariant()}-min-light.png", "minimum window · light");
        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 1720, 980, output, snapshots, $"advanced-{page.ToLowerInvariant()}-wide-light.png", "wide window · light");
        RenderAdvanced(app, homeFanAuto, "Home", 1160, 760, output, snapshots,
            "advanced-home-fan-auto-light.png", "firmware Auto · presets disabled · light");
        RenderAdvanced(app, charging, "Home", 1160, 760, output, snapshots,
            "advanced-home-audio-silent-light.png", "Audio safety · Silent · light", audioSafetyMode: AudioSafetyMode.Silent);
        RenderAdvanced(app, batteryProtectionPaused, "Battery", 1160, 760, output, snapshots,
            "advanced-battery-preservation-paused-light.png", "90% limit · resume below 85% · charging paused · light");
        RenderAdvanced(app, charging, "Battery", 1160, 760, output, snapshots,
            "advanced-battery-custom-limits-light.png", "custom 50–80% · light", batteryCustom: true);
        RenderAdvanced(app, unknownReady, "Home", 1160, 760, output, snapshots,
            "advanced-home-device-report-ready-light.png", "device report ready · light", deviceLearning: true, deviceReportReady: true);
        RenderAdvanced(app, charging, "Modes", 1160, 760, output, snapshots,
            "advanced-modes-context-light.png", "saved context modes · light", modeList: true);
        RenderAdvanced(app, charging, "Modes", 1160, 980, output, snapshots,
            "advanced-modes-editor-light.png", "context mode editor · light", modeEditor: true);
        RenderAdvanced(app, charging, "Automation", 980, 900, output, snapshots,
            "advanced-rule-editor-light.png", "separate automation rule · light", ruleEditor: true);
        RenderAdvanced(app, serviceOffline, "Modes", 980, 650, output, snapshots,
            "advanced-modes-apply-failed-min-light.png", "cooling unavailable · failed mode preflight · light", modeFailure: true);
        RenderAdvanced(app, batteryLongStatus, "Battery", 980, 650, output, snapshots,
            "advanced-battery-long-status-min-light.png", "long charge status · minimum · light");
        RenderAdvanced(app, firmwareFanRecovery, "Fans", 1160, 760, output, snapshots,
            "advanced-fans-firmware-policy-recovery-light.png", "direct provider unavailable · firmware profiles available · light");
        RenderAdvanced(app, fullSpeedOnly, "Fans", 980, 650, output, snapshots, "advanced-fans-full-speed-min-light.png", "verified Auto/Max controller · light");
        RenderAdvanced(app, fullSpeedOnly, "Home", 1160, 760, output, snapshots, "advanced-home-full-speed-light.png", "verified Auto/Max controller · light");
        RenderAdvanced(app, externalFanOwner, "Fans", 1160, 760, output, snapshots,
            "advanced-fans-external-owner-light.png", "external full-speed owner · explicit Lenovo Auto recovery · light");
        RenderAdvanced(app, readOnlyCooling, "Fans", 980, 650, output, snapshots,
            "advanced-fans-read-only-min-light.png", "read-only firmware · profiles/Auto unavailable · light");
        RenderAdvanced(app, autoRecoveryOnly, "Fans", 980, 650, output, snapshots,
            "advanced-fans-auto-recovery-min-light.png", "shared tachometer · independent Auto recovery · light");
        RenderAdvanced(app, autoRecoveryOnly, "Fans", 980, 650, output, snapshots,
            "advanced-fans-auto-recovery-result-min-light.png", "last Auto recovery confirmed · light", fanRecoveryResult: true);
        RenderAdvanced(app, activeFanCurve, "Fans", 1160, 760, output, snapshots,
            "advanced-fans-active-curve-light.png", "Balanced curve · live marker · light", fanActiveCurve: true);
        RenderSensorDetails(app, charging, 900, 700, output, snapshots, "sensor-details-light.png", "normal · light");
        RenderNotificationSheet(app, pawnIoRepair, 1160, 760, output, snapshots,
            "notifications-hardware-attention-light.png", "PawnIO + provider attention · light");
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 560, 360, output, snapshots,
            "hardware-setup-pawnio-repair-light.png", "PawnIO device repair · light");
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 500, 330, output, snapshots,
            "hardware-setup-pawnio-repair-min-light.png", "PawnIO device repair · minimum window · light");
        RenderHardwareSetup(app, charging, readySetup, 560, 360, output, snapshots,
            "hardware-setup-ready-light.png", "all providers ready · light");
        RenderHardwareSetup(app, charging, readySetup, 500, 330, output, snapshots,
            "hardware-setup-ready-min-light.png", "all providers ready · minimum window · light");
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 560, 360, output, snapshots,
            "hardware-setup-pawnio-error-light.png", "PawnIO setup unsuccessful · light", terminalFailure: true);
        RenderHardwareSetup(app, pawnIoRepair, pawnIoRepairSetup, 500, 330, output, snapshots,
            "hardware-setup-pawnio-error-min-light.png", "PawnIO setup unsuccessful · minimum window · light", terminalFailure: true);
        RenderDiagnostics(app, unknownReady, 1160, 760, output, snapshots,
            "diagnostics-ready-light.png", "capabilities detected · report ready · light");
        RenderDiagnostics(app, unknownOffline, 1160, 760, output, snapshots,
            "diagnostics-discovering-light.png", "provider data not ready · light");

        WriteManifest(output, snapshots);
        WriteGallery(output, snapshots);
        WriteMarkdownGallery(output, snapshots);

        Console.WriteLine($"Rendered {snapshots.Count} ThinkControl visual-QA snapshots to {output}");
        Console.WriteLine($"Open {Path.Combine(output, "gallery.html")} to review them as one gallery.");
        return 0;
    }

    private static AppState CreateDemoState(bool charging, bool hardwareReady)
    {
        var state = new AppState
        {
            DeviceName = "ThinkPad X9-15 Gen 1",
            CpuTemperatureC = hardwareReady ? 44 : null,
            ControlTemperatureC = hardwareReady ? 47.2 : null,
            ControlTemperatureSource = hardwareReady ? "CPU Package · hottest canonical domain" : "Unavailable",
            FanRpm = hardwareReady ? 2050 : null,
            FanStateText = hardwareReady ? "Normal (level 3)" : "Firmware managed: fan readings unavailable",
            BatteryPercent = charging ? 78 : 63,
            BatteryCharging = charging,
            BatteryStatus = charging ? "Charging" : "On battery",
            BatteryPowerWatts = charging ? 18.4 : 7.2,
            BatterySmoothedPowerWatts = charging ? 17.8 : 6.9,
            BatteryHealthPercent = 97.6,
            BatteryTemperatureC = hardwareReady ? 34.8 : null,
            BatteryRemainingWh = charging ? 56.2 : 45.4,
            BatteryFullWh = 72.0,
            BatteryEtaToChargeTarget = charging ? TimeSpan.FromMinutes(24) : null,
            BatteryEtaRemaining = charging ? null : TimeSpan.FromHours(6.4),
            BatteryCycleCount = 12,
            BatteryChargeCurveLabel = "Current charge session",
            BatteryCurrentSessionText = "61% to 78% in 43 min, average power: 17.8 W, energy added: 12.1 Wh",
            BatteryTypicalChargeText = "18.1 W (8 sessions)",
            BatteryHealthTrendText = "Health: 97.6%, stable, 8 daily readings",
            BatterySource = "Windows ACPI battery",
            Brightness = 68,
            BrightnessAvailable = true,
            AdaptiveBrightnessAvailable = true,
            AdaptiveBrightnessEnabled = true,
            CurrentRefreshHz = 120,
            MaxRefreshHz = 120,
            RefreshAutoEnabled = true,
            HardwareAccess = hardwareReady
                ? "Full · verified X9 EC + PawnIO sensors + Lenovo keyboard provider"
                : "Limited support: hardware service offline",
            CpuName = "Intel Core Ultra 7 258V",
            GpuName = "Intel Arc 140V",
            RamText = "32 GB",
            BiosVersion = "N4CET44W (1.20)",
            MachineType = "21Q6",
            ThermalSolution = hardwareReady ? "Lenovo Intelligent Thermal Solution" : "—",
            DriverStatus = hardwareReady
                ? "Ready"
                : "ThinkControl hardware service stopped · repair available",
            KeyboardStatus = hardwareReady ? "High" : "Hardware backend unavailable",
            KeyboardBackend = hardwareReady ? "Lenovo PM Driver · ThinkPad" : "Not exposed",
            KeyboardMode = hardwareReady ? "Breathing" : "Auto",
            KeyboardBaseLevel = "Low",
            KeyboardEffectSpeed = 1.0,
            SelectedPowerMode = "Balanced",
            UpdateStatus = $"Up to date: v{UpdateService.CurrentVersion}",
            CanFanControl = hardwareReady,
            CanFanTelemetry = hardwareReady,
            CanKeyboardBacklight = hardwareReady,
            CanKeyboardEffects = hardwareReady,
            BatteryProtectionEnabled = hardwareReady ? true : null,
            BatteryProtectionStartPercent = hardwareReady ? 75 : null,
            BatteryProtectionStopPercent = hardwareReady ? 85 : null,
            BatteryProtectionWritable = hardwareReady,
            CanCpuTemperature = hardwareReady,
            CanSensorTelemetry = hardwareReady
        };

        if (hardwareReady)
        {
            state.ApplyHardwareTelemetry(
            [
                new FanTelemetrySnapshot("x9-ec-shared", "Shared tachometer", 2050, "ThinkPad X9 EC shared tachometer 0x84/0x85 · 0x66/0x62", true, Shared: true)
            ],
            [
                new HardwareSensorSnapshot("cpu-package", "Intel Core Ultra 7 258V", "Cpu", "CPU Package", "Temperature", 47.2, "°C", true, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("cpu-power", "Intel Core Ultra 7 258V", "Cpu", "CPU Package", "Power", 12.8, "W", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("gpu-temp", "Intel Arc 140V", "GpuIntel", "GPU Core", "Temperature", 43.5, "°C", true, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("gpu-load", "Intel Arc 140V", "GpuIntel", "GPU Core", "Load", 18.0, "%", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("ssd-temp", "NVMe SSD", "Storage", "Temperature", "Temperature", 39.0, "°C", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("memory-temp", "LPDDR5X memory", "Memory", "Memory modules", "Temperature", 41.1, "°C", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("gpu-power", "Intel Arc 140V", "GpuIntel", "GPU Package", "Power", 4.7, "W", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("cpu-load", "Intel Core Ultra 7 258V", "Cpu", "CPU Total", "Load", 21.4, "%", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("ssd-load", "NVMe SSD", "Storage", "Total activity", "Load", 3.0, "%", false, "LibreHardwareMonitor")
            ]);
        }

        for (int i = 0; i < 60; i++)
            state.TemperatureHistory.Add(43 + Math.Sin(i / 4d) * 2 + (i % 11 == 0 ? 1 : 0));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 0; i <= 43; i++)
        {
            double watts = 18.8 - i * 0.028 + Math.Sin(i / 3.5) * 0.55;
            int percent = (int)Math.Round(61d + 17d * i / 43d);
            DateTimeOffset at = now - TimeSpan.FromMinutes(43 - i);
            state.BatteryChargePowerTimeline.Add(new TimeSeriesPoint(at, watts, $"{percent}%"));
            state.BatteryChargePercentTimeline.Add(new TimeSeriesPoint(at, percent));
        }

        for (int i = 0; i < 8; i++)
        {
            double health = 98.0 - i * 0.055 + Math.Sin(i * 0.8) * 0.06;
            state.BatteryHealthTrendTimeline.Add(new TimeSeriesPoint(now - TimeSpan.FromDays((7 - i) * 21), health));
        }

        state.RecentChargeSessions.Add("Today · 61% to 78% in 43 min, average power: 17.8 W · +12.1 Wh");
        state.BatteryCycleCountTimeline.Add(new TimeSeriesPoint(now.AddDays(-7), 10));
        state.BatteryCycleCountTimeline.Add(new TimeSeriesPoint(now.AddDays(-3), 11));
        state.BatteryCycleCountTimeline.Add(new TimeSeriesPoint(now, 12));
        state.BatteryCycleTrendText = "2 cycles added over 7 days (2 per week). Firmware readings.";
        state.RecentChargeSessions.Add("21 Aug · 34% → 91% · 2h 12m · 18.3 W avg · +40.6 Wh");
        state.RecentChargeSessions.Add("20 Aug · 52% → 86% · 1h 18m · 17.9 W avg · +24.0 Wh");
        return state;
    }

    private static void SyncAppState(AppState source, AppState target)
    {
        foreach (PropertyInfo property in typeof(AppState).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                continue;
            property.SetValue(target, property.GetValue(source));
        }

        target.ApplyHardwareTelemetry(source.Fans.ToArray(), source.Sensors.ToArray());
        ReplaceCollection(target.TemperatureHistory, source.TemperatureHistory);
        ReplaceCollection(target.BatteryChargePowerTimeline, source.BatteryChargePowerTimeline);
        ReplaceCollection(target.BatteryChargePercentTimeline, source.BatteryChargePercentTimeline);
        ReplaceCollection(target.BatteryHealthTrendTimeline, source.BatteryHealthTrendTimeline);
        ReplaceCollection(target.BatteryCycleCountTimeline, source.BatteryCycleCountTimeline);
        ReplaceCollection(target.RecentChargeSessions, source.RecentChargeSessions);
    }

    private static void ReplaceCollection<T>(ICollection<T> target, IEnumerable<T> values)
    {
        T[] copy = values.ToArray();
        target.Clear();
        foreach (T value in copy)
            target.Add(value);
    }

    private static void RenderCompact(
        App app,
        AppState state,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string stateName,
        bool editMetrics = false,
        AudioSafetyMode? audioSafetyMode = null)
    {
        const int width = 420;
        const int height = 565;
        SyncAppState(state, app.State);
        var window = new MainWindow(app) { DataContext = app.State, Width = width, Height = height };
        if (editMetrics)
        {
            var editor = window.CreateLayoutEditorForSnapshot();
            RenderWindowContent(editor, Path.Combine(output, fileName));
            snapshots.Add(new SnapshotEntry(fileName, "Compact layout", stateName, 920, 590));
            editor.Close(); window.ForceClose(); return;
        }
        if (audioSafetyMode is AudioSafetyMode mode)
            window.PrepareAudioSafetyForSnapshot(mode);
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "Compact", stateName, width, height));
        window.ForceClose();
    }

    private static void RenderAdvanced(
        App app,
        AppState state,
        string page,
        int width,
        int height,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string stateName,
        bool audioProvidersAvailable = true,
        bool expandBatteryDay = false,
        TouchpadCorner? touchpadCorner = null,
        bool touchpadCornerLive = false,
        bool fanActiveCurve = false,
        bool fanManualTest = false,
        bool deviceLearning = false,
        bool deviceReportReady = false,
        string? openingView = null,
        AudioSafetyMode? audioSafetyMode = null,
        bool modeEditor = false,
        bool modeList = false,
        bool batteryCustom = false,
        bool modeFailure = false,
        bool fanRecoveryResult = false,
        bool ruleEditor = false,
        bool fanMeasuredCurve = false,
        bool systemDetails = false,
        bool batteryCycles = false)
    {
        SyncAppState(state, app.State);
        var window = new AdvancedWindow(app) { DataContext = app.State, Width = width, Height = height };
        window.PrepareEnhancedUiForSnapshot();
        if (fanRecoveryResult && window.FindName("PageFans") is System.Windows.Controls.ScrollViewer { Content: FansPanel recoveryPanel })
            typeof(FansPanel).GetMethod("ConfirmAutoRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(recoveryPanel, []);
        if (deviceLearning)
            window.PrepareDeviceLearningForSnapshot(deviceReportReady);
        if (string.Equals(page, "Battery", StringComparison.OrdinalIgnoreCase) && state.BatteryTemperatureC is null)
            app.State.BatteryTemperatureC = null;
        if (string.Equals(page, "Touchpad", StringComparison.OrdinalIgnoreCase))
        {
            window.NavigateTouchpad();
            if (touchpadCorner is TouchpadCorner corner)
                window.PrepareTouchpadCornerForSnapshot(corner, touchpadCornerLive);
        }
        else if (string.Equals(page, "Audio", StringComparison.OrdinalIgnoreCase))
        {
            window.NavigateAudio();
            window.PrepareAudioForSnapshot(audioProvidersAvailable);
        }
        else
            window.Navigate(page);

        // Drain real navigation callbacks before applying presentation fixtures.
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Page navigation refreshes a few Settings/Home selectors from the real app
        // state. Apply deterministic visual-only overrides after navigation so the
        // screenshot name and the actually rendered selection cannot disagree.
        if (openingView is not null)
            window.PrepareOpeningViewForSnapshot(openingView);
        if (audioSafetyMode is AudioSafetyMode audioSafety)
            window.PrepareAudioSafetyForSnapshot(
                audioSafety,
                audioPage: string.Equals(page, "Audio", StringComparison.OrdinalIgnoreCase));
        if (modeList && string.Equals(page, "Modes", StringComparison.OrdinalIgnoreCase))
            window.PrepareModesListForSnapshot();
        if (string.Equals(page, "Automation", StringComparison.OrdinalIgnoreCase) && !ruleEditor)
            window.PrepareAutomationListForSnapshot();
        if (modeEditor && string.Equals(page, "Modes", StringComparison.OrdinalIgnoreCase))
            window.PrepareModesEditorForSnapshot();
        if (ruleEditor) window.PrepareRuleEditorForSnapshot();
        if (systemDetails) window.PrepareSystemDetailsForSnapshot();
        if (modeFailure)
        {
            // Inject only the coordinator error state; no provider or saved mode is
            // changed. Production rendering handles both manual/automatic failure.
            typeof(ThinkControlModeCoordinator).GetProperty("LastTransitionError", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(app.Modes, "Cooling: no writable cooling controller is available. Open Fans to inspect the current firmware state, or remove Cooling from this mode.");
            typeof(ThinkControlModeCoordinator).GetProperty("IsModified", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(app.Modes, true);
            window.PrepareModesListForSnapshot();
        }

        if (string.Equals(page, "Performance", StringComparison.OrdinalIgnoreCase))
            window.PreparePerformanceForSnapshot();

        if (fanActiveCurve &&
            string.Equals(page, "Fans", StringComparison.OrdinalIgnoreCase) &&
            window.FindName("PageFans") is System.Windows.Controls.ScrollViewer
            {
                Content: FansPanel activeCurvePanel
            })
        {
            activeCurvePanel.PrepareActiveFanCurveForSnapshot();
        }

        if (fanMeasuredCurve && window.FindName("PageFans") is System.Windows.Controls.ScrollViewer { Content: FansPanel measuredPanel })
            measuredPanel.PrepareMeasuredCurveForSnapshot();

        if (fanManualTest &&
            string.Equals(page, "Fans", StringComparison.OrdinalIgnoreCase) &&
            window.FindName("PageFans") is System.Windows.Controls.ScrollViewer
            {
                Content: FansPanel fansPanel
            } fanScroll)
        {
            fansPanel.PrepareManualFanTestForSnapshot();
            if (window.Content is FrameworkElement fanRoot)
            {
                fanRoot.Measure(new Size(width, height));
                fanRoot.Arrange(new Rect(0, 0, width, height));
                fanRoot.UpdateLayout();
            }
            fanScroll.ScrollToEnd();
            fanScroll.UpdateLayout();
        }

        if (batteryCustom && string.Equals(page, "Battery", StringComparison.OrdinalIgnoreCase))
        {
            window.PrepareBatteryCustomLimitsForSnapshot();
            if (window.Content is FrameworkElement batteryRoot)
            {
                batteryRoot.Measure(new Size(width, height));
                batteryRoot.Arrange(new Rect(0, 0, width, height));
                batteryRoot.UpdateLayout();
            }
            if (window.FindName("PageBattery") is System.Windows.Controls.ScrollViewer batteryScroll)
            {
                batteryScroll.ScrollToVerticalOffset(330);
                batteryScroll.UpdateLayout();
            }
        }

        if (expandBatteryDay)
        {
            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();
            }
            window.ExpandBatteryHistoryForSnapshot();
        }

        if (string.Equals(page, "Updates", StringComparison.OrdinalIgnoreCase))
            window.PrepareUpdateUiForSnapshot(DateTimeOffset.Now.AddMinutes(-4));

        if (string.Equals(page, "Touchpad", StringComparison.OrdinalIgnoreCase) && window.Content is FrameworkElement touchpadRoot)
        {
            touchpadRoot.Measure(new Size(width, height));
            touchpadRoot.Arrange(new Rect(0, 0, width, height));
            touchpadRoot.UpdateLayout();
            window.ValidateTouchpadCornerSymmetryForSnapshot();
        }

        if (batteryCycles)
        {
            window.PrepareBatteryCyclesForSnapshot();
            if (window.FindName("PageBattery") is System.Windows.Controls.ScrollViewer scroll)
            {
                window.UpdateLayout();
                scroll.ScrollToVerticalOffset(330);
                scroll.UpdateLayout();
            }
        }
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, $"Advanced · {page}", stateName, width, height));
        if (modeFailure)
        {
            typeof(ThinkControlModeCoordinator).GetProperty("LastTransitionError", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app.Modes, null);
            typeof(ThinkControlModeCoordinator).GetProperty("IsModified", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app.Modes, false);
        }
        window.ForceClose();
    }

    private static void RenderUpdateAttention(
        App app,
        string output,
        ICollection<SnapshotEntry> snapshots)
    {
        Window toast = app.PrepareUpdateAttentionForSnapshot();
        if (toast.Content is not FrameworkElement root)
            throw new InvalidOperationException("Update attention window has no renderable content.");

        const double width = 390;
        root.Measure(new Size(width, double.PositiveInfinity));
        double height = Math.Clamp(Math.Ceiling(root.DesiredSize.Height), toast.MinHeight, toast.MaxHeight);
        toast.SizeToContent = SizeToContent.Manual;
        toast.Width = width;
        toast.Height = height;

        const string fileName = "update-attention-first-seen.png";
        RenderWindowContent(toast, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(
            fileName,
            "Update attention",
            "first seen · Install now / Later",
            (int)width,
            (int)height));
        toast.Hide();
    }

    private static void RenderNotificationSheet(
        App app,
        AppState state,
        int width,
        int height,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string stateName,
        bool updateAvailable = false,
        bool updateDismissed = false)
    {
        SyncAppState(state, app.State);
        var window = new AdvancedWindow(app) { DataContext = app.State, Width = width, Height = height };
        window.PrepareEnhancedUiForSnapshot();
        window.Navigate("Home");
        if (updateAvailable)
            window.PrepareUpdateNotificationSheetForSnapshot(updateDismissed);
        else
            window.PrepareNotificationSheetForSnapshot();
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "Notifications sheet", stateName, width, height));
        window.ForceClose();
    }

    private static void RenderHardwareSetup(
        App app,
        AppState state,
        HardwareSetupStatus setup,
        int width,
        int height,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string stateName,
        bool terminalFailure = false,
        HardwarePrerequisiteIssue issue = HardwarePrerequisiteIssue.Auto)
    {
        SyncAppState(state, app.State);
        var window = new HardwareSetupWindow(app, new HardwareSetupService(), issue)
        {
            Width = width,
            Height = height,
            DataContext = app.State
        };
        window.PrepareForSnapshot(setup, terminalFailure);
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "Required component", stateName, width, height));
        window.Close();
    }

    private static void RenderTelemetryDetail(string output, ICollection<SnapshotEntry> snapshots)
    {
        const int width = 720;
        const int height = 720;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var powerTimeline = Enumerable.Range(0, 46)
            .Select(index => new TimeSeriesPoint(
                now - TimeSpan.FromMinutes(45 - index),
                20.1 - index * 0.055 + Math.Sin(index / 3.2) * 0.5))
            .ToArray();
        var percentTimeline = Enumerable.Range(0, 46)
            .Select(index => new TimeSeriesPoint(
                now - TimeSpan.FromMinutes(45 - index),
                61d + 17d * index / 45d))
            .ToArray();
        var model = new TelemetryDetailModel(
            "Charge session",
            "Today · 61% → 78% · 43 min",
            "Charge power",
            powerTimeline,
            "W",
            "0.0",
            [
                new TelemetryDetailMetric("Duration", "43 min", "61% → 78%"),
                new TelemetryDetailMetric("Energy added", "+12.1 Wh"),
                new TelemetryDetailMetric("Average power", "17.8 W"),
                new TelemetryDetailMetric("Peak power", "20.4 W")
            ],
            SecondaryTimeline: percentTimeline,
            SecondaryChartTitle: "Battery level",
            SecondaryUnit: "%",
            SecondaryValueFormat: "0");
        var window = new TelemetryDetailWindow(model) { Width = width, Height = height };
        RenderWindowContent(window, Path.Combine(output, "telemetry-detail-battery.png"));
        snapshots.Add(new SnapshotEntry("telemetry-detail-battery.png", "Telemetry detail", "battery charge session · % + W", width, height));
        window.Close();
    }

    private static void RenderBootstrap(string output, ICollection<SnapshotEntry> snapshots)
    {
        var window = new BootstrapWindow();
        RenderWindowContent(window, Path.Combine(output, "startup-loading.png"));
        snapshots.Add(new SnapshotEntry("startup-loading.png", "Startup", "manual launch · preparing controls", 450, 250));
        window.Close();
    }

    private static void RenderDiagnostics(
        App app,
        AppState state,
        int width,
        int height,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string stateName,
        bool verifiedDevice = false,
        bool crashQueue = false)
    {
        SyncAppState(state, app.State);
        var window = new AdvancedWindow(app) { DataContext = app.State, Width = width, Height = height };
        window.PrepareEnhancedUiForSnapshot();
        window.Navigate("Diagnostics");
        window.PrepareDiagnosticsForSnapshot(ThinkControl.Core.Diagnostics.DiagnosticsConsent.Enabled, verifiedDevice);
        if (crashQueue)
            window.PrepareCrashQueueForSnapshot();
        if (window.Content is FrameworkElement root)
        {
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
        }
        window.ScrollDiagnosticsIntoViewForSnapshot();
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "Settings · Diagnostics", stateName, width, height));
        window.ForceClose();
    }

    private static void RenderSensorDetails(
        App app,
        AppState state,
        int width,
        int height,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string stateName)
    {
        SyncAppState(state, app.State);
        var window = new SensorDetailsWindow(app) { Width = width, Height = height };
        window.PrepareForSnapshot(app.State);
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "System · Sensor details", stateName, width, height));
        window.Close();
    }

    private static void RenderGestureOsd(
        App app,
        string output,
        ICollection<SnapshotEntry> snapshots,
        string fileName,
        string label,
        int value,
        bool? nextTrack = null)
    {
        using var osd = new GestureOsdService(
            () => app.UserSettings.Current,
            (_, _) => true,
            () => true);
        Window window = osd.PrepareForSnapshot(label, value, nextTrack);
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "Gesture pop-up", label, (int)window.Width, (int)window.Height));
    }

    private static void RenderFanCurveEditor(
        App app,
        AppState state,
        string output,
        ICollection<SnapshotEntry> snapshots)
    {
        const int width = 940;
        const int height = 680;
        SyncAppState(state, app.State);
        var window = new FanCurveEditorWindow(app) { Width = width, Height = height };
        window.PrepareForSnapshot();
        RenderWindowContent(window, Path.Combine(output, "fan-curve-editor.png"));
        snapshots.Add(new SnapshotEntry("fan-curve-editor.png", "Fan curve editor", "custom profile · live marker", width, height));
        window.Close();
    }

    private static void RenderWindowContent(Window window, string path)
    {
        if (window.Content is not FrameworkElement root)
            throw new InvalidOperationException($"{window.GetType().Name} has no renderable content.");
        double width = window.Width;
        double height = window.Height;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        TypographyContract.Validate(root);
        UiLayoutContract.Validate(root);

        int pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(height));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Console.WriteLine($"Rendered {Path.GetFileName(path)} ({pixelWidth}x{pixelHeight})");
    }

    private static void WriteManifest(string output, IReadOnlyCollection<SnapshotEntry> snapshots)
    {
        string json = JsonSerializer.Serialize(new { generatedAt = DateTimeOffset.UtcNow, snapshots }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "manifest.json"), json, Encoding.UTF8);
    }

    private static void WriteGallery(string output, IReadOnlyCollection<SnapshotEntry> snapshots)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>ThinkControl Visual QA</title><style>body{font-family:Segoe UI,system-ui;background:#0f1113;color:#f2f3f4;margin:0;padding:32px}h1{margin:0 0 6px}p{color:#9fa5ac;margin:0 0 28px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:22px}.card{background:#171a1d;border:1px solid #34383d;border-radius:8px;padding:12px}.meta{display:flex;justify-content:space-between;gap:10px;margin:0 2px 10px;font-size:13px;color:#a3a8ae}.card img{display:block;width:100%;height:auto;background:#101214;border-radius:4px;box-shadow:0 12px 32px #0008}.wide{grid-column:1/-1}.wide img{max-width:1400px;margin:auto}</style></head><body>");
        html.AppendLine("<h1>ThinkControl Visual QA</h1><p>Deterministic WPF snapshots. Review alignment, clipping, hierarchy, overlays and state handling at fixed viewport sizes before merging UI changes.</p><div class=\"grid\">");
        foreach (SnapshotEntry snapshot in snapshots)
        {
            string css = snapshot.Width >= 1500 ? "card wide" : "card";
            html.Append("<article class=\"").Append(css).Append("\"><div class=\"meta\"><strong>")
                .Append(Html(snapshot.Surface)).Append("</strong><span>")
                .Append(Html(snapshot.State)).Append(" · ").Append(snapshot.Width).Append('×').Append(snapshot.Height)
                .Append("</span></div><a href=\"").Append(snapshot.File).Append("\"><img loading=\"lazy\" src=\"")
                .Append(snapshot.File).Append("\" alt=\"").Append(Html(snapshot.Surface)).Append("\"></a></article>");
        }
        html.AppendLine("</div></body></html>");
        File.WriteAllText(Path.Combine(output, "gallery.html"), html.ToString(), Encoding.UTF8);
    }

    private static void WriteMarkdownGallery(string output, IReadOnlyCollection<SnapshotEntry> snapshots)
    {
        var markdown = new StringBuilder("# ThinkControl Visual QA\n\nGenerated from the real WPF interface. Click an image for the full-size render.\n\n");
        foreach (SnapshotEntry snapshot in snapshots)
        {
            markdown.Append("## ").Append(snapshot.Surface).Append(" — ").Append(snapshot.State).Append("\n\n")
                .Append(snapshot.Width).Append('×').Append(snapshot.Height).Append("\n\n")
                .Append("[![").Append(snapshot.Surface).Append("](").Append(snapshot.File).Append(")](").Append(snapshot.File).Append(")\n\n");
        }
        File.WriteAllText(Path.Combine(output, "README.md"), markdown.ToString(), Encoding.UTF8);
    }

    private static string Html(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
