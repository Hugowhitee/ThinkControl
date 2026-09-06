using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackCenterGesturePolicyTests
{
    [Theory]
    [InlineData(8.76, 0.50)]
    [InlineData(-0.01, 0.50)]
    [InlineData(0.2, 0.39)]
    [InlineData(0.2, 0.61)]
    public void UnsafeCenterButtonReleaseDoesNotCommit(double travelMm, double position) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(travelMm, position));

    [Theory]
    [InlineData(0, 0.50)]
    [InlineData(0.25, 0.40)]
    [InlineData(2.4, 0.50)]
    [InlineData(6.8, 0.50)]
    [InlineData(8.75, 0.60)]
    public void CenterButtonReleaseCommitsAcrossNaturalFingerDrift(double travelMm, double position) =>
        Assert.True(TrackCenterGesturePolicy.ShouldCommit(travelMm, position));

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(700)]
    [InlineData(2500)]
    [InlineData(10000)]
    public void HoldDurationDoesNotChangeButtonMeaning(double durationMs) =>
        Assert.True(TrackCenterGesturePolicy.ShouldCommit(durationMs, 1.0, 0.5));

    [Fact]
    public void CenterButtonEnvelopeRunsUpToButNotThroughSkipThreshold()
    {
        Assert.Equal(9.0, TrackCenterGesturePolicy.SwipeThresholdMm);
        Assert.Equal(TrackCenterGesturePolicy.ButtonTravelToleranceMm, TrackCenterGesturePolicy.MovementToleranceMm);
        Assert.True(TrackCenterGesturePolicy.ButtonTravelToleranceMm > TouchpadGestureConfiguration.Default.ActivationDistanceMm);
        Assert.True(TrackCenterGesturePolicy.ButtonTravelToleranceMm < TrackCenterGesturePolicy.SwipeThresholdMm);
        Assert.InRange(
            TrackCenterGesturePolicy.SwipeThresholdMm - TrackCenterGesturePolicy.ButtonTravelToleranceMm,
            0.0,
            0.30);
    }

    [Fact]
    public void NonFiniteValuesDoNotCommit()
    {
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(double.PositiveInfinity, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(120, 0, double.NaN));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(double.NaN, 0, 0.5));
    }
}
