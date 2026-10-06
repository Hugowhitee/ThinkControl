using ThinkControl.Core.Ipc;

namespace ThinkControl.Core.Cooling;

/// <summary>
/// Pure validation contract for persisted or newly measured discrete fan-state data.
/// Hardware code may collect evidence, but it may only promote that evidence to the
/// runtime mapping when this policy accepts a complete, internally credible run.
/// </summary>
public static class FanCalibrationPolicy
{
    public const int RequiredLevelCount = 7;
    public const double MaximumOvershootRatio = 1.08;

    public static bool TryValidate(
        IReadOnlyList<FanLevelCalibrationSnapshot>? levels,
        out string? error)
        => TryValidate(levels, Enumerable.Range(1, RequiredLevelCount).ToArray(), out error);

    public static bool TryValidate(
        IReadOnlyList<FanLevelCalibrationSnapshot>? levels,
        IReadOnlyList<int> requiredStates,
        out string? error)
    {
        error = null;
        if (requiredStates.Count < 2 || requiredStates.Distinct().Count() != requiredStates.Count ||
            levels is null || levels.Count != requiredStates.Count)
        {
            error = "A reliable calibration requires every supported fan state; incomplete results were discarded.";
            return false;
        }

        FanLevelCalibrationSnapshot[] ordered = levels.OrderBy(level => level.Level).ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            FanLevelCalibrationSnapshot level = ordered[index];
            if (level.Level != requiredStates[index] || level.Fans is null || level.Fans.Count == 0)
            {
                error = "Calibration is missing a verified tachometer response for one or more EC states.";
                return false;
            }

            if (level.Fans.Any(fan => fan.MedianRpm <= 0))
            {
                error = $"EC step {level.Level} did not produce a credible running-fan RPM.";
                return false;
            }
        }

        double maximum = AverageRpm(ordered[^1]);
        if (!ordered[^1].Stable || ordered[^1].Fans.Any(fan => !fan.Stable))
        {
            error = "The highest fan speed varies too much. Let the laptop cool and measure again.";
            return false;
        }
        if (!double.IsFinite(maximum) || maximum <= 0)
        {
            error = "The highest fan state did not produce a usable verified maximum RPM.";
            return false;
        }

        foreach (FanLevelCalibrationSnapshot level in ordered.Take(ordered.Length - 1))
        {
            double average = AverageRpm(level);
            if (!double.IsFinite(average) || average > maximum * MaximumOvershootRatio)
            {
                error = $"Fan state {level.Level} measured faster than the verified maximum; the run was rejected.";
                return false;
            }
        }

        return true;
    }

    private static double AverageRpm(FanLevelCalibrationSnapshot level) =>
        level.Fans.Average(fan => fan.MedianRpm);
}
