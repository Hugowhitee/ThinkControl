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
    private AdvancedWindow? _advancedWindow;

    public AppState State { get; } = new();
    public DisplayService DisplayService { get; } = new();
    public PowerModeService PowerModeService { get; } = new();
    public SystemStatusService SystemStatusService { get; } = new();
    public HardwareServiceClient HardwareClient { get; } = new();
    public UpdateService UpdateService { get; } = new();
    public MainWindow CompactWindow { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Apply(ThinkControl.UI.Services.ThemeMode.System);

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
            State.BatteryPercent = system.BatteryPercent;
            State.BatteryStatus = system.BatteryStatus;

            if (forceSystemInfo || State.CpuName == "—")
            {
                State.DeviceName = system.DeviceName;
                State.CpuName = system.CpuName;
                State.GpuName = system.GpuName;
                State.RamText = system.RamText;
                State.BiosVersion = system.BiosVersion;
                State.MachineType = system.MachineType;
            }

            ThinkControlPowerMode? mode = PowerModeService.GetCurrent(system.BatteryStatus.StartsWith("On battery", StringComparison.OrdinalIgnoreCase));
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
                ApplyRefreshAuto(system.BatteryStatus);
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
        bool changed = DisplayService.SetRefreshRate(hz);
        if (changed)
            State.CurrentRefreshHz = DisplayService.GetCurrentRefreshRate();
        return changed;
    }

    public bool EnableRefreshAuto()
    {
        State.RefreshAutoEnabled = true;
        SystemStatusSnapshot system = SystemStatusService.Read();
        ApplyRefreshAuto(system.BatteryStatus);
        return true;
    }

    public void ExitApplication()
    {
        _statusTimer?.Stop();
        _trayIcon?.Dispose();
        _ownedTrayIcon?.Dispose();
        _advancedWindow?.ForceClose();
        CompactWindow.ForceClose();
        Shutdown();
    }

    private void ApplyRefreshAuto(string batteryStatus)
    {
        IReadOnlyList<int> supported = DisplayService.GetSupportedRefreshRates();
        bool onBattery = batteryStatus.StartsWith("On battery", StringComparison.OrdinalIgnoreCase);
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
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var textBrush = new SolidBrush(Color.White);
        using var accentBrush = new SolidBrush(Color.FromArgb(227, 41, 41));
        using var font = new Font("Segoe UI", 18, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString("T", font, textBrush, 4, 3);
        graphics.FillEllipse(accentBrush, 21, 6, 7, 7);

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
