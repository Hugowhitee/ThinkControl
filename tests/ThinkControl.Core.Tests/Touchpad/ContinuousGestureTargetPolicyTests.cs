using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class ContinuousGestureTargetPolicyTests
{
    [Fact]
    public void FastSlideThenReverse_DoesNotDrainHiddenBacklogOrTeleport()
    {
        // The service has confirmed 20% and only permits an 8% lead.
        // Fast upward frames must not build an invisible 60% backlog.
        double accumulated = 32;
        int start = 20;
        int queued = 28;
        accumulated = ContinuousGestureTargetPolicy.Reconcile(accumulated, start, 52, queued);
        Assert.Equal(8, accumulated);

        // Finger reverses by 6%, and the next requested value must descend
        // immediately instead of continuing upwards for several frames.
        accumulated -= 6;
        int nextRequest = start + (int)Math.Round(accumulated);
        Assert.Equal(22, nextRequest);
        accumulated = ContinuousGestureTargetPolicy.Reconcile(accumulated, start, nextRequest, nextRequest);
        Assert.Equal(2, accumulated);
    }

    [Fact]
    public void NaturalSlowMotion_KeepsFractionalTravelUntilItCanCommit()
    {
        Assert.Equal(0.35,
            ContinuousGestureTargetPolicy.Reconcile(0.35, 40, 40, 40),
            precision: 8);
        Assert.Equal(0,
            ContinuousGestureTargetPolicy.Reconcile(4.2, 40, 44, null));
    }

    [Fact]
    public void BrightnessBackpressure_UsesSameBidirectionalBoundary()
    {
        Assert.Equal(-8,
            ContinuousGestureTargetPolicy.Reconcile(-29, 65, 36, 57));
        Assert.Equal(8,
            ContinuousGestureTargetPolicy.Reconcile(29, 65, 94, 73));
    }
}
