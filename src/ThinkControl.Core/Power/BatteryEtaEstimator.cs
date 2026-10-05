namespace ThinkControl.Core.Power;

public sealed record BatteryEtaSample(
    DateTimeOffset At,
    int? Percent,
    bool Charging,
    bool Discharging,
    double? PowerWatts,
    double? RemainingWh,
    double? FullWh,
    TimeSpan? NativeRemaining = null,
    int ChargeTargetPercent = 100);

public sealed record BatteryEtaEstimate(
    TimeSpan? ToChargeTarget,
    int ChargeTargetPercent,
    TimeSpan? Remaining,
    double? SmoothedPowerWatts,
    int SampleCount);

/// <summary>
/// Low-cost rolling battery ETA estimator for the app's single runtime sampler.
/// It uses real energy and power readings, publishes only after a short stable
/// warm-up, and keeps the Windows-native discharge estimate as a safe fallback.
///
/// Charging ETA is always calculated to the active charge target. With Battery
/// Preservation disabled that target is 100%; with an 85% stop threshold the ETA
/// is to 85%, not to an unreachable full charge.
/// </summary>
public sealed class BatteryEtaEstimator
{
    private const int MinimumSamples = 3;
    private const int MaximumSamples = 12;
    private const double MinimumPowerWatts = 0.4;
    private static readonly TimeSpan MinimumWarmup = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaximumEta = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaximumSampleGap = TimeSpan.FromMinutes(2);

    private readonly Queue<double> _powerSamples = new();
    private bool _charging;
    private bool _discharging;
    private int _chargeTargetPercent = 100;
    private DateTimeOffset? _modeStartedAt;
    private DateTimeOffset? _lastSampleAt;
    private DateTimeOffset? _lastValidPowerAt;
    private double? _smoothedPowerWatts;
    private double? _smoothedEtaSeconds;

    public BatteryEtaEstimate Update(BatteryEtaSample sample)
    {
        int targetPercent = NormalizeChargeTarget(sample.ChargeTargetPercent);
        bool targetChanged = sample.Charging && targetPercent != _chargeTargetPercent;
        bool modeChanged = sample.Charging != _charging ||
                           sample.Discharging != _discharging ||
                           targetChanged;
        bool samplingInterrupted = _lastSampleAt.HasValue &&
            (sample.At <= _lastSampleAt.Value || sample.At - _lastSampleAt.Value > MaximumSampleGap);
        bool measurementsInterrupted = _lastValidPowerAt.HasValue &&
            sample.At - _lastValidPowerAt.Value > MaximumSampleGap;
        if (modeChanged || samplingInterrupted || measurementsInterrupted)
            Reset();

        _charging = sample.Charging;
        _discharging = sample.Discharging;
        _chargeTargetPercent = targetPercent;
        _lastSampleAt = sample.At;

        if (sample.Charging == sample.Discharging)
        {
            Reset();
            _chargeTargetPercent = targetPercent;
            return new(null, targetPercent, null, null, 0);
        }

        _modeStartedAt ??= sample.At;
        bool validPower = sample.PowerWatts is > MinimumPowerWatts and < 500;
        if (validPower)
        {
            _lastValidPowerAt = sample.At;
            _powerSamples.Enqueue(sample.PowerWatts.GetValueOrDefault());
            while (_powerSamples.Count > MaximumSamples)
                _powerSamples.Dequeue();
            double median = Median(_powerSamples);
            _smoothedPowerWatts = _smoothedPowerWatts.HasValue
                ? _smoothedPowerWatts.Value + 0.22 * (median - _smoothedPowerWatts.Value)
                : median;
        }

        bool warmedUp = _powerSamples.Count >= MinimumSamples &&
                        sample.At - _modeStartedAt.Value >= MinimumWarmup;
        bool targetReached = sample.Charging && !sample.Discharging &&
            sample.Percent is int percent && percent >= targetPercent && percent <= 100;
        double? rawEtaSeconds = targetReached ? 0 :
            warmedUp && validPower ? CalculateEnergyEtaSeconds(sample, targetPercent) : null;
        if (rawEtaSeconds is >= 0 && rawEtaSeconds <= MaximumEta.TotalSeconds)
        {
            if (rawEtaSeconds.Value == 0)
            {
                // Reaching the active charge target is a terminal state, not another
                // noisy ETA sample. Publish zero immediately instead of allowing the
                // previous estimate's clamp/EWMA to keep stale minutes visible.
                _smoothedEtaSeconds = 0;
            }
            else
            {
                double bounded = rawEtaSeconds.Value;
                if (_smoothedEtaSeconds is > 60)
                    bounded = Math.Clamp(bounded, _smoothedEtaSeconds.Value * 0.75, _smoothedEtaSeconds.Value * 1.25);
                _smoothedEtaSeconds = _smoothedEtaSeconds.HasValue
                    ? _smoothedEtaSeconds.Value + 0.18 * (bounded - _smoothedEtaSeconds.Value)
                    : bounded;
            }
        }
        else
        {
            // Missing/invalid current energy or power cannot validate an older ETA.
            // Keep the power window for an isolated dropout, but publish no stale time.
            _smoothedEtaSeconds = null;
        }

        TimeSpan? toChargeTarget = sample.Charging && _smoothedEtaSeconds.HasValue
            ? TimeSpan.FromSeconds(_smoothedEtaSeconds.Value)
            : null;

        TimeSpan? remaining = null;
        if (sample.Discharging)
        {
            if (_smoothedEtaSeconds.HasValue)
                remaining = TimeSpan.FromSeconds(_smoothedEtaSeconds.Value);
            else if (sample.NativeRemaining.HasValue &&
                     sample.NativeRemaining.Value > TimeSpan.Zero &&
                     sample.NativeRemaining.Value <= MaximumEta)
                remaining = sample.NativeRemaining;
        }

        return new(toChargeTarget, targetPercent, remaining, _smoothedPowerWatts, _powerSamples.Count);
    }

    public void Reset()
    {
        _powerSamples.Clear();
        _modeStartedAt = null;
        _smoothedPowerWatts = null;
        _smoothedEtaSeconds = null;
        _lastSampleAt = null;
        _lastValidPowerAt = null;
    }

    private double? CalculateEnergyEtaSeconds(BatteryEtaSample sample, int targetPercent)
    {
        if (_smoothedPowerWatts is not > MinimumPowerWatts)
            return null;

        if (sample.Charging && !sample.Discharging && sample.FullWh is > 0 &&
            double.IsFinite(sample.FullWh.Value) && sample.RemainingWh is >= 0 &&
            double.IsFinite(sample.RemainingWh.Value))
        {
            double targetWh = sample.FullWh.Value * targetPercent / 100d;
            double needed = Math.Max(0, targetWh - sample.RemainingWh.Value);
            if (needed <= 0.2 || sample.Percent is int percent && percent >= targetPercent)
                return 0;
            return needed / _smoothedPowerWatts.Value * 3600;
        }

        if (sample.Discharging && !sample.Charging && sample.RemainingWh is > 0 &&
            double.IsFinite(sample.RemainingWh.Value))
            return sample.RemainingWh.Value / _smoothedPowerWatts.Value * 3600;

        return null;
    }

    private static int NormalizeChargeTarget(int percent) => Math.Clamp(percent, 1, 100);

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
            return 0;
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2.0
            : sorted[middle];
    }
}
