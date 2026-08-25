namespace ThinkControl.Core.Power;

public sealed record BatteryEtaSample(
    DateTimeOffset At,
    int? Percent,
    bool Charging,
    bool Discharging,
    double? PowerWatts,
    double? RemainingWh,
    double? FullWh,
    TimeSpan? NativeRemaining = null);

public sealed record BatteryEtaEstimate(
    TimeSpan? ToFull,
    TimeSpan? Remaining,
    double? SmoothedPowerWatts,
    int SampleCount);

/// <summary>
/// Low-cost rolling battery ETA estimator for the app's single runtime sampler.
/// It uses real energy and power readings, publishes only after a short stable
/// warm-up, and keeps the Windows-native discharge estimate as a safe fallback.
/// </summary>
public sealed class BatteryEtaEstimator
{
    private const int MinimumSamples = 3;
    private const int MaximumSamples = 12;
    private const double MinimumPowerWatts = 0.4;
    private static readonly TimeSpan MinimumWarmup = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaximumEta = TimeSpan.FromHours(24);

    private readonly Queue<double> _powerSamples = new();
    private bool _charging;
    private bool _discharging;
    private DateTimeOffset? _modeStartedAt;
    private double? _smoothedPowerWatts;
    private double? _smoothedEtaSeconds;

    public BatteryEtaEstimate Update(BatteryEtaSample sample)
    {
        bool modeChanged = sample.Charging != _charging || sample.Discharging != _discharging;
        if (modeChanged)
            Reset();

        _charging = sample.Charging;
        _discharging = sample.Discharging;
        if (!sample.Charging && !sample.Discharging)
        {
            Reset();
            return new(null, null, null, 0);
        }

        _modeStartedAt ??= sample.At;
        if (sample.PowerWatts is > MinimumPowerWatts and < 500)
        {
            _powerSamples.Enqueue(sample.PowerWatts.Value);
            while (_powerSamples.Count > MaximumSamples)
                _powerSamples.Dequeue();
            double median = Median(_powerSamples);
            _smoothedPowerWatts = _smoothedPowerWatts.HasValue
                ? _smoothedPowerWatts.Value + 0.22 * (median - _smoothedPowerWatts.Value)
                : median;
        }

        bool warmedUp = _powerSamples.Count >= MinimumSamples &&
                        sample.At - _modeStartedAt.Value >= MinimumWarmup;
        double? rawEtaSeconds = warmedUp ? CalculateEnergyEtaSeconds(sample) : null;
        if (rawEtaSeconds is >= 0 && rawEtaSeconds <= MaximumEta.TotalSeconds)
        {
            double bounded = rawEtaSeconds.Value;
            if (_smoothedEtaSeconds is > 60)
                bounded = Math.Clamp(bounded, _smoothedEtaSeconds.Value * 0.75, _smoothedEtaSeconds.Value * 1.25);
            _smoothedEtaSeconds = _smoothedEtaSeconds.HasValue
                ? _smoothedEtaSeconds.Value + 0.18 * (bounded - _smoothedEtaSeconds.Value)
                : bounded;
        }

        TimeSpan? toFull = sample.Charging && _smoothedEtaSeconds.HasValue
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

        return new(toFull, remaining, _smoothedPowerWatts, _powerSamples.Count);
    }

    public void Reset()
    {
        _powerSamples.Clear();
        _modeStartedAt = null;
        _smoothedPowerWatts = null;
        _smoothedEtaSeconds = null;
    }

    private double? CalculateEnergyEtaSeconds(BatteryEtaSample sample)
    {
        if (_smoothedPowerWatts is not > MinimumPowerWatts)
            return null;
        if (sample.Charging && sample.FullWh is > 0 && sample.RemainingWh is >= 0)
        {
            double needed = Math.Max(0, sample.FullWh.Value - sample.RemainingWh.Value);
            if (needed <= 0.2 || sample.Percent is >= 100)
                return 0;
            return needed / _smoothedPowerWatts.Value * 3600;
        }
        if (sample.Discharging && sample.RemainingWh is > 0)
            return sample.RemainingWh.Value / _smoothedPowerWatts.Value * 3600;
        return null;
    }

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
