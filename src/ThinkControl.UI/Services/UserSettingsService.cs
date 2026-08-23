using System.IO;
using System.Text.Json;

namespace ThinkControl.UI.Services;

public sealed record ThinkControlUserSettings(
    ThemeMode Theme = ThemeMode.System,
    bool RefreshAuto = true,
    string KeyboardMode = "Auto",
    string KeyboardBaseLevel = "High",
    string KeyboardStaticLevel = "High",
    double KeyboardEffectSpeed = 1.0);

public sealed class UserSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;
    private ThinkControlUserSettings _current;

    public UserSettingsService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl");
        _path = Path.Combine(folder, "settings.json");
        _current = LoadInternal();
    }

    public ThinkControlUserSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public void Update(Func<ThinkControlUserSettings, ThinkControlUserSettings> update)
    {
        lock (_gate)
        {
            _current = Sanitize(update(_current));
            SaveInternal(_current);
        }
    }

    private ThinkControlUserSettings LoadInternal()
    {
        try
        {
            if (!File.Exists(_path))
                return new ThinkControlUserSettings();

            string json = File.ReadAllText(_path);
            ThinkControlUserSettings? settings = JsonSerializer.Deserialize<ThinkControlUserSettings>(json, JsonOptions);
            return Sanitize(settings ?? new ThinkControlUserSettings());
        }
        catch
        {
            return new ThinkControlUserSettings();
        }
    }

    private void SaveInternal(ThinkControlUserSettings settings)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            // Preferences are best-effort. A read-only profile must never prevent
            // ThinkControl from starting or controlling verified hardware safely.
        }
    }

    private static ThinkControlUserSettings Sanitize(ThinkControlUserSettings settings)
    {
        string mode = settings.KeyboardMode switch
        {
            "Static" or "Auto" or "Breathing" or "Reactive" or "Audio" => settings.KeyboardMode,
            _ => "Auto"
        };
        string baseLevel = settings.KeyboardBaseLevel == "Low" ? "Low" : "High";
        string staticLevel = settings.KeyboardStaticLevel switch
        {
            "Off" => "Off",
            "Low" => "Low",
            _ => "High"
        };
        double speed = Math.Clamp(settings.KeyboardEffectSpeed, 0.5, 2.0);
        return settings with
        {
            KeyboardMode = mode,
            KeyboardBaseLevel = baseLevel,
            KeyboardStaticLevel = staticLevel,
            KeyboardEffectSpeed = speed
        };
    }
}
