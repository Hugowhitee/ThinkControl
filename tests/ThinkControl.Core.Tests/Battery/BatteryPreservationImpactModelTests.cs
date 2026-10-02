using ThinkControl.Core.Battery;
using Xunit;

namespace ThinkControl.Core.Tests.Battery;

public sealed class BatteryPreservationImpactModelTests
{
    [Theory]
    [InlineData(75, 80, 20, 5, 0.00)]
    [InlineData(80, 85, 15, 5, 0.05)]
    [InlineData(85, 90, 10, 5, 0.10)]
    [InlineData(90, 95, 5, 5, 0.22)]
    public void Estimate_DerivesThresholdAndRechargeWindowWear(
        int start,
        int stop,
        int expectedHeadroom,
        int expectedWindow,
        double expectedWindowWear)
    {
        BatteryPreservationImpact result = BatteryPreservationImpactModel.Estimate(start, stop);

        Assert.True(result.Enabled);
        Assert.Equal(expectedHeadroom, result.TopEndHeadroomPercent);
        Assert.Equal(expectedWindow, result.RechargeWindowPercent);
        Assert.Equal(expectedWindowWear, result.EstimatedRechargeWindowWear, 2);
        Assert.InRange(result.EstimatedEndVoltage, 3.52, 4.35);
    }

    [Fact]
    public void WearCurve_IsMonotonicAndNormalizesFullChargeToOne()
    {
        double at60 = BatteryPreservationImpactModel.EstimateCumulativeWearTo(60);
        double at80 = BatteryPreservationImpactModel.EstimateCumulativeWearTo(80);
        double at85 = BatteryPreservationImpactModel.EstimateCumulativeWearTo(85);
        double at90 = BatteryPreservationImpactModel.EstimateCumulativeWearTo(90);
        double at100 = BatteryPreservationImpactModel.EstimateCumulativeWearTo(100);

        Assert.True(at60 < at80);
        Assert.True(at80 < at85);
        Assert.True(at85 < at90);
        Assert.True(at90 < at100);
        Assert.Equal(1d, at100, 6);
    }

    [Fact]
    public void WearBetween_MatchesAccuBatteryStyleCurrentToTargetPresentation()
    {
        Assert.Equal(0d, BatteryPreservationImpactModel.EstimateWearBetween(60, 60), 6);
        Assert.Equal(0.05d, BatteryPreservationImpactModel.EstimateWearBetween(75, 85), 2);
        Assert.Equal(0.02d, BatteryPreservationImpactModel.EstimateWearBetween(55, 80), 2);

        Assert.Equal(
            "At the 60% limit · no additional charge wear to estimate.",
            BatteryPreservationImpactModel.DescribeChargeWear(60, 60));
        Assert.Equal(
            "Estimated wear from 78% to 85%: ~4.7% of a full 0→100% charge.",
            BatteryPreservationImpactModel.DescribeChargeWear(78, 85));
    }

    [Fact]
    public void TypicalWindowCopy_IsCalculatedFromPresetThresholds()
    {
        Assert.Equal(
            "Typical 80% to 85% top-up: ~4.6% of a full 0→100% charge.",
            BatteryPreservationImpactModel.DescribeWearContext(80, 85));
        Assert.Equal(
            "Typical 85% to 90% top-up: ~9.6% of a full 0→100% charge.",
            BatteryPreservationImpactModel.DescribeWearContext(85, 90));
        Assert.Equal(
            "Typical 90% to 95% top-up: ~22.3% of a full 0→100% charge.",
            BatteryPreservationImpactModel.DescribeWearContext(90, 95));
        Assert.Contains("Comparative estimate", BatteryPreservationImpactModel.LimitationsText, StringComparison.Ordinal);
    }

    [Fact]
    public void FullCharge_IsTheOneWearCycleComparisonBaseline()
    {
        BatteryPreservationImpact result = BatteryPreservationImpactModel.Estimate(100, 100, enabled: false);

        Assert.False(result.Enabled);
        Assert.Equal(0, result.TopEndHeadroomPercent);
        Assert.Equal(0, result.RechargeWindowPercent);
        Assert.Equal(1d, result.EstimatedWearToStop, 6);
        Assert.Equal(0d, result.EstimatedRechargeWindowWear, 6);
        Assert.Equal(
            "No charge limit · a full 0→100% charge is the 100% comparison baseline.",
            BatteryPreservationImpactModel.DescribeWearContext(100, 100, enabled: false));
    }
}
