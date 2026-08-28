namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Safety gate for the optional stationary center action on the top-edge media
/// gesture. A commit must look like a deliberate hold-and-release: not a tap, not
/// meaningful travel, and not a finger that simply rested there for a long time.
/// </summary>
public static class TrackCenterGesturePolicy
{
    public const double MinimumHoldMs = 420;
    public const double MaximumHoldMs = 1050;
    public const double MovementToleranceMm = 1.0;

    public static bool ShouldCommit(double holdMs, double maximumTravelMm) =>
        double.IsFinite(holdMs) &&
        double.IsFinite(maximumTravelMm) &&
        holdMs >= MinimumHoldMs &&
        holdMs <= MaximumHoldMs &&
        maximumTravelMm >= 0 &&
        maximumTravelMm <= MovementToleranceMm;
}
