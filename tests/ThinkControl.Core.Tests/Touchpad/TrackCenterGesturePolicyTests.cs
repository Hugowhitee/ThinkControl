using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackCenterGesturePolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(250)]
    [InlineData(449)]
    public void QuickCenterTapDoesNotCommit(double durationMs) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(durationMs, 0.5, 0.50));

    [Theory]
    [InlineData(450, 0, 0.50)]
    [InlineData(500, 0.8, 0.36)]
    [InlineData(900, 2.2, 0.50)]
    [InlineData(2500, 3.0, 0.64)]
    public void DeliberateStationaryHoldCommitsOnRelease(
        double durationMs,
        double travelMm,
        double position) =>
        Assert.True(TrackCenterGesturePolicy.ShouldCommitHold(durationMs, travelMm, position));

    [Theory]
    [InlineData(700, 3.01, 0.50)]
    [InlineData(700, 8.0, 0.50)]
    [InlineData(700, 0.5, 0.35)]
    [InlineData(700, 0.5, 0.65)]
    [InlineData(449, 0.0, 0.50)]
    public void UnsafeCenterHoldDoesNotCommit(
        double durationMs,
        double travelMm,
        double position) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(durationMs, travelMm, position));

    [Fact]
    public void HoldSlopIsSmallButAboveOrdinaryActivationNoise()
    {
        Assert.Equal(9.0, TrackCenterGesturePolicy.SwipeThresholdMm);
        Assert.Equal(3.0, TrackCenterGesturePolicy.HoldMovementToleranceMm);
        Assert.Equal(TrackCenterGesturePolicy.HoldMovementToleranceMm, TrackCenterGesturePolicy.MovementToleranceMm);
        Assert.True(TrackCenterGesturePolicy.HoldMovementToleranceMm > TouchpadGestureConfiguration.Default.ActivationDistanceMm);
        Assert.True(TrackCenterGesturePolicy.HoldMovementToleranceMm < TrackCenterGesturePolicy.SwipeThresholdMm);
    }

    [Fact]
    public void CenterTargetStaysSpatiallyForgivingButTemporallyDeliberate()
    {
        Assert.Equal(0.36, TrackCenterGesturePolicy.CenterZoneStart, 3);
        Assert.Equal(0.64, TrackCenterGesturePolicy.CenterZoneEnd, 3);
        Assert.Equal(0.28, TrackCenterGesturePolicy.CenterZoneEnd - TrackCenterGesturePolicy.CenterZoneStart, 3);
        Assert.Equal(450, TrackCenterGesturePolicy.HoldMinimumMs);
    }

    [Fact]
    public void NonFiniteValuesDoNotCommit()
    {
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(double.NaN, 0, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(double.PositiveInfinity, 0, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(600, double.NaN, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(600, double.PositiveInfinity, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommitHold(600, 0, double.NaN));
    }
}
