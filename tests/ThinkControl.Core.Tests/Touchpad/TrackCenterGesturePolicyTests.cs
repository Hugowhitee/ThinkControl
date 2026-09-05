using ThinkControl.Core.Touchpad;
using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackCenterGesturePolicyTests
{
    [Theory]
    [InlineData(421, 0.1, 0.50)]
    [InlineData(120, 1.81, 0.50)]
    [InlineData(120, -0.01, 0.50)]
    [InlineData(120, 0.2, 0.43)]
    [InlineData(120, 0.2, 0.57)]
    public void UnsafeCenterTapDoesNotCommit(double durationMs, double travelMm, double position) =>
        Assert.False(TrackCenterGesturePolicy.ShouldCommit(durationMs, travelMm, position));

    [Theory]
    [InlineData(0, 0, 0.50)]
    [InlineData(90, 0.25, 0.44)]
    [InlineData(260, 0.8, 0.50)]
    [InlineData(420, 1.8, 0.56)]
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
