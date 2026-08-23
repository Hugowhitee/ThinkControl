using System.Management;

namespace ThinkControl.UI.Services;

public sealed record BatteryTelemetrySnapshot(
    int? Percent,
    bool OnAc,
    bool Charging,
    bool Discharging,
    double? PowerWatts,
    double? SmoothedPowerWatts,
    double? RemainingCapacityWh,
    double? FullChargeCapacityWh,
    double? DesignCapacityWh,
    double? HealthPercent,
    TimeSpan? EstimatedTimeToFull,
    TimeSpan? EstimatedTimeRemaining,
    string Source);

/// <summary>
/// Reads the Windows ACPI battery telemetry exposed by root\wmi and turns the noisy
/// charge/discharge rate into a deliberately slow-moving estimate. The displayed
/// watt value can remain close to the live sensor, while ETA uses a median-filtered
/// EWMA so a single charger/CPU spike does not make the UI jump by tens of minutes.
/// </summary>
public sealed class BatteryTelemetryService
{
    private const int PowerWindowSize = 15;
    private static readonly TimeSpan PowerHalfLife = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan EtaHalfLife = TimeSpan.FromSeconds(55);
    private static readonly TimeSpan MaxEta = TimeSpan.FromHours(24);

    private readonly object _gate = new();
    private readonly Queue<double> _powerWindow = new();
    private DateTimeOffset? _lastSampleAt;
    private double? _smoothedPowerWatts;
    private double? _smoothedEtaSeconds;
    private bool _lastCharging;
    private bool _lastDischarging;

