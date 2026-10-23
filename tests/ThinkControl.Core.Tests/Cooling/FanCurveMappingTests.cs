using ThinkControl.Core.Cooling;
using Xunit;

namespace ThinkControl.Core.Tests.Cooling;

public sealed class FanCurveMappingTests
{
    private static readonly IReadOnlyList<FanOutputMapping.State> States = FanOutputMapping.BuildStates(
        new Dictionary<int, int> { [4] = 3500, [5] = 3700, [6] = 4000, [7] = 4400, [0x40] = 9400 },
        [4, 5, 6, 7, 0x40]);

    [Theory]
    [InlineData(40, 5)]
    [InlineData(48, 7)]
    [InlineData(60, 7)]
    [InlineData(80, 0x40)]
    [InlineData(100, 0x40)]
    public void SparseMeasuredStates_DoNotRoundModerateTargetsToFullSpeed(int target, int state)
        => Assert.Equal(state, FanOutputMapping.ResolveCurve(target, States).HardwareState);

    [Theory]
    [InlineData(0, 4)]
    [InlineData(40, 5)]
    [InlineData(50, 7)]
    [InlineData(60, 7)]
    [InlineData(100, 0x40)]
    public void ManualTarget_UsesNearestStableMeasuredSpeedNotCeiling(int target, int hardwareState)
        => Assert.Equal(hardwareState, FanOutputMapping.ResolveManual(target, States)?.HardwareState);

    [Fact]
    public void ManualTarget_RefusesHiddenFullSpeedOrUnstableFallback()
    {
        Assert.Null(FanOutputMapping.ResolveManual(80, States));
        Assert.Null(FanOutputMapping.ResolveManual(50, States, new HashSet<int> { 4, 5, 6, 7 }));
        Assert.Equal(6, FanOutputMapping.ResolveManual(50, States, new HashSet<int> { 7 })?.HardwareState);
        Assert.Null(FanOutputMapping.ResolveManual(100, States, new HashSet<int> { 0x40 }));
    }

    [Fact]
    public void BoundaryNoise_HoldsCurrentStateUntilMeaningfulChange()
    {
        Assert.Equal(7, FanOutputMapping.ResolveCurve(74, States, 7).HardwareState);
        Assert.Equal(0x40, FanOutputMapping.ResolveCurve(73, States, 0x40).HardwareState);
        Assert.Equal(0x40, FanOutputMapping.ResolveCurve(77, States, 7).HardwareState);
        Assert.Equal(7, FanOutputMapping.ResolveCurve(70, States, 0x40).HardwareState);
    }

    [Fact]
    public void HotInputAndExplicitMaximum_AreNotHeldByHysteresis()
    {
        Assert.Equal(0x40, FanOutputMapping.ResolveCurve(48, States, 7, hot: true).HardwareState);
        Assert.Equal(0x40, FanOutputMapping.ResolveCurve(100, States, 7).HardwareState);
        Assert.Throws<InvalidOperationException>(() => FanOutputMapping.ResolveCurve(40, []));
    }
}
