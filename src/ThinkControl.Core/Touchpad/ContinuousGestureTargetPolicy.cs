namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Continuous touchpad controls must not accumulate a hidden target ahead of
/// slow OS readback. When the caller accepts only a bounded target, discard the
/// uncommitted backlog so reversing direction responds to the finger immediately.
/// Preserve sub-percent fractional motion when no backpressure occurs.
/// </summary>
public static class ContinuousGestureTargetPolicy
{
    public static double Reconcile(
        double accumulatedPercent,
        int startingPercent,
        int requestedPercent,
        int? acceptedPercent)
    {
        if (!double.IsFinite(accumulatedPercent) || acceptedPercent is null)
            return 0;

        // The Windows 0/100 boundary is also backpressure: once the control
        // reaches an endpoint, discard travel beyond it so reversing does not
        // first have to unwind an invisible overshoot.
        if (acceptedPercent.Value == requestedPercent)
            return Math.Clamp(accumulatedPercent, -startingPercent, 100 - startingPercent);

        return Math.Clamp(acceptedPercent.Value - startingPercent, -100, 100);
    }
}