    public BatteryTelemetrySnapshot Read()
    {
        RawBattery raw = ReadRaw();

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            double? etaToFull = null;
            double? etaRemaining = null;

            bool modeChanged = raw.Charging != _lastCharging || raw.Discharging != _lastDischarging;
            if (modeChanged)
                ResetSmoothing();

            _lastCharging = raw.Charging;
            _lastDischarging = raw.Discharging;

            if (raw.PowerWatts is double livePower && livePower >= 0.4)
            {
                _powerWindow.Enqueue(livePower);
                while (_powerWindow.Count > PowerWindowSize)
                    _powerWindow.Dequeue();

                double median = Median(_powerWindow);
                double dtSeconds = _lastSampleAt.HasValue
                    ? Math.Clamp((now - _lastSampleAt.Value).TotalSeconds, 0.25, 15)
                    : 2;
                double alphaPower = EwmaAlpha(dtSeconds, PowerHalfLife.TotalSeconds);
                _smoothedPowerWatts = _smoothedPowerWatts.HasValue
                    ? Lerp(_smoothedPowerWatts.Value, median, alphaPower)
                    : median;

                double? rawEtaSeconds = null;
                if (raw.Charging && raw.FullChargeCapacityWh is > 0 && raw.RemainingCapacityWh is >= 0)
                {
                    double energyNeededWh = Math.Max(0, raw.FullChargeCapacityWh.Value - raw.RemainingCapacityWh.Value);
                    if (energyNeededWh <= 0.2 || raw.Percent is >= 100)
                    {
                        rawEtaSeconds = 0;
                    }
                    else if (_smoothedPowerWatts is > 0.4)
                    {
                        rawEtaSeconds = energyNeededWh / _smoothedPowerWatts.Value * 3600d;
                    }
                }
                else if (raw.Discharging && raw.RemainingCapacityWh is > 0 && _smoothedPowerWatts is > 0.4)
                {
                    rawEtaSeconds = raw.RemainingCapacityWh.Value / _smoothedPowerWatts.Value * 3600d;
                }

                if (rawEtaSeconds.HasValue && rawEtaSeconds.Value >= 0 && rawEtaSeconds.Value <= MaxEta.TotalSeconds)
                {
                    double dtSecondsEta = _lastSampleAt.HasValue
                        ? Math.Clamp((now - _lastSampleAt.Value).TotalSeconds, 0.25, 15)
                        : 2;
                    double alphaEta = EwmaAlpha(dtSecondsEta, EtaHalfLife.TotalSeconds);

                    // Bound one update to ±20% of the previous estimate. Real tapering
                    // still moves the ETA, just without flickering on every 2 s poll.
                    double bounded = rawEtaSeconds.Value;
                    if (_smoothedEtaSeconds is > 30)
                    {
                        bounded = Math.Clamp(
                            rawEtaSeconds.Value,
                            _smoothedEtaSeconds.Value * 0.80,
                            _smoothedEtaSeconds.Value * 1.20);
                    }

                    _smoothedEtaSeconds = _smoothedEtaSeconds.HasValue
                        ? Lerp(_smoothedEtaSeconds.Value, bounded, alphaEta)
                        : bounded;

                    if (raw.Charging)
                        etaToFull = _smoothedEtaSeconds;
                    else if (raw.Discharging)
                        etaRemaining = _smoothedEtaSeconds;
                }
            }
            else if (!raw.Charging && !raw.Discharging)
            {
                ResetSmoothing();
            }

            _lastSampleAt = now;

            return new BatteryTelemetrySnapshot(
                raw.Percent,
                raw.OnAc,
                raw.Charging,
                raw.Discharging,
                raw.PowerWatts,
                _smoothedPowerWatts,
                raw.RemainingCapacityWh,
                raw.FullChargeCapacityWh,
                raw.DesignCapacityWh,
                raw.HealthPercent,
                etaToFull.HasValue ? TimeSpan.FromSeconds(etaToFull.Value) : null,
                etaRemaining.HasValue ? TimeSpan.FromSeconds(etaRemaining.Value) : null,
                raw.Source);
        }
    }

    private static RawBattery ReadRaw()
    {
        bool onAc = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus ==
                    System.Windows.Forms.PowerLineStatus.Online;
        int? percent = null;
        bool charging = false;
        bool discharging = false;
        double? chargeRateMw = null;
        double? dischargeRateMw = null;
        double? remainingMwh = null;
        double? fullMwh = null;
        double? designMwh = null;
        string source = "Windows ACPI battery";

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT * FROM BatteryStatus");

            foreach (ManagementObject item in searcher.Get())
            {
                if (!IsActive(item))
                {
                    item.Dispose();
                    continue;
                }

                charging = ReadBool(item, "Charging") ?? false;
                discharging = ReadBool(item, "Discharging") ?? false;
                onAc = ReadBool(item, "PowerOnline") ?? onAc;
                chargeRateMw = ReadDouble(item, "ChargeRate");
                dischargeRateMw = ReadDouble(item, "DischargeRate");
                remainingMwh = ReadDouble(item, "RemainingCapacity");
                item.Dispose();
                break;
            }
        }
        catch
        {
            source = "Windows battery fallback";
        }

        fullMwh = ReadFirstWmiValue("BatteryFullChargedCapacity", "FullChargedCapacity");
        designMwh = ReadFirstWmiValue("BatteryStaticData", "DesignedCapacity");

        if (remainingMwh.HasValue && fullMwh is > 0)
            percent = Math.Clamp((int)Math.Round(remainingMwh.Value / fullMwh.Value * 100d), 0, 100);
        else
        {
            float fallback = System.Windows.Forms.SystemInformation.PowerStatus.BatteryLifePercent;
            if (fallback is >= 0 and <= 1)
                percent = Math.Clamp((int)Math.Round(fallback * 100d), 0, 100);
        }

        // ChargeRate and DischargeRate are separate ACPI fields and are expressed
        // in mW on Windows battery WMI. Keep PowerWatts positive and let the state
        // describe the direction; this is clearer in the UI than a signed number.
        double? powerWatts = null;
        if (charging && chargeRateMw is > 0)
            powerWatts = chargeRateMw.Value / 1000d;
        else if (discharging && dischargeRateMw is > 0)
            powerWatts = dischargeRateMw.Value / 1000d;
        else if (onAc && chargeRateMw is > 0)
            powerWatts = chargeRateMw.Value / 1000d;
        else if (!onAc && dischargeRateMw is > 0)
            powerWatts = dischargeRateMw.Value / 1000d;

        double? health = designMwh is > 0 && fullMwh is > 0
            ? Math.Round(fullMwh.Value / designMwh.Value * 100d, 1)
            : null;

        return new RawBattery(
            percent,
            onAc,
            charging,
            discharging,
            powerWatts,
            ToWh(remainingMwh),
            ToWh(fullMwh),
            ToWh(designMwh),
            health,
            source);
    }

    private static double? ReadFirstWmiValue(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                $"SELECT {propertyName} FROM {className}");
            foreach (ManagementObject item in searcher.Get())
            {
                double? value = ReadDouble(item, propertyName);
                item.Dispose();
                if (value.HasValue)
                    return value;
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool IsActive(ManagementBaseObject item)
    {
        try
        {
            PropertyData? property = item.Properties["Active"];
            return property is null || Convert.ToBoolean(property.Value);
        }
        catch
        {
            return true;
        }
    }

    private static double? ReadDouble(ManagementBaseObject item, string propertyName)
    {
        try
        {
            PropertyData? property = item.Properties[propertyName];
            if (property?.Value is null)
                return null;
            double value = Convert.ToDouble(property.Value);
            return double.IsFinite(value) && value >= 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool? ReadBool(ManagementBaseObject item, string propertyName)
    {
        try
        {
            PropertyData? property = item.Properties[propertyName];
            return property?.Value is null ? null : Convert.ToBoolean(property.Value);
        }
        catch
        {
            return null;
        }
    }

    private void ResetSmoothing()
    {
        _powerWindow.Clear();
        _smoothedPowerWatts = null;
        _smoothedEtaSeconds = null;
        _lastSampleAt = null;
    }

    private static double? ToWh(double? milliWattHours) =>
        milliWattHours.HasValue ? milliWattHours.Value / 1000d : null;

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
            return 0;
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
    }

    private static double EwmaAlpha(double dtSeconds, double halfLifeSeconds) =>
        1d - Math.Exp(-Math.Log(2d) * dtSeconds / halfLifeSeconds);

    private static double Lerp(double from, double to, double alpha) =>
        from + (to - from) * Math.Clamp(alpha, 0d, 1d);

    private sealed record RawBattery(
        int? Percent,
        bool OnAc,
        bool Charging,
        bool Discharging,
        double? PowerWatts,
        double? RemainingCapacityWh,
        double? FullChargeCapacityWh,
        double? DesignCapacityWh,
        double? HealthPercent,
        string Source);
}
