using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackCenterGesturePolicyTests
{
    [Theory]
    [InlineData(180, 0.1)]
    [InlineData(419, 0.1)]
    [InlineData(1051, 0.1)]
    [InlineData(600, 1.01)]
    [InlineData(600, -0.01)]
    public void UnsafeCenterHoldDoesNotCommit(double holdMs, double travelMm) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(holdMs, travelMm));

    [Theory]
    [InlineData(420, 0)]
    [InlineData(700, 0.25)]
    [InlineData(1050, 1.0)]
    public void DeliberateBoundedCenterHoldCommits(double holdMs, double travelMm) =>
        Assert.True(TrackCenterGesturePolicy.ShouldCommit(holdMs, travelMm));

    [Fact]
    public void NonFiniteValuesDoNotCommit()
    {
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(double.NaN, 0));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(600, double.PositiveInfinity));
    }
}
