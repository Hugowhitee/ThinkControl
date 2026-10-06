using System.IO;
using ThinkControl.Core.Audio;

namespace ThinkControl.UI.Services;

public sealed record ThinkControlModeTrigger(
    string Type,
    string Value = "",
    int Number = 0,
    string StartTime = "",
    string EndTime = "",
    int DaysMask = 127,
    bool Enabled = true);

public sealed record ThinkControlModeDefinition(
    string Id,
    string Name,
    string? AudioSafety = null,
    bool? TouchpadGesturesEnabled = null,
    string? KeyboardLight = null,
    string? PerformanceMode = null,
    string? CoolingProfile = null,
    string? RefreshRate = null,
    ThinkControlModeTrigger[]? Triggers = null,
    bool AutomationEnabled = false,
    bool MatchAllTriggers = false,
    int AutomationPriority = 0);

internal enum ThinkControlModeFacet
{
    PerformanceMode,
    CoolingProfile,
    RefreshRate,
    AudioSafety,
    TouchpadGestures,
    KeyboardLight
}

internal enum ThinkControlModeActivationOrigin
{
    Manual,
    Automatic,
    Restore
}

internal sealed record KeyboardModeSnapshot(
    string Mode,
    string StaticLevel,
    string BaseLevel);

internal sealed record RefreshModeSnapshot(
    bool Auto,
    int RefreshHz);

internal static class ThinkControlModeCatalog
{
    internal const int MaxCustomModes = 12;
    internal const int MaxTriggersPerMode = 8;
    internal const string NormalId = "normal";
    internal const string GestureLockId = "gesture-lock";
    internal const string SilentId = "silent";

    internal static readonly ThinkControlModeDefinition NoMode =
        new(NormalId, "No mode");

    // Gesture lock and Silent remain readable for settings/backward compatibility,
    // but alpha.55 no longer presents them as first-class modes. They are settings
    // a real user mode can compose.
    internal static readonly IReadOnlyList<ThinkControlModeDefinition> LegacyBuiltIns =
    [
        new(GestureLockId, "Gesture lock", AudioSafety: "GestureLock"),
        new(SilentId, "Silent", AudioSafety: "Silent")
    ];

    // Compatibility surface for snapshot/tests that still need to render legacy
    // audio-safety states. The actual alpha.55 Modes UI uses VisibleModes instead.
    internal static readonly IReadOnlyList<ThinkControlModeDefinition> BuiltIns =
        [NoMode, .. LegacyBuiltIns];

    internal static readonly IReadOnlyList<ThinkControlModeDefinition> StarterModes =
    [
        new(
            "custom:focus",
            "Focus",
            TouchpadGesturesEnabled: false,
            KeyboardLight: "Low",
            PerformanceMode: "Efficiency",
            CoolingProfile: "Quiet",
            RefreshRate: "60 Hz"),
        new(
            "custom:battery-saver",
            "Battery saver",
            PerformanceMode: "Efficiency",
            CoolingProfile: "Quiet",
            RefreshRate: "60 Hz",
            KeyboardLight: "Off",
            Triggers: [new ThinkControlModeTrigger("BatteryBelow", Number: 25)],
            AutomationEnabled: true),
        new(
            "custom:performance",
            "Performance",
            KeyboardLight: "Auto",
            PerformanceMode: "Performance",
            CoolingProfile: "Balanced",
            RefreshRate: "Max")
    ];

