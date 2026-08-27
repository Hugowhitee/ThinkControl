using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

internal sealed record CrashReport(
    string Fingerprint,
    string Version,
    string TimestampUtc,
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly Regex WindowsPath = new(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\t]*", RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThinkControl",
        "Crashes");
    private int _capturing;

    private string PendingPath => Path.Combine(_folder, "pending-crash.json");
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
            string[] events = ReadEventSummary(recorder);
            string[] signatureParts = [
                UpdateService.CurrentVersion,
                Safe(source, 80),
                type,
                .. stack.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(8)
            ];
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", signatureParts))));

            var report = new CrashReport(
                fingerprint,
                UpdateService.CurrentVersion,
                DateTimeOffset.UtcNow.ToString("O"),
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
            string temporary = PendingPath + ".tmp";
            byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, JsonOptions));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, PendingPath, overwrite: true);
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
            try
            {
                if (!File.Exists(PendingPath))
                    return null;
                return JsonSerializer.Deserialize<CrashReport>(File.ReadAllText(PendingPath), JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    internal void ClearPending()
    {
        lock (_gate)
        {
            try { File.Delete(PendingPath); } catch { }
            try { File.Delete(PendingPath + ".tmp"); } catch { }
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
                                         (item.Success ? ":ok" : ":failed") +
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
}
