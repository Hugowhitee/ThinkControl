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
    double? TemperatureC,
    TimeSpan? EstimatedTimeToFull,
    TimeSpan? EstimatedTimeRemaining,
    string Source);

/// <summary>
/// Reads Windows battery telemetry and turns the noisy charge/discharge rate into a
/// deliberately slow-moving estimate. Current-session data stays dominant; compact
/// historical charge/discharge priors only stabilize the first few minutes.
/// Slow-changing WMI classes are cached so the UI refresh does not create needless
/// system-wide WMI work.
/// </summary>
public sealed class BatteryTelemetryService
{
    private const int PowerWindowSize = 15;
    private const int CapacityWindowSize = 150;
    private const int MinEtaPowerSamples = 8;
    private const double EarlyChargingPowerFloorWatts = 3.0;
    private static readonly TimeSpan HotSampleInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PowerHalfLife = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EtaHalfLife = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan EtaWarmup = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan LowPowerGrace = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CapacityTrendMinimumSpan = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CapacityTrendMaximumSpan = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaticBatteryInfoRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaticBatteryInfoMinimumRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TemperatureRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TemperatureStaleGrace = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxEta = TimeSpan.FromHours(24);

    private readonly object _gate = new();
    private readonly Queue<double> _powerWindow = new();
    private readonly Queue<ChargeObservation> _chargeObservations = new();
    private DateTimeOffset? _lastSampleAt;
    private DateTimeOffset? _modeStartedAt;
    private double? _smoothedPowerWatts;
    private double? _smoothedEtaSeconds;
    private double? _historicalChargePowerWatts;
    private bool _lastCharging;
    private bool _lastDischarging;
    private BatteryTelemetrySnapshot? _cachedSnapshot;
    private DateTimeOffset _cachedSnapshotAt = DateTimeOffset.MinValue;

    private DateTimeOffset _lastStaticInfoRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastTemperatureRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastValidTemperatureAt = DateTimeOffset.MinValue;
    private int _staticInfoFailureCount;
    private double? _cachedFullCapacityMwh;
    private double? _cachedDesignCapacityMwh;
    private double? _cachedTemperatureC;

    public void SetHistoricalChargePower(double? watts)
    {
        lock (_gate)
        {
            _historicalChargePowerWatts = watts is > 0.4 and < 200
                ? watts.Value
                : null;
        }
    }

