using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
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
    private AdvancedWindow? _advancedWindow;

    public AppState State { get; } = new();
    public DisplayService DisplayService { get; } = new();
    public PowerModeService PowerModeService { get; } = new();
    public SystemStatusService SystemStatusService { get; } = new();
    public BatteryTelemetryService BatteryTelemetryService { get; } = new();
    public HardwareServiceClient HardwareClient { get; } = new();
    public UpdateService UpdateService { get; } = new();
    public UserSettingsService UserSettings { get; } = new();
    public KeyboardEffectService KeyboardEffects { get; private set; } = null!;
    public MainWindow CompactWindow { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThinkControlUserSettings preferences = UserSettings.Current;
        ThemeService.Apply(preferences.Theme);
        State.RefreshAutoEnabled = preferences.RefreshAuto;
        State.KeyboardMode = preferences.KeyboardMode;
        State.KeyboardBaseLevel = preferences.KeyboardBaseLevel;
        State.KeyboardEffectSpeed = preferences.KeyboardEffectSpeed;

        KeyboardEffects = new KeyboardEffectService(HardwareClient, State);
        CompactWindow = new MainWindow(this) { DataContext = State };
        MainWindow = CompactWindow;
        CreateTrayIcon();

        _statusTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, OnStatusTimer, Dispatcher);
        _statusTimer.Start();
        _ = RefreshStatusAsync(forceSystemInfo: true);

        CompactWindow.ShowNearTray(animate: true);
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

            State.BatteryPercent = battery.Percent ?? system.BatteryPercent;
            State.BatteryStatus = battery.Charging
                ? "Charging"
                : battery.OnAc
                    ? State.BatteryPercent >= 100 ? "Fully charged" : "Plugged in"
                    : battery.Discharging ? "On battery" : system.BatteryStatus;
            State.BatteryPowerWatts = battery.PowerWatts;
            State.BatterySmoothedPowerWatts = battery.SmoothedPowerWatts;
            State.BatteryHealthPercent = battery.HealthPercent;
            State.BatteryRemainingWh = battery.RemainingCapacityWh;
            State.BatteryFullWh = battery.FullChargeCapacityWh;
            State.BatteryEtaToFull = battery.EstimatedTimeToFull;
            State.BatteryEtaRemaining = battery.EstimatedTimeRemaining;
            State.BatterySource = battery.Source;

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
            if (service?.Success == true && service.Telemetry is not null)
            {
                State.HardwareAccess = service.Telemetry.HardwareAccess;
                State.CpuTemperatureC = service.Telemetry.CpuTemperatureC;
                if (service.Telemetry.CpuTemperatureC is double temp)
                    State.AddTemperature(temp);
                State.FanRpm = service.Telemetry.FanRpm;
                State.FanStateText = service.Telemetry.FanState;
                State.KeyboardStatus = service.Telemetry.KeyboardBacklight;
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
                State.HardwareAccess = "Limited · hardware service offline";
                State.CpuTemperatureC = null;
                State.FanRpm = null;
                State.FanStateText = "Lenovo Auto · telemetry unavailable";
                State.KeyboardStatus = "Hardware service unavailable";
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
        bool changed = PowerModeService.Set(mode);
        if (changed)
            State.SelectedMode = mode.ToString();
        return changed;
    }

    public bool SetBrightness(int value)
    {
        bool changed = DisplayService.SetBrightness(value);
        if (changed)
            State.Brightness = value;
        return changed;
    }

    public bool SetAdaptiveBrightness(bool enabled)
    {
        bool changed = DisplayService.SetAdaptiveBrightness(enabled);
        if (changed)
            State.AdaptiveBrightnessEnabled = enabled;
        return changed;
    }

    public bool SetRefresh(int hz)
    {
        State.RefreshAutoEnabled = false;
        UserSettings.Update(settings => settings with { RefreshAuto = false });
        bool changed = DisplayService.SetRefreshRate(hz);
        if (changed)
            State.CurrentRefreshHz = DisplayService.GetCurrentRefreshRate();
        return changed;
    }

    public bool EnableRefreshAuto()
    {
        State.RefreshAutoEnabled = true;
        UserSettings.Update(settings => settings with { RefreshAuto = true });
        BatteryTelemetrySnapshot battery = BatteryTelemetryService.Read();
        ApplyRefreshAuto(onBattery: !battery.OnAc);
        return true;
    }

    public async Task SetKeyboardStaticLevelAsync(string level)
    {
        string normalized = NormalizeStaticKeyboardLevel(level);
        string restingLevel = State.KeyboardBaseLevel;
        await KeyboardEffects.SetStaticLevelAsync(normalized);
        KeyboardEffects.SetBaseLevel(restingLevel);
        UserSettings.Update(settings => settings with
        {
            KeyboardMode = "Static",
            KeyboardStaticLevel = normalized,
            KeyboardBaseLevel = restingLevel
        });
        await RefreshStatusAsync();
    }

    public async Task SetKeyboardModeAsync(string mode)
    {
        await KeyboardEffects.SetModeAsync(mode);
        UserSettings.Update(settings => settings with { KeyboardMode = State.KeyboardMode });
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

    public void ExitApplication()
    {
        _statusTimer?.Stop();
        try { KeyboardEffects?.Dispose(); } catch { }
        _trayIcon?.Dispose();
        _ownedTrayIcon?.Dispose();
        _advancedWindow?.ForceClose();
        CompactWindow.ForceClose();
        Shutdown();
    }

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
