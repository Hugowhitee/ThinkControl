namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Safety gate for the integrated center segment inside Track control. Play/Pause is
/// part of the same visible edge lane as Previous/Next: the contact must start inside
/// the center segment and lift like a tap. Small real-finger drift is allowed even if
/// the general edge recognizer briefly claims the contact; a deliberate track swipe
/// still wins once it crosses the much larger skip threshold in the action router.
/// </summary>
public static class TrackCenterGesturePolicy
{
    public const double MaximumTapMs = 700;
    public const double MovementToleranceMm = 4.5;
    public const double CenterZoneStart = 0.40;
    public const double CenterZoneEnd = 0.60;

    public static bool IsInsideCenterZone(double? edgePosition01) =>
        edgePosition01 is double position &&
        double.IsFinite(position) &&
        position >= CenterZoneStart &&
        position <= CenterZoneEnd;

    public static bool ShouldCommit(
        double durationMs,
        double maximumTravelMm,
        double? edgePosition01) =>
        double.IsFinite(durationMs) &&
        double.IsFinite(maximumTravelMm) &&
        durationMs >= 0 &&
        durationMs <= MaximumTapMs &&
        maximumTravelMm >= 0 &&
        maximumTravelMm <= MovementToleranceMm &&
        IsInsideCenterZone(edgePosition01);

    // Kept for policy callers/tests that only exercise timing/travel semantics.
    public static bool ShouldCommit(double durationMs, double maximumTravelMm) =>
        ShouldCommit(durationMs, maximumTravelMm, 0.5);
}
