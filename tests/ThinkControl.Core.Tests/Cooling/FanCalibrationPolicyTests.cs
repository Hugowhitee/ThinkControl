using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using Xunit;

namespace ThinkControl.Core.Tests.Cooling;

public sealed class FanCalibrationPolicyTests
{
    [Fact]
    public void CompleteSevenStepRun_IsAccepted()
    {
        Assert.True(FanCalibrationPolicy.TryValidate(CompleteRun(), out string? error), error);
    }

    [Fact]
    public void PartialRun_IsRejected()
    {
        FanLevelCalibrationSnapshot[] partial = CompleteRun()[..6];
        Assert.False(FanCalibrationPolicy.TryValidate(partial, out string? error));
        Assert.Contains("every supported fan state", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingTachometerEvidence_IsRejected()
    {
        FanLevelCalibrationSnapshot[] run = CompleteRun();
        run[3] = new FanLevelCalibrationSnapshot(4, [], false);

        Assert.False(FanCalibrationPolicy.TryValidate(run, out string? error));
        Assert.Contains("tachometer", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonRunningRpm_IsRejected()
    {
        FanLevelCalibrationSnapshot[] run = CompleteRun();
        run[1] = Point(2, 0);

        Assert.False(FanCalibrationPolicy.TryValidate(run, out string? error));
        Assert.Contains("credible", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EarlierStateCannotExceedVerifiedMaximumBeyondTolerance()
    {
        FanLevelCalibrationSnapshot[] run = CompleteRun();
        run[5] = Point(6, 5500);
        run[6] = Point(7, 5000);

        Assert.False(FanCalibrationPolicy.TryValidate(run, out string? error));
        Assert.Contains("verified maximum", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmallMeasurementNoiseAboveStepSeven_IsTolerated()
    {
        FanLevelCalibrationSnapshot[] run = CompleteRun();
        run[5] = Point(6, 5200);
        run[6] = Point(7, 5000);

        Assert.True(FanCalibrationPolicy.TryValidate(run, out string? error), error);
    }

    [Fact]
    public void DuplicateOrMissingLevelNumber_IsRejected()
    {
        FanLevelCalibrationSnapshot[] run = CompleteRun();
        run[4] = Point(4, 3400);

        Assert.False(FanCalibrationPolicy.TryValidate(run, out string? error));
        Assert.Contains("one or more EC states", error, StringComparison.OrdinalIgnoreCase);
    }

    private static FanLevelCalibrationSnapshot[] CompleteRun() =>
    [
        Point(1, 1100),
        Point(2, 1450),
        Point(3, 1900),
        Point(4, 2400),
        Point(5, 3000),
        Point(6, 3700),
        Point(7, 5000)
    ];

    [Fact]
    public void ProviderSubsetUsesMeasuredMaximumAndRejectsLegacyOrPartialRuns()
    {
        int[] states = [4, 5, 6, 7, 0x40];
        var rpm = new Dictionary<int, int> { [4] = 3500, [5] = 3700, [6] = 4000, [7] = 4400, [0x40] = 9400 };
        var run = states.Select(state => Point(state, rpm[state])).ToArray();
        Assert.True(FanCalibrationPolicy.TryValidate(run, states, out _));
        Assert.False(FanCalibrationPolicy.TryValidate(CompleteRun(), states, out _));
        Assert.False(FanCalibrationPolicy.TryValidate(run[..^1], states, out _));
        var mapping = FanOutputMapping.BuildStates(rpm, states);
        Assert.Equal(4, mapping.First(state => state.EstimatedPercent >= 0).HardwareState);
        Assert.Equal(7, mapping.First(state => state.EstimatedPercent >= 45).HardwareState);
        Assert.Equal(0x40, mapping.First(state => state.EstimatedPercent >= 99).HardwareState);
        Assert.Equal(100, mapping[^1].EstimatedPercent);
        rpm.Remove(6);
        Assert.Throws<InvalidOperationException>(() => FanOutputMapping.BuildStates(rpm, states));
    }

    [Fact]
    public void VariableMaximumCannotAuthorizeAStoredCurve()
    {
        var run = CompleteRun();
        run[^1] = run[^1] with { Stable = false };
        Assert.False(FanCalibrationPolicy.TryValidate(run, out _));
    }

    private static FanLevelCalibrationSnapshot Point(int level, int rpm) =>
        new(level,
        [
            new FanCalibrationFanSnapshot("fan0", "Fan", rpm, 80, true)
        ],
        true);
}
