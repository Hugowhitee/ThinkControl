namespace ThinkControl.Core.Cooling;

public enum CoolingProfile
{
    LenovoAuto,
    Silent,
    Normal,
    Cool
}

public static class FanCurvePolicy
{
    public const double DownshiftHysteresisC = 4.0;
    public const double SafetyHandoffC = 94.0;
    public const double SafetyResumeC = 90.0;

    private static readonly double[] SilentThresholds = [double.NegativeInfinity, 62, 70, 77, 83, 88, 92];
    private static readonly double[] NormalThresholds = [double.NegativeInfinity, 55, 63, 70, 77, 84, 90];
    private static readonly double[] CoolThresholds = [double.NegativeInfinity, 48, 56, 64, 72, 80, 87];

    public static int ResolveLevel(CoolingProfile profile, double smoothedTemperatureC, int? currentLevel)
    {
        if (profile == CoolingProfile.LenovoAuto)
            throw new ArgumentOutOfRangeException(nameof(profile), "Lenovo Auto does not map to a manual fan level.");
        if (!double.IsFinite(smoothedTemperatureC))
            throw new ArgumentOutOfRangeException(nameof(smoothedTemperatureC));

        double[] thresholds = Thresholds(profile);
        int requested = 1;
        for (int level = 2; level <= 7; level++)
        {
            if (smoothedTemperatureC >= thresholds[level - 1])
                requested = level;
        }

        if (!currentLevel.HasValue || currentLevel.Value < 1 || currentLevel.Value > 7 || requested >= currentLevel.Value)
            return requested;

        // Downshifts are intentionally sticky. The temperature has to clear the
        // entry threshold for the current level by a few degrees before fan speed
        // may fall, avoiding the common 2↔3↔2 oscillation around a threshold.
        int held = currentLevel.Value;
        while (held > requested)
        {
            double entry = thresholds[held - 1];
            if (smoothedTemperatureC > entry - DownshiftHysteresisC)
                return held;
            held--;
        }

        return requested;
    }

    public static bool RequiresFirmwareSafetyHandoff(double rawControlTemperatureC) =>
        double.IsFinite(rawControlTemperatureC) && rawControlTemperatureC >= SafetyHandoffC;

    public static bool CanResumeAfterSafetyHandoff(double rawControlTemperatureC) =>
        double.IsFinite(rawControlTemperatureC) && rawControlTemperatureC <= SafetyResumeC;

    public static int PreferStableLevel(int requestedLevel, IReadOnlySet<int>? unstableLevels)
    {
        int requested = Math.Clamp(requestedLevel, 1, 7);
        if (unstableLevels is null || !unstableLevels.Contains(requested))
            return requested;

        // Never choose a lower level just to avoid an acoustically unstable step:
        // the next stable step above the thermal request preserves cooling safety.
        for (int level = requested + 1; level <= 7; level++)
        {
            if (!unstableLevels.Contains(level))
                return level;
        }

        return requested;
    }

    private static double[] Thresholds(CoolingProfile profile) => profile switch
    {
        CoolingProfile.Silent => SilentThresholds,
        CoolingProfile.Normal => NormalThresholds,
        CoolingProfile.Cool => CoolThresholds,
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
