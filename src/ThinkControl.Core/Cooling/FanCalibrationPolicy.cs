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
    {
        error = null;
        if (levels is null || levels.Count != RequiredLevelCount)
        {
            error = "A reliable calibration requires all seven EC states; incomplete results were discarded.";
            return false;
        }

        FanLevelCalibrationSnapshot[] ordered = levels.OrderBy(level => level.Level).ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            FanLevelCalibrationSnapshot level = ordered[index];
            if (level.Level != index + 1 || level.Fans is null || level.Fans.Count == 0)
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
        if (!double.IsFinite(maximum) || maximum <= 0)
        {
            error = "EC step 7 did not produce a usable verified maximum RPM.";
            return false;
        }

        foreach (FanLevelCalibrationSnapshot level in ordered.Take(ordered.Length - 1))
        {
            double average = AverageRpm(level);
            if (!double.IsFinite(average) || average > maximum * MaximumOvershootRatio)
            {
                error = $"EC step {level.Level} measured faster than the verified step-7 maximum; the run was rejected.";
                return false;
            }
        }

        return true;
    }

    private static double AverageRpm(FanLevelCalibrationSnapshot level) =>
        level.Fans.Average(fan => fan.MedianRpm);
}
