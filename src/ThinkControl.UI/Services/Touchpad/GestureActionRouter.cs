using System.Diagnostics;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class GestureActionRouter
{
    private const double VolumeBaseGain = 1.0;
    private const double BrightnessBaseGain = 1.15;

    private readonly NativeInputService _nativeInput;
    private readonly MediaSessionService _media;
    private readonly Func<int> _getVolume;
    private readonly Action<int> _queueVolume;
    private readonly Func<int> _getBrightness;
    private readonly Action<int> _queueBrightness;
    private readonly Func<int> _getKeyboardIndex;
    private readonly Action<int> _queueKeyboardIndex;
    private readonly Func<int> _getPerformanceIndex;
    private readonly Action<int> _queuePerformanceIndex;
    private readonly Action<GestureActionKind, bool> _setGestureActive;

    private int _volumeAtStart;
    private int _brightnessAtStart;
    private int _keyboardAtStart;
    private int _performanceAtStart;
    private double _continuousDeltaPercent;
    private long _lastContinuousTimestamp;
    private double _seekCumulativeSeconds;
    private Task<bool>? _mediaBeginTask;
    private long _lastMediaTimestamp;

    internal GestureActionRouter(
        NativeInputService nativeInput,
        MediaSessionService media,
        Func<int> getVolume,
        Action<int> queueVolume,
        Func<int> getBrightness,
        Action<int> queueBrightness,
        Func<int> getKeyboardIndex,
        Action<int> queueKeyboardIndex,
        Func<int> getPerformanceIndex,
        Action<int> queuePerformanceIndex,
        Action<GestureActionKind, bool> setGestureActive)
    {
        _nativeInput = nativeInput;
        _media = media;
        _getVolume = getVolume;
        _queueVolume = queueVolume;
        _getBrightness = getBrightness;
        _queueBrightness = queueBrightness;
        _getKeyboardIndex = getKeyboardIndex;
        _queueKeyboardIndex = queueKeyboardIndex;
        _getPerformanceIndex = getPerformanceIndex;
        _queuePerformanceIndex = queuePerformanceIndex;
        _setGestureActive = setGestureActive;
    }

    internal double CurrentSeekDeltaSeconds => _seekCumulativeSeconds;

    internal void Handle(GestureSignal signal)
    {
        switch (signal.Phase)
        {
            case GesturePhase.Claimed:
                Begin(signal);
                break;
            case GesturePhase.Active:
                Update(signal);
                break;
            case GesturePhase.Released:
            case GesturePhase.Cancelled:
                End(signal.Action);
                break;
        }
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
            case GestureActionKind.KeyboardBacklight:
                _setGestureActive(signal.Action, true);
                _keyboardAtStart = _getKeyboardIndex();
                ApplyDiscreteTarget(signal, signal.TotalTravelMm, _keyboardAtStart, _queueKeyboardIndex);
                break;
            case GestureActionKind.PerformanceMode:
                _setGestureActive(signal.Action, true);
                _performanceAtStart = _getPerformanceIndex();
                ApplyDiscreteTarget(signal, signal.TotalTravelMm, _performanceAtStart, _queuePerformanceIndex);
                break;
            case GestureActionKind.PreviousNextTrack:
                if (ToPositiveControlDelta(signal, signal.TotalTravelMm) >= 0) _nativeInput.NextTrack();
                else _nativeInput.PreviousTrack();
                break;
            case GestureActionKind.PlayPause:
                _nativeInput.TogglePlayPause();
                break;
            case GestureActionKind.Mute:
                _nativeInput.ToggleMute();
                break;
            case GestureActionKind.TaskView:
                _nativeInput.ShowTaskView();
                break;
            case GestureActionKind.ShowDesktop:
                _nativeInput.ShowDesktop();
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
            case GestureActionKind.KeyboardBacklight:
                ApplyDiscreteTarget(signal, signal.TotalTravelMm, _keyboardAtStart, _queueKeyboardIndex);
                break;
            case GestureActionKind.PerformanceMode:
                ApplyDiscreteTarget(signal, signal.TotalTravelMm, _performanceAtStart, _queuePerformanceIndex);
                break;
        }
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

    private static void ApplyDiscreteTarget(
        GestureSignal signal,
        double travelMm,
        int startIndex,
        Action<int> queueTarget)
    {
        double signedTravel = ToPositiveControlDelta(signal, travelMm);
        int steps = (int)Math.Truncate(signedTravel / 8.0);
        queueTarget(Math.Clamp(startIndex + steps, 0, 2));
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

        // A slow drag stays precise, but a deliberate swipe ramps much harder than
        // alpha.15.1. This behaves more like direct timeline scrubbing: small finger
        // corrections move by fractions of a second while a fast 40–60 mm sweep can
        // cover tens of seconds instead of feeling stuck. MediaSessionService still
        // coalesces GSMTC writes, and Spotify/Apple Music keep their lower cadence.
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

        if (action == GestureActionKind.MediaSeek)
        {
            _ = _media.EndSeekAsync();
            _mediaBeginTask = null;
            _lastMediaTimestamp = 0;
        }
    }
}