    public BatteryTelemetrySnapshot Read()
    {
        lock (_gate)
        {
            DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
            if (_cachedSnapshot is not null && requestedAt - _cachedSnapshotAt < HotSampleInterval)
                return _cachedSnapshot;

            // BatteryStatus is a real root\wmi query. Sampling it multiple times per
            // second (or once from every page-local timer) can consume measurable
            // kernel/WMI CPU on some Lenovo firmware. One five-second owner is still
            // much faster than battery percentage can meaningfully change.
            RawBattery raw = ReadRaw();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            double? etaToFull = null;
            double? etaRemaining = null;

            bool modeChanged = raw.Charging != _lastCharging || raw.Discharging != _lastDischarging;
            if (modeChanged)
            {
                ResetSmoothing();
                if (raw.Charging || raw.Discharging)
                    _modeStartedAt = now;
            }
            else if (!_modeStartedAt.HasValue && (raw.Charging || raw.Discharging))
            {
                _modeStartedAt = now;
            }

            _lastCharging = raw.Charging;
            _lastDischarging = raw.Discharging;

            RecordChargeObservation(raw, now);

            if (raw.PowerWatts is double livePower && livePower >= 0.4)
            {
                _powerWindow.Enqueue(livePower);
                while (_powerWindow.Count > PowerWindowSize)
                    _powerWindow.Dequeue();

                double median = Median(_powerWindow);
                double dtSeconds = _lastSampleAt.HasValue
                    ? Math.Clamp((now - _lastSampleAt.Value).TotalSeconds, 0.25, 15)
                    : HotSampleInterval.TotalSeconds;
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
                    else if (CanPublishChargingEta(now, raw.Percent) && GetEffectiveChargingPower(now) is double effectivePower)
                    {
                        rawEtaSeconds = energyNeededWh / effectivePower * 3600d;
                    }
                }
                else if (raw.Discharging && raw.RemainingCapacityWh is > 0 && GetEffectiveDischargePower(now) is double effectiveDischargePower)
                {
                    rawEtaSeconds = raw.RemainingCapacityWh.Value / effectiveDischargePower * 3600d;
                }

                if (rawEtaSeconds.HasValue && rawEtaSeconds.Value >= 0 && rawEtaSeconds.Value <= MaxEta.TotalSeconds)
                {
                    double dtSecondsEta = _lastSampleAt.HasValue
                        ? Math.Clamp((now - _lastSampleAt.Value).TotalSeconds, 0.25, 15)
                        : HotSampleInterval.TotalSeconds;
                    double alphaEta = EwmaAlpha(dtSecondsEta, EtaHalfLife.TotalSeconds);

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

            _cachedSnapshot = new BatteryTelemetrySnapshot(
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
                raw.TemperatureC,
                etaToFull.HasValue ? TimeSpan.FromSeconds(etaToFull.Value) : null,
                etaRemaining.HasValue ? TimeSpan.FromSeconds(etaRemaining.Value) : null,
                raw.Source);
            _cachedSnapshotAt = now;
            return _cachedSnapshot;
        }
    }

    private bool CanPublishChargingEta(DateTimeOffset now, int? percent)
    {
        if (_powerWindow.Count < MinEtaPowerSamples || !_modeStartedAt.HasValue || !_smoothedPowerWatts.HasValue)
            return false;

        TimeSpan elapsed = now - _modeStartedAt.Value;
        if (elapsed < EtaWarmup)
            return false;

        if (percent is < 90 && _smoothedPowerWatts.Value < EarlyChargingPowerFloorWatts && elapsed < LowPowerGrace)
            return false;

        return _smoothedPowerWatts.Value >= 0.4;
    }

    private double? GetEffectiveChargingPower(DateTimeOffset now)
    {
        if (_smoothedPowerWatts is not > 0.4)
            return null;

        TimeSpan elapsed = _modeStartedAt.HasValue ? now - _modeStartedAt.Value : TimeSpan.MaxValue;
        double robustWindowPower = Percentile(_powerWindow, elapsed < LowPowerGrace ? 0.65 : 0.50);
        double effectivePower = Lerp(_smoothedPowerWatts.Value, robustWindowPower, 0.50);

        if (_historicalChargePowerWatts is > 0.4 && effectivePower >= EarlyChargingPowerFloorWatts && elapsed < TimeSpan.FromMinutes(5))
        {
            double boundedHistory = Math.Clamp(
                _historicalChargePowerWatts.Value,
                effectivePower * 0.60,
                effectivePower * 1.80);
            double historyWeight = elapsed < TimeSpan.FromSeconds(90)
                ? 0.35
                : elapsed < TimeSpan.FromMinutes(3) ? 0.20 : 0.10;
            effectivePower = Lerp(effectivePower, boundedHistory, historyWeight);
        }

        double? observedCapacityPower = GetObservedCapacityChargePower(now);
        if (observedCapacityPower is > 0.4)
        {
            double constrainedObserved = Math.Clamp(
                observedCapacityPower.Value,
                effectivePower * 0.50,
                effectivePower * 1.75);
            effectivePower = Lerp(effectivePower, constrainedObserved, 0.60);
        }

        return Math.Max(0.4, effectivePower);
    }

    private double? GetEffectiveDischargePower(DateTimeOffset now)
    {
        if (_smoothedPowerWatts is not > 0.4)
            return null;

        TimeSpan elapsed = _modeStartedAt.HasValue ? now - _modeStartedAt.Value : TimeSpan.MaxValue;
        double robustWindowPower = Percentile(_powerWindow, 0.50);
        double effectivePower = Lerp(_smoothedPowerWatts.Value, robustWindowPower, 0.55);
        double? historical = BatteryPowerHistoryPriors.TypicalDischargePowerWatts;

        if (historical is > 0.4 && elapsed < TimeSpan.FromMinutes(8))
        {
            double boundedHistory = Math.Clamp(historical.Value, effectivePower * 0.55, effectivePower * 1.75);
            double weight = elapsed < TimeSpan.FromMinutes(2)
                ? 0.28
                : elapsed < TimeSpan.FromMinutes(5) ? 0.14 : 0.07;
            effectivePower = Lerp(effectivePower, boundedHistory, weight);
        }

        return Math.Max(0.4, effectivePower);
    }

    private void RecordChargeObservation(RawBattery raw, DateTimeOffset now)
    {
        if (!raw.Charging || raw.RemainingCapacityWh is not double remainingWh)
        {
            _chargeObservations.Clear();
            return;
        }

        _chargeObservations.Enqueue(new ChargeObservation(now, remainingWh));
        while (_chargeObservations.Count > CapacityWindowSize)
            _chargeObservations.Dequeue();

        DateTimeOffset cutoff = now - CapacityTrendMaximumSpan;
        while (_chargeObservations.Count > 2 && _chargeObservations.Peek().At < cutoff)
            _chargeObservations.Dequeue();
    }

    private double? GetObservedCapacityChargePower(DateTimeOffset now)
    {
        if (_chargeObservations.Count < 2)
            return null;

        ChargeObservation latest = _chargeObservations.Last();
        ChargeObservation? earliest = null;
        foreach (ChargeObservation observation in _chargeObservations)
        {
            if (latest.At - observation.At >= CapacityTrendMinimumSpan)
            {
                earliest = observation;
                break;
            }
        }

        if (earliest is null)
            return null;

        TimeSpan span = latest.At - earliest.At;
        if (span < CapacityTrendMinimumSpan || span.TotalHours <= 0)
            return null;

        double gainedWh = latest.RemainingWh - earliest.RemainingWh;
        if (gainedWh <= 0.03)
            return null;

        double watts = gainedWh / span.TotalHours;
        return double.IsFinite(watts) && watts > 0 ? watts : null;
    }

    private RawBattery ReadRaw()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RefreshSlowBatteryInfo(now);

        bool onAc = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus ==
                    System.Windows.Forms.PowerLineStatus.Online;
        int? percent = null;
        bool charging = false;
        bool discharging = false;
        double? chargeRateMw = null;
        double? dischargeRateMw = null;
        double? remainingMwh = null;
        string source = "Windows ACPI battery";

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT Active,Charging,Discharging,PowerOnline,ChargeRate,DischargeRate,RemainingCapacity FROM BatteryStatus");

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

        if (remainingMwh.HasValue && _cachedFullCapacityMwh is > 0)
            percent = Math.Clamp((int)Math.Round(remainingMwh.Value / _cachedFullCapacityMwh.Value * 100d), 0, 100);
        else
        {
            float fallback = System.Windows.Forms.SystemInformation.PowerStatus.BatteryLifePercent;
            if (fallback is >= 0 and <= 1)
                percent = Math.Clamp((int)Math.Round(fallback * 100d), 0, 100);
        }

        double? powerWatts = null;
        if (charging && chargeRateMw is > 0)
            powerWatts = chargeRateMw.Value / 1000d;
        else if (discharging && dischargeRateMw is > 0)
            powerWatts = dischargeRateMw.Value / 1000d;
        else if (onAc && chargeRateMw is > 0)
            powerWatts = chargeRateMw.Value / 1000d;
        else if (!onAc && dischargeRateMw is > 0)
            powerWatts = dischargeRateMw.Value / 1000d;

        double? health = _cachedDesignCapacityMwh is > 0 && _cachedFullCapacityMwh is > 0
            ? Math.Round(_cachedFullCapacityMwh.Value / _cachedDesignCapacityMwh.Value * 100d, 1)
            : null;

        return new RawBattery(
            percent,
            onAc,
            charging,
            discharging,
            powerWatts,
            ToWh(remainingMwh),
            ToWh(_cachedFullCapacityMwh),
            ToWh(_cachedDesignCapacityMwh),
            health,
            _cachedTemperatureC,
            source);
    }

