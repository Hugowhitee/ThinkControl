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
    public void CustomCurve_UsesSameHysteresisAndLevelFloor()
    {
        double[] curve = [50, 58, 66, 74, 82, 90];
        Assert.Equal(1, FanCurvePolicy.ResolveCustomLevel(curve, 49.9, null));
        Assert.Equal(4, FanCurvePolicy.ResolveCustomLevel(curve, 66, null));
        Assert.Equal(5, FanCurvePolicy.ResolveCustomLevel(curve, 71, 5));
        Assert.Equal(4, FanCurvePolicy.ResolveCustomLevel(curve, 69, 5));
    }

    [Fact]
    public void CustomCurve_RejectsUnsafeOrCrowdedThresholds()
    {
        Assert.False(FanCurvePolicy.TryValidateCustomThresholds([50, 58, 66, 74, 82, 93], out _, out _));
        Assert.False(FanCurvePolicy.TryValidateCustomThresholds([50, 51, 66, 74, 82, 90], out _, out _));
        Assert.True(FanCurvePolicy.TryValidateCustomThresholds([50, 58, 66, 74, 82, 90], out double[] normalized, out _));
        Assert.Equal(6, normalized.Length);
    }

    [Fact]
    public void SafetyHandoff_HasSeparateResumeThreshold()
    {
        Assert.True(FanCurvePolicy.RequiresFirmwareSafetyHandoff(94));
        Assert.False(FanCurvePolicy.RequiresFirmwareSafetyHandoff(93.9));
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

    [Fact]
    public void FanOutputMapping_UsesMeasuredFullSpeedAsRealHundredPercent()
    {
        var rpm = new Dictionary<int, int>
        {
            [1] = 900,
            [2] = 1200,
            [3] = 1600,
            [4] = 2000,
            [5] = 2400,
            [6] = 2800,
            [7] = 3100,
            [8] = 5200
        };

        IReadOnlyList<FanOutputMapping.State> states = FanOutputMapping.BuildStates(rpm);
        Assert.Equal(60, states[6].EstimatedPercent);
        Assert.Equal(100, states[7].EstimatedPercent);
        Assert.True(states[7].FullSpeed);
    }

    [Fact]
    public void FanOutputMapping_NeverSilentlyUndershootsRequestedCalibratedOutput()
    {
        var rpm = new Dictionary<int, int>
        {
            [1] = 900,
            [2] = 1200,
            [3] = 1600,
            [4] = 2000,
            [5] = 2400,
            [6] = 2800,
            [7] = 3000,
            [8] = 5200
        };

        FanOutputMapping.State state = FanOutputMapping.Resolve(60, rpm);
        Assert.Equal(8, state.HardwareState);
        Assert.Equal(100, state.EstimatedPercent);
        Assert.True(state.FullSpeed);
    }

    [Fact]
    public void FanOutputMapping_FallsBackToDiscreteSafeStatesBeforeCalibration()
    {
        Assert.Equal(1, FanOutputMapping.Resolve(0).HardwareState);
        Assert.Equal(4, FanOutputMapping.Resolve(40).HardwareState);
        Assert.Equal(8, FanOutputMapping.Resolve(100).HardwareState);
    }

    [Fact]
    public void GraphCurve_AllowsThreeToEightPointsAndRejectsMore()
    {
        Assert.True(FanCurveGraphPolicy.TryNormalize(
            [new(40, 0), new(70, 50), new(92, 100)], out FanCurvePoint[] compact, out _));
        Assert.Equal(3, compact.Length);

        Assert.False(FanCurveGraphPolicy.TryNormalize(
            [
                new(35, 0), new(42, 10), new(49, 20), new(56, 30), new(63, 40),
                new(70, 50), new(77, 60), new(84, 80), new(92, 100)
            ], out _, out string? error));
        Assert.Contains("between 3 and 8", error);
    }

    [Fact]
    public void GraphCurve_SmoothsAbruptInteriorStepsWithoutMovingTemperatures()
    {
        FanCurvePoint[] rough =
        [
            new(40, 0), new(52, 0), new(64, 70), new(78, 70), new(92, 100)
        ];

        FanCurvePoint[] smooth = FanCurveGraphPolicy.Smooth(rough);

        Assert.Equal([40d, 52d, 64d, 78d, 92d], smooth.Select(point => point.TemperatureC));
        Assert.Equal([0, 18, 53, 78, 100], smooth.Select(point => point.Percent));
        Assert.True(FanCurveGraphPolicy.TryNormalize(smooth, out _, out _));
    }
}
