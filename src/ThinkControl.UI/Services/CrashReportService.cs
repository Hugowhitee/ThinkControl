using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal enum CrashReportState
{
    Pending,
    Opened,
    Reported,
    Dismissed
}

internal sealed record CrashReport(
    string Id,
    string Fingerprint,
    string Version,
    string FirstSeenUtc,
    string LastSeenUtc,
    int OccurrenceCount,
    CrashReportState State,
    string Source,
    string ExceptionType,
    string Message,
    string StackTrace,
    string Product,
    string MachineType,
    string BiosVersion,
    string WindowsVersion,
    string[] RecentEvents);

/// <summary>
/// Persists a compact redacted fatal-crash envelope before the process dies. The
/// report is never uploaded automatically; the next healthy launch owns consent.
/// </summary>
internal sealed class CrashReportService
{
    private const int ResolvedRetentionLimit = 16;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly Regex WindowsPath = new(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\t]*", RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly string _folder;
    private int _capturing;
    private string? _lastCapturedFingerprint;
    private DateTimeOffset _lastCapturedAtUtc;

    internal CrashReportService(string? folder = null)
    {
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl",
            "Crashes");
    }

    private string LegacyPendingPath => Path.Combine(_folder, "pending-crash.json");
    private string RunMarkerPath => Path.Combine(_folder, "active-run.json");

    internal bool BeginRun()
    {
        lock (_gate)
        {
            bool previousUnclean = File.Exists(RunMarkerPath);
            try
            {
                Directory.CreateDirectory(_folder);
                File.WriteAllText(RunMarkerPath, JsonSerializer.Serialize(new
                {
                    startedAtUtc = DateTimeOffset.UtcNow,
                    version = UpdateService.CurrentVersion,
                    processId = Environment.ProcessId
                }, JsonOptions));
            }
            catch { }
            return previousUnclean;
        }
    }

    internal void CompleteCleanRun()
    {
        lock (_gate)
        {
            try { File.Delete(RunMarkerPath); } catch { }
        }
    }

    internal void CaptureFatal(
        string source,
        Exception exception,
        AppState state,
        DiagnosticsRecorder recorder)
    {
        if (Interlocked.Exchange(ref _capturing, 1) != 0)
            return;

        try
        {
            string type = Safe(exception.GetType().FullName ?? exception.GetType().Name, 160);
            string message = Safe(exception.Message, 700);
            string stack = SanitizeStack(exception.ToString());
            string signatureStack = SanitizeStack(exception.StackTrace ?? string.Empty);
            string[] events = ReadEventSummary(recorder);
            string[] signatureParts = [
                type,
                .. signatureStack.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(8)
            ];
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", signatureParts))));

            DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
            if (string.Equals(_lastCapturedFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                capturedAt - _lastCapturedAtUtc < TimeSpan.FromSeconds(5))
            {
                // WPF can surface one fatal exception through both Dispatcher and
                // AppDomain hooks while the same process unwinds. Count that once;
                // the next process has a fresh service instance and increments the
                // persisted occurrence as a genuinely repeated crash.
                return;
            }
            _lastCapturedFingerprint = fingerprint;
            _lastCapturedAtUtc = capturedAt;

            string now = capturedAt.ToString("O");
            CrashReport? existing = ReadAllUnsafe()
                .Where(item => IsUnresolved(item.State))
                .FirstOrDefault(item => string.Equals(item.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
            var report = new CrashReport(
                existing?.Id ?? Guid.NewGuid().ToString("N"),
                fingerprint,
                UpdateService.CurrentVersion,
                existing?.FirstSeenUtc ?? now,
                now,
                Math.Max(1, (existing?.OccurrenceCount ?? 0) + 1),
                CrashReportState.Pending,
                Safe(source, 80),
                type,
                message,
                stack,
                Safe(state.DeviceName, 120),
                Safe(state.MachineType, 40),
                Safe(state.BiosVersion, 80),
                Safe(Environment.OSVersion.VersionString, 120),
                events);

            Directory.CreateDirectory(_folder);
            WriteReportUnsafe(report);
            PruneResolvedUnsafe();
        }
        catch
        {
            // A fatal path must never throw another exception while WPF is unwinding.
        }
        finally
        {
            Volatile.Write(ref _capturing, 0);
        }
    }

    internal CrashReport? TryGetPending()
    {
        lock (_gate)
        {
            MigrateLegacyUnsafe();
            return ReadAllUnsafe()
                .Where(item => IsUnresolved(item.State))
                .OrderByDescending(item => ParseTimestamp(item.LastSeenUtc))
                .FirstOrDefault();
        }
    }

    internal IReadOnlyList<CrashReport> GetUnresolved()
    {
        lock (_gate)
        {
            MigrateLegacyUnsafe();
            return ReadAllUnsafe()
                .Where(item => IsUnresolved(item.State))
                .OrderByDescending(item => ParseTimestamp(item.LastSeenUtc))
                .ToArray();
        }
    }

    internal void MarkOpened(string id) => SetState(id, CrashReportState.Opened);

    internal void MarkReported(string id) => SetState(id, CrashReportState.Reported);

    internal void Dismiss(string id) => SetState(id, CrashReportState.Dismissed);

    internal void ClearAll()
    {
        lock (_gate)
        {
            try
            {
                if (Directory.Exists(_folder))
                {
                    foreach (string path in Directory.EnumerateFiles(_folder, "crash-*.json"))
                        File.Delete(path);
                }
            }
            catch { }
            try { File.Delete(LegacyPendingPath); } catch { }
            try { File.Delete(LegacyPendingPath + ".tmp"); } catch { }
        }
    }

    internal string BuildIssueUrl(CrashReport report)
    {
        string title = $"Crash: {report.ExceptionType.Split('.').LastOrDefault() ?? "ThinkControl"} ({report.MachineType})";
        var body = new StringBuilder();
        body.AppendLine("## ThinkControl crash report");
        body.AppendLine();
        body.AppendLine("> Prepared locally after an unexpected termination. Review before submitting. Usernames, hostnames and local file paths are redacted; nothing is uploaded automatically.");
        body.AppendLine();
        body.AppendLine("### Environment");
        body.AppendLine($"- ThinkControl: `{report.Version}`");
        body.AppendLine($"- Windows: `{report.WindowsVersion}`");
        body.AppendLine($"- Device: `{report.Product}` (`{report.MachineType}`)");
        body.AppendLine($"- BIOS: `{report.BiosVersion}`");
        body.AppendLine($"- Crash source: `{report.Source}`");
        body.AppendLine($"- Signature: `{report.Fingerprint[..Math.Min(16, report.Fingerprint.Length)]}`");
        body.AppendLine($"- Occurrences preserved locally: `{report.OccurrenceCount}`");
        body.AppendLine();
        body.AppendLine("### Exception");
        body.AppendLine($"`{report.ExceptionType}`: {report.Message}");
        body.AppendLine();
        body.AppendLine("```text");
        body.AppendLine(report.StackTrace);
        body.AppendLine("```");
        if (report.RecentEvents.Length > 0)
        {
            body.AppendLine();
            body.AppendLine("### Recent redacted app events");
            foreach (string item in report.RecentEvents.TakeLast(16))
                body.AppendLine("- `" + item.Replace('`', '\'') + "`");
        }
        body.AppendLine();
        body.AppendLine("### What were you doing?");
        body.AppendLine("Please add the last action you remember before ThinkControl closed.");

        return "https://github.com/Hugowhitee/ThinkControl/issues/new?title=" +
               Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(body.ToString());
    }

    private static string[] ReadEventSummary(DiagnosticsRecorder recorder)
    {
        var result = new List<string>();
        try
        {
            if (!Directory.Exists(recorder.DirectoryPath))
                return [];
            foreach (string file in Directory.EnumerateFiles(recorder.DirectoryPath, "diagnostics-*.jsonl")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                IEnumerable<string> lines;
                try { lines = File.ReadLines(file).TakeLast(80).ToArray(); }
                catch { continue; }
                foreach (string line in lines)
                {
                    try
                    {
                        DiagnosticEvent? item = JsonSerializer.Deserialize<DiagnosticEvent>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        if (item is null)
                            continue;
                        string summary = item.Name +
                                         (item.Success == true ? ":ok" : ":failed") +
                                         (string.IsNullOrWhiteSpace(item.ErrorCode) ? string.Empty : ":" + item.ErrorCode);
                        result.Add(Safe(summary, 180));
                    }
                    catch { }
                }
            }
        }
        catch { }
        return result.TakeLast(24).ToArray();
    }

    private static string SanitizeStack(string value)
    {
        string safe = Safe(value, 12_000);
        safe = WindowsPath.Replace(safe, "[path]");
        string[] lines = safe.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Take(28)
            .ToArray();
        return string.Join(Environment.NewLine, lines);
    }

    private static string Safe(string? value, int maxLength)
    {
        string text = value ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(Environment.UserName))
            text = text.Replace(Environment.UserName, "[redacted]", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(Environment.MachineName))
            text = text.Replace(Environment.MachineName, "[redacted]", StringComparison.OrdinalIgnoreCase);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            text = text.Replace(home, "[user]", StringComparison.OrdinalIgnoreCase);
        text = new string(text.Where(ch => !char.IsControl(ch) || ch is '\r' or '\n' or '\t').ToArray()).Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private void SetState(string id, CrashReportState state)
    {
        lock (_gate)
        {
            CrashReport? report = ReadAllUnsafe().FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (report is null)
                return;
            WriteReportUnsafe(report with { State = state });
            PruneResolvedUnsafe();
        }
    }

    private IReadOnlyList<CrashReport> ReadAllUnsafe()
    {
        var reports = new List<CrashReport>();
        try
        {
            if (!Directory.Exists(_folder))
                return reports;
            foreach (string path in Directory.EnumerateFiles(_folder, "crash-*.json"))
            {
                try
                {
                    CrashReport? report = JsonSerializer.Deserialize<CrashReport>(File.ReadAllText(path), JsonOptions);
                    if (report is not null && !string.IsNullOrWhiteSpace(report.Id))
                        reports.Add(report);
                }
                catch { }
            }
        }
        catch { }
        return reports;
    }

    private void WriteReportUnsafe(CrashReport report)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, $"crash-{report.Id}.json");
        string temporary = path + ".tmp";
        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, JsonOptions));
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
        {
            stream.Write(payload);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }

    private void PruneResolvedUnsafe()
    {
        CrashReport[] resolved = ReadAllUnsafe()
            .Where(item => !IsUnresolved(item.State))
            .OrderByDescending(item => ParseTimestamp(item.LastSeenUtc))
            .Skip(ResolvedRetentionLimit)
            .ToArray();
        foreach (CrashReport report in resolved)
        {
            try { File.Delete(Path.Combine(_folder, $"crash-{report.Id}.json")); } catch { }
        }
    }

    private void MigrateLegacyUnsafe()
    {
        if (!File.Exists(LegacyPendingPath))
            return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(LegacyPendingPath));
            JsonElement root = document.RootElement;
            string timestamp = ReadLegacyString(root, "timestampUtc", DateTimeOffset.UtcNow.ToString("O"));
            var migrated = new CrashReport(
                Guid.NewGuid().ToString("N"),
                ReadLegacyString(root, "fingerprint", Guid.NewGuid().ToString("N")),
                ReadLegacyString(root, "version", "unknown"),
                timestamp,
                timestamp,
                1,
                CrashReportState.Pending,
                ReadLegacyString(root, "source", "legacy"),
                ReadLegacyString(root, "exceptionType", "System.Exception"),
                ReadLegacyString(root, "message", string.Empty),
                ReadLegacyString(root, "stackTrace", string.Empty),
                ReadLegacyString(root, "product", string.Empty),
                ReadLegacyString(root, "machineType", string.Empty),
                ReadLegacyString(root, "biosVersion", string.Empty),
                ReadLegacyString(root, "windowsVersion", string.Empty),
                root.TryGetProperty("recentEvents", out JsonElement recent) && recent.ValueKind == JsonValueKind.Array
                    ? recent.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                    : []);
            WriteReportUnsafe(migrated);
            File.Delete(LegacyPendingPath);
        }
        catch
        {
            // Preserve an unreadable legacy record for manual recovery.
        }
    }

    private static string ReadLegacyString(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static bool IsUnresolved(CrashReportState state) =>
        state is CrashReportState.Pending or CrashReportState.Opened;

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : DateTimeOffset.MinValue;
}
