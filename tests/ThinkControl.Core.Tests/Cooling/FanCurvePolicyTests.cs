using ThinkControl.Core.Cooling;
using Xunit;

namespace ThinkControl.Core.Tests.Cooling;

public sealed class FanCurvePolicyTests
{
    [Theory]
    [InlineData(CoolingProfile.Silent, 61, 1)]
    [InlineData(CoolingProfile.Silent, 62, 2)]
    [InlineData(CoolingProfile.Normal, 63, 3)]
    [InlineData(CoolingProfile.Cool, 64, 4)]
    [InlineData(CoolingProfile.Cool, 90, 7)]
    public void ResolveLevel_UsesProfileThresholds(CoolingProfile profile, double temperature, int expected)
    {
        Assert.Equal(expected, FanCurvePolicy.ResolveLevel(profile, temperature, null));
    }

    [Fact]
    public void ResolveLevel_HoldsCurrentLevelInsideDownshiftHysteresis()
    {
        Assert.Equal(4, FanCurvePolicy.ResolveLevel(CoolingProfile.Normal, 67, 4));
        Assert.Equal(3, FanCurvePolicy.ResolveLevel(CoolingProfile.Normal, 65, 4));
    }

    [Fact]
    public void ResolveLevel_UpshiftsImmediately()
    {
        Assert.Equal(6, FanCurvePolicy.ResolveLevel(CoolingProfile.Normal, 85, 2));
    }

    [Fact]
    public void SafetyHandoff_HasSeparateResumeThreshold()
    {
        Assert.True(FanCurvePolicy.RequiresFirmwareSafetyHandoff(96));
        Assert.False(FanCurvePolicy.RequiresFirmwareSafetyHandoff(95.9));
        Assert.True(FanCurvePolicy.CanResumeAfterSafetyHandoff(90));
        Assert.False(FanCurvePolicy.CanResumeAfterSafetyHandoff(90.1));
    }

    [Fact]
    public void PreferStableLevel_NeverDropsBelowThermalRequest()
    {
        var unstable = new HashSet<int> { 3, 4 };
        Assert.Equal(5, FanCurvePolicy.PreferStableLevel(3, unstable));
        Assert.Equal(6, FanCurvePolicy.PreferStableLevel(6, unstable));
    }
}
