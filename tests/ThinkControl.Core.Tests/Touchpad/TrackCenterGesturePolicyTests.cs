using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackCenterGesturePolicyTests
{
    [Theory]
    [InlineData(461, 0.1, 0.50)]
    [InlineData(120, 2.41, 0.50)]
    [InlineData(120, -0.01, 0.50)]
    [InlineData(120, 0.2, 0.39)]
    [InlineData(120, 0.2, 0.61)]
    public void UnsafeCenterTapDoesNotCommit(double durationMs, double travelMm, double position) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(durationMs, travelMm, position));

    [Theory]
    [InlineData(0, 0, 0.50)]
    [InlineData(90, 0.25, 0.40)]
    [InlineData(260, 0.8, 0.50)]
    [InlineData(460, 2.4, 0.60)]
    public void BoundedCenterTapCommits(double durationMs, double travelMm, double position) =>
        Assert.True(TrackCenterGesturePolicy.ShouldCommit(durationMs, travelMm, position));

    [Fact]
    public void NonFiniteValuesDoNotCommit()
    {
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(double.NaN, 0, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(120, double.PositiveInfinity, 0.5));
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(120, 0, double.NaN));
    }
}