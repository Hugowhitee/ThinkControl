using ThinkControl.Core.Battery;
using Xunit;

namespace ThinkControl.Core.Tests.Battery;

public sealed class BatteryPreservationImpactModelTests
{
    [Theory]
    [InlineData(75, 85, 15, 10, 0.11, 89)]
    [InlineData(55, 80, 20, 25, 0.06, 94)]
    [InlineData(40, 60, 40, 20, 0.01, 99)]
    public void Estimate_DerivesThresholdAndComparativeWearMetrics(
        int start,
        int stop,
        int expectedHeadroom,
        int expectedWindow,
        double expectedWearCycles,
        int expectedWearReduction)
    {
        BatteryPreservationImpact result = BatteryPreservationImpactModel.Estimate(start, stop);

        Assert.True(result.Enabled);
        Assert.Equal(expectedHeadroom, result.TopEndHeadroomPercent);
        Assert.Equal(expectedWindow, result.RechargeWindowPercent);
        Assert.Equal(expectedWearCycles, result.EstimatedWearCycles, 2);
        Assert.Equal(expectedWearReduction, result.EstimatedWearReductionPercent);
        Assert.InRange(result.EstimatedEndVoltage, 3.52, 4.35);
    }

    [Fact]
    public void WearCurve_IsMonotonicAndNormalizesFullChargeToOne()
    {
        double at60 = BatteryPreservationImpactModel.EstimateWearCyclesTo(60);
        double at80 = BatteryPreservationImpactModel.EstimateWearCyclesTo(80);
        double at85 = BatteryPreservationImpactModel.EstimateWearCyclesTo(85);
        double at90 = BatteryPreservationImpactModel.EstimateWearCyclesTo(90);
        double at100 = BatteryPreservationImpactModel.EstimateWearCyclesTo(100);

        Assert.True(at60 < at80);
        Assert.True(at80 < at85);
        Assert.True(at85 < at90);
        Assert.True(at90 < at100);
        Assert.Equal(1d, at100, 6);
    }

    [Fact]
    public void Describe_UsesAccuBatteryStyleComparativeWearWithoutClaimingMeasuredPackWear()
    {
        string daily = BatteryPreservationImpactModel.DescribeWearContext(75, 85);
        string desk = BatteryPreservationImpactModel.DescribeWearContext(55, 80);
        string care = BatteryPreservationImpactModel.DescribeWearContext(40, 60);

        Assert.Equal("Estimated wear to 85%: 0.11 wear cycles, about 89% less than 100%.", daily);
        Assert.Equal("Estimated wear to 80%: 0.06 wear cycles, about 94% less than 100%.", desk);
        Assert.Equal("Estimated wear to 60%: 0.01 wear cycles, about 99% less than 100%.", care);
        Assert.Contains("Comparative estimate", BatteryPreservationImpactModel.LimitationsText, StringComparison.Ordinal);
        Assert.DoesNotContain("guarantee", BatteryPreservationImpactModel.LimitationsText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullCharge_IsTheOneWearCycleComparisonBaseline()
    {
        BatteryPreservationImpact result = BatteryPreservationImpactModel.Estimate(100, 100, enabled: false);

        Assert.False(result.Enabled);
        Assert.Equal(0, result.TopEndHeadroomPercent);
        Assert.Equal(0, result.RechargeWindowPercent);
        Assert.Equal(1d, result.EstimatedWearCycles, 6);
        Assert.Equal(0, result.EstimatedWearReductionPercent);
        Assert.Equal(
            "Estimated wear to 100%: 1.00 wear cycle baseline.",
            BatteryPreservationImpactModel.DescribeWearContext(100, 100, enabled: false));
    }
}
