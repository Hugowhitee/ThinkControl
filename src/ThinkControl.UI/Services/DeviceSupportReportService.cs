using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal enum DeviceSupportPhase
{
    Verified,
    Learning,
    ReadyToShare,
    Shared
}

internal sealed record DeviceSupportReport(string Title, string Body, string Fingerprint);

internal sealed record DeviceSupportStatus(
    DeviceSupportPhase Phase,
    int CompletedChecks,
    int TotalChecks,
    string Label,
    string Detail,
    DeviceSupportReport? Report)
{
    internal bool IsLearning => Phase == DeviceSupportPhase.Learning;
    internal bool IsReady => Phase == DeviceSupportPhase.ReadyToShare;
}

internal static class DeviceSupportReportService
{
    private const string NewIssueUrl = "https://github.com/Hugowhitee/ThinkControl/issues/new";
    private const string PreparedReportFileName = "device-support-report.md";
    private static readonly ConcurrentDictionary<string, DeviceSupportStatus> StatusCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Legacy compatibility hook used by older notification surfaces. It now means
    /// "a materially new report is ready", not merely "the app saw some sensors".
    /// The real lifecycle is maintained by Evaluate().
    /// </summary>
    internal static bool HasUsefulDiscovery(AppState state) =>
        TryGetCached(state)?.Phase == DeviceSupportPhase.ReadyToShare;

    internal static string DiscoverySummary(AppState state) =>
        TryGetCached(state)?.Detail ?? "Compatibility learning has not produced a new report";

    internal static DeviceSupportStatus Evaluate(
        AppState state,
        string? manufacturer,
        DiagnosticsRecorder recorder,
        DiagnosticLifecycleStore lifecycle)
    {
        DeviceValidationState validation = App.GetDeviceValidationState(
            state.MachineType,
            manufacturer,
            state.DeviceName);

        if (validation == DeviceValidationState.Verified)
        {
            var verified = new DeviceSupportStatus(
                DeviceSupportPhase.Verified,
                0,
                0,
                "Supported device",
                "Known profile · compatibility learning is skipped; only bounded local troubleshooting history remains active",
                null);
            Cache(state, verified);
            return verified;
        }

        IReadOnlyList<DiagnosticEvent> events = ReadRecentEvents(recorder);
        bool identityReady = !IsPlaceholder(state.DeviceName) && !IsPlaceholder(state.MachineType);
        bool discoverySettled = !IsTransient(state.DriverStatus ?? string.Empty) &&
                                !IsTransient(state.HardwareAccess ?? string.Empty);
        bool sensorObserved = discoverySettled && (!state.CanSensorTelemetry || state.Sensors.Count > 0);
        bool fanExercise = !state.CanFanControl || HasExercise(events, "FanControl");
        bool keyboardExercise = !state.CanKeyboardBacklight || HasExercise(events, "KeyboardBacklight");

        bool[] checks = [identityReady, discoverySettled, sensorObserved, discoverySettled && fanExercise, discoverySettled && keyboardExercise];
        int completed = checks.Count(value => value);
        const int total = 5;

        DeviceSupportReport report = BuildReport(state, manufacturer, events);
        DeviceSupportPhase phase = completed < total
            ? DeviceSupportPhase.Learning
            : lifecycle.IsHandled(report.Fingerprint)
                ? DeviceSupportPhase.Shared
                : DeviceSupportPhase.ReadyToShare;

        string label = phase switch
        {
            DeviceSupportPhase.Learning => "New device · learning",
            DeviceSupportPhase.ReadyToShare => "Device report ready",
            DeviceSupportPhase.Shared => "Device report shared",
            _ => "Supported device"
        };
        string detail = phase switch
        {
            DeviceSupportPhase.Learning => $"{completed}/{total} checks · learning continues quietly while you use ThinkControl",
            DeviceSupportPhase.ReadyToShare => "Background learning found stable compatibility evidence · review one redacted report",
            DeviceSupportPhase.Shared => "Shared · no new compatibility findings since the current report was handled",
            _ => "Known profile"
        };

        var status = new DeviceSupportStatus(phase, completed, total, label, detail, report);
        Cache(state, status);
        return status;
    }

