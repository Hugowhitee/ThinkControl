using ThinkControl.Core.Audio;

namespace ThinkControl.UI.Services;

public sealed record ThinkControlModeDefinition(
    string Id,
    string Name,
    string? AudioSafety = null,
    bool? TouchpadGesturesEnabled = null,
    string? KeyboardLight = null);

internal enum ThinkControlModeFacet
{
    AudioSafety,
    TouchpadGestures,
    KeyboardLight
}

internal sealed record KeyboardModeSnapshot(
    string Mode,
    string StaticLevel,
    string BaseLevel);

internal static class ThinkControlModeCatalog
{
    internal const int MaxCustomModes = 8;
    internal const string NormalId = "normal";
    internal const string GestureLockId = "gesture-lock";
    internal const string SilentId = "silent";

    internal static readonly IReadOnlyList<ThinkControlModeDefinition> BuiltIns =
    [
        new(NormalId, "Normal"),
        new(GestureLockId, "Gesture lock", AudioSafety: "GestureLock"),
        new(SilentId, "Silent", AudioSafety: "Silent")
    ];

    internal static ThinkControlModeDefinition? Find(
        string? id,
        IReadOnlyList<ThinkControlModeDefinition>? customs)
    {
        string key = id?.Trim() ?? string.Empty;
        ThinkControlModeDefinition? builtIn = BuiltIns.FirstOrDefault(mode =>
            mode.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (builtIn is not null)
            return builtIn;

        return customs?.FirstOrDefault(mode =>
            mode.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    internal static IEnumerable<ThinkControlModeFacet> Facets(ThinkControlModeDefinition mode)
    {
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
            BuiltIns.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
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

        if (audio is null && !mode.TouchpadGesturesEnabled.HasValue && keyboard is null)
            return null;

        return new ThinkControlModeDefinition(
            id,
            name,
            audio,
            mode.TouchpadGesturesEnabled,
            keyboard);
    }

    internal static string Summary(ThinkControlModeDefinition mode)
    {
        if (mode.Id.Equals(NormalId, StringComparison.OrdinalIgnoreCase))
            return "No temporary overrides.";
        if (mode.Id.Equals(GestureLockId, StringComparison.OrdinalIgnoreCase))
            return "Blocks ThinkControl audio and media gestures.";
        if (mode.Id.Equals(SilentId, StringComparison.OrdinalIgnoreCase))
            return "Keeps Windows output muted.";

        var parts = new List<string>(3);
        if (mode.AudioSafety is not null)
        {
            parts.Add(mode.AudioSafety switch
            {
                "GestureLock" => "gesture lock",
                "Silent" => "silent output",
                _ => "normal audio"
            });
        }

        if (mode.TouchpadGesturesEnabled is bool gestures)
            parts.Add(gestures ? "gestures on" : "gestures off");
        if (mode.KeyboardLight is string keyboard)
            parts.Add($"keyboard {keyboard}");

        return parts.Count == 0 ? "No controls." : string.Join(", ", parts);
    }

    internal static AudioSafetyMode ParseAudioSafety(string? value) => value switch
    {
        "GestureLock" => AudioSafetyMode.MediaLock,
        "Silent" => AudioSafetyMode.Silent,
        _ => AudioSafetyMode.Normal
    };
}
