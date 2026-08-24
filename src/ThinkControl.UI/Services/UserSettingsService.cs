using System.IO;
using System.Text.Json;
using ThinkControl.Core.Diagnostics;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services;

public sealed record ThinkControlUserSettings(
    ThemeMode Theme = ThemeMode.System,
    bool RefreshAuto = true,
    string KeyboardMode = "Auto",
    string KeyboardBaseLevel = "High",
    string KeyboardStaticLevel = "High",
    double KeyboardEffectSpeed = 1.0,
    DiagnosticsConsent DiagnosticsConsent = DiagnosticsConsent.Enabled,
    TouchpadGestureConfiguration? TouchpadGestures = null,
    bool TouchpadOsdEnabled = true,
    double TouchpadOsdOpacity = 0.92,
    string TouchpadOsdPosition = "Center",
    string HardwareSetupPromptedVersion = "",
    string BatteryPowerMode = "",
    string AcPowerMode = "",
    string CoolingProfile = "Lenovo Auto",
    string DolbyProfile = "Dynamic",
    string DolbySubProfile = "Balanced",
    bool AutomaticUpdates = true);

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
                return Sanitize(new ThinkControlUserSettings());

            string json = File.ReadAllText(_path);
            ThinkControlUserSettings? settings = JsonSerializer.Deserialize<ThinkControlUserSettings>(json, JsonOptions);
            return Sanitize(settings ?? new ThinkControlUserSettings());
        }
        catch
        {
            return Sanitize(new ThinkControlUserSettings());
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
        DiagnosticsConsent consent = settings.DiagnosticsConsent switch
        {
            DiagnosticsConsent.Disabled => DiagnosticsConsent.Disabled,
            _ => DiagnosticsConsent.Enabled
        };
        TouchpadGestureConfiguration touchpad = (settings.TouchpadGestures ??
            (TouchpadGestureConfiguration.Default with { Enabled = false })).Sanitize();
        string osdPosition = settings.TouchpadOsdPosition?.Trim() switch
        {
            "Left" => "Left",
            "Right" => "Right",
            _ => "Center"
        };
        double osdOpacity = Math.Clamp(
            double.IsFinite(settings.TouchpadOsdOpacity) ? settings.TouchpadOsdOpacity : 0.92,
            0.65,
            1.0);
        string hardwarePrompt = settings.HardwareSetupPromptedVersion?.Trim() ?? string.Empty;
        if (hardwarePrompt.Length > 64)
            hardwarePrompt = hardwarePrompt[..64];

        string batteryPower = NormalizePowerPreference(settings.BatteryPowerMode);
        string acPower = NormalizePowerPreference(settings.AcPowerMode);
        string cooling = settings.CoolingProfile?.Trim() switch
        {
            "Silent" => "Silent",
            "Normal" => "Normal",
            "Cool" => "Cool",
            _ => "Lenovo Auto"
        };
        string dolby = settings.DolbyProfile?.Trim() switch
        {
            "Movie" => "Movie",
            "Music" => "Music",
            "Game" => "Game",
            "Voice" => "Voice",
            _ => "Dynamic"
        };
        string dolbyTone = settings.DolbySubProfile?.Trim() switch
        {
            "Detailed" => "Detailed",
            "Warm" => "Warm",
            "Off" => "Off",
            _ => "Balanced"
        };

        return settings with
        {
            KeyboardMode = mode,
            KeyboardBaseLevel = baseLevel,
            KeyboardStaticLevel = staticLevel,
            KeyboardEffectSpeed = speed,
            DiagnosticsConsent = consent,
            TouchpadGestures = touchpad,
            TouchpadOsdOpacity = osdOpacity,
            TouchpadOsdPosition = osdPosition,
            HardwareSetupPromptedVersion = hardwarePrompt,
            BatteryPowerMode = batteryPower,
            AcPowerMode = acPower,
            CoolingProfile = cooling,
            DolbyProfile = dolby,
            DolbySubProfile = dolbyTone
        };
    }

    private static string NormalizePowerPreference(string? value) => value?.Trim() switch
    {
        "Efficiency" or "Quiet" => "Quiet",
        "Balanced" => "Balanced",
        "Performance" => "Performance",
        _ => string.Empty
    };
}
