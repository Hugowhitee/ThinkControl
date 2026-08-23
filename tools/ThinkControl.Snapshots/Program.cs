using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkControl.Core.Ipc;
using ThinkControl.UI;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.Snapshots;

internal static class Program
{
    private sealed record SnapshotEntry(string File, string Surface, string State, int Width, int Height);

    private static readonly string[] AdvancedPages =
    [
        "Home", "Performance", "Fans", "Sensors", "Display", "Audio",
        "Keyboard", "Battery", "Touchpad", "System", "Updates", "Settings"
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        string output = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "snapshots"));
        Directory.CreateDirectory(output);

        var app = new App();
        app.InitializeComponent();
        var snapshots = new List<SnapshotEntry>();

        AppState charging = CreateDemoState(charging: true, hardwareReady: true);
        AppState onBattery = CreateDemoState(charging: false, hardwareReady: true);
        AppState serviceOffline = CreateDemoState(charging: true, hardwareReady: false);

        ThemeService.Apply(ThemeMode.Dark);
        RenderCompact(app, charging, output, snapshots, "compact-dark.png", "charging");
        RenderCompact(app, onBattery, output, snapshots, "compact-on-battery.png", "on battery");

        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 1160, 760, output, snapshots, $"advanced-{page.ToLowerInvariant()}.png", "normal");

        // Every page gets minimum-size coverage so clipping and scrollbar overlap
        // cannot hide on a page that happened not to be in a hand-picked subset.
        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 980, 650, output, snapshots, $"advanced-{page.ToLowerInvariant()}-min.png", "minimum window");

        // Wide-screen geometry is also checked on every page. The left edge of each
        // page should stay fixed while unused space grows only on the right.
        foreach (string page in AdvancedPages)
            RenderAdvanced(app, charging, page, 1720, 980, output, snapshots, $"advanced-{page.ToLowerInvariant()}-wide.png", "wide window");

        RenderAdvanced(app, serviceOffline, "System", 1160, 760, output, snapshots, "advanced-system-service-offline.png", "hardware service offline");
        RenderAdvanced(app, serviceOffline, "Keyboard", 1160, 760, output, snapshots, "advanced-keyboard-unavailable.png", "hardware service offline");
        RenderAdvanced(app, serviceOffline, "Fans", 1160, 760, output, snapshots, "advanced-fans-unavailable.png", "hardware service offline");

        ThemeService.Apply(ThemeMode.Light);
        RenderCompact(app, charging, output, snapshots, "compact-light.png", "charging · light");
        RenderAdvanced(app, charging, "Home", 1160, 760, output, snapshots, "advanced-home-light.png", "normal · light");
        RenderAdvanced(app, charging, "Touchpad", 1160, 760, output, snapshots, "advanced-touchpad-light.png", "normal · light");
        RenderAdvanced(app, charging, "Sensors", 1160, 760, output, snapshots, "advanced-sensors-light.png", "normal · light");

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
            FanStateText = hardwareReady ? "Normal · level 3" : "Lenovo managed · telemetry unavailable",
            BatteryPercent = charging ? 78 : 63,
            BatteryCharging = charging,
            BatteryStatus = charging ? "Charging" : "On battery",
            BatteryPowerWatts = charging ? 18.4 : 7.2,
            BatterySmoothedPowerWatts = charging ? 17.8 : 6.9,
            BatteryHealthPercent = 97.6,
            BatteryRemainingWh = charging ? 56.2 : 45.4,
            BatteryFullWh = 72.0,
            BatteryEtaToFull = charging ? TimeSpan.FromMinutes(52) : null,
            BatteryEtaRemaining = charging ? null : TimeSpan.FromHours(6.4),
            BatteryCycleCount = 12,
            BatteryChargeCurveLabel = "Current charge · full session curve",
            BatteryCurrentSessionText = "61% → 78% · 43 min · 17.8 W avg · +12.1 Wh",
            BatteryTypicalChargeText = "Typical 18.1 W · 8 sessions",
            BatteryHealthTrendText = "Health trend · 97.6% · stable",
            BatterySource = "Windows ACPI battery",
            Brightness = 68,
            BrightnessAvailable = true,
            AdaptiveBrightnessAvailable = true,
            AdaptiveBrightnessEnabled = true,
            CurrentRefreshHz = 120,
            MaxRefreshHz = 120,
            RefreshAutoEnabled = true,
            HardwareAccess = hardwareReady
                ? "Full · verified X9 EC + Lenovo keyboard provider"
                : "Limited · hardware service offline",
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
            KeyboardMode = hardwareReady ? "Breathing" : "Auto",
            KeyboardBaseLevel = "Low",
            KeyboardEffectSpeed = 1.0,
            SelectedMode = "Balanced",
            UpdateStatus = "Up to date · v0.1.0-alpha.3",
            CanFanControl = hardwareReady,
            CanFanTelemetry = hardwareReady,
            CanKeyboardBacklight = hardwareReady,
            CanCpuTemperature = hardwareReady,
            CanSensorTelemetry = hardwareReady
        };

        if (hardwareReady)
        {
            state.ApplyHardwareTelemetry(
            [
                new FanTelemetrySnapshot("fan-ec-primary", "System fan", 2050, "ThinkPad X9 EC tachometer 0x84/0x85", true)
            ],
            [
                new HardwareSensorSnapshot("cpu-package", "Intel Core Ultra 7 258V", "Cpu", "CPU Package", "Temperature", 47.2, "°C", true, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("cpu-power", "Intel Core Ultra 7 258V", "Cpu", "CPU Package", "Power", 12.8, "W", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("gpu-temp", "Intel Arc 140V", "GpuIntel", "GPU Core", "Temperature", 43.5, "°C", true, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("gpu-load", "Intel Arc 140V", "GpuIntel", "GPU Core", "Load", 18.0, "%", false, "LibreHardwareMonitor"),
                new HardwareSensorSnapshot("ssd-temp", "NVMe SSD", "Storage", "Temperature", "Temperature", 39.0, "°C", false, "LibreHardwareMonitor")
            ]);
        }

        for (int i = 0; i < 60; i++)
            state.TemperatureHistory.Add(43 + Math.Sin(i / 4d) * 2 + (i % 11 == 0 ? 1 : 0));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 0; i <= 43; i++)
        {
            double watts = 18.8 - i * 0.028 + Math.Sin(i / 3.5) * 0.55;
            state.BatteryChargePowerTimeline.Add(new TimeSeriesPoint(now - TimeSpan.FromMinutes(43 - i), watts));
        }

        for (int i = 0; i < 8; i++)
        {
            double health = 98.0 - i * 0.055 + Math.Sin(i * 0.8) * 0.06;
            state.BatteryHealthTrendTimeline.Add(new TimeSeriesPoint(now - TimeSpan.FromDays((7 - i) * 21), health));
        }

        state.RecentChargeSessions.Add("Today · 61% → 78% · 43 min · 17.8 W avg · +12.1 Wh");
        state.RecentChargeSessions.Add("21 Aug · 34% → 91% · 2h 12m · 18.3 W avg · +40.6 Wh");
        state.RecentChargeSessions.Add("20 Aug · 52% → 86% · 1h 18m · 17.9 W avg · +24.0 Wh");
        return state;
    }

    private static void RenderCompact(App app, AppState state, string output, ICollection<SnapshotEntry> snapshots, string fileName, string stateName)
    {
        const int width = 410;
        const int height = 640;
        var window = new MainWindow(app) { DataContext = state, Width = width, Height = height };
        window.PrepareBrandingForSnapshot();
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, "Compact", stateName, width, height));
        window.ForceClose();
    }

    private static void RenderAdvanced(App app, AppState state, string page, int width, int height, string output, ICollection<SnapshotEntry> snapshots, string fileName, string stateName)
    {
        var window = new AdvancedWindow(app) { DataContext = state, Width = width, Height = height };
        window.PrepareEnhancedUiForSnapshot();
        if (string.Equals(page, "Touchpad", StringComparison.OrdinalIgnoreCase))
            window.NavigateTouchpad();
        else if (string.Equals(page, "Sensors", StringComparison.OrdinalIgnoreCase))
            window.NavigateSensors();
        else if (string.Equals(page, "Audio", StringComparison.OrdinalIgnoreCase))
            window.NavigateAudio();
        else
            window.Navigate(page);
        RenderWindowContent(window, Path.Combine(output, fileName));
        snapshots.Add(new SnapshotEntry(fileName, $"Advanced · {page}", stateName, width, height));
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
        html.AppendLine("<h1>ThinkControl Visual QA</h1><p>Deterministic WPF snapshots. Review alignment, clipping, hierarchy and state handling at fixed viewport sizes before merging UI changes.</p><div class=\"grid\">");
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
