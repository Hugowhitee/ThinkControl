namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Safety gate for the optional center action inside Track control. Play/pause is a
/// real tap target rather than a hidden hold gesture: the contact must start inside
/// the small visible center zone, stay nearly stationary and lift within a short tap
/// window. Normal previous/next swipes still use the surrounding edge lane.
/// </summary>
public static class TrackCenterGesturePolicy
{
    public const double MaximumTapMs = 420;
    public const double MovementToleranceMm = 1.8;
    public const double CenterZoneStart = 0.44;
    public const double CenterZoneEnd = 0.56;

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