    internal static ThinkControlModeDefinition[] SeedStarterModes(
        IReadOnlyList<ThinkControlModeDefinition>? existing)
    {
        var result = SanitizeCustomModes(existing).ToList();
        foreach (ThinkControlModeDefinition starter in StarterModes)
        {
            if (result.Count >= MaxCustomModes)
                break;
            if (result.Any(mode =>
                    mode.Id.Equals(starter.Id, StringComparison.OrdinalIgnoreCase) ||
                    mode.Name.Equals(starter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(starter with
            {
                Triggers = starter.Triggers?.Select(trigger => trigger with { }).ToArray()
            });
        }
        return result.ToArray();
    }

    internal static ThinkControlModeDefinition CreateStarterTemplate(string template, string id)
    {
        ThinkControlModeDefinition source = template switch
        {
            "focus" => StarterModes[0],
            "battery" => StarterModes[1],
            "performance" => StarterModes[2],
            _ => new ThinkControlModeDefinition(id, string.Empty)
        };
        return source with
        {
            Id = id,
            Triggers = source.Triggers?.Select(trigger => trigger with { }).ToArray()
        };
    }

    internal static IReadOnlyList<ThinkControlModeDefinition> VisibleModes(
        IReadOnlyList<ThinkControlModeDefinition>? customs) =>
        [NoMode, .. (customs ?? [])];

    internal static ThinkControlModeDefinition? Find(
        string? id,
        IReadOnlyList<ThinkControlModeDefinition>? customs)
    {
        string key = id?.Trim() ?? string.Empty;
        if (NoMode.Id.Equals(key, StringComparison.OrdinalIgnoreCase))
            return NoMode;

        ThinkControlModeDefinition? legacy = LegacyBuiltIns.FirstOrDefault(mode =>
            mode.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (legacy is not null)
            return legacy;

        return customs?.FirstOrDefault(mode =>
            mode.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    internal static IEnumerable<ThinkControlModeFacet> Facets(ThinkControlModeDefinition mode)
    {
        if (mode.PerformanceMode is not null)
            yield return ThinkControlModeFacet.PerformanceMode;
        if (mode.CoolingProfile is not null)
            yield return ThinkControlModeFacet.CoolingProfile;
        if (mode.RefreshRate is not null)
            yield return ThinkControlModeFacet.RefreshRate;
        if (mode.AudioSafety is not null)
            yield return ThinkControlModeFacet.AudioSafety;
        if (mode.TouchpadGesturesEnabled.HasValue)
            yield return ThinkControlModeFacet.TouchpadGestures;
        if (mode.KeyboardLight is not null)
            yield return ThinkControlModeFacet.KeyboardLight;
    }

    internal static ThinkControlModeDefinition[] SanitizeCustomModes(
        IReadOnlyList<ThinkControlModeDefinition>? modes)
    {
        if (modes is null)
            return [];

        var result = new List<ThinkControlModeDefinition>(MaxCustomModes);
        foreach (ThinkControlModeDefinition mode in modes)
        {
            if (result.Count >= MaxCustomModes)
                break;

            ThinkControlModeDefinition? sanitized = SanitizeCustomMode(mode);
            if (sanitized is null ||
                result.Any(existing =>
                    existing.Id.Equals(sanitized.Id, StringComparison.OrdinalIgnoreCase) ||
                    existing.Name.Equals(sanitized.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(sanitized);
        }

        return result.ToArray();
    }

    internal static ThinkControlModeDefinition? SanitizeCustomMode(ThinkControlModeDefinition? mode)
    {
        if (mode is null)
            return null;

        string id = mode.Id?.Trim() ?? string.Empty;
        string name = mode.Name?.Trim() ?? string.Empty;
        if (!id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) ||
            id.Length > 80 ||
            string.IsNullOrWhiteSpace(name) ||
            name.Length > 32 ||
            name.Equals(NoMode.Name, StringComparison.OrdinalIgnoreCase) ||
            LegacyBuiltIns.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string? audio = mode.AudioSafety?.Trim() switch
        {
            "Normal" => "Normal",
            "GestureLock" or "MediaLock" => "GestureLock",
            "Silent" => "Silent",
            _ => null
        };
        string? keyboard = mode.KeyboardLight?.Trim() switch
        {
            "Off" => "Off",
            "Low" => "Low",
            "High" => "High",
            "Auto" => "Auto",
            _ => null
        };
        string? performance = mode.PerformanceMode?.Trim() switch
        {
            "Quiet" or "Efficiency" => "Efficiency",
            "Balanced" => "Balanced",
            "Performance" => "Performance",
            _ => null
        };
        string? cooling = SanitizeCoolingProfile(mode.CoolingProfile);
        string? refresh = mode.RefreshRate?.Trim() switch
        {
            "Auto" => "Auto",
            "60" or "60 Hz" => "60 Hz",
            "Max" => "Max",
            _ => null
        };

        ThinkControlModeTrigger[] triggers = SanitizeTriggers(mode.Triggers);
        bool hasSetting =
            audio is not null ||
            mode.TouchpadGesturesEnabled.HasValue ||
            keyboard is not null ||
            performance is not null ||
            cooling is not null ||
            refresh is not null;
        if (!hasSetting)
            return null;

        return new ThinkControlModeDefinition(
            id,
            name,
            audio,
            mode.TouchpadGesturesEnabled,
            keyboard,
            performance,
            cooling,
            refresh,
            triggers,
            mode.AutomationEnabled && triggers.Length > 0,
            mode.MatchAllTriggers,
            Math.Clamp(mode.AutomationPriority, -1, 1));
    }

    internal static ThinkControlModeTrigger[] SanitizeTriggers(
        IReadOnlyList<ThinkControlModeTrigger>? triggers)
    {
        if (triggers is null)
            return [];

        var result = new List<ThinkControlModeTrigger>(MaxTriggersPerMode);
        foreach (ThinkControlModeTrigger raw in triggers)
        {
            if (result.Count >= MaxTriggersPerMode)
                break;
            ThinkControlModeTrigger? trigger = SanitizeTrigger(raw);
            if (trigger is not null)
                result.Add(trigger);
        }
        return result.ToArray();
    }

    internal static ThinkControlModeTrigger? SanitizeTrigger(ThinkControlModeTrigger? trigger)
    {
        if (trigger is null)
            return null;

        string type = trigger.Type?.Trim() ?? string.Empty;
        string value = trigger.Value?.Trim() ?? string.Empty;
        int days = trigger.DaysMask & 0x7F;
        if (days == 0)
            days = 0x7F;

        return type switch
        {
            "Wifi" when value.Length is > 0 and <= 64 =>
                new("Wifi", value, Enabled: trigger.Enabled),
            "Process" when value.Length is > 0 and <= 96 =>
                new("Process", NormalizeProcessName(value), Enabled: trigger.Enabled),
            "Power" when value is "Battery" or "AC" =>
                new("Power", value, Enabled: trigger.Enabled),
            "BatteryBelow" when trigger.Number is >= 5 and <= 95 =>
                new("BatteryBelow", Number: trigger.Number, Enabled: trigger.Enabled),
            "Schedule" when
                TimeOnly.TryParse(trigger.StartTime, out TimeOnly start) &&
                TimeOnly.TryParse(trigger.EndTime, out TimeOnly end) &&
                start != end =>
                new(
                    "Schedule",
                    StartTime: start.ToString("HH:mm"),
                    EndTime: end.ToString("HH:mm"),
                    DaysMask: days,
                    Enabled: trigger.Enabled),
            _ => null
        };
    }

    internal static string Summary(ThinkControlModeDefinition mode)
    {
        if (mode.Id.Equals(NormalId, StringComparison.OrdinalIgnoreCase))
            return "Use your regular settings.";

        var parts = new List<string>(6);
        if (mode.PerformanceMode is string performance)
            parts.Add($"Power: {performance}");
        if (mode.CoolingProfile is string cooling)
            parts.Add($"Cooling: {cooling}");
        if (mode.RefreshRate is string refresh)
            parts.Add($"Display: {(refresh == "Max" ? "max refresh" : refresh)}");
        if (mode.AudioSafety is not null)
        {
            parts.Add(mode.AudioSafety switch
            {
                "GestureLock" => "Audio: gesture lock",
                "Silent" => "Audio: silent",
                _ => "Audio: normal"
            });
        }
        if (mode.KeyboardLight is string keyboard)
            parts.Add($"Keyboard: {keyboard.ToLowerInvariant()}");
        if (mode.TouchpadGesturesEnabled is bool gestures)
            parts.Add(gestures ? "Gestures: on" : "Gestures: off");

        return parts.Count == 0 ? "No settings." : string.Join(", ", parts);
    }

    internal static string AutomationSummary(ThinkControlModeDefinition mode)
    {
        if (!mode.AutomationEnabled)
            return string.Empty;

        ThinkControlModeTrigger[] triggers = SanitizeTriggers(mode.Triggers);
        if (triggers.Length == 0)
            return string.Empty;

        string match = triggers.Length > 1
            ? mode.MatchAllTriggers ? "All" : "Any"
            : "When";
        string priority = mode.AutomationPriority switch
        {
            1 => ". High priority",
            -1 => ". Low priority",
            _ => string.Empty
        };
        return match + ": " + string.Join(mode.MatchAllTriggers ? " and " : " or ", triggers.Take(2).Select(TriggerSummary)) +
               (triggers.Length > 2 ? $" +{triggers.Length - 2}" : string.Empty) + priority;
    }

    internal static string TriggerSummary(ThinkControlModeTrigger trigger) => trigger.Type switch
    {
        "Wifi" => $"Wi-Fi {trigger.Value}",
        "Process" => $"App {trigger.Value}",
        "Power" => trigger.Value == "Battery" ? "On battery" : "Plugged in",
        "BatteryBelow" => $"Battery ≤ {trigger.Number}%",
        "Schedule" => $"{DaysSummary(trigger.DaysMask)} {trigger.StartTime}–{trigger.EndTime}",
        _ => trigger.Type
    };

    internal static AudioSafetyMode ParseAudioSafety(string? value) => value switch
    {
        "GestureLock" => AudioSafetyMode.MediaLock,
        "Silent" => AudioSafetyMode.Silent,
        _ => AudioSafetyMode.Normal
    };

    internal static bool TryParsePowerMode(string? value, out ThinkControlPowerMode mode)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Equals("Efficiency", StringComparison.OrdinalIgnoreCase))
            normalized = "Quiet";
        return Enum.TryParse(normalized, true, out mode);
    }

    private static string? SanitizeCoolingProfile(string? value)
    {
        string raw = value?.Trim() ?? string.Empty;
        if (raw.Length is 0 or > 80)
            return null;
        return raw switch
        {
            "Auto" => "Lenovo Auto",
            "Silent" => "Quiet",
            "Normal" => "Balanced",
            "Cool" or "MaxCooling" => "Max cooling",
            _ => raw
        };
    }

    private static string NormalizeProcessName(string value)
    {
        string file = Path.GetFileName(value.Trim());
        return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? file[..^4]
            : file;
    }

    private static string DaysSummary(int mask)
    {
        mask &= 0x7F;
        if (mask == 0x7F)
            return "Every day";
        if (mask == 0b0111110)
            return "Weekdays";
        if (mask == 0b1000001)
            return "Weekend";
        return "Scheduled";
    }
}
