namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Safety gate for the integrated center segment inside Track control. Play/Pause is
/// part of the same visible edge lane as Previous/Next: the contact must start inside
/// the center segment, stay nearly stationary and lift within a short tap window.
/// Normal previous/next swipes continue to use the surrounding lane.
/// </summary>
public static class TrackCenterGesturePolicy
{
    public const double MaximumTapMs = 460;
    public const double MovementToleranceMm = 1.9;
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
