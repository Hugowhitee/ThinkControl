using ThinkControl.Core.Ipc;

namespace ThinkControl.Core.Cooling;

/// <summary>
/// The verified discrete EC backend exposes a finite set of measured fan speeds.
/// Do not pretend that arbitrary slider percentages (or target RPM) are writable.
/// Other providers with genuine target-RPM control retain their own continuous UI.
/// </summary>
public static class ManualFanSpeedChoices
{
    public sealed record Choice(int Level, int MedianRpm, int PercentOfMax, bool FullSpeed)
    {
        public string Label => FullSpeed
            ? $"Full speed · {MedianRpm:N0} calibrated RPM"
            : $"{MedianRpm:N0} calibrated RPM · ~{PercentOfMax}% of max";
    }

    public static IReadOnlyList<Choice> FromCalibration(FanCharacterizationSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Running ||
            snapshot.TotalLevels < 2 || snapshot.CompletedLevels != snapshot.TotalLevels ||
            snapshot.Levels.Count != snapshot.TotalLevels)
        {
            return [];
        }

        FanLevelCalibrationSnapshot[] levels = snapshot.Levels.OrderBy(x => x.Level).ToArray();
        int[] supportedStates = levels.Select(x => x.Level).ToArray();
        if (!FanCalibrationPolicy.TryValidate(levels, supportedStates, out _))
            return [];

        // The final state is the provider-advertised maximum, not inferred from
        // a magic EC register in generic code. Only complete live service-supplied
        // calibrations are valid. Unstable intermediate steps are never offered.
        int maximumRpm = (int)Math.Round(levels[^1].Fans.Average(x => x.MedianRpm));
        if (maximumRpm <= 0)
            return [];

        List<Choice> choices = [];
        foreach (FanLevelCalibrationSnapshot level in levels)
        {
            bool isMax = level.Level == levels[^1].Level;
            if (!level.Stable || level.Fans.Any(fan => !fan.Stable))
                continue;
            int rpm = (int)Math.Round(level.Fans.Average(fan => fan.MedianRpm));
            if (rpm <= 0)
                continue;
            int relative = isMax
                ? 100
                : Math.Clamp((int)Math.Round(rpm * 100.0 / maximumRpm), 1, 99);
            choices.Add(new Choice(level.Level, rpm, relative, isMax));
        }
        return choices;
    }
}
