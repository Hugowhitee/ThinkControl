using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using ThinkControl.Core.Cooling;
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
    double[]? CustomFanThresholds = null,
    FanCurveDefinition[]? FanProfileOverrides = null,
    FanCurveDefinition[]? CustomFanProfiles = null,
    string DolbyProfile = "Dynamic",
    string DolbySubProfile = "Balanced",
    bool AutomaticUpdates = true,
    int BatteryDetailRetentionDays = 7,
    string DefaultOpeningView = "Compact",
    string AttentionAcknowledgedKey = "",
    string AttentionAcknowledgedAtUtc = "");

public sealed class UserSettingsService
{
    private const string PreferencesRegistryPath = @"Software\ThinkControl";
    private const string DiagnosticsConsentValue = "DiagnosticsConsent";
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
        ThinkControlUserSettings loaded = LoadInternal();
        _current = ApplyInstallerConsent(loaded);
        if (_current.DiagnosticsConsent != loaded.DiagnosticsConsent)
            SaveInternal(_current);
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
            SaveDiagnosticsConsentPreference(_current.DiagnosticsConsent);
        }
    }

    private ThinkControlUserSettings LoadInternal()
    {
        string temporary = _path + ".tmp";
        string backup = _path + ".bak";

        // A shutdown or updater handoff can interrupt File.Replace after the fully
        // flushed .tmp file was written. Prefer the newest valid generation instead
        // of blindly accepting an older settings.json and making the restart look
        // like preferences were reset.
        foreach (string candidate in new[] { _path, temporary, backup }
                     .Where(File.Exists)
                     .OrderByDescending(path => File.GetLastWriteTimeUtc(path)))
        {
            if (!TryLoad(candidate, out ThinkControlUserSettings settings))
                continue;
            if (!string.Equals(candidate, _path, StringComparison.OrdinalIgnoreCase))
                TryRestoreSettingsFile(candidate);
            return settings;
        }

        return Sanitize(new ThinkControlUserSettings());
    }

    private static bool TryLoad(string path, out ThinkControlUserSettings settings)
    {
        settings = new ThinkControlUserSettings();
        try
        {
            if (!File.Exists(path))
                return false;
            string json = File.ReadAllText(path);
            ThinkControlUserSettings? parsed = JsonSerializer.Deserialize<ThinkControlUserSettings>(json, JsonOptions);
            if (parsed is null)
                return false;
            settings = Sanitize(parsed);
            return true;
        }
        catch { return false; }
    }

    private void TryRestoreSettingsFile(string source)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.Copy(source, _path, overwrite: true);
        }
        catch { }
    }

    private void SaveInternal(ThinkControlUserSettings settings)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            string temporary = _path + ".tmp";
            byte[] content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings, JsonOptions));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
                File.Replace(temporary, _path, _path + ".bak", ignoreMetadataErrors: true);
            else
                File.Move(temporary, _path);
        }
        catch
        {
        }
    }

    private static ThinkControlUserSettings ApplyInstallerConsent(ThinkControlUserSettings settings)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PreferencesRegistryPath, writable: false);
            int? value = key?.GetValue(DiagnosticsConsentValue) switch
            {
                int number => number,
                string text when int.TryParse(text, out int number) => number,
                _ => null
            };
            return value switch
            {
                0 => settings with { DiagnosticsConsent = DiagnosticsConsent.Disabled },
                1 => settings with { DiagnosticsConsent = DiagnosticsConsent.Enabled },
                _ => settings
            };
        }
        catch
        {
            return settings;
        }
    }

    private static void SaveDiagnosticsConsentPreference(DiagnosticsConsent consent)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(PreferencesRegistryPath, writable: true);
            key.SetValue(DiagnosticsConsentValue, consent == DiagnosticsConsent.Disabled ? 0 : 1, RegistryValueKind.DWord);
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
            0,
            1.0);
        string hardwarePrompt = settings.HardwareSetupPromptedVersion?.Trim() ?? string.Empty;
        if (hardwarePrompt.Length > 64)
            hardwarePrompt = hardwarePrompt[..64];

        string batteryPower = NormalizePowerPreference(settings.BatteryPowerMode);
        string acPower = NormalizePowerPreference(settings.AcPowerMode);

        FanCurveDefinition[] overrides = SanitizeBuiltInOverrides(settings.FanProfileOverrides);
        var customProfiles = SanitizeCustomProfiles(settings.CustomFanProfiles).ToList();
        if (customProfiles.Count == 0 && settings.CoolingProfile?.Trim() == "Custom")
        {
            FanCurveDefinition migrated = MigrateLegacyCustom(settings.CustomFanThresholds);
            customProfiles.Add(migrated);
        }
        string cooling = NormalizeCoolingProfile(settings.CoolingProfile, customProfiles);

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
        string defaultOpeningView = settings.DefaultOpeningView?.Trim() switch
        {
            "Advanced" => "Advanced",
            _ => "Compact"
        };
        string acknowledgedKey = settings.AttentionAcknowledgedKey?.Trim() ?? string.Empty;
        if (acknowledgedKey.Length > 80)
            acknowledgedKey = string.Empty;
        string acknowledgedAt = DateTimeOffset.TryParse(settings.AttentionAcknowledgedAtUtc, out DateTimeOffset parsedAcknowledged)
            ? parsedAcknowledged.ToUniversalTime().ToString("O")
            : string.Empty;

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
            CustomFanThresholds = null,
            FanProfileOverrides = overrides,
            CustomFanProfiles = customProfiles.ToArray(),
            DolbyProfile = dolby,
            DolbySubProfile = dolbyTone,
            BatteryDetailRetentionDays = settings.BatteryDetailRetentionDays switch { <= 7 => 7, <= 14 => 14, _ => 30 },
            DefaultOpeningView = defaultOpeningView,
            AttentionAcknowledgedKey = acknowledgedKey,
            AttentionAcknowledgedAtUtc = acknowledgedAt
        };
    }

    private static FanCurveDefinition[] SanitizeBuiltInOverrides(IReadOnlyList<FanCurveDefinition>? profiles)
    {
        if (profiles is null)
            return [];

        var result = new List<FanCurveDefinition>(3);
        foreach (FanCurveDefinition profile in profiles)
        {
            FanCurveDefinition factory = FanCurveDefaults.ById(profile.Id);
            if (!string.Equals(factory.Id, profile.Id, StringComparison.OrdinalIgnoreCase) ||
                result.Any(existing => string.Equals(existing.Id, factory.Id, StringComparison.OrdinalIgnoreCase)) ||
                !FanCurveGraphPolicy.TryNormalize(profile.Points, out FanCurvePoint[] points, out _))
            {
                continue;
            }
            result.Add(new FanCurveDefinition(factory.Id, factory.Name, points));
        }
        return result.ToArray();
    }

    private static FanCurveDefinition[] SanitizeCustomProfiles(IReadOnlyList<FanCurveDefinition>? profiles)
    {
        if (profiles is null)
            return [];

        var result = new List<FanCurveDefinition>(FanProfileCatalog.MaxCustomProfiles);
        foreach (FanCurveDefinition profile in profiles)
        {
            if (result.Count >= FanProfileCatalog.MaxCustomProfiles)
                break;
            string id = profile.Id?.Trim() ?? string.Empty;
            string name = profile.Name?.Trim() ?? string.Empty;
            if (!id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) || id.Length > 80 ||
                string.IsNullOrWhiteSpace(name) || name.Length > 32 ||
                result.Any(existing => string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)) ||
                !FanCurveGraphPolicy.TryNormalize(profile.Points, out FanCurvePoint[] points, out _))
            {
                continue;
            }
            result.Add(new FanCurveDefinition(id, name, points));
        }
        return result.ToArray();
    }

    private static FanCurveDefinition MigrateLegacyCustom(IReadOnlyList<double>? thresholds)
    {
        if (!FanCurvePolicy.TryValidateCustomThresholds(thresholds, out double[] old, out _))
            return FanCurveDefaults.Balanced with { Id = "custom:migrated", Name = "Custom" };

        FanCurvePoint[] points =
        [
            new(35, 0),
            new(old[0], 16),
            new(old[1], 32),
            new(old[2], 48),
            new(old[3], 64),
            new(old[4], 80),
            new(old[5], 94),
            new(92, 100)
        ];
        Array.Sort(points, (a, b) => a.TemperatureC.CompareTo(b.TemperatureC));
        return FanCurveGraphPolicy.TryNormalize(points, out FanCurvePoint[] normalized, out _)
            ? new FanCurveDefinition("custom:migrated", "Custom", normalized)
            : FanCurveDefaults.Balanced with { Id = "custom:migrated", Name = "Custom" };
    }

    private static string NormalizeCoolingProfile(string? value, IReadOnlyList<FanCurveDefinition> customs)
    {
        string raw = value?.Trim() ?? string.Empty;
        string migrated = raw switch
        {
            "Quiet" or "Silent" => FanCurveDefaults.QuietId,
            "Balanced" or "Normal" => FanCurveDefaults.BalancedId,
            "Max cooling" or "Cool" => FanCurveDefaults.MaxCoolingId,
            "Custom" => customs.FirstOrDefault()?.Id ?? "Lenovo Auto",
            _ => raw
        };

        if (migrated is FanCurveDefaults.QuietId or FanCurveDefaults.BalancedId or FanCurveDefaults.MaxCoolingId)
            return migrated;
        if (migrated.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) &&
            customs.Any(profile => string.Equals(profile.Id, migrated, StringComparison.OrdinalIgnoreCase)))
        {
            return migrated;
        }
        return "Lenovo Auto";
    }

    private static string NormalizePowerPreference(string? value) => value?.Trim() switch
    {
        "Efficiency" or "Quiet" => "Quiet",
        "Balanced" => "Balanced",
        "Performance" => "Performance",
        _ => string.Empty
    };
}
