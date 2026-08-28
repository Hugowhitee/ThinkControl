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
        Assert.Contains("all seven", error, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("step-7 maximum", error, StringComparison.OrdinalIgnoreCase);
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

    private static FanLevelCalibrationSnapshot Point(int level, int rpm) =>
        new(level,
        [
            new FanCalibrationFanSnapshot("fan0", "Fan", rpm, 80, true)
        ],
        true);
}
