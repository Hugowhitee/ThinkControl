using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackCenterGesturePolicyTests
{
    [Theory]
    [InlineData(180, 0.1, 0.50)]
    [InlineData(459, 0.1, 0.50)]
    [InlineData(951, 0.1, 0.50)]
    [InlineData(650, 1.16, 0.50)]
    [InlineData(650, -0.01, 0.50)]
    [InlineData(650, 0.2, 0.20)]
    [InlineData(650, 0.2, 0.80)]
    public void UnsafeCenterHoldDoesNotCommit(double holdMs, double travelMm, double position) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(holdMs, travelMm, position));

    [Theory]
    [InlineData(460, 0, 0.50)]
    [InlineData(700, 0.25, 0.38)]
    [InlineData(950, 1.15, 0.62)]
    public void DeliberateBoundedCenterHoldCommits(double holdMs, double travelMm, double position) =>
        Assert.True(TrackCenterGesturePolicy.ShouldCommit(holdMs, travelMm, position));

    [Fact]
    public void NonFiniteValuesDoNotCommit()
    {
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(double.NaN, 0, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(600, double.PositiveInfinity, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(600, 0, double.NaN));
    }
}
