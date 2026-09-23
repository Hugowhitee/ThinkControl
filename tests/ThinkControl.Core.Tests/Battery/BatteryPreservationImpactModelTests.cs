using ThinkControl.Core.Battery;
using Xunit;

namespace ThinkControl.Core.Tests.Battery;

public sealed class BatteryPreservationImpactModelTests
{
    [Theory]
    [InlineData(75, 85, 15, 50, 10, 0.15)]
    [InlineData(55, 80, 20, 67, 25, 0.20)]
    [InlineData(40, 60, 40, 100, 20, 0.40)]
    public void Estimate_DerivesFutureProofThresholdMetrics(
        int start,
        int stop,
        int expectedHeadroom,
        int expectedHighSocAvoided,
        int expectedWindow,
        double expectedThroughput)
    {
        BatteryPreservationImpact result = BatteryPreservationImpactModel.Estimate(start, stop);

        Assert.True(result.Enabled);
        Assert.Equal(expectedHeadroom, result.TopEndHeadroomPercent);
        Assert.Equal(expectedHighSocAvoided, result.HighSocBandAvoidedPercent);
        Assert.Equal(expectedWindow, result.RechargeWindowPercent);
        Assert.Equal((decimal)expectedThroughput, result.EquivalentFullChargeThroughputAvoided);
    }

    [Fact]
    public void Describe_UsesCalculatedThresholdsWithoutInventingLiteralCycleCount()
    {
        string daily = BatteryPreservationImpactModel.Describe(75, 85);
        string desk = BatteryPreservationImpactModel.Describe(55, 80);
        string care = BatteryPreservationImpactModel.Describe(40, 60);

        Assert.Contains("15% top-end headroom", daily, StringComparison.Ordinal);
        Assert.Contains("~50% of >70% high-SOC band avoided", daily, StringComparison.Ordinal);

        Assert.Contains("20% top-end headroom", desk, StringComparison.Ordinal);
        Assert.Contains("~67% of >70% high-SOC band avoided", desk, StringComparison.Ordinal);

        Assert.Contains("40% top-end headroom", care, StringComparison.Ordinal);
        Assert.Contains("all of >70% high-SOC band avoided", care, StringComparison.Ordinal);

        Assert.Contains("Exact cycle-life gain cannot be derived", daily, StringComparison.Ordinal);
        Assert.DoesNotContain("cycles saved", daily, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("×", daily, StringComparison.Ordinal);
    }

    [Fact]
    public void FullCharge_HasNoInventedPreservationBenefit()
    {
        BatteryPreservationImpact result = BatteryPreservationImpactModel.Estimate(100, 100, enabled: false);
        Assert.False(result.Enabled);
        Assert.Equal(0, result.TopEndHeadroomPercent);
        Assert.Equal(0, result.HighSocBandAvoidedPercent);
        Assert.Equal(0m, result.EquivalentFullChargeThroughputAvoided);

        Assert.Contains(
            "Wear context · no top-end charge headroom",
            BatteryPreservationImpactModel.Describe(100, 100, enabled: false),
            StringComparison.Ordinal);
    }
}
