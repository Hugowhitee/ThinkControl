using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private Icon? _ownedTrayIcon;
    private DispatcherTimer? _statusTimer;
    private bool _refreshBusy;
    private bool _keyboardPreferenceRestored;
    private bool _batteryCycleRead;
    private bool? _lastServiceOnline;
    private string _manufacturer = string.Empty;
    private AdvancedWindow? _advancedWindow;

    public AppState State { get; } = new();
    public DisplayService DisplayService { get; } = new();
    public PowerModeService PowerModeService { get; } = new();
    public SystemStatusService SystemStatusService { get; } = new();
    public BatteryTelemetryService BatteryTelemetryService { get; } = new();
    public UserSettingsService UserSettings { get; } = new();
    public BatteryHistoryService BatteryHistoryService { get; }
    public HardwareServiceClient HardwareClient { get; } = new();
    public UpdateService UpdateService { get; } = new();
    public DiagnosticsRecorder DiagnosticsRecorder { get; } = new();
    public KeyboardEffectService KeyboardEffects { get; private set; } = null!;
    public MainWindow CompactWindow { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var synchronousStartup = Stopwatch.StartNew();
        base.OnStartup(e);

        ThinkControlUserSettings preferences = UserSettings.Current;
        BatteryHistoryService.ConfigureDetailedRetentionDays(preferences.BatteryDetailRetentionDays);
        ThemeService.Apply(preferences.Theme);
        State.RefreshAutoEnabled = preferences.RefreshAuto;
        State.KeyboardMode = preferences.KeyboardMode;
        State.KeyboardBaseLevel = preferences.KeyboardBaseLevel;
        State.KeyboardEffectSpeed = preferences.KeyboardEffectSpeed;

        SystemStatusSnapshot preflight = SystemStatusService.Read();
        State.DeviceName = preflight.DeviceName;
        State.CpuName = preflight.CpuName;
        State.GpuName = preflight.GpuName;
        State.RamText = preflight.RamText;
        State.BiosVersion = preflight.BiosVersion;
        State.MachineType = preflight.MachineType;
        _manufacturer = preflight.Manufacturer;

        DeviceValidationState validation = GetDeviceValidationState(
            preflight.MachineType,
            preflight.Manufacturer,
            preflight.DeviceName);
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "app.started",
            ValidationState: validation,
            Success: true,
            Tags: new Dictionary<string, string>
            {
                ["state"] = validation.ToString(),
                ["windowsBuild"] = Environment.OSVersion.Version.Build.ToString()
            }));
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "compatibility.device_detected",
            ValidationState: validation,
            Success: true,
            Tags: new Dictionary<string, string>
            {
                ["state"] = validation.ToString()
            }));

        if (validation != DeviceValidationState.Verified &&
            preferences.DiagnosticsConsent == DiagnosticsConsent.Unknown)
        {
            PromptForDeviceValidation(preflight, validation);
        }

        KeyboardEffects = new KeyboardEffectService(HardwareClient, State);
        CompactWindow = new MainWindow(this) { DataContext = State };
        MainWindow = CompactWindow;
        CreateTrayIcon();

        _statusTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, OnStatusTimer, Dispatcher);
        _statusTimer.Start();
        Task initialRefresh = RefreshStatusAsync(forceSystemInfo: true);
        PresentInitialShell(initialRefresh, synchronousStartup.Elapsed);
    }

    public void ToggleCompact()
    {
        if (_advancedWindow?.IsVisible == true)
        {
            _advancedWindow.Activate();
            return;
        }

        if (CompactWindow.IsVisible)
            CompactWindow.Hide();
        else
            CompactWindow.ShowNearTray(animate: true);
    }

    public void OpenAdvanced(string page = "Home")
    {
        if (_advancedWindow is null)
        {
            _advancedWindow = new AdvancedWindow(this) { DataContext = State };
            _advancedWindow.Closed += (_, _) => _advancedWindow = null;
        }

        CompactWindow.HideAnimated();
        _advancedWindow.Navigate(page);
        _advancedWindow.ShowAdvanced(animate: true);
    }

    public void ReturnToCompact()
    {
        if (_advancedWindow is not null)
            _advancedWindow.HideAnimated();

        CompactWindow.ShowNearTray(animate: true);
    }

    public void HideAdvancedToTray() => _advancedWindow?.HideAnimated();

    public async Task RefreshStatusAsync(bool forceSystemInfo = false)
    {
        if (_refreshBusy)
            return;

        _refreshBusy = true;
        try
        {
            SystemStatusSnapshot system = await Task.Run(SystemStatusService.Read);
            BatteryTelemetrySnapshot battery = await Task.Run(BatteryTelemetryService.Read);
            _manufacturer = system.Manufacturer;

            State.BatteryPercent = battery.Percent ?? system.BatteryPercent;
            State.BatteryCharging = battery.Charging;
            State.BatteryStatus = battery.Charging
                ? "Charging"
                : battery.OnAc
                    ? State.BatteryPercent >= 100 ? "Fully charged" : "Plugged in"
                    : battery.Discharging ? "On battery" : system.BatteryStatus;
            State.BatteryPowerWatts = battery.PowerWatts;
            State.BatterySmoothedPowerWatts = battery.SmoothedPowerWatts;
            State.BatteryHealthPercent = battery.HealthPercent;
            State.BatteryTemperatureC = battery.TemperatureC ?? ResolveCredibleBatteryTemperature(State.Sensors);
            State.BatteryRemainingWh = battery.RemainingCapacityWh;
            State.BatteryFullWh = battery.FullChargeCapacityWh;
            State.BatteryEtaToFull = battery.EstimatedTimeToFull;
            State.BatteryEtaRemaining = battery.EstimatedTimeRemaining;
            State.BatterySource = battery.Source;

            if (!_batteryCycleRead)
            {
                State.BatteryCycleCount = await Task.Run(BatteryCycleCountService.Read);
                _batteryCycleRead = true;
            }

            BatteryHistoryView batteryHistory = BatteryHistoryService.Record(
                battery.Charging,
                State.BatteryPercent,
                battery.PowerWatts,
                battery.RemainingCapacityWh,
                battery.FullChargeCapacityWh,
                battery.DesignCapacityWh);
            State.ApplyBatteryHistory(batteryHistory);
            BatteryTelemetryService.SetHistoricalChargePower(batteryHistory.TypicalChargePowerWatts);

            if (forceSystemInfo || State.CpuName == "—")
            {
                State.DeviceName = system.DeviceName;
                State.CpuName = system.CpuName;
                State.GpuName = system.GpuName;
                State.RamText = system.RamText;
                State.BiosVersion = system.BiosVersion;
                State.MachineType = system.MachineType;
            }

            ThinkControlPowerMode? mode = PowerModeService.GetCurrent(!battery.OnAc);
            if (mode.HasValue)
                State.SelectedMode = mode.Value.ToString();

            DisplaySnapshot display = await Task.Run(DisplayService.Read);
            State.CurrentRefreshHz = display.CurrentRefreshHz;
            State.MaxRefreshHz = display.SupportedRefreshRates.DefaultIfEmpty(0).Max();
            if (display.Brightness is int brightness)
            {
                State.BrightnessAvailable = true;
                State.Brightness = brightness;
            }
            else
            {
                State.BrightnessAvailable = false;
            }

            State.AdaptiveBrightnessAvailable = display.AdaptiveBrightness.HasValue;
            State.AdaptiveBrightnessEnabled = display.AdaptiveBrightness;

            var service = await HardwareClient.GetStatusAsync();
            bool serviceOnline = service?.Success == true && service.Telemetry is not null;
            if (_lastServiceOnline != serviceOnline)
            {
                _lastServiceOnline = serviceOnline;
                RecordDiagnostic(new DiagnosticEvent(
                    DateTimeOffset.UtcNow,
                    serviceOnline ? "service.connected" : "service.disconnected",
                    ValidationState: GetCurrentDeviceValidationState(),
                    Success: serviceOnline,
                    ErrorCode: serviceOnline ? null : "service_unavailable"));
            }

            if (serviceOnline && service?.Telemetry is not null)
            {
                State.HardwareAccess = service.Telemetry.HardwareAccess;
                State.CpuTemperatureC = service.Telemetry.CpuTemperatureC;
                if (service.Telemetry.CpuTemperatureC is double temp)
                    State.AddTemperature(temp);
                State.FanRpm = service.Telemetry.FanRpm;
                State.FanStateText = service.Telemetry.FanState;
                State.KeyboardStatus = service.Telemetry.KeyboardBacklight;
                State.KeyboardBackend = service.Telemetry.KeyboardBackend ?? "Not exposed";
                if (!string.IsNullOrWhiteSpace(service.Telemetry.ThermalSolutionVersion))
                    State.ThermalSolution = service.Telemetry.ThermalSolutionVersion!;

                if (service.Capabilities is not null)
                {
                    State.CanFanControl = service.Capabilities.FanControl;
                    State.CanFanTelemetry = service.Capabilities.FanTelemetry;
                    State.CanKeyboardBacklight = service.Capabilities.KeyboardBacklight;
                    State.CanCpuTemperature = service.Capabilities.CpuTemperature;
                }

                if (State.CanKeyboardBacklight && !_keyboardPreferenceRestored)
                {
                    await RestoreKeyboardPreferenceAsync();
                    _keyboardPreferenceRestored = true;
                }
            }
            else
            {
                State.HardwareAccess = GetCurrentDeviceValidationState() switch
                {
                    DeviceValidationState.Experimental => "Beta / Untested · Lenovo provider checks active",
                    DeviceValidationState.NotValidated => "Not validated · Windows features available",
                    _ => "Limited · hardware service offline"
                };
                State.CpuTemperatureC = null;
                State.FanRpm = null;
                State.FanStateText = "Lenovo managed · telemetry unavailable";
                State.KeyboardStatus = "Hardware backend unavailable";
                State.KeyboardBackend = "Not exposed";
                State.CanFanControl = false;
                State.CanFanTelemetry = false;
                State.CanKeyboardBacklight = false;
                State.CanCpuTemperature = false;
            }

            if (State.RefreshAutoEnabled)
                ApplyRefreshAuto(onBattery: !battery.OnAc);
        }
        finally
        {
            _refreshBusy = false;
        }
    }

    public bool SetPowerMode(ThinkControlPowerMode mode)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        bool changed = PowerModeService.Set(mode);
        if (changed)
            State.SelectedMode = mode.ToString();
        RecordOperation("power.profile_set", "PerformanceMode", "Windows", changed, started,
            new Dictionary<string, string> { ["state"] = mode.ToString() });
        return changed;
    }

    public bool SetBrightness(int value)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        bool changed = DisplayService.SetBrightness(value);
        if (changed)
            State.Brightness = value;
        RecordOperation("display.brightness_set", "Brightness", "Windows", changed, started);
        return changed;
    }

    public bool SetAdaptiveBrightness(bool enabled)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        bool changed = DisplayService.SetAdaptiveBrightness(enabled);
        if (changed)
            State.AdaptiveBrightnessEnabled = enabled;
        RecordOperation("display.adaptive_brightness_set", "AdaptiveBrightness", "Windows", changed, started,
            new Dictionary<string, string> { ["state"] = enabled ? "enabled" : "disabled" });
        return changed;
    }

    public bool SetRefresh(int hz)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        State.RefreshAutoEnabled = false;
        UserSettings.Update(settings => settings with { RefreshAuto = false });
        bool changed = DisplayService.SetRefreshRate(hz);
        if (changed)
            State.CurrentRefreshHz = DisplayService.GetCurrentRefreshRate();
        RecordOperation("display.refresh_set", "DisplayRefresh", "Windows", changed, started,
            new Dictionary<string, string> { ["state"] = hz.ToString() });
        return changed;
    }

    public bool EnableRefreshAuto()
    {
        State.RefreshAutoEnabled = true;
        UserSettings.Update(settings => settings with { RefreshAuto = true });
        BatteryTelemetrySnapshot battery = BatteryTelemetryService.Read();
        ApplyRefreshAuto(onBattery: !battery.OnAc);
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "display.refresh_auto_enabled",
            Capability: "DisplayRefresh",
            Provider: "Windows",
            ValidationState: GetCurrentDeviceValidationState(),
            Success: true));
        return true;
    }

    public async Task SetKeyboardStaticLevelAsync(string level)
    {
        string normalized = NormalizeStaticKeyboardLevel(level);
        string restingLevel = State.KeyboardBaseLevel;
        DateTimeOffset started = DateTimeOffset.UtcNow;
        await KeyboardEffects.SetStaticLevelAsync(normalized);
        KeyboardEffects.SetBaseLevel(restingLevel);
        UserSettings.Update(settings => settings with
        {
            KeyboardMode = "Static",
            KeyboardStaticLevel = normalized,
            KeyboardBaseLevel = restingLevel
        });
        await RefreshStatusAsync();
        bool success = State.KeyboardStatus.Contains(normalized, StringComparison.OrdinalIgnoreCase);
        RecordOperation("keyboard.level_set", "KeyboardBacklight", "Lenovo", success, started,
            new Dictionary<string, string> { ["state"] = normalized });
    }

    public async Task SetKeyboardModeAsync(string mode)
    {
        await KeyboardEffects.SetModeAsync(mode);
        UserSettings.Update(settings => settings with { KeyboardMode = State.KeyboardMode });
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "keyboard.effect_mode_set",
            Capability: "KeyboardBacklight",
            Provider: "ThinkControlUserSession",
            ValidationState: GetCurrentDeviceValidationState(),
            Success: true,
            Tags: new Dictionary<string, string> { ["state"] = State.KeyboardMode }));
        await RefreshStatusAsync();
    }

    public void SetKeyboardBaseLevel(string level)
    {
        KeyboardEffects.SetBaseLevel(level);
        UserSettings.Update(settings => settings with { KeyboardBaseLevel = State.KeyboardBaseLevel });
    }

    public void SetKeyboardEffectSpeed(double speed)
    {
        KeyboardEffects.SetSpeed(speed);
        UserSettings.Update(settings => settings with { KeyboardEffectSpeed = State.KeyboardEffectSpeed });
    }

    public void ApplyTheme(ThinkControl.UI.Services.ThemeMode mode)
    {
        ThemeService.Apply(mode);
        UserSettings.Update(settings => settings with { Theme = mode });
    }

    public void ClearBatteryHistory()
    {
        State.ApplyBatteryHistory(BatteryHistoryService.Clear());
        BatteryTelemetryService.SetHistoricalChargePower(null);
    }

    public void RecordDiagnostic(DiagnosticEvent diagnosticEvent)
    {
        try { DiagnosticsRecorder.Record(diagnosticEvent); }
        catch { }
    }

    public void ExitApplication()
    {
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "app.exit",
            ValidationState: GetCurrentDeviceValidationState(),
            Success: true));
        _statusTimer?.Stop();
        try { KeyboardEffects?.Dispose(); } catch { }
        _trayIcon?.Dispose();
        _ownedTrayIcon?.Dispose();
        _advancedWindow?.ForceClose();
        CompactWindow.ForceClose();
        Shutdown();
    }

    private void PromptForDeviceValidation(SystemStatusSnapshot system, DeviceValidationState validation)
    {
        bool beta = validation == DeviceValidationState.Experimental;
        string heading = beta ? "Beta / Untested Lenovo profile" : "Device not validated";
        string intro = beta
            ? $"{system.DeviceName} is recognized as a Lenovo device, but this exact model has not been physically validated with ThinkControl yet."
            : $"{system.DeviceName} has not been validated with ThinkControl yet.";

        MessageBoxResult answer = MessageBox.Show(
            intro + "\n\n" +
            "The normal ThinkControl interface will still open and Windows-level features remain available. " +
            "Lenovo hardware controls only activate when a known provider passes its compatibility/readback checks. " +
            "Direct X9 EC fan writes remain limited to the verified 21Q6/21Q7 profile.\n\n" +
            "Help validate this device by allowing redacted compatibility diagnostics? You can change this later in Settings.",
            $"ThinkControl · {heading}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        DiagnosticsConsent consent = answer == MessageBoxResult.Yes
            ? DiagnosticsConsent.Enabled
            : DiagnosticsConsent.Disabled;
        UserSettings.Update(settings => settings with { DiagnosticsConsent = consent });
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "diagnostics.consent_initial",
            ValidationState: validation,
            Success: true,
            Tags: new Dictionary<string, string> { ["state"] = consent.ToString() }));
    }

    private void RecordOperation(
        string name,
        string capability,
        string provider,
        bool success,
        DateTimeOffset started,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        int duration = (int)Math.Clamp((DateTimeOffset.UtcNow - started).TotalMilliseconds, 0, 600_000);
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            name,
            Capability: capability,
            Provider: provider,
            ValidationState: GetCurrentDeviceValidationState(),
            Success: success,
            ErrorCode: success ? null : "operation_failed",
            DurationMs: duration,
            Tags: tags));
    }

    public static DeviceValidationState GetDeviceValidationState(string? machineType) =>
        GetDeviceValidationState(machineType, null, null);

    public static DeviceValidationState GetDeviceValidationState(
        string? machineType,
        string? manufacturer,
        string? productName)
    {
        if (string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase))
            return DeviceValidationState.Verified;

        if ((!string.IsNullOrWhiteSpace(manufacturer) &&
             manufacturer.Contains("LENOVO", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(productName) &&
             productName.Contains("Lenovo", StringComparison.OrdinalIgnoreCase)))
        {
            return DeviceValidationState.Experimental;
        }

        return DeviceValidationState.NotValidated;
    }

    private DeviceValidationState GetCurrentDeviceValidationState() =>
        GetDeviceValidationState(State.MachineType, _manufacturer, State.DeviceName);

    private async Task RestoreKeyboardPreferenceAsync()
    {
        ThinkControlUserSettings preferences = UserSettings.Current;
        State.KeyboardBaseLevel = preferences.KeyboardBaseLevel;
        State.KeyboardEffectSpeed = preferences.KeyboardEffectSpeed;

        if (preferences.KeyboardMode == "Static")
        {
            string restingLevel = preferences.KeyboardBaseLevel;
            await KeyboardEffects.SetStaticLevelAsync(preferences.KeyboardStaticLevel);
            KeyboardEffects.SetBaseLevel(restingLevel);
        }
        else
        {
            await KeyboardEffects.SetModeAsync(preferences.KeyboardMode);
            if (State.KeyboardMode == "Static")
                UserSettings.Update(settings => settings with { KeyboardMode = "Static" });
        }
    }

    private static string NormalizeStaticKeyboardLevel(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "off" => "Off",
        "low" => "Low",
        _ => "High"
    };

    private void ApplyRefreshAuto(bool onBattery)
    {
        IReadOnlyList<int> supported = DisplayService.GetSupportedRefreshRates();
        int target = onBattery && supported.Contains(60)
            ? 60
            : supported.DefaultIfEmpty(State.CurrentRefreshHz).Max();

        if (target > 0 && target != State.CurrentRefreshHz && DisplayService.SetRefreshRate(target))
            State.CurrentRefreshHz = target;
    }

    private async void OnStatusTimer(object? sender, EventArgs e) => await RefreshStatusAsync();

    private void CreateTrayIcon()
    {
        _ownedTrayIcon = CreateIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _ownedTrayIcon,
            Text = "ThinkControl",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _trayIcon.ContextMenuStrip.Items.Add("Open ThinkControl", null, (_, _) => Dispatcher.Invoke(ToggleCompact));
        _trayIcon.ContextMenuStrip.Items.Add("Advanced", null, (_, _) => Dispatcher.Invoke(() => OpenAdvanced("Home")));
        _trayIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _trayIcon.ContextMenuStrip.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _trayIcon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
                Dispatcher.Invoke(ToggleCompact);
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => OpenAdvanced("Home"));
    }

    private static Icon CreateIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/tray.ico", UriKind.Absolute));
            if (resource?.Stream is not null)
            {
                using Icon tray = new(resource.Stream);
                return (Icon)tray.Clone();
            }
        }
        catch
        {
        }

        try
        {
            string? executable = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executable))
            {
                Icon? applicationIcon = Icon.ExtractAssociatedIcon(executable);
                if (applicationIcon is not null)
                    return applicationIcon;
            }
        }
        catch
        {
        }

        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var accentBrush = new SolidBrush(Color.FromArgb(227, 41, 41));
        graphics.FillEllipse(accentBrush, 4, 4, 24, 24);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
