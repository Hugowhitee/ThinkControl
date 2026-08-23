using System.IO;
using System.Text.Json;
using ThinkControl.Core.Diagnostics;

namespace ThinkControl.UI.Services;

public sealed class DiagnosticsRecorder
{
    private const long MaxFileBytes = 1 * 1024 * 1024;
    private const int MaxFiles = 3;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private readonly string _directory;

    public DiagnosticsRecorder()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl",
            "Diagnostics");
    }

    public string DirectoryPath => _directory;

    public int LocalEventCount
    {
        get
        {
            lock (_gate)
            {
                CleanupLocked();
                int count = 0;
                foreach (string file in EnumerateFilesLocked())
                {
                    try { count += File.ReadLines(file).Take(DiagnosticsPolicy.MaximumEventsPerBundle + 1).Count(); }
                    catch { }
                    if (count >= DiagnosticsPolicy.MaximumEventsPerBundle)
                        return DiagnosticsPolicy.MaximumEventsPerBundle;
                }
                return count;
            }
        }
    }

    public DateTimeOffset? LastEventAtUtc
    {
        get
        {
            lock (_gate)
            {
                CleanupLocked();
                FileInfo? newest = EnumerateFilesLocked()
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault();
                return newest is null ? null : new DateTimeOffset(newest.LastWriteTimeUtc, TimeSpan.Zero);
            }
        }
    }

    public void Record(DiagnosticEvent diagnosticEvent)
    {
        DiagnosticEvent safe = Sanitize(diagnosticEvent);
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            CleanupLocked();
            string path = GetWritableFileLocked();
            File.AppendAllText(path, JsonSerializer.Serialize(safe, JsonOptions) + Environment.NewLine);
        }
    }

    public DiagnosticBundle CreateBundle(
        DiagnosticDeviceInfo device,
        string version,
        string channel,
        string windowsVersion)
    {
        lock (_gate)
        {
            CleanupLocked();
            List<DiagnosticEvent> events = new();
            foreach (string file in EnumerateFilesLocked().OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                foreach (string line in SafeReadLines(file))
                {
                    try
                    {
                        DiagnosticEvent? item = JsonSerializer.Deserialize<DiagnosticEvent>(line, JsonOptions);
                        if (item is not null)
                            events.Add(Sanitize(item));
                    }
                    catch (JsonException)
                    {
                    }
                }
            }

            IReadOnlyList<DiagnosticEvent> bounded = events
                .OrderBy(item => item.TimestampUtc)
                .TakeLast(DiagnosticsPolicy.MaximumEventsPerBundle)
                .ToArray();

            return new DiagnosticBundle(
                DiagnosticsPolicy.SchemaVersion,
                version,
                channel,
                SanitizeSimple(windowsVersion, 120),
                SanitizeDevice(device),
                DateTimeOffset.UtcNow,
                bounded);
        }
    }

    public string ExportBundle(
        string destinationPath,
        DiagnosticDeviceInfo device,
        string version,
        string channel,
        string windowsVersion)
    {
        DiagnosticBundle bundle = CreateBundle(device, version, channel, windowsVersion);
        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(bundle, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
        return destinationPath;
    }

    public string PreviewJson(
        DiagnosticDeviceInfo device,
        string version,
        string channel,
        string windowsVersion)
    {
        DiagnosticBundle bundle = CreateBundle(device, version, channel, windowsVersion);
        return JsonSerializer.Serialize(bundle, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });
    }

    public void DeleteLocal()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_directory))
                return;

            try { Directory.Delete(_directory, recursive: true); }
            catch
            {
                foreach (string file in EnumerateFilesLocked())
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
    }

    private string GetWritableFileLocked()
    {
        string today = DateTime.UtcNow.ToString("yyyyMMdd");
        for (int index = 0; index < MaxFiles; index++)
        {
            string path = Path.Combine(_directory, $"diagnostics-{today}-{index + 1}.jsonl");
            if (!File.Exists(path) || new FileInfo(path).Length < MaxFileBytes)
                return path;
        }

        string oldest = EnumerateFilesLocked()
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.LastWriteTimeUtc)
            .First().FullName;
        try { File.Delete(oldest); } catch { }
        return Path.Combine(_directory, $"diagnostics-{today}-{MaxFiles}.jsonl");
    }

    private void CleanupLocked()
    {
        if (!Directory.Exists(_directory))
            return;

        DateTime cutoff = DateTime.UtcNow - MaxAge;
        foreach (string file in Directory.EnumerateFiles(_directory, "diagnostics-*.jsonl"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch { }
        }

        FileInfo[] files = Directory.EnumerateFiles(_directory, "diagnostics-*.jsonl")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
        foreach (FileInfo extra in files.Skip(MaxFiles))
        {
            try { extra.Delete(); } catch { }
        }
    }

    private IEnumerable<string> EnumerateFilesLocked() =>
        Directory.Exists(_directory)
            ? Directory.EnumerateFiles(_directory, "diagnostics-*.jsonl")
            : Array.Empty<string>();

    private static IEnumerable<string> SafeReadLines(string path)
    {
        try { return File.ReadLines(path).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private static DiagnosticEvent Sanitize(DiagnosticEvent item)
    {
        IReadOnlyDictionary<string, string>? tags = item.Tags?
            .Where(pair => DiagnosticsPolicy.AllowedTags.Contains(pair.Key))
            .Take(16)
            .ToDictionary(
                pair => SanitizeToken(pair.Key, 48),
                pair => SanitizeSimple(pair.Value, 120),
                StringComparer.OrdinalIgnoreCase);

        return item with
        {
            TimestampUtc = new DateTimeOffset(item.TimestampUtc.UtcDateTime.AddTicks(-(item.TimestampUtc.UtcTicks % TimeSpan.TicksPerSecond)), TimeSpan.Zero),
            Name = SanitizeToken(item.Name, 80),
            Capability = item.Capability is null ? null : SanitizeToken(item.Capability, 64),
            Provider = item.Provider is null ? null : SanitizeToken(item.Provider, 80),
            ErrorCode = item.ErrorCode is null ? null : SanitizeToken(item.ErrorCode, 80),
            DurationMs = item.DurationMs.HasValue ? Math.Clamp(item.DurationMs.Value, 0, 600_000) : null,
            FanLevel = item.FanLevel.HasValue ? Math.Clamp(item.FanLevel.Value, 0, 255) : null,
            FanRpm = item.FanRpm.HasValue ? Math.Clamp(item.FanRpm.Value, 0, 20_000) : null,
            TemperatureC = item.TemperatureC.HasValue ? Math.Clamp(item.TemperatureC.Value, -20, 150) : null,
            Tags = tags
        };
    }

    private static DiagnosticDeviceInfo SanitizeDevice(DiagnosticDeviceInfo device) => device with
    {
        Manufacturer = SanitizeSimple(device.Manufacturer, 80),
        ProductName = SanitizeSimple(device.ProductName, 120),
        MachineType = device.MachineType is null ? null : SanitizeToken(device.MachineType, 32),
        BiosVersion = device.BiosVersion is null ? null : SanitizeToken(device.BiosVersion, 64)
    };

    private static string SanitizeToken(string value, int maxLength)
    {
        string token = new(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ':' or '/').ToArray());
        return token.Length <= maxLength ? token : token[..maxLength];
    }

    private static string SanitizeSimple(string value, int maxLength)
    {
        char directorySeparator = Path.DirectorySeparatorChar;
        string sanitized = value.Replace(Environment.UserName, "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace(Environment.MachineName, "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace(directorySeparator, '/');

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace(directorySeparator, '/');
        if (!string.IsNullOrWhiteSpace(home))
            sanitized = sanitized.Replace(home, "[user]", StringComparison.OrdinalIgnoreCase);

        sanitized = new string(sanitized.Where(ch => !char.IsControl(ch) || ch == (char)32).ToArray()).Trim();
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }
}
