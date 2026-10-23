using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using Xunit;

namespace ThinkControl.Core.Tests.Cooling;

public sealed class ManualFanSpeedChoicesTests
{
    private static FanCharacterizationSnapshot Calibrated(bool variable = false)
    {
        int[] states = [4, 5, 6, 7, 0x40];
        int[] rpm = [3500, 3700, 4000, 4400, 9400];
        var levels = states.Select((level, index) => new FanLevelCalibrationSnapshot(
            level,
            [new FanCalibrationFanSnapshot("fan", "Shared tachometer", rpm[index], 65,
                !(variable && level == 5))],
            !(variable && level == 5))).ToArray();
        return new FanCharacterizationSnapshot(false, null, 5, 5, "Measured", null, levels);
    }

    [Fact]
    public void CompleteCalibration_OnlyOffersRealDiscreteSpeedsAndExplicitMax()
    {
        var choices = ManualFanSpeedChoices.FromCalibration(Calibrated());
        Assert.Equal(5, choices.Count);
        Assert.Equal(new[] { 4, 5, 6, 7, 0x40 }, choices.Select(c => c.Level));
        Assert.Equal(new[] { 37, 39, 43, 47, 100 }, choices.Select(c => c.PercentOfMax));
        Assert.Single(choices.Where(c => c.FullSpeed));
        Assert.Contains("Full speed", choices[^1].Label);
        Assert.Contains("calibrated RPM", choices[0].Label);
    }

    [Fact]
    public void MissingPartialOrActiveCalibration_HidesStaleOrUnverifiedChoices()
    {
        Assert.Empty(ManualFanSpeedChoices.FromCalibration(null));
        Assert.Empty(ManualFanSpeedChoices.FromCalibration(Calibrated() with { CompletedLevels = 4 }));
        Assert.Empty(ManualFanSpeedChoices.FromCalibration(Calibrated() with { Running = true }));
        Assert.Empty(ManualFanSpeedChoices.FromCalibration(Calibrated() with { Levels = Calibrated().Levels.Take(3).ToArray() }));
    }

    [Fact]
    public void UnstableIntermediateReading_IsUnavailableInsteadOfExposingBrokenStep()
    {
        var choices = ManualFanSpeedChoices.FromCalibration(Calibrated(variable: true));
        Assert.Equal(new[] { 4, 6, 7, 0x40 }, choices.Select(c => c.Level));
        Assert.DoesNotContain(choices, c => c.Level == 5);
        Assert.Empty(ManualFanSpeedChoices.FromCalibration(Calibrated() with
        {
            Levels = Calibrated().Levels.Select((x, i) => i == 4 ? x with { Stable = false } : x).ToArray()
        }));
    }
}
