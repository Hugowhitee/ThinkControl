using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

internal enum TouchpadVisualCueKind
{
    None,
    ResourceIcon,
    Text,
    Disabled
}

internal readonly record struct TouchpadVisualCue(
    TouchpadVisualCueKind Kind,
    string? Value = null)
{
    internal static TouchpadVisualCue None => new(TouchpadVisualCueKind.None);
    internal static TouchpadVisualCue Icon(string resourceKey) => new(TouchpadVisualCueKind.ResourceIcon, resourceKey);
    internal static TouchpadVisualCue Text(string text) => new(TouchpadVisualCueKind.Text, text);
    internal static TouchpadVisualCue Disabled => new(TouchpadVisualCueKind.Disabled);
}

internal enum TouchpadGestureMotionKind
{
    AlongEdge,
    Inward
}

internal enum TouchpadGestureBehavior
{
    Disabled,
    Continuous,
    Discrete,
    Inward
}

internal sealed record TouchpadActionVisualSpec(
    GestureActionKind Action,
    TouchpadVisualCue Center,
    TouchpadVisualCue Negative,
    TouchpadVisualCue Positive,
    bool Directional,
    TouchpadGestureMotionKind Motion,
    TouchpadGestureBehavior Behavior,
    bool CenterRequiresTrackOption = false,
    double Spread = 38);

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
                Directional: false,
                Motion: TouchpadGestureMotionKind.AlongEdge,
                Behavior: TouchpadGestureBehavior.Disabled),

            [GestureActionKind.Volume] = new(
                GestureActionKind.Volume,
                TouchpadVisualCue.Icon(SemanticIconKeys.Volume),
                TouchpadVisualCue.Text("−"),
                TouchpadVisualCue.Text("+"),
                Directional: true,
                Motion: TouchpadGestureMotionKind.AlongEdge,
                Behavior: TouchpadGestureBehavior.Continuous),

            [GestureActionKind.Brightness] = new(
                GestureActionKind.Brightness,
                TouchpadVisualCue.Icon(SemanticIconKeys.Brightness),
                TouchpadVisualCue.Text("−"),
                TouchpadVisualCue.Text("+"),
                Directional: true,
                Motion: TouchpadGestureMotionKind.AlongEdge,
                Behavior: TouchpadGestureBehavior.Continuous),

            [GestureActionKind.MediaSeek] = new(
                GestureActionKind.MediaSeek,
                TouchpadVisualCue.Icon(SemanticIconKeys.MediaScrub),
                TouchpadVisualCue.Icon(SemanticIconKeys.SeekBackward),
                TouchpadVisualCue.Icon(SemanticIconKeys.SeekForward),
                Directional: true,
                Motion: TouchpadGestureMotionKind.AlongEdge,
                Behavior: TouchpadGestureBehavior.Continuous),

            [GestureActionKind.PreviousNextTrack] = new(
                GestureActionKind.PreviousNextTrack,
                // The optional center action is a real bounded target drawn by the
                // gesture-zone overlay. Keeping a combined play/pause material icon
                // here made it look like a third skip action and left a stray dot
                // when the center option was disabled.
                TouchpadVisualCue.None,
                TouchpadVisualCue.Icon(SemanticIconKeys.Previous),
                TouchpadVisualCue.Icon(SemanticIconKeys.Next),
                Directional: true,
                Motion: TouchpadGestureMotionKind.AlongEdge,
                Behavior: TouchpadGestureBehavior.Discrete),

            [GestureActionKind.PlayPause] = new(
                GestureActionKind.PlayPause,
                TouchpadVisualCue.Icon(SemanticIconKeys.PlayPause),
                TouchpadVisualCue.None,
                TouchpadVisualCue.None,
                Directional: false,
                Motion: TouchpadGestureMotionKind.AlongEdge,
                Behavior: TouchpadGestureBehavior.Discrete),

            [GestureActionKind.OpenThinkControl] = new(
                GestureActionKind.OpenThinkControl,
                TouchpadVisualCue.Icon(SemanticIconKeys.CompactView),
                TouchpadVisualCue.None,
                TouchpadVisualCue.None,
                Directional: false,
                Motion: TouchpadGestureMotionKind.Inward,
                Behavior: TouchpadGestureBehavior.Inward)
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
