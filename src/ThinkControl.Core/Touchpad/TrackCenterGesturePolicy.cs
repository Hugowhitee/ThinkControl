namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Policy for the integrated center button inside Track control.
///
/// Play/Pause is intentionally safer than Previous/Next: a center contact must start
/// inside the visible target, remain nearly stationary for a deliberate hold, and only
/// commits when the finger is released. Brief taps therefore cannot unexpectedly start
/// media while ordinary Track swipes remain available from the same lane.
/// </summary>
public static class TrackCenterGesturePolicy
{
    // Previous/Next remains a deliberate movement gesture.
    public const double SwipeThresholdMm = 9.0;

    // A single-finger hold cannot require perfect stillness because real touchpads
    // report small position deltas even when a finger feels stationary. Three mm keeps
    // natural micro-drift usable without allowing a broad resting touch to arm media.
    public const double HoldMovementToleranceMm = 3.0;

    // The recognizer reserves the center contact only while it still qualifies as a
    // deliberate hold. Beyond this distance ordinary edge-direction recognition resumes.
    public const double MovementToleranceMm = HoldMovementToleranceMm;

    // Quick taps are intentionally ignored. 450 ms feels like a deliberate long press
    // without making Play/Pause sluggish once the user actually intends to invoke it.
    public const int HoldMinimumMs = 450;

    // Keep the alpha.42 28% target: easier to hit spatially, but safer temporally.
    public const double CenterZoneStart = 0.36;
    public const double CenterZoneEnd = 0.64;

    public static bool IsInsideCenterZone(double? edgePosition01) =>
        edgePosition01 is double position &&
        double.IsFinite(position) &&
        position >= CenterZoneStart &&
        position <= CenterZoneEnd;

    public static bool ShouldCommitHold(
        double durationMs,
        double maximumTravelMm,
        double? edgePosition01) =>
        double.IsFinite(durationMs) &&
        durationMs >= HoldMinimumMs &&
        double.IsFinite(maximumTravelMm) &&
        maximumTravelMm >= 0 &&
        maximumTravelMm <= HoldMovementToleranceMm &&
        IsInsideCenterZone(edgePosition01);
}