    private void RefreshSlowBatteryInfo(DateTimeOffset now)
    {
        if (now - _lastStaticInfoRead >= StaticBatteryInfoRefreshInterval)
        {
            double? full = ReadFirstWmiValue("BatteryFullChargedCapacity", "FullChargedCapacity");
            double? design = ReadFirstWmiValue("BatteryStaticData", "DesignedCapacity");
            bool fullValid = full is > 0;
            bool designValid = design is > 0;

            if (fullValid)
                _cachedFullCapacityMwh = full;
            if (designValid)
                _cachedDesignCapacityMwh = design;

            if (fullValid && designValid)
            {
                _staticInfoFailureCount = 0;
                _lastStaticInfoRead = now;
            }
            else
            {
                _staticInfoFailureCount = Math.Min(_staticInfoFailureCount + 1, 5);
                double multiplier = Math.Pow(2, _staticInfoFailureCount - 1);
                TimeSpan retry = TimeSpan.FromSeconds(Math.Min(
                    StaticBatteryInfoRefreshInterval.TotalSeconds,
                    StaticBatteryInfoMinimumRetry.TotalSeconds * multiplier));
                _lastStaticInfoRead = now - StaticBatteryInfoRefreshInterval + retry;
            }
        }

        if (now - _lastTemperatureRead >= TemperatureRefreshInterval)
        {
            _lastTemperatureRead = now;
            double? rawTemperature = ReadFirstWmiValue("BatteryTemperature", "Temperature");
            if (rawTemperature is >= 2000 and <= 4500)
            {
                double converted = Math.Round(rawTemperature.Value / 10d - 273.15, 1);
                if (converted is >= -20 and <= 100)
                {
                    _cachedTemperatureC = converted;
                    _lastValidTemperatureAt = now;
                }
                else
                {
                    ExpireCachedTemperatureIfStale(now);
                }
            }
            else
            {
                ExpireCachedTemperatureIfStale(now);
            }
        }
    }

