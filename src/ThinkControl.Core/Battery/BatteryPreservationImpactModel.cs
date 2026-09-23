namespace ThinkControl.Core.Battery;

/// <summary>
/// Comparative Battery Preservation context calculated from the configured
/// start/stop thresholds.
///
/// The wear-cycle estimate follows the same broad idea AccuBattery documents:
/// lower end-of-charge voltage costs a fraction of a full high-voltage cycle.
/// ThinkControl cannot measure the exact per-cell voltage curve of every laptop,
/// so percentage is mapped to a transparent generic Li-ion voltage curve first.
/// The result is useful for comparing presets, not a laboratory prediction for
/// one specific battery pack.
/// </summary>
public static class BatteryPreservationImpactModel
{
    private const double MinIdealizedCellVoltage = 3.52;
    private const double MaxIdealizedCellVoltage = 4.35;
    private const double VoltageCurveLinearWeight = 0.28;
    private const double VoltageCurveTopWeight = 0.72;

    public static BatteryPreservationImpact Estimate(int startPercent, int stopPercent, bool enabled = true)
    {
        int start = Math.Clamp(startPercent, 0, 100);
        int stop = Math.Clamp(stopPercent, start, 100);

        if (!enabled)
        {
            stop = 100;
            start = 100;
        }

        int headroom = Math.Max(0, 100 - stop);
        int rechargeWindow = Math.Max(0, stop - start);
        double endVoltage = EstimateIdealizedEndVoltage(stop);
        double wearCycles = EstimateWearCyclesTo(stop);
        int wearReduction = (int)Math.Round(
            Math.Clamp((1d - wearCycles) * 100d, 0d, 100d),
            MidpointRounding.AwayFromZero);

        return new BatteryPreservationImpact(
            Enabled: enabled && stop < 100,
            StartPercent: start,
            StopPercent: stop,
            TopEndHeadroomPercent: headroom,
            RechargeWindowPercent: rechargeWindow,
            EstimatedEndVoltage: endVoltage,
            EstimatedWearCycles: wearCycles,
            EstimatedWearReductionPercent: wearReduction);
    }

    /// <summary>
    /// Generic smooth SOC-to-cell-voltage approximation. It deliberately rises much
    /// faster near the top of charge, where lithium-ion voltage stress also rises.
    /// 0% maps to 3.52 V and 100% to 4.35 V.
    /// </summary>
    public static double EstimateIdealizedEndVoltage(int percent)
    {
        double soc = Math.Clamp(percent, 0, 100) / 100d;
        double shaped = VoltageCurveLinearWeight * soc +
                        VoltageCurveTopWeight * Math.Pow(soc, 4);
        return MinIdealizedCellVoltage +
               (MaxIdealizedCellVoltage - MinIdealizedCellVoltage) * shaped;
    }

    /// <summary>
    /// Relative wear-cycle cost, normalized so charging to 100% is 1.00.
    /// The voltage relationship uses the published rule of thumb that a 0.10 V
    /// decrease in end-of-charge voltage roughly doubles cycle life. The tiny
    /// non-zero baseline of the idealized curve is removed so 0% maps to 0.
    /// </summary>
    public static double EstimateWearCyclesTo(int stopPercent)
    {
        double endVoltage = EstimateIdealizedEndVoltage(stopPercent);
        double raw = Math.Pow(2d, -10d * (MaxIdealizedCellVoltage - endVoltage));
        double baseline = Math.Pow(
            2d,
            -10d * (MaxIdealizedCellVoltage - MinIdealizedCellVoltage));
        double normalized = (raw - baseline) / (1d - baseline);
        return Math.Clamp(normalized, 0d, 1d);
    }

    public const string LimitationsText =
        "Comparative estimate based on a generic Li-ion state-of-charge/voltage curve and published end-voltage cycle-life research. Actual pack wear varies with chemistry, temperature, charge rate and use.";

    public static string DescribeWearContext(int startPercent, int stopPercent, bool enabled = true)
    {
        BatteryPreservationImpact impact = Estimate(startPercent, stopPercent, enabled);
        if (!impact.Enabled)
            return "Estimated wear to 100%: 1.00 wear cycle baseline.";

        return $"Estimated wear to {impact.StopPercent}%: {impact.EstimatedWearCycles:0.00} wear cycles, about {impact.EstimatedWearReductionPercent}% less than 100%.";
    }

    public static string Describe(int startPercent, int stopPercent, bool enabled = true) =>
        $"{DescribeWearContext(startPercent, stopPercent, enabled)} {LimitationsText}";
}

public sealed record BatteryPreservationImpact(
    bool Enabled,
    int StartPercent,
    int StopPercent,
    int TopEndHeadroomPercent,
    int RechargeWindowPercent,
    double EstimatedEndVoltage,
    double EstimatedWearCycles,
    int EstimatedWearReductionPercent);
