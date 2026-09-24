namespace ThinkControl.Core.Battery;

/// <summary>
/// Comparative Battery Preservation context calculated from the configured
/// thresholds and current charge level.
///
/// AccuBattery's public methodology is the presentation reference: map state of
/// charge onto an idealized Li-ion end voltage, use the published high-voltage
/// cycle-life relation above ~3.95 V, and treat lower-voltage wear as a small
/// linear baseline. ThinkControl does not know the installed pack's exact
/// per-cell voltage curve, so these are comparison estimates rather than measured
/// firmware cycles.
/// </summary>
public static class BatteryPreservationImpactModel
{
    private const double MinIdealizedCellVoltage = 3.52;
    private const double MaxIdealizedCellVoltage = 4.35;
    private const double HighVoltageWearThreshold = 3.95;
    private const int HighVoltageWearThresholdPercent = 80;
    private const double VoltageCurveLinearWeight = 0.28;
    private const double VoltageCurveTopWeight = 0.72;
    private const double WearAtHighVoltageThreshold = 1d / 16d; // 0.40 V below 4.35 => 2^4 life factor.

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
        double wearToStop = EstimateCumulativeWearTo(stop);
        double windowWear = EstimateWearBetween(start, stop);

        return new BatteryPreservationImpact(
            Enabled: enabled && stop < 100,
            StartPercent: start,
            StopPercent: stop,
            TopEndHeadroomPercent: headroom,
            RechargeWindowPercent: rechargeWindow,
            EstimatedEndVoltage: endVoltage,
            EstimatedWearToStop: wearToStop,
            EstimatedRechargeWindowWear: windowWear);
    }

    /// <summary>
    /// Generic smooth SOC-to-cell-voltage approximation. It rises more quickly
    /// near full charge and maps 0% to 3.52 V and 100% to 4.35 V.
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
    /// Cumulative comparative wear index from 0% to a target percentage.
    /// 100% is normalized to 1.00. Above ~3.95 V the public AccuBattery/Choi
    /// rule of thumb is used: every 0.10 V lower end voltage roughly doubles
    /// cycle life. Below that region a small linear baseline joins continuously.
    /// </summary>
    public static double EstimateCumulativeWearTo(int percent)
    {
        int clamped = Math.Clamp(percent, 0, 100);
        double voltage = EstimateIdealizedEndVoltage(clamped);

        if (voltage < HighVoltageWearThreshold)
        {
            double fraction = clamped / (double)HighVoltageWearThresholdPercent;
            return Math.Clamp(WearAtHighVoltageThreshold * fraction, 0d, WearAtHighVoltageThreshold);
        }

        return Math.Clamp(
            Math.Pow(2d, -10d * (MaxIdealizedCellVoltage - voltage)),
            WearAtHighVoltageThreshold,
            1d);
    }

    public static double EstimateWearBetween(int fromPercent, int toPercent)
    {
        int from = Math.Clamp(fromPercent, 0, 100);
        int to = Math.Clamp(toPercent, 0, 100);
        if (to <= from)
            return 0d;

        return Math.Max(0d, EstimateCumulativeWearTo(to) - EstimateCumulativeWearTo(from));
    }

    public const string LimitationsText =
        "Comparative estimate based on a generic Li-ion state-of-charge/voltage curve and published end-voltage research. 1.00× means the model's 0→100% reference charge; it is not the firmware battery cycle count. Actual pack wear varies with chemistry, real voltage mapping, temperature, charge rate and use.";

    public static string DescribeChargeWear(int currentPercent, int targetPercent)
    {
        int current = Math.Clamp(currentPercent, 0, 100);
        int target = Math.Clamp(targetPercent, 0, 100);
        int from = Math.Min(current, target);
        double wear = EstimateWearBetween(from, target);

        return $"Charge wear {from}→{target}%: ~{wear:0.00}× · 1.00× = 0→100% reference.";
    }

    public static string DescribeWearContext(int startPercent, int stopPercent, bool enabled = true)
    {
        BatteryPreservationImpact impact = Estimate(startPercent, stopPercent, enabled);
        if (!impact.Enabled)
            return "Reference: 0→100% = 1.00× comparative charge wear · not the firmware cycle count.";

        return $"Typical {impact.StartPercent}→{impact.StopPercent}% recharge: ~{impact.EstimatedRechargeWindowWear:0.00}× of the 0→100% reference.";
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
    double EstimatedWearToStop,
    double EstimatedRechargeWindowWear);
