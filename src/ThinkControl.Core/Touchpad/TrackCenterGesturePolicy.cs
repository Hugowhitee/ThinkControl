namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Policy for the integrated center button inside Track control.
///
/// Play/Pause should feel like a real button, not a timing challenge. A contact that
/// starts inside the visible center segment remains a center-button candidate until
/// it becomes a deliberate Previous/Next swipe. A quick tap commits on lift, while a
/// short stationary hold may commit before lift so the control never feels inert.
/// </summary>
public static class TrackCenterGesturePolicy
{
    // Shared discrete-swipe threshold. Keeping this in the same policy as the center
    // button removes the former no-man's-land where a center contact could stop being
    // a tap without yet being large enough to become Previous/Next.
    public const double SwipeThresholdMm = 9.0;

    // A center press can drift almost all the way to the deliberate swipe threshold
    // and still behave like a button on release. The small gap prevents floating-point
    // noise around the exact swipe boundary from toggling Play/Pause after a skip.
    public const double ButtonTravelToleranceMm = 8.75;

    // Holding the visible center segment should produce feedback without requiring the
    // user to guess that lift is the only commit moment. The delay is long enough for
    // an ordinary deliberate Track swipe to claim first, but short enough to feel like
    // a button when the finger is intentionally held in place.
    public const int HoldCommitMs = 240;

    // Existing recognizer callers use this semantic name. It means the center button's
    // movement envelope rather than a short-tap-only slop value.
    public const double MovementToleranceMm = ButtonTravelToleranceMm;

    // Alpha.41's 20% center segment was still unnecessarily precise on the physical
    // X9 pad. Keep clear Previous/Next side regions while making Play/Pause a more
    // forgiving 28% target that exactly matches the visual separators.
    public const double CenterZoneStart = 0.36;
    public const double CenterZoneEnd = 0.64;

    public static bool IsInsideCenterZone(double? edgePosition01) =>
        edgePosition01 is double position &&
        double.IsFinite(position) &&
        position >= CenterZoneStart &&
        position <= CenterZoneEnd;

    public static bool ShouldCommit(
        double maximumTravelMm,
        double? edgePosition01) =>
        double.IsFinite(maximumTravelMm) &&
        maximumTravelMm >= 0 &&
        maximumTravelMm <= ButtonTravelToleranceMm &&
        IsInsideCenterZone(edgePosition01);

    // Compatibility overload for callers/tests that still carry an elapsed time.
    // Duration is deliberately not a release-time product rule anymore: a long press
    // is still a press, provided it never turned into a deliberate track swipe.
    public static bool ShouldCommit(
        double durationMs,
        double maximumTravelMm,
        double? edgePosition01) =>
        double.IsFinite(durationMs) &&
        durationMs >= 0 &&
        ShouldCommit(maximumTravelMm, edgePosition01);

    // Do not add back the old two-double (duration, travel) overload. It is ambiguous
    // with the current (travel, position) API when callers hold a non-nullable double
    // position, and can silently bypass the center-zone check through overload choice.
}
