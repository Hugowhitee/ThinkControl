namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Policy for the integrated center button inside Track control.
///
/// Play/Pause should feel like a real button, not a timing challenge. A contact that
/// starts inside the visible center segment remains a center-button candidate until
/// it becomes a deliberate Previous/Next swipe. Holding the finger still for longer
/// does not invalidate the button; only movement/intent does.
/// </summary>
public static class TrackCenterGesturePolicy
{
    // Shared discrete-swipe threshold. Keeping this in the same policy as the center
    // button removes the former 4.5-9 mm no-man's-land where a center contact could
    // stop being a tap without yet being large enough to become Previous/Next.
    public const double SwipeThresholdMm = 9.0;

    // A center press can drift almost all the way to the deliberate swipe threshold
    // and still behave like a button on release. The small gap prevents floating-point
    // noise around the exact swipe boundary from toggling Play/Pause after a skip.
    public const double ButtonTravelToleranceMm = 8.75;

    public const double CenterZoneStart = 0.40;
    public const double CenterZoneEnd = 0.60;

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
    // Duration is deliberately not a product rule anymore: a long press is still a
    // press, provided it never turned into a deliberate track swipe.
    public static bool ShouldCommit(
        double durationMs,
        double maximumTravelMm,
        double? edgePosition01) =>
        double.IsFinite(durationMs) &&
        durationMs >= 0 &&
        ShouldCommit(maximumTravelMm, edgePosition01);

    public static bool ShouldCommit(double durationMs, double maximumTravelMm) =>
        ShouldCommit(durationMs, maximumTravelMm, 0.5);
}
