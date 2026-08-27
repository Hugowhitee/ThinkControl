using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

internal enum TouchpadVisualCueKind
{
    None,
    ResourceIcon,
    Text,
    PlayPause,
    Scrub,
    Backward,
    Forward,
    Compact,
    Disabled
}

internal readonly record struct TouchpadVisualCue(
    TouchpadVisualCueKind Kind,
    string? Value = null)
{
    internal static TouchpadVisualCue None => new(TouchpadVisualCueKind.None);
    internal static TouchpadVisualCue Icon(string resourceKey) => new(TouchpadVisualCueKind.ResourceIcon, resourceKey);
    internal static TouchpadVisualCue Text(string text) => new(TouchpadVisualCueKind.Text, text);
    internal static TouchpadVisualCue PlayPause => new(TouchpadVisualCueKind.PlayPause);
    internal static TouchpadVisualCue Scrub => new(TouchpadVisualCueKind.Scrub);
    internal static TouchpadVisualCue Backward => new(TouchpadVisualCueKind.Backward);
    internal static TouchpadVisualCue Forward => new(TouchpadVisualCueKind.Forward);
    internal static TouchpadVisualCue Compact => new(TouchpadVisualCueKind.Compact);
    internal static TouchpadVisualCue Disabled => new(TouchpadVisualCueKind.Disabled);
}

internal sealed record TouchpadActionVisualSpec(
    GestureActionKind Action,
    TouchpadVisualCue Center,
    TouchpadVisualCue Negative,
    TouchpadVisualCue Positive,
    bool Directional,
    bool CenterRequiresTrackOption = false,
    double Spread = 34);

/// <summary>
/// Single visual contract for every gesture action shown around the touchpad.
///
/// An action declares meaning only: negative direction, center action and positive
/// direction. TouchpadVisualizer owns orientation, spacing, inversion and active
/// highlighting. New actions must be registered here, preventing one-off visualizer
/// layouts from drifting away from the rest of the gesture system.
/// </summary>
internal static class TouchpadActionVisualCatalog
{
    private static readonly IReadOnlyDictionary<GestureActionKind, TouchpadActionVisualSpec> Specs =
        new Dictionary<GestureActionKind, TouchpadActionVisualSpec>
        {
            [GestureActionKind.Disabled] = new(
                GestureActionKind.Disabled,
                TouchpadVisualCue.Disabled,
                TouchpadVisualCue.None,
                TouchpadVisualCue.None,
                Directional: false),

            [GestureActionKind.Volume] = new(
                GestureActionKind.Volume,
                TouchpadVisualCue.Icon("Tc.Icon.Audio"),
                TouchpadVisualCue.Text("−"),
                TouchpadVisualCue.Text("+"),
                Directional: true),

            [GestureActionKind.Brightness] = new(
                GestureActionKind.Brightness,
                TouchpadVisualCue.Icon("Tc.Icon.Brightness"),
                TouchpadVisualCue.Text("−"),
                TouchpadVisualCue.Text("+"),
                Directional: true),

            [GestureActionKind.MediaSeek] = new(
                GestureActionKind.MediaSeek,
                TouchpadVisualCue.Scrub,
                TouchpadVisualCue.Backward,
                TouchpadVisualCue.Forward,
                Directional: true),

            [GestureActionKind.PreviousNextTrack] = new(
                GestureActionKind.PreviousNextTrack,
                TouchpadVisualCue.Text("⏯"),
                TouchpadVisualCue.Icon("Tc.Icon.SkipPrevious"),
                TouchpadVisualCue.Icon("Tc.Icon.SkipNext"),
                Directional: true,
                CenterRequiresTrackOption: true),

            [GestureActionKind.PlayPause] = new(
                GestureActionKind.PlayPause,
                TouchpadVisualCue.Text("⏯"),
                TouchpadVisualCue.None,
                TouchpadVisualCue.None,
                Directional: false),

            [GestureActionKind.OpenThinkControl] = new(
                GestureActionKind.OpenThinkControl,
                TouchpadVisualCue.Compact,
                TouchpadVisualCue.None,
                TouchpadVisualCue.None,
                Directional: false)
        };

    internal static TouchpadActionVisualSpec Get(GestureActionKind action)
    {
        if (Specs.TryGetValue(action, out TouchpadActionVisualSpec? spec))
            return spec;

        // Legacy/unknown actions should already be removed by Sanitize(). Throwing
        // here makes visual QA catch a newly added action that forgot its visual
        // definition instead of silently shipping another inconsistent fallback.
        throw new InvalidOperationException(
            $"Gesture action '{action}' has no TouchpadActionVisualCatalog entry.");
    }

    internal static void ValidateCurrentActionSet()
    {
        GestureActionKind[] current =
        [
            GestureActionKind.Disabled,
            GestureActionKind.Volume,
            GestureActionKind.Brightness,
            GestureActionKind.MediaSeek,
            GestureActionKind.PreviousNextTrack,
            GestureActionKind.PlayPause,
            GestureActionKind.OpenThinkControl
        ];

        foreach (GestureActionKind action in current)
            _ = Get(action);
    }
}
