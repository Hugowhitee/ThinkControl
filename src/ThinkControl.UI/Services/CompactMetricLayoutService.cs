using System.IO;
using System.Text.Json;

namespace ThinkControl.UI.Services;

internal sealed class CompactMetricLayoutService
{
    private static readonly string[] DefaultLayout = ["Battery", "CPU", "Fans"];
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Battery", "CPU", "Fans", "Power", "Sensors", "Display", "Keyboard", "Performance"
    };

    private readonly string _path;
    private static readonly string[] DefaultControls = ["Performance", "Fans", "Display", "Mode"];
    internal static readonly string[] AvailableControls = ["Performance", "Fans", "Display", "Keyboard", "Mode", "Automation"];
    private sealed record LayoutDocument(int Schema, string[] Metrics, string[] Controls);
    private bool IsTransient => System.Windows.Application.Current is App { IsVisualQa: true };

    internal CompactMetricLayoutService(string? path = null)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl");
        _path = path ?? Path.Combine(folder, "compact-layout.json");
    }

    internal string[] Load()
    {
        try
        {
            if (IsTransient || !File.Exists(_path))
                return [.. DefaultLayout];
            return ReadDocument().Metrics;
        }
        catch
        {
            return [.. DefaultLayout];
        }
    }

    internal void Save(IReadOnlyList<string> values)
    {
        if (IsTransient) return;
        var previous = ReadDocument();
        string[] clean = Sanitize(values);
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(previous with { Metrics = clean }));
            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            // Layout customization is cosmetic; never block the compact window.
        }
    }

    private LayoutDocument ReadDocument()
    {
        var fallback = new LayoutDocument(3, [.. DefaultLayout], [.. DefaultControls]);
        try
        {
            if (IsTransient || !File.Exists(_path)) return fallback;
            string json = File.ReadAllText(_path);
            if (json.TrimStart().StartsWith('[')) return fallback with { Metrics = Sanitize(JsonSerializer.Deserialize<string[]>(json)) };
            var saved = JsonSerializer.Deserialize<LayoutDocument>(json);
            if (saved is null) return fallback;
            return fallback with { Metrics = Sanitize(saved.Metrics), Controls = SanitizeControls(saved.Schema < 3 ? saved.Controls?.Select(id => id == "Keyboard" ? "Mode" : id).ToArray() : saved.Controls) };
        }
        catch { return fallback; }
    }

    internal string[] LoadControls() => ReadDocument().Controls;
    internal void SaveControls(IReadOnlyList<string> values)
    {
        if (IsTransient) return;
        try
        {
            var saved = ReadDocument() with { Controls = SanitizeControls(values) };
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(saved));
            File.Move(_path + ".tmp", _path, true);
        }
        catch { }
    }
    private static string[] SanitizeControls(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count != 4 || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 || values.Any(v => !AvailableControls.Contains(v, StringComparer.OrdinalIgnoreCase))) return [.. DefaultControls];
        return values.Select(v => AvailableControls.First(d => d.Equals(v, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
    private static string[] Sanitize(IReadOnlyList<string>? values)
    {
        if (values is null)
            return [.. DefaultLayout];

        string[] clean = values
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(Allowed.Contains)
            .Select(value => Allowed.First(id => id.Equals(value, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        if (clean.Length != 3)
            return [.. DefaultLayout];
        return clean;
    }
}
