namespace ThinkControl.Core.Battery;

/// <summary>
/// Transparent threshold-only context for Battery Preservation.
///
/// This intentionally does NOT predict literal battery cycles. Real lithium-ion
/// ageing depends on cell chemistry, voltage mapping, temperature, charge rate,
/// depth of discharge and calendar time. The model exposes quantities that can be
/// calculated honestly from the configured thresholds and remain valid for custom
/// future presets.
///
/// The 70% reference marks the start of a deliberately broad "high-SOC" band for
/// the UI exposure metric. It is not a chemistry-specific knee or lifetime claim.
/// </summary>
public static class BatteryPreservationImpactModel
{
    public const int HighSocReferencePercent = 70;

    public static BatteryPreservationImpact Estimate(int startPercent, int stopPercent, bool enabled = true)
    {
        int start = Math.Clamp(startPercent, 0, 100);
        int stop = Math.Clamp(stopPercent, start, 100);

        if (!enabled || stop >= 100)
        {
            return new BatteryPreservationImpact(
                Enabled: false,
                StartPercent: 100,
                StopPercent: 100,
                TopEndHeadroomPercent: 0,
                HighSocBandAvoidedPercent: 0,
                RechargeWindowPercent: 0,
                EquivalentFullChargeThroughputAvoided: 0m);
        }

        int headroom = 100 - stop;
        int highSocBandWidth = 100 - HighSocReferencePercent;
        int highSocBandAvoided = (int)Math.Round(
            Math.Clamp(headroom / (double)highSocBandWidth, 0d, 1d) * 100d,
            MidpointRounding.AwayFromZero);

        return new BatteryPreservationImpact(
            Enabled: true,
            StartPercent: start,
            StopPercent: stop,
            TopEndHeadroomPercent: headroom,
            HighSocBandAvoidedPercent: highSocBandAvoided,
            RechargeWindowPercent: Math.Max(0, stop - start),
            EquivalentFullChargeThroughputAvoided: Math.Round(headroom / 100m, 2));
    }

    public static string Describe(int startPercent, int stopPercent, bool enabled = true)
    {
        BatteryPreservationImpact impact = Estimate(startPercent, stopPercent, enabled);
        if (!impact.Enabled)
            return "100% cap · maximum unplugged capacity · no top-end charge headroom.";

        string band = impact.HighSocBandAvoidedPercent >= 100
            ? $"all of the >{HighSocReferencePercent}% high-SOC band"
            : $"~{impact.HighSocBandAvoidedPercent}% of the >{HighSocReferencePercent}% high-SOC band";

        return $"{impact.StopPercent}% cap · avoids {impact.TopEndHeadroomPercent}% top-end capacity · {band}. Exact cycle-life gain varies with chemistry and temperature.";
    }
}

public sealed record BatteryPreservationImpact(
    bool Enabled,
    int StartPercent,
    int StopPercent,
    int TopEndHeadroomPercent,
    int HighSocBandAvoidedPercent,
    int RechargeWindowPercent,
    decimal EquivalentFullChargeThroughputAvoided);
