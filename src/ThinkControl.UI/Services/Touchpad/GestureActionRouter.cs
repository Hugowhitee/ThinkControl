using System.Diagnostics;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class GestureActionRouter
{
    private const double VolumeBaseGain = 1.0;
    private const double BrightnessBaseGain = 1.15;
    private const double TrackSwipeThresholdMm = 5.5;

    private readonly NativeInputService _nativeInput;
    private readonly MediaSessionService _media;
    private readonly Func<TouchpadGestureConfiguration> _getConfiguration;
    private readonly Func<int> _getVolume;
    private readonly Action<int> _queueVolume;
    private readonly Func<int> _getBrightness;
    private readonly Action<int> _queueBrightness;
    private readonly Action<GestureActionKind, bool> _setGestureActive;
    private readonly Action<bool> _showTrackOsd;
    private readonly Action _showTrackCenterOsd;
    private readonly Action _openThinkControl;

    private int _volumeAtStart;
    private int _brightnessAtStart;
    private double _continuousDeltaPercent;
    private long _lastContinuousTimestamp;
    private double _seekCumulativeSeconds;
    private Task<bool>? _mediaBeginTask;
    private long _lastMediaTimestamp;
    private bool _trackSwipeFired;
    private long _trackGestureStarted;
    private double _trackMaxTravelMm;
    private bool _trackStayedCandidate;

    internal GestureActionRouter(
        NativeInputService nativeInput,
        MediaSessionService media,
        Func<TouchpadGestureConfiguration> getConfiguration,
        Func<int> getVolume,
        Action<int> queueVolume,
        Func<int> getBrightness,
        Action<int> queueBrightness,
        Action<GestureActionKind, bool> setGestureActive,
        Action<bool> showTrackOsd,
        Action showTrackCenterOsd,
        Action openThinkControl)
    {
        _nativeInput = nativeInput;
        _media = media;
        _getConfiguration = getConfiguration;
        _getVolume = getVolume;
        _queueVolume = queueVolume;
        _getBrightness = getBrightness;
        _queueBrightness = queueBrightness;
        _setGestureActive = setGestureActive;
        _showTrackOsd = showTrackOsd;
        _showTrackCenterOsd = showTrackCenterOsd;
        _openThinkControl = openThinkControl;
    }

    internal double CurrentSeekDeltaSeconds => _seekCumulativeSeconds;

    internal void Handle(GestureSignal signal)
    {
        switch (signal.Phase)
        {
            case GesturePhase.Candidate:
                ObserveCandidate(signal);
                break;
            case GesturePhase.Claimed:
                Begin(signal);
                break;
            case GesturePhase.Active:
                Update(signal);
                break;
            case GesturePhase.Released:
                Release(signal);
                break;
            case GesturePhase.Cancelled:
                // Cancellation (second finger, lost confidence, leaving the edge
                // corridor) is never an action commit. A valid lift arrives as
                // Released and owns the discrete fallback path.
                End(signal.Action);
                break;
        }
    }

    private void ObserveCandidate(GestureSignal signal)
    {
        if (signal.Action != GestureActionKind.PreviousNextTrack)
            return;

        _setGestureActive(signal.Action, true);
        _trackSwipeFired = false;
        _trackGestureStarted = Stopwatch.GetTimestamp();
        _trackMaxTravelMm = 0;
        _trackStayedCandidate = true;
    }

    private void Begin(GestureSignal signal)
    {
        switch (signal.Action)
        {
            case GestureActionKind.Volume:
                _setGestureActive(signal.Action, true);
                _volumeAtStart = _getVolume();
                BeginContinuous(signal, VolumeBaseGain);
                QueueContinuousTarget(_volumeAtStart, _queueVolume);
                break;
            case GestureActionKind.Brightness:
                _setGestureActive(signal.Action, true);
                _brightnessAtStart = _getBrightness();
                BeginContinuous(signal, BrightnessBaseGain);
                QueueContinuousTarget(_brightnessAtStart, _queueBrightness);
                break;
            case GestureActionKind.MediaSeek:
                _setGestureActive(signal.Action, true);
                _seekCumulativeSeconds = 0;
                _lastMediaTimestamp = Stopwatch.GetTimestamp();
                _mediaBeginTask = _media.BeginSeekAsync();
                break;
            case GestureActionKind.PreviousNextTrack:
                _setGestureActive(signal.Action, true);
                _trackSwipeFired = false;
                if (_trackGestureStarted == 0)
                    _trackGestureStarted = Stopwatch.GetTimestamp();
                _trackStayedCandidate = false;
                _trackMaxTravelMm = Math.Max(_trackMaxTravelMm, Math.Abs(signal.TotalTravelMm));
                TryFireTrackSwipe(signal);
                break;
            case GestureActionKind.PlayPause:
                _nativeInput.TogglePlayPause();
                break;
            case GestureActionKind.OpenThinkControl:
                _openThinkControl();
                break;
        }
    }

    private void Update(GestureSignal signal)
    {
        switch (signal.Action)
        {
            case GestureActionKind.Volume:
                AdvanceContinuous(signal, VolumeBaseGain);
                QueueContinuousTarget(_volumeAtStart, _queueVolume);
                break;
            case GestureActionKind.Brightness:
                AdvanceContinuous(signal, BrightnessBaseGain);
                QueueContinuousTarget(_brightnessAtStart, _queueBrightness);
                break;
            case GestureActionKind.MediaSeek:
                QueueMediaSeek(signal);
                break;
            case GestureActionKind.PreviousNextTrack:
                _trackStayedCandidate = false;
                _trackMaxTravelMm = Math.Max(_trackMaxTravelMm, Math.Abs(signal.TotalTravelMm));
                TryFireTrackSwipe(signal);
                break;
        }
    }

    private void Release(GestureSignal signal)
    {
        if (signal.Action == GestureActionKind.PreviousNextTrack)
        {
            _trackMaxTravelMm = Math.Max(_trackMaxTravelMm, Math.Abs(signal.TotalTravelMm));
            if (!_trackStayedCandidate)
                TryFireTrackSwipe(signal, allowReleaseFallback: true);
            if (!_trackSwipeFired && _trackStayedCandidate)
                TryFireTrackCenter();
        }
        End(signal.Action);
    }

    private void TryFireTrackSwipe(GestureSignal signal, bool allowReleaseFallback = false)
    {
        if (_trackSwipeFired)
            return;

        double signed = ToPositiveControlDelta(signal, signal.TotalTravelMm);
        double threshold = allowReleaseFallback ? TrackSwipeThresholdMm * 0.82 : TrackSwipeThresholdMm;
        if (Math.Abs(signed) < threshold)
            return;

        _trackSwipeFired = true;
        bool next = signed > 0;
        bool injected = next ? _nativeInput.NextTrack() : _nativeInput.PreviousTrack();
        if (!injected)
            _ = SkipTrackWithSessionAsync(next);
        _showTrackOsd(next);
    }

    private void TryFireTrackCenter()
    {
        TouchpadGestureConfiguration configuration = _getConfiguration().Sanitize();
        if (!configuration.TrackCenterPlayPauseEnabled || _trackGestureStarted == 0)
            return;

        double elapsedMs = (Stopwatch.GetTimestamp() - _trackGestureStarted) * 1000d / Stopwatch.Frequency;
        // Center play/pause is intentionally a hold-and-release gesture, not a
        // passive-rest gesture. Releasing too quickly is accidental; releasing
        // after a long stationary rest is also treated as accidental. This keeps
        // media from suddenly starting when a palm/finger has simply been parked
        // on the top-edge control area.
        if (!TrackCenterGesturePolicy.ShouldCommit(elapsedMs, _trackMaxTravelMm))
            return;

        _trackSwipeFired = true;
        if (_nativeInput.TogglePlayPause())
            _showTrackCenterOsd();
    }

    private async Task SkipTrackWithSessionAsync(bool next)
    {
        _ = next
            ? await _media.TrySkipNextAsync().ConfigureAwait(false)
            : await _media.TrySkipPreviousAsync().ConfigureAwait(false);
    }

    private void BeginContinuous(GestureSignal signal, double baseGain)
    {
        _continuousDeltaPercent = Math.Clamp(
            ToPositiveControlDelta(signal, signal.TotalTravelMm) * baseGain,
            -100.0,
            100.0);
        _lastContinuousTimestamp = Stopwatch.GetTimestamp();
    }

    private void AdvanceContinuous(GestureSignal signal, double baseGain)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = _lastContinuousTimestamp == 0
            ? 1d / 60d
            : Math.Clamp((now - _lastContinuousTimestamp) / (double)Stopwatch.Frequency, 1d / 240d, 0.20d);
        _lastContinuousTimestamp = now;

        double deltaMm = ToPositiveControlDelta(signal, signal.DeltaMm);
        double velocity = Math.Abs(deltaMm) / elapsed;
        double speed01 = Math.Clamp((velocity - 38.0) / 190.0, 0.0, 1.0);
        double acceleration = 1.0 + 1.9 * Math.Pow(speed01, 1.35);
        _continuousDeltaPercent = Math.Clamp(
            _continuousDeltaPercent + deltaMm * baseGain * acceleration,
            -100.0,
            100.0);
    }

    private void QueueContinuousTarget(int startValue, Action<int> queueTarget)
    {
        int target = Math.Clamp(startValue + (int)Math.Round(_continuousDeltaPercent), 0, 100);
        queueTarget(target);
    }

    private void QueueMediaSeek(GestureSignal signal)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = _lastMediaTimestamp == 0
            ? 1d / 60d
            : Math.Clamp((now - _lastMediaTimestamp) / (double)Stopwatch.Frequency, 1d / 240d, 0.20d);
        _lastMediaTimestamp = now;

        double deltaMm = ToPositiveControlDelta(signal, signal.DeltaMm);
        double velocity = Math.Abs(deltaMm) / elapsed;
        double speed01 = Math.Clamp((velocity - 32.0) / 185.0, 0.0, 1.0);
        double acceleration = 1.0 + 2.7 * Math.Pow(speed01, 1.45);
        double secondsPerMm = 0.18 * acceleration;
        double seconds = Math.Clamp(deltaMm * secondsPerMm, -8.0, 8.0);
        _seekCumulativeSeconds = Math.Clamp(_seekCumulativeSeconds + seconds, -1800.0, 1800.0);
        _ = QueueMediaWhenReadyAsync(seconds);
    }

    private async Task QueueMediaWhenReadyAsync(double deltaSeconds)
    {
        try
        {
            Task<bool>? begin = _mediaBeginTask;
            if (begin is null || !await begin.ConfigureAwait(false))
                return;
            _media.QueueSeekDelta(deltaSeconds);
        }
        catch
        {
        }
    }

    private static double ToPositiveControlDelta(GestureSignal signal, double value) =>
        signal.Edge is TouchpadEdge.Left or TouchpadEdge.Right ? -value : value;

    private void End(GestureActionKind action)
    {
        _setGestureActive(action, false);
        _continuousDeltaPercent = 0;
        _lastContinuousTimestamp = 0;

        if (action == GestureActionKind.PreviousNextTrack)
        {
            _trackSwipeFired = false;
            _trackGestureStarted = 0;
            _trackMaxTravelMm = 0;
            _trackStayedCandidate = false;
        }

        if (action == GestureActionKind.MediaSeek)
        {
            _ = _media.EndSeekAsync();
            _mediaBeginTask = null;
            _lastMediaTimestamp = 0;
        }
    }
}
