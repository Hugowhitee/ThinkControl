using System.Text;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal static class DeviceSupportReportService
{
    private const string NewIssueUrl = "https://github.com/Hugowhitee/ThinkControl/issues/new";

    internal static bool HasUsefulDiscovery(AppState state)
    {
        if (state.Sensors.Count > 0 || state.Fans.Count > 0 ||
            state.CanFanControl || state.CanKeyboardBacklight ||
            state.ControlTemperatureC.HasValue)
        {
            return true;
        }

        string access = state.HardwareAccess ?? string.Empty;
        return access.Contains("PawnIO", StringComparison.OrdinalIgnoreCase) ||
               access.Contains("LibreHardwareMonitor", StringComparison.OrdinalIgnoreCase) ||
               access.Contains("EC", StringComparison.OrdinalIgnoreCase) ||
               access.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
               access.Contains("provider", StringComparison.OrdinalIgnoreCase) &&
               !access.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
    }

    internal static string DiscoverySummary(AppState state)
    {
        if (!HasUsefulDiscovery(state))
            return "Still learning · run Hardware setup / Retry detection first";

        var parts = new List<string>();
        if (state.Sensors.Count > 0) parts.Add($"{state.Sensors.Count} sensors");
        if (state.Fans.Count > 0) parts.Add($"{state.Fans.Count} fan source{(state.Fans.Count == 1 ? string.Empty : "s")}");
        if (state.CanKeyboardBacklight) parts.Add("keyboard provider");
        if (state.CanFanControl) parts.Add("verified fan control");
        if (state.ControlTemperatureC.HasValue) parts.Add("control temperature");
        return parts.Count == 0 ? "Useful provider information detected" : string.Join(" · ", parts);
    }

    internal static string BuildIssueUrl(AppState state, SystemStatusSnapshot system)
    {
        string machine = Safe(system.MachineType, "unknown");
        string product = Safe(system.DeviceName, "Unknown device");
        string title = $"Device support: {product} ({machine})";

        string[] sensorTypes = state.Sensors
            .Select(sensor => Safe(sensor.SensorType, "Unknown"))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
        string[] sensorSources = state.Sensors
            .Select(sensor => Safe(sensor.Source, "Unknown"))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
        string[] fanSources = state.Fans
            .Select(fan => Safe(fan.Source, "Unknown"))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        var body = new StringBuilder();
        body.AppendLine("## ThinkControl device support report");
        body.AppendLine();
        body.AppendLine("> Generated locally after ThinkControl found useful hardware/provider information. Please review before submitting. Serial numbers, Windows usernames, hostnames, paths and raw logs are intentionally excluded.");
        body.AppendLine();
        body.AppendLine("### Device");
        body.AppendLine($"- ThinkControl: `{Safe(UpdateService.CurrentVersion, "unknown")}`");
        body.AppendLine($"- Windows: `{Safe(Environment.OSVersion.VersionString, "unknown")}`");
        body.AppendLine($"- Manufacturer: `{Safe(system.Manufacturer, "unknown")}`");
        body.AppendLine($"- Product: `{product}`");
        body.AppendLine($"- Machine type: `{machine}`");
        body.AppendLine($"- BIOS: `{Safe(system.BiosVersion, "unknown")}`");
        body.AppendLine();
        body.AppendLine("### Capability probe");
        body.AppendLine($"- Sensor telemetry: `{YesNo(state.CanSensorTelemetry)}` ({state.Sensors.Count} readings)");
        body.AppendLine($"- CPU/control temperature: `{YesNo(state.CanCpuTemperature)}` / `{Safe(state.ControlTemperatureSource, "Unavailable")}`");
        body.AppendLine($"- Fan telemetry: `{YesNo(state.CanFanTelemetry)}` ({state.Fans.Count} fan readings)");
        body.AppendLine($"- Fan control: `{YesNo(state.CanFanControl)}`");
        body.AppendLine($"- Keyboard backlight: `{YesNo(state.CanKeyboardBacklight)}` / `{Safe(state.KeyboardStatus, "Unavailable")}`");
        body.AppendLine($"- Hardware access: `{Safe(state.HardwareAccess, "Unavailable")}`");
        body.AppendLine($"- Setup status: `{Safe(state.DriverStatus, "Unknown")}`");
        body.AppendLine();
        body.AppendLine("### Read-only discovery");
        body.AppendLine($"- Sensor types: `{Join(sensorTypes)}`");
        body.AppendLine($"- Sensor providers: `{Join(sensorSources)}`");
        body.AppendLine($"- Fan providers: `{Join(fanSources)}`");
        body.AppendLine();
        body.AppendLine("### Physical verification");
        body.AppendLine("Please add anything you personally verified on this laptop (for example whether RPM changes match audible fan speed, whether keyboard levels read back correctly, and whether haptic controls work). Do not paste serial numbers or unrelated logs.");

        return NewIssueUrl +
               "?title=" + Uri.EscapeDataString(title) +
               "&body=" + Uri.EscapeDataString(body.ToString());
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string Join(IEnumerable<string> values)
    {
        string joined = string.Join(", ", values);
        return string.IsNullOrWhiteSpace(joined) ? "none detected" : joined;
    }

    private static string Safe(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string text = value.Trim()
            .Replace(Environment.UserName, "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace(Environment.MachineName, "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace('`', '\'');
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            text = text.Replace(home, "[user]", StringComparison.OrdinalIgnoreCase);
        text = new string(text.Where(ch => !char.IsControl(ch)).ToArray());
        return text.Length <= 180 ? text : text[..180];
    }
}