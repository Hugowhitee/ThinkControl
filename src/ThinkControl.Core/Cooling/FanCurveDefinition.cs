namespace ThinkControl.Core.Cooling;

public sealed record FanCurvePoint(double TemperatureC, int Percent);

public sealed record FanCurveDefinition(
    string Id,
    string Name,
    IReadOnlyList<FanCurvePoint> Points);

public static class FanCurveDefaults
{
    public const string QuietId = "builtin:quiet";
    public const string BalancedId = "builtin:balanced";
    public const string MaxCoolingId = "builtin:max";

    public static FanCurveDefinition Quiet { get; } = new(
        QuietId,
        "Quiet",
        [
            new(40, 0),
            new(50, 0),
            new(60, 14),
            new(70, 28),
            new(78, 42),
            new(84, 58),
            new(89, 78),
            new(92, 100)
        ]);

    public static FanCurveDefinition Balanced { get; } = new(
        BalancedId,
        "Balanced",
        [
            new(40, 0),
            new(50, 12),
            new(60, 24),
            new(70, 38),
            new(78, 54),
            new(84, 70),
            new(89, 86),
            new(92, 100)
        ]);

    public static FanCurveDefinition MaxCooling { get; } = new(
        MaxCoolingId,
        "Max cooling",
        [
            new(40, 14),
            new(50, 26),
            new(60, 40),
            new(70, 56),
            new(78, 70),
            new(84, 84),
            new(89, 94),
            new(92, 100)
        ]);

    public static IReadOnlyList<FanCurveDefinition> BuiltIns { get; } =
        [Quiet, Balanced, MaxCooling];

    public static FanCurveDefinition ById(string? id) => id?.Trim().ToLowerInvariant() switch
    {
        QuietId => Quiet,
        MaxCoolingId => MaxCooling,
        _ => Balanced
    };
}

public static class FanCurveGraphPolicy
{
    public const int MinPointCount = 3;
    public const int MaxPointCount = 8;
    public const int PointCount = MaxPointCount;
    public const double MinTemperatureC = 35;
    public const double MaxTemperatureC = 92;
    public const double MinimumTemperatureSpacingC = 2;
    public const double DownshiftHoldC = 3;

    public static bool TryNormalize(
        IReadOnlyList<FanCurvePoint>? points,
        out FanCurvePoint[] normalized,
        out string? error)
    {
        normalized = [];
        error = null;
        if (points is null || points.Count is < MinPointCount or > MaxPointCount)
        {
            error = $"A fan curve needs between {MinPointCount} and {MaxPointCount} graph points.";
            return false;
        }

        var result = new FanCurvePoint[points.Count];
        double previousTemperature = double.NegativeInfinity;
        int previousPercent = 0;
        for (int i = 0; i < points.Count; i++)
        {
            FanCurvePoint source = points[i];
            double temperature = Math.Round(source.TemperatureC, 1);
            int percent = Math.Clamp(source.Percent, 0, 100);
            if (!double.IsFinite(temperature) || temperature < MinTemperatureC || temperature > MaxTemperatureC)
            {
                error = $"Curve temperatures must stay between {MinTemperatureC:0} °C and {MaxTemperatureC:0} °C.";
                return false;
            }
            if (i > 0 && temperature < previousTemperature + MinimumTemperatureSpacingC)
            {
                error = $"Each curve point must be at least {MinimumTemperatureSpacingC:0} °C above the previous point.";
                return false;
            }
            if (i > 0 && percent < previousPercent)
            {
                error = "Fan percentage may not decrease as temperature increases.";
                return false;
            }

            result[i] = new FanCurvePoint(temperature, percent);
            previousTemperature = temperature;
            previousPercent = percent;
        }

        // The final editable point must request the hardware maximum before the
        // independent 94 °C firmware handoff. This prevents a custom profile from
        // accidentally defining a thermally weak top end.
        if (result[^1].Percent != 100)
        {
            error = "The final fan-curve point must be 100%.";
            return false;
        }

        normalized = result;
        return true;
    }

    public static FanCurvePoint[] Smooth(IReadOnlyList<FanCurvePoint> points)
    {
        if (!TryNormalize(points, out FanCurvePoint[] curve, out string? error))
            throw new ArgumentException(error ?? "Invalid fan curve.", nameof(points));
        if (curve.Length <= MinPointCount)
            return curve;

        var smoothed = curve.Select(point => point with { }).ToArray();
        for (int i = 1; i < curve.Length - 1; i++)
        {
            double weighted = (curve[i - 1].Percent + (2 * curve[i].Percent) + curve[i + 1].Percent) / 4.0;
            int percent = (int)Math.Round(weighted, MidpointRounding.AwayFromZero);
            smoothed[i] = smoothed[i] with
            {
                Percent = Math.Clamp(percent, smoothed[i - 1].Percent, curve[i + 1].Percent)
            };
        }

        smoothed[^1] = smoothed[^1] with { Percent = 100 };
        return smoothed;
    }

    public static int ResolvePercent(
        IReadOnlyList<FanCurvePoint> points,
        double temperatureC,
        int? currentPercent = null)
    {
        if (!TryNormalize(points, out FanCurvePoint[] curve, out string? error))
            throw new ArgumentException(error ?? "Invalid fan curve.", nameof(points));
        if (!double.IsFinite(temperatureC))
            throw new ArgumentOutOfRangeException(nameof(temperatureC));

        int requested;
        if (temperatureC <= curve[0].TemperatureC)
        {
            requested = curve[0].Percent;
        }
        else if (temperatureC >= curve[^1].TemperatureC)
        {
            requested = curve[^1].Percent;
        }
        else
        {
            requested = curve[^1].Percent;
            for (int i = 1; i < curve.Length; i++)
            {
                FanCurvePoint upper = curve[i];
                FanCurvePoint lower = curve[i - 1];
                if (temperatureC > upper.TemperatureC)
                    continue;

                double span = upper.TemperatureC - lower.TemperatureC;
                double t = span <= 0 ? 1 : (temperatureC - lower.TemperatureC) / span;
                requested = (int)Math.Round(lower.Percent + (upper.Percent - lower.Percent) * t);
                break;
            }
        }

        // Independent thermal floor. The graph remains freely editable at normal
        // temperatures, but above 70 °C cooling can only become more aggressive.
        // 92 °C forces the verified maximum request before the 94 °C firmware handoff.
        int thermalFloor = temperatureC <= 70
            ? 0
            : (int)Math.Round(Math.Clamp((temperatureC - 70) / 22.0 * 100.0, 0, 100));
        requested = Math.Max(requested, thermalFloor);

        if (currentPercent is int current && requested < current)
        {
            // Keep downshifts sticky around graph nodes. The supervisor also has a
            // time dwell; this small temperature hysteresis prevents point chatter.
            FanCurvePoint? governing = curve.LastOrDefault(point => point.Percent <= current);
            if (governing is not null && temperatureC > governing.TemperatureC - DownshiftHoldC)
                return current;
        }

        return Math.Clamp(requested, 0, 100);
    }
}
