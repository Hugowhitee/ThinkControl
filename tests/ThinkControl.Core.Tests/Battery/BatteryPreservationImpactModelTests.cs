using ThinkControl.Core.Battery;
using Xunit;

namespace ThinkControl.Core.Tests.Battery;

public sealed class BatteryPreservationImpactModelTests
{
    [Theory]
    [InlineData(75, 85, 15, 10, 0.05)]
    [InlineData(55, 80, 20, 25, 0.02)]
    [InlineData(40, 60, 40, 20, 0.02)]
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
            "Charge wear 60→60%: ~0.00× · 1.00× = 0→100% reference.",
            BatteryPreservationImpactModel.DescribeChargeWear(60, 60));
        Assert.Equal(
            "Charge wear 78→85%: ~0.05× · 1.00× = 0→100% reference.",
            BatteryPreservationImpactModel.DescribeChargeWear(78, 85));
    }

    [Fact]
    public void TypicalWindowCopy_IsCalculatedFromPresetThresholds()
    {
        Assert.Equal(
            "Typical 75→85% recharge: ~0.05× of the 0→100% reference.",
            BatteryPreservationImpactModel.DescribeWearContext(75, 85));
        Assert.Equal(
            "Typical 55→80% recharge: ~0.02× of the 0→100% reference.",
            BatteryPreservationImpactModel.DescribeWearContext(55, 80));
        Assert.Equal(
            "Typical 40→60% recharge: ~0.02× of the 0→100% reference.",
            BatteryPreservationImpactModel.DescribeWearContext(40, 60));
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
            "Reference: 0→100% = 1.00× comparative charge wear · not the firmware cycle count.",
            BatteryPreservationImpactModel.DescribeWearContext(100, 100, enabled: false));
    }
}
