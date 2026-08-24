using System.Diagnostics;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class GestureActionRouter
{
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
                ApplyVolume(signal, signal.TotalTravelMm);
                break;
            case GestureActionKind.Brightness:
                _setGestureActive(signal.Action, true);
                _brightnessAtStart = _getBrightness();
                ApplyBrightness(signal, signal.TotalTravelMm);
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
                ApplyVolume(signal, signal.TotalTravelMm);
                break;
            case GestureActionKind.Brightness:
                ApplyBrightness(signal, signal.TotalTravelMm);
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

    private void ApplyVolume(GestureSignal signal, double travelMm)
    {
        int target = Math.Clamp(
            _volumeAtStart + (int)Math.Round(ToPositiveControlDelta(signal, travelMm) * 1.4),
            0,
            100);
        _queueVolume(target);
    }

    private void ApplyBrightness(GestureSignal signal, double travelMm)
    {
        int target = Math.Clamp(
            _brightnessAtStart + (int)Math.Round(ToPositiveControlDelta(signal, travelMm) * 1.25),
            0,
            100);
        _queueBrightness(target);
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

        // Slow movement stays precise. Fast movement accelerates, but MediaSessionService
        // receives only the newest accumulated target on its bounded cadence.
        double speed01 = Math.Clamp((velocity - 24.0) / 175.0, 0.0, 1.0);
        double acceleration = 1.0 + 5.0 * Math.Pow(speed01, 1.45);
        double seconds = deltaMm * 0.28 * acceleration;
        seconds = Math.Clamp(seconds, -22.0, 22.0);
        _seekCumulativeSeconds = Math.Clamp(_seekCumulativeSeconds + seconds, -600.0, 600.0);
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
        // Cancelling first guarantees workers throw away stale targets immediately.
        // At most one already-running hardware/Windows write can finish after release.
        _setGestureActive(action, false);

        if (action == GestureActionKind.MediaSeek)
        {
            _ = _media.EndSeekAsync();
            _mediaBeginTask = null;
            _lastMediaTimestamp = 0;
        }
    }
}