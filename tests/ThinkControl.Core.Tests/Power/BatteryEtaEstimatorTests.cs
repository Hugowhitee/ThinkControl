using ThinkControl.Core.Power;
using Xunit;

namespace ThinkControl.Core.Tests.Power;

public sealed class BatteryEtaEstimatorTests
{
    [Fact]
    public void ChargingToFull_PublishesAfterShortStableWarmup()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset start = DateTimeOffset.UtcNow;

        BatteryEtaEstimate estimate = estimator.Update(Sample(start, charging: true, power: 18, remaining: 54, full: 72));
        estimate = estimator.Update(Sample(start.AddSeconds(10), charging: true, power: 18.4, remaining: 54.05, full: 72));
        estimate = estimator.Update(Sample(start.AddSeconds(20), charging: true, power: 17.8, remaining: 54.1, full: 72));

        Assert.NotNull(estimate.ToChargeTarget);
        Assert.Equal(100, estimate.ChargeTargetPercent);
        Assert.InRange(estimate.ToChargeTarget!.Value.TotalMinutes, 55, 65);
        Assert.Null(estimate.Remaining);
    }

    [Fact]
    public void PreservationTarget_UsesTargetEnergyInsteadOfFullCapacity()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset start = DateTimeOffset.UtcNow;

        BatteryEtaEstimate estimate = estimator.Update(Sample(start, true, 18, 54, 72, target: 85));
        estimate = estimator.Update(Sample(start.AddSeconds(10), true, 18, 54.05, 72, target: 85));
        estimate = estimator.Update(Sample(start.AddSeconds(20), true, 18, 54.1, 72, target: 85));

        Assert.NotNull(estimate.ToChargeTarget);
        Assert.Equal(85, estimate.ChargeTargetPercent);
        Assert.InRange(estimate.ToChargeTarget!.Value.TotalMinutes, 22, 27);
    }

    [Fact]
    public void ChangingChargeTarget_ClearsOldEtaUntilNewTargetWarmsUp()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset start = DateTimeOffset.UtcNow;

        estimator.Update(Sample(start, true, 18, 54, 72, target: 100));
        estimator.Update(Sample(start.AddSeconds(10), true, 18, 54.1, 72, target: 100));
        Assert.NotNull(estimator.Update(Sample(start.AddSeconds(20), true, 18, 54.2, 72, target: 100)).ToChargeTarget);

        BatteryEtaEstimate changed = estimator.Update(Sample(
            start.AddSeconds(30), true, 18, 54.3, 72, target: 85));

        Assert.Null(changed.ToChargeTarget);
        Assert.Equal(85, changed.ChargeTargetPercent);
        Assert.Equal(1, changed.SampleCount);
    }

    [Fact]
    public void ReachingPreservationTarget_ReturnsZeroEta()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset start = DateTimeOffset.UtcNow;

        estimator.Update(Sample(start, true, 18, 61.1, 72, target: 85, percent: 85));
        estimator.Update(Sample(start.AddSeconds(10), true, 18, 61.15, 72, target: 85, percent: 85));
        BatteryEtaEstimate estimate = estimator.Update(Sample(
            start.AddSeconds(20), true, 18, 61.2, 72, target: 85, percent: 85));

        Assert.Equal(TimeSpan.Zero, estimate.ToChargeTarget);
    }

    [Fact]
    public void ReachingTarget_AfterNonZeroEstimate_PublishesZeroImmediately()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset start = DateTimeOffset.UtcNow;

        estimator.Update(Sample(start, true, 18, 54, 72, target: 85, percent: 75));
        estimator.Update(Sample(start.AddSeconds(10), true, 18, 54.1, 72, target: 85, percent: 75));
        BatteryEtaEstimate before = estimator.Update(Sample(
            start.AddSeconds(20), true, 18, 54.2, 72, target: 85, percent: 75));
        Assert.NotNull(before.ToChargeTarget);
        Assert.True(before.ToChargeTarget > TimeSpan.Zero);

        BatteryEtaEstimate reached = estimator.Update(Sample(
            start.AddSeconds(30), true, 18, 61.2, 72, target: 85, percent: 85));

        Assert.Equal(TimeSpan.Zero, reached.ToChargeTarget);
    }

    [Fact]
    public void ModeChange_ClearsChargingEstimateUntilNewModeWarmsUp()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        estimator.Update(Sample(start, true, 18, 54, 72));
        estimator.Update(Sample(start.AddSeconds(10), true, 18, 54.1, 72));
        Assert.NotNull(estimator.Update(Sample(start.AddSeconds(20), true, 18, 54.2, 72)).ToChargeTarget);

        BatteryEtaEstimate changed = estimator.Update(new BatteryEtaSample(
            start.AddSeconds(30), 74, Charging: false, Discharging: true,
            PowerWatts: 7, RemainingWh: 53, FullWh: 72));

        Assert.Null(changed.ToChargeTarget);
        Assert.Null(changed.Remaining);
        Assert.Equal(1, changed.SampleCount);
    }

    [Fact]
    public void Discharging_UsesCredibleWindowsEstimateDuringWarmup()
    {
        var estimator = new BatteryEtaEstimator();
        BatteryEtaEstimate estimate = estimator.Update(new BatteryEtaSample(
            DateTimeOffset.UtcNow, 63, Charging: false, Discharging: true,
            PowerWatts: null, RemainingWh: 45, FullWh: 72,
            NativeRemaining: TimeSpan.FromHours(6.25)));

        Assert.Equal(TimeSpan.FromHours(6.25), estimate.Remaining);
        Assert.Null(estimate.ToChargeTarget);
    }

    [Fact]
    public void InvalidOrIdleData_DoesNotInventEta()
    {
        var estimator = new BatteryEtaEstimator();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.Null(estimator.Update(Sample(now, true, 0.1, 54, 72)).ToChargeTarget);
        BatteryEtaEstimate idle = estimator.Update(new BatteryEtaSample(
            now.AddSeconds(20), 75, Charging: false, Discharging: false,
            PowerWatts: 18, RemainingWh: 54, FullWh: 72,
            ChargeTargetPercent: 85));
        Assert.Null(idle.ToChargeTarget);
        Assert.Equal(85, idle.ChargeTargetPercent);
        Assert.Null(idle.Remaining);
        Assert.Null(idle.SmoothedPowerWatts);
    }

    private static BatteryEtaSample Sample(
        DateTimeOffset at,
        bool charging,
        double power,
        double remaining,
        double full,
        int target = 100,
        int percent = 75) =>
        new(at, percent, charging, !charging, power, remaining, full,
            ChargeTargetPercent: target);
}
