namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Safety gate for the optional center action inside Track control. Play/pause is
/// deliberately harder to trigger than previous/next: the contact must start in a
/// small visible center zone, remain nearly stationary, then lift inside a bounded
/// hold window. Quick taps, normal swipes and passive rests are no-ops.
/// </summary>
public static class TrackCenterGesturePolicy
{
    public const double MinimumHoldMs = 460;
    public const double MaximumHoldMs = 950;
    public const double MovementToleranceMm = 1.15;
    public const double CenterZoneStart = 0.38;
    public const double CenterZoneEnd = 0.62;

    public static bool IsInsideCenterZone(double? edgePosition01) =>
        edgePosition01 is double position &&
        double.IsFinite(position) &&
        position >= CenterZoneStart &&
        position <= CenterZoneEnd;

    public static bool ShouldCommit(
        double holdMs,
        double maximumTravelMm,
        double? edgePosition01) =>
        double.IsFinite(holdMs) &&
        double.IsFinite(maximumTravelMm) &&
        holdMs >= MinimumHoldMs &&
        holdMs <= MaximumHoldMs &&
        maximumTravelMm >= 0 &&
        maximumTravelMm <= MovementToleranceMm &&
        IsInsideCenterZone(edgePosition01);

    // Kept for policy callers/tests that only exercise timing/travel semantics.
    public static bool ShouldCommit(double holdMs, double maximumTravelMm) =>
        ShouldCommit(holdMs, maximumTravelMm, 0.5);
}
