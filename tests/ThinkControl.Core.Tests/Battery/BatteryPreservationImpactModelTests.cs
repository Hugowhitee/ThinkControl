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
    public void PartialSessionWear_RemainsLowerThanZeroToLimitReference()
    {
        double zeroTo90 = BatteryPreservationImpactModel.EstimateCumulativeWearTo(90);
        double eightyFiveTo90 = BatteryPreservationImpactModel.EstimateWearBetween(85, 90);

        Assert.Equal(0.10d, eightyFiveTo90, 2);
        Assert.True(eightyFiveTo90 < zeroTo90);
        Assert.Equal(0d, BatteryPreservationImpactModel.EstimateWearBetween(90, 90), 6);
    }

    [Theory]
    [InlineData(80, "Charging from 0% to 80%: ~6% of the modeled wear of charging to 100%.")]
    [InlineData(85, "Charging from 0% to 85%: ~11% of the modeled wear of charging to 100%.")]
    [InlineData(90, "Charging from 0% to 90%: ~20% of the modeled wear of charging to 100%.")]
    [InlineData(95, "Charging from 0% to 95%: ~43% of the modeled wear of charging to 100%.")]
    public void LimitCopy_UsesStableZeroToTargetComparison(int target, string expected)
    {
        Assert.Equal(expected, BatteryPreservationImpactModel.DescribeLimitWear(target));
        Assert.Contains("0–100% charge", BatteryPreservationImpactModel.LimitationsText, StringComparison.Ordinal);
        Assert.Contains("Partial top-ups count only the added range", BatteryPreservationImpactModel.LimitationsText, StringComparison.Ordinal);
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
            "Charging from 0% to 100% is the model's 100% wear reference.",
            BatteryPreservationImpactModel.DescribeWearContext(100, 100, enabled: false));
    }
}
