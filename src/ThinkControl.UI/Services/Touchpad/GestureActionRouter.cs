using System.Diagnostics;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class GestureActionRouter
{
    private readonly NativeInputService _nativeInput;
    private readonly MediaSessionService _media;
    private readonly Func<int> _getBrightness;
    private readonly Action<int> _queueBrightness;
    private readonly Func<int, Task> _stepKeyboard;
    private readonly Func<int, bool> _stepPerformance;

    private int _brightnessAtStart;
    private double _stepAccumulator;
    private Task<bool>? _mediaBeginTask;
    private long _lastMediaTimestamp;

    internal GestureActionRouter(
        NativeInputService nativeInput,
        MediaSessionService media,
        Func<int> getBrightness,
        Action<int> queueBrightness,
        Func<int, Task> stepKeyboard,
        Func<int, bool> stepPerformance)
    {
        _nativeInput = nativeInput;
        _media = media;
        _getBrightness = getBrightness;
        _queueBrightness = queueBrightness;
        _stepKeyboard = stepKeyboard;
        _stepPerformance = stepPerformance;
    }

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
        _stepAccumulator = 0;
        switch (signal.Action)
        {
            case GestureActionKind.Volume:
                ApplyStepped(ToPositiveControlDelta(signal, signal.DeltaMm), 2.0,
                    _nativeInput.VolumeUp, _nativeInput.VolumeDown);
                break;
            case GestureActionKind.Brightness:
                _brightnessAtStart = _getBrightness();
                ApplyBrightness(signal, signal.TotalTravelMm);
                break;
            case GestureActionKind.MediaSeek:
                _lastMediaTimestamp = Stopwatch.GetTimestamp();
                _mediaBeginTask = _media.BeginSeekAsync();
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
            case GestureActionKind.KeyboardBacklight:
                _ = ApplyAsyncStep(ToPositiveControlDelta(signal, signal.TotalTravelMm), 8.0, _stepKeyboard);
                break;
            case GestureActionKind.PerformanceMode:
                ApplyStep(ToPositiveControlDelta(signal, signal.TotalTravelMm), 8.0, _stepPerformance);
                break;
        }
    }

    private void Update(GestureSignal signal)
    {
        switch (signal.Action)
        {
            case GestureActionKind.Volume:
                ApplyStepped(ToPositiveControlDelta(signal, signal.DeltaMm), 2.0,
                    _nativeInput.VolumeUp, _nativeInput.VolumeDown);
                break;
            case GestureActionKind.Brightness:
                ApplyBrightness(signal, signal.TotalTravelMm);
                break;
            case GestureActionKind.MediaSeek:
                QueueMediaSeek(signal);
                break;
            case GestureActionKind.KeyboardBacklight:
                _ = ApplyAsyncStep(ToPositiveControlDelta(signal, signal.DeltaMm), 8.0, _stepKeyboard);
                break;
            case GestureActionKind.PerformanceMode:
                ApplyStep(ToPositiveControlDelta(signal, signal.DeltaMm), 8.0, _stepPerformance);
                break;
        }
    }

    private void QueueMediaSeek(GestureSignal signal)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = _lastMediaTimestamp == 0
            ? 1d / 60d
            : Math.Clamp((now - _lastMediaTimestamp) / (double)Stopwatch.Frequency, 1d / 240d, 0.15d);
        _lastMediaTimestamp = now;

        double deltaMm = ToPositiveControlDelta(signal, signal.DeltaMm);
        double velocity = Math.Abs(deltaMm) / elapsed;

        // Precision first, acceleration second. A slow 20 mm movement seeks about
        // five seconds; the same distance flicked quickly can seek ~15-20 seconds.
        // This mirrors modern touch/scroll acceleration without flooding the media
        // session with playback-position requests.
        double acceleration = 1.0 + Math.Clamp((velocity - 35.0) / 80.0, 0.0, 2.5);
        double seconds = deltaMm * 0.25 * acceleration;
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

    private void ApplyBrightness(GestureSignal signal, double travelMm)
    {
        int target = Math.Clamp(
            _brightnessAtStart + (int)Math.Round(ToPositiveControlDelta(signal, travelMm) * 1.25),
            0,
            100);
        _queueBrightness(target);
    }

    private static double ToPositiveControlDelta(GestureSignal signal, double value) =>
        signal.Edge is TouchpadEdge.Left or TouchpadEdge.Right ? -value : value;

    private void ApplyStepped(double deltaMm, double millimetresPerStep, Func<bool> increase, Func<bool> decrease)
    {
        _stepAccumulator += deltaMm;
        while (Math.Abs(_stepAccumulator) >= millimetresPerStep)
        {
            int direction = Math.Sign(_stepAccumulator);
            if (direction > 0) increase(); else decrease();
            _stepAccumulator -= direction * millimetresPerStep;
        }
    }

    private async Task ApplyAsyncStep(double deltaMm, double millimetresPerStep, Func<int, Task> step)
    {
        _stepAccumulator += deltaMm;
        while (Math.Abs(_stepAccumulator) >= millimetresPerStep)
        {
            int direction = Math.Sign(_stepAccumulator);
            _stepAccumulator -= direction * millimetresPerStep;
            try { await step(direction).ConfigureAwait(false); }
            catch { return; }
        }
    }

    private void ApplyStep(double deltaMm, double millimetresPerStep, Func<int, bool> step)
    {
        _stepAccumulator += deltaMm;
        while (Math.Abs(_stepAccumulator) >= millimetresPerStep)
        {
            int direction = Math.Sign(_stepAccumulator);
            if (!step(direction))
                return;
            _stepAccumulator -= direction * millimetresPerStep;
        }
    }

    private void End(GestureActionKind action)
    {
        _stepAccumulator = 0;
        if (action == GestureActionKind.MediaSeek)
        {
            _media.EndSeek();
            _mediaBeginTask = null;
            _lastMediaTimestamp = 0;
        }
    }
}
