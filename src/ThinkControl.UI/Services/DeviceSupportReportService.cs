using System.Security.Cryptography;
using System.Text;
using System.IO;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal sealed record DeviceSupportReport(string Title, string Body, string Fingerprint);

internal static class DeviceSupportReportService
{
    private const string NewIssueUrl = "https://github.com/Hugowhitee/ThinkControl/issues/new";
    private const string PreparedReportFileName = "device-support-report.md";

    internal static bool HasUsefulDiscovery(AppState state)
    {
        // Do not unlock sharing while startup/provider discovery is still in flight.
        // A few generic Windows/LHM values can arrive before ThinkControl knows
        // whether a provider is actually usable; that is not yet a useful report.
        string driver = state.DriverStatus ?? string.Empty;
        string access = state.HardwareAccess ?? string.Empty;
        if (IsTransient(driver) || IsTransient(access))
            return false;

        bool usefulSensors = state.CanSensorTelemetry && state.Sensors.Count >= 3;
        bool usefulTemperature = state.CanCpuTemperature && state.ControlTemperatureC.HasValue &&
                                 !string.IsNullOrWhiteSpace(state.ControlTemperatureSource) &&
                                 !state.ControlTemperatureSource.Equals("Unavailable", StringComparison.OrdinalIgnoreCase);
        bool usefulFans = state.CanFanTelemetry && state.Fans.Count > 0;
        bool usefulWritableProvider = state.CanFanControl || state.CanKeyboardBacklight;
        bool explicitProviderEvidence = access.Contains("PawnIO", StringComparison.OrdinalIgnoreCase) ||
                                        access.Contains("LibreHardwareMonitor", StringComparison.OrdinalIgnoreCase) ||
                                        access.Contains("EC", StringComparison.OrdinalIgnoreCase) ||
                                        access.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
                                        access.Contains("provider", StringComparison.OrdinalIgnoreCase) &&
                                        !access.Contains("unavailable", StringComparison.OrdinalIgnoreCase);

        return usefulSensors || usefulTemperature || usefulFans || usefulWritableProvider || explicitProviderEvidence;
    }

    internal static string DiscoverySummary(AppState state)
    {
        if (!HasUsefulDiscovery(state))
            return "Still learning · wait for hardware discovery to finish or run Hardware setup / Retry detection";

        var parts = new List<string>();
        if (state.CanSensorTelemetry && state.Sensors.Count > 0) parts.Add($"{state.Sensors.Count} sensors");
        if (state.CanFanTelemetry && state.Fans.Count > 0) parts.Add($"{state.Fans.Count} fan source{(state.Fans.Count == 1 ? string.Empty : "s")}");
        if (state.CanKeyboardBacklight) parts.Add("keyboard provider");
        if (state.CanFanControl) parts.Add("verified fan control");
        if (state.CanCpuTemperature && state.ControlTemperatureC.HasValue) parts.Add("control temperature");
        return parts.Count == 0 ? "Useful provider information detected" : string.Join(" · ", parts);
    }

    internal static DeviceSupportReport BuildReport(AppState state, SystemStatusSnapshot system)
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
        body.AppendLine("> Generated locally after ThinkControl found stable hardware/provider information. Serial numbers, Windows usernames, hostnames, paths and raw logs are intentionally excluded.");
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

        string text = body.ToString();
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title + "\n" + text)));
        return new DeviceSupportReport(title, text, fingerprint);
    }

    internal static DeviceSupportReport? PrepareReport(AppState state, SystemStatusSnapshot system)
    {
        if (!HasUsefulDiscovery(state))
            return null;

        DeviceSupportReport report = BuildReport(state, system);
        try
        {
            string folder = PreparedReportFolder();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, PreparedReportFileName);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, report.Body, Encoding.UTF8);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            // Preparation remains available in memory when the local preview cache
            // cannot be updated. Nothing is uploaded by this operation.
        }
        return report;
    }

    internal static void DeletePreparedReport()
    {
        try
        {
            string folder = PreparedReportFolder();
            File.Delete(Path.Combine(folder, PreparedReportFileName));
            File.Delete(Path.Combine(folder, PreparedReportFileName + ".tmp"));
        }
        catch { }
    }

    internal static string BuildIssueUrl(AppState state, SystemStatusSnapshot system) =>
        BuildIssueUrl(BuildReport(state, system));

    internal static string BuildIssueUrl(DeviceSupportReport report) =>
        NewIssueUrl +
        "?title=" + Uri.EscapeDataString(report.Title) +
        "&body=" + Uri.EscapeDataString(report.Body);

    private static bool IsTransient(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Contains("Checking", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Refreshing", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Retrying", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Starting", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Installing", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Verifying", StringComparison.OrdinalIgnoreCase);

    private static string PreparedReportFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThinkControl");

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