    private void ExpireCachedTemperatureIfStale(DateTimeOffset now)
    {
        if (!_cachedTemperatureC.HasValue)
            return;

        if (_lastValidTemperatureAt == DateTimeOffset.MinValue ||
            now - _lastValidTemperatureAt >= TemperatureStaleGrace)
        {
            _cachedTemperatureC = null;
        }
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
        _chargeObservations.Clear();
        _smoothedPowerWatts = null;
        _smoothedEtaSeconds = null;
        _lastSampleAt = null;
        _modeStartedAt = null;
    }

    private static double? ToWh(double? milliWattHours) =>
        milliWattHours.HasValue ? milliWattHours.Value / 1000d : null;

    private static double Median(IEnumerable<double> values) => Percentile(values, 0.50);

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
            return 0;
        if (sorted.Length == 1)
            return sorted[0];

        double position = Math.Clamp(percentile, 0d, 1d) * (sorted.Length - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return sorted[lower];

        return Lerp(sorted[lower], sorted[upper], position - lower);
    }

    private static double EwmaAlpha(double dtSeconds, double halfLifeSeconds) =>
        1d - Math.Exp(-Math.Log(2d) * dtSeconds / halfLifeSeconds);

    private static double Lerp(double from, double to, double alpha) =>
        from + (to - from) * Math.Clamp(alpha, 0d, 1d);

    private sealed record ChargeObservation(DateTimeOffset At, double RemainingWh);

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
        double? TemperatureC,
        string Source);
}
