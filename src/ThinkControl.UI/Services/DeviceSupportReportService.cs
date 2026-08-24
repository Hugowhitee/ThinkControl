using System.Text;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal static class DeviceSupportReportService
{
    private const string NewIssueUrl = "https://github.com/Hugowhitee/ThinkControl/issues/new";

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
        body.AppendLine("> Generated locally by ThinkControl. Please review before submitting. Serial numbers, Windows usernames, hostnames, paths and raw logs are intentionally excluded.");
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
