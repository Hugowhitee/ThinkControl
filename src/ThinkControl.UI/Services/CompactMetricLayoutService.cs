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

    internal CompactMetricLayoutService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl");
        _path = Path.Combine(folder, "compact-layout.json");
    }

    internal string[] Load()
    {
        try
        {
            if (!File.Exists(_path))
                return [.. DefaultLayout];
            string[]? saved = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_path));
            return Sanitize(saved);
        }
        catch
        {
            return [.. DefaultLayout];
        }
    }

    internal void Save(IReadOnlyList<string> values)
    {
        string[] clean = Sanitize(values);
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(clean));
            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            // Layout customization is cosmetic; never block the compact window.
        }
    }

    private static string[] Sanitize(IReadOnlyList<string>? values)
    {
        if (values is null)
            return [.. DefaultLayout];

        string[] clean = values
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(Allowed.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        if (clean.Length != 3)
            return [.. DefaultLayout];
        return clean;
    }
}
