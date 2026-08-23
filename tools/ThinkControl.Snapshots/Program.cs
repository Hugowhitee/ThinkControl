using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkControl.UI;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.Snapshots;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string output = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "snapshots"));
        Directory.CreateDirectory(output);

        var app = new App();
        app.InitializeComponent();
        AppState state = CreateDemoState();

        ThemeService.Apply(ThemeMode.Dark);
        RenderCompact(app, state, Path.Combine(output, "compact-dark.png"));
        RenderAdvanced(app, state, "Home", 1160, 760, Path.Combine(output, "advanced-home.png"));
        RenderAdvanced(app, state, "Performance", 1160, 760, Path.Combine(output, "advanced-performance.png"));
        RenderAdvanced(app, state, "Fans", 1160, 760, Path.Combine(output, "advanced-fans.png"));
        RenderAdvanced(app, state, "Display", 1160, 760, Path.Combine(output, "advanced-display.png"));
        RenderAdvanced(app, state, "Keyboard", 1160, 760, Path.Combine(output, "advanced-keyboard.png"));
        RenderAdvanced(app, state, "Battery", 1160, 760, Path.Combine(output, "advanced-battery.png"));
        RenderAdvanced(app, state, "Touchpad", 1160, 760, Path.Combine(output, "advanced-touchpad.png"));
        RenderAdvanced(app, state, "Settings", 1160, 760, Path.Combine(output, "advanced-settings.png"));

        RenderAdvanced(app, state, "Home", 1720, 980, Path.Combine(output, "advanced-home-wide.png"));
        RenderAdvanced(app, state, "Display", 1720, 980, Path.Combine(output, "advanced-display-wide.png"));
        RenderAdvanced(app, state, "Touchpad", 1720, 980, Path.Combine(output, "advanced-touchpad-wide.png"));
        RenderAdvanced(app, state, "Battery", 1720, 980, Path.Combine(output, "advanced-battery-wide.png"));

        ThemeService.Apply(ThemeMode.Light);
        RenderCompact(app, state, Path.Combine(output, "compact-light.png"));
        RenderAdvanced(app, state, "Home", 1160, 760, Path.Combine(output, "advanced-home-light.png"));
        RenderAdvanced(app, state, "Touchpad", 1160, 760, Path.Combine(output, "advanced-touchpad-light.png"));

        Console.WriteLine($"Rendered ThinkControl snapshots to {output}");
        return 0;
    }

    private static AppState CreateDemoState()
    {
        var state = new AppState
        {
            DeviceName = "ThinkPad X9-15 Gen 1",
            CpuTemperatureC = 44,
            FanRpm = 2050,
            FanStateText = "Level 3",
            BatteryPercent = 78,
            BatteryStatus = "Charging",
            BatteryPowerWatts = 18.4,
            BatterySmoothedPowerWatts = 17.8,
            BatteryHealthPercent = 97.6,
            BatteryRemainingWh = 56.2,
            BatteryFullWh = 72.0,
            BatteryEtaToFull = TimeSpan.FromMinutes(52),
            BatterySource = "Windows ACPI battery",
            Brightness = 68,
            BrightnessAvailable = true,
            AdaptiveBrightnessAvailable = true,
            AdaptiveBrightnessEnabled = true,
            CurrentRefreshHz = 120,
            MaxRefreshHz = 120,
            RefreshAutoEnabled = true,
            HardwareAccess = "Verified · X9 profile · EC + Lenovo keyboard",
            CpuName = "Intel Core Ultra 7 258V",
            GpuName = "Intel Arc 140V",
            RamText = "32 GB",
            BiosVersion = "N4CETxxW",
            MachineType = "21Q6",
            ThermalSolution = "Lenovo Intelligent Thermal Solution",
            DriverStatus = "Hardware access ready",
            KeyboardStatus = "High",
            KeyboardMode = "Breathing",
            KeyboardBaseLevel = "Low",
            KeyboardEffectSpeed = 1.0,
            SelectedMode = "Balanced",
            UpdateStatus = "Up to date · v0.1.0-alpha.3",
            CanFanControl = true,
            CanFanTelemetry = true,
            CanKeyboardBacklight = true,
            CanCpuTemperature = true
        };

        for (int i = 0; i < 60; i++)
            state.TemperatureHistory.Add(43 + Math.Sin(i / 4d) * 2 + (i % 11 == 0 ? 1 : 0));

        return state;
    }

    private static void RenderCompact(App app, AppState state, string path)
    {
        var window = new MainWindow(app) { DataContext = state, Width = 410, Height = 640 };
        window.PrepareBrandingForSnapshot();
        RenderWindowContent(window, path);
        window.ForceClose();
    }

    private static void RenderAdvanced(App app, AppState state, string page, double width, double height, string path)
    {
        var window = new AdvancedWindow(app) { DataContext = state, Width = width, Height = height };
        window.PrepareEnhancedUiForSnapshot();
        if (string.Equals(page, "Touchpad", StringComparison.OrdinalIgnoreCase))
            window.NavigateTouchpad();
        else
            window.Navigate(page);
        RenderWindowContent(window, path);
        window.ForceClose();
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
}
