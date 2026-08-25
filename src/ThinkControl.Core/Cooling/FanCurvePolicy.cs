namespace ThinkControl.Core.Cooling;

public enum CoolingProfile
{
    LenovoAuto,
    Silent,
    Normal,
    Cool,
    Custom
}

public static class FanCurvePolicy
{
    public const double DownshiftHysteresisC = 4.0;
    public const double SafetyHandoffC = 94.0;
    public const double SafetyResumeC = 90.0;

    private static readonly double[] SilentThresholds = [double.NegativeInfinity, 62, 70, 77, 83, 88, 92];
    private static readonly double[] NormalThresholds = [double.NegativeInfinity, 55, 63, 70, 77, 84, 90];
    private static readonly double[] CoolThresholds = [double.NegativeInfinity, 48, 56, 64, 72, 80, 87];

    // Six temperatures select the entry points for EC levels 2 through 7. Level 1
    // remains the floor. Keeping the last point below the firmware handoff threshold
    // guarantees custom curves cannot bypass ThinkControl's independent safety gate.
    public static IReadOnlyList<double> DefaultCustomThresholds { get; } = [55, 63, 70, 77, 84, 90];

    public static int ResolveLevel(CoolingProfile profile, double smoothedTemperatureC, int? currentLevel)
    {
        if (profile is CoolingProfile.LenovoAuto or CoolingProfile.Custom)
            throw new ArgumentOutOfRangeException(nameof(profile), $"{profile} does not map to a built-in fan curve.");
        if (!double.IsFinite(smoothedTemperatureC))
            throw new ArgumentOutOfRangeException(nameof(smoothedTemperatureC));

        return ResolveWithThresholds(Thresholds(profile), smoothedTemperatureC, currentLevel);
    }

    public static int ResolveCustomLevel(
        IReadOnlyList<double> thresholds,
        double smoothedTemperatureC,
        int? currentLevel)
    {
        if (!TryValidateCustomThresholds(thresholds, out double[] normalized, out string? error))
            throw new ArgumentException(error ?? "Invalid custom fan curve.", nameof(thresholds));
        if (!double.IsFinite(smoothedTemperatureC))
            throw new ArgumentOutOfRangeException(nameof(smoothedTemperatureC));

        double[] expanded = [double.NegativeInfinity, .. normalized];
        return ResolveWithThresholds(expanded, smoothedTemperatureC, currentLevel);
    }

    public static bool TryValidateCustomThresholds(
        IReadOnlyList<double>? thresholds,
        out double[] normalized,
        out string? error)
    {
        normalized = [];
        error = null;
        if (thresholds is null || thresholds.Count != 6)
        {
            error = "A custom fan curve needs exactly six temperatures for levels 2 through 7.";
            return false;
        }

        normalized = thresholds.Select(value => Math.Round(value, 1)).ToArray();
        for (int i = 0; i < normalized.Length; i++)
        {
            double value = normalized[i];
            if (!double.IsFinite(value) || value < 35 || value > 92)
            {
                error = "Custom fan temperatures must stay between 35 °C and 92 °C.";
                normalized = [];
                return false;
            }

            if (i > 0 && value < normalized[i - 1] + 2)
            {
                error = "Each custom fan step must be at least 2 °C above the previous step.";
                normalized = [];
                return false;
            }
        }

        return true;
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

        for (int level = requested + 1; level <= 7; level++)
        {
            if (!unstableLevels.Contains(level))
                return level;
        }

        return requested;
    }

    private static int ResolveWithThresholds(double[] thresholds, double temperature, int? currentLevel)
    {
        int requested = 1;
        for (int level = 2; level <= 7; level++)
        {
            if (temperature >= thresholds[level - 1])
                requested = level;
        }

        if (!currentLevel.HasValue || currentLevel.Value < 1 || currentLevel.Value > 7 || requested >= currentLevel.Value)
            return requested;

        int held = currentLevel.Value;
        while (held > requested)
        {
            double entry = thresholds[held - 1];
            if (temperature > entry - DownshiftHysteresisC)
                return held;
            held--;
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