    internal static DeviceSupportReport BuildReport(
        AppState state,
        string? manufacturer,
        IReadOnlyList<DiagnosticEvent>? recentEvents = null) =>
        BuildReportCore(
            state,
            manufacturer,
            state.DeviceName,
            state.MachineType,
            state.BiosVersion,
            recentEvents ?? Array.Empty<DiagnosticEvent>());

    internal static DeviceSupportReport BuildReport(
        AppState state,
        SystemStatusSnapshot system,
        IReadOnlyList<DiagnosticEvent>? recentEvents = null) =>
        BuildReportCore(
            state,
            system.Manufacturer,
            system.DeviceName,
            system.MachineType,
            system.BiosVersion,
            recentEvents ?? Array.Empty<DiagnosticEvent>());

    private static DeviceSupportReport BuildReportCore(
        AppState state,
        string? manufacturer,
        string? deviceName,
        string? machineType,
        string? biosVersion,
        IReadOnlyList<DiagnosticEvent> recentEvents)
    {
        string machine = Safe(machineType, "unknown");
        string product = Safe(deviceName, "Unknown device");
        string title = $"Device support: {product} ({machine})";

        string[] sensorTypes = state.Sensors
            .Select(sensor => Safe(sensor.SensorType, "Unknown"))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
        string[] providers = ProviderFamilies(state)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] exercised = ExercisedCapabilities(recentEvents)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] failures = recentEvents
            .Where(item => item.Success != true)
            .Select(item => $"{Safe(item.Name, "operation")}:{Safe(item.ErrorCode, "failed")}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(8)
            .ToArray();

        var body = new StringBuilder();
        body.AppendLine("## ThinkControl device support report");
        body.AppendLine();
        body.AppendLine("> Prepared locally from stable capability evidence and normal feature use. Serial numbers, Windows usernames, hostnames, file paths and raw logs are intentionally excluded.");
        body.AppendLine();
        body.AppendLine("### Device");
        body.AppendLine($"- ThinkControl: `{Safe(UpdateService.CurrentVersion, "unknown")}`");
        body.AppendLine($"- Windows: `{Safe(Environment.OSVersion.VersionString, "unknown")}`");
        body.AppendLine($"- Manufacturer: `{Safe(manufacturer, "unknown")}`");
        body.AppendLine($"- Product: `{product}`");
        body.AppendLine($"- Machine type: `{machine}`");
        body.AppendLine($"- BIOS: `{Safe(biosVersion, "unknown")}`");
        body.AppendLine();
        body.AppendLine("### Compatibility evidence");
        body.AppendLine($"- Sensor telemetry: `{YesNo(state.CanSensorTelemetry)}` · types: `{Join(sensorTypes)}`");
        body.AppendLine($"- CPU/control temperature: `{YesNo(state.CanCpuTemperature)}` · source: `{ProviderFamily(state.ControlTemperatureSource)}`");
        body.AppendLine($"- Fan telemetry: `{YesNo(state.CanFanTelemetry)}`");
        body.AppendLine($"- Fan control advertised: `{YesNo(state.CanFanControl)}` · exercised: `{YesNo(exercised.Contains("fan", StringComparer.OrdinalIgnoreCase))}`");
        body.AppendLine($"- Keyboard backlight advertised: `{YesNo(state.CanKeyboardBacklight)}` · exercised: `{YesNo(exercised.Contains("keyboard", StringComparer.OrdinalIgnoreCase))}`");
        body.AppendLine($"- Provider families: `{Join(providers)}`");
        body.AppendLine($"- Recent exercised areas: `{Join(exercised)}`");
        body.AppendLine($"- Grouped recent failures: `{Join(failures)}`");
        body.AppendLine();
        body.AppendLine("### Physical verification");
        body.AppendLine("Add only anything you personally noticed that the automatic evidence cannot prove (for example whether an RPM change matched audible fan speed). Do not paste serial numbers or unrelated logs.");

        string semantic = string.Join("|",
            Safe(manufacturer, "unknown"),
            product,
            machine,
            Safe(biosVersion, "unknown"),
            state.CanSensorTelemetry,
            state.CanCpuTemperature,
            state.CanFanTelemetry,
            state.CanFanControl,
            state.CanKeyboardBacklight,
            Join(sensorTypes),
            Join(providers),
            Join(exercised),
            Join(failures));
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semantic)));
        return new DeviceSupportReport(title, body.ToString(), fingerprint);
    }

    internal static DeviceSupportReport? PrepareReport(AppState state, SystemStatusSnapshot system)
    {
        DeviceSupportReport report = BuildReport(state, system);
        WritePreparedReport(report);
        return report;
    }

    internal static void WritePreparedReport(DeviceSupportReport report)
    {
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
            // The reviewed report remains available in memory. Nothing is uploaded.
        }
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

    private static IReadOnlyList<DiagnosticEvent> ReadRecentEvents(DiagnosticsRecorder recorder)
    {
        var events = new List<DiagnosticEvent>();
        try
        {
            if (!Directory.Exists(recorder.DirectoryPath))
                return events;
            foreach (string file in Directory.EnumerateFiles(recorder.DirectoryPath, "diagnostics-*.jsonl")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                IEnumerable<string> lines;
                try { lines = File.ReadLines(file).ToArray(); }
                catch { continue; }
                foreach (string line in lines)
                {
                    try
                    {
                        DiagnosticEvent? item = JsonSerializer.Deserialize<DiagnosticEvent>(line, JsonOptions);
                        if (item is not null)
                            events.Add(item);
                    }
                    catch { }
                }
            }
        }
        catch { }
        return events.OrderBy(item => item.TimestampUtc).TakeLast(160).ToArray();
    }

    private static bool HasExercise(IEnumerable<DiagnosticEvent> events, string capability) =>
        events.Any(item => item.Success == true && item.ReadBackVerified != false &&
                           string.Equals(item.Capability, capability, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ExercisedCapabilities(IEnumerable<DiagnosticEvent> events)
    {
        foreach (DiagnosticEvent item in events.Where(item => item.Success == true))
        {
            if (string.Equals(item.Capability, "FanControl", StringComparison.OrdinalIgnoreCase)) yield return "fan";
            else if (string.Equals(item.Capability, "KeyboardBacklight", StringComparison.OrdinalIgnoreCase)) yield return "keyboard";
            else if (string.Equals(item.Capability, "ThermalPolicy", StringComparison.OrdinalIgnoreCase)) yield return "thermal policy";
        }
    }

    private static IEnumerable<string> ProviderFamilies(AppState state)
    {
        var values = new List<string> { state.HardwareAccess, state.ControlTemperatureSource };
        values.AddRange(state.Sensors.Select(sensor => sensor.Source));
        values.AddRange(state.Fans.Select(fan => fan.Source));
        if (!string.IsNullOrWhiteSpace(state.KeyboardBackend))
            values.Add(state.KeyboardBackend);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(ProviderFamily)
            .Where(value => value != "Other")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8);
    }

    private static string ProviderFamily(string? value)
    {
        string text = value ?? string.Empty;
        if (text.Contains("LibreHardwareMonitor", StringComparison.OrdinalIgnoreCase)) return "LibreHardwareMonitor";
        if (text.Contains("PawnIO", StringComparison.OrdinalIgnoreCase)) return "PawnIO";
        if (text.Contains("ThinkPad", StringComparison.OrdinalIgnoreCase) && text.Contains("EC", StringComparison.OrdinalIgnoreCase)) return "ThinkPad EC";
        if (text.Contains("Lenovo", StringComparison.OrdinalIgnoreCase)) return "Lenovo";
        if (text.Contains("ACPI", StringComparison.OrdinalIgnoreCase)) return "Windows ACPI";
        if (text.Contains("WMI", StringComparison.OrdinalIgnoreCase)) return "Windows WMI";
        if (text.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        return string.IsNullOrWhiteSpace(text) ? "Unavailable" : "Other";
    }

    private static DeviceSupportStatus? TryGetCached(AppState state)
    {
        string key = CacheKey(state);
        return StatusCache.TryGetValue(key, out DeviceSupportStatus? status) ? status : null;
    }

    private static void Cache(AppState state, DeviceSupportStatus status) => StatusCache[CacheKey(state)] = status;

    private static string CacheKey(AppState state) =>
        !IsPlaceholder(state.MachineType) ? state.MachineType.Trim() : state.DeviceName.Trim();

    private static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) || value is "—" or "unknown" or "Unknown device";

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
        string joined = string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(joined) ? "none" : joined;
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