using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class TouchpadGestureService : IDisposable
{
    private readonly WindowsTouchpadInput _input;
    private readonly EdgeGestureRecognizer _recognizer;
    private readonly CursorGestureGuard _cursorGuard;
    private readonly GestureActionRouter _actions;
    private readonly System.Threading.Timer _watchdog;
    private readonly object _gate = new();

    private TouchpadGestureConfiguration _configuration;
    private DateTimeOffset _lastFrame = DateTimeOffset.MinValue;
    private bool _disposed;

    internal TouchpadGestureService(
        TouchpadGestureConfiguration configuration,
        GestureActionRouter actions,
        double fallbackWidthMm,
        double fallbackHeightMm)
    {
        _configuration = configuration.Sanitize();
        _actions = actions;
        _recognizer = new EdgeGestureRecognizer(_configuration);
        _cursorGuard = new CursorGestureGuard();
        _input = new WindowsTouchpadInput(fallbackWidthMm, fallbackHeightMm);
        _input.FrameReceived += OnFrameReceived;
        _input.TouchpadDetected += geometry => TouchpadDetected?.Invoke(geometry);
        _watchdog = new System.Threading.Timer(OnWatchdog, null, Timeout.Infinite, Timeout.Infinite);
    }

    internal event Action<GestureSignal>? GestureChanged;
    internal event Action<TouchpadGeometry>? TouchpadDetected;
    internal event Action<IReadOnlyList<TouchContact>, TouchpadGeometry>? ContactFrameReceived;

    internal TouchpadGeometry? Geometry => _input.Geometry;
    internal bool IsRunning => _input.IsStarted;
    internal bool HapticFeedbackSupported => _input.HapticFeedbackSupported;
    internal bool ClickForceSupported => _input.ClickForceSupported;
    internal TouchpadGestureConfiguration Configuration => _configuration;

    internal bool Start()
    {
        if (_disposed)
            return false;

        bool started = _input.Start();
        if (started)
            _watchdog.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        return started;
    }

    internal void StopIfDisabled()
    {
        lock (_gate)
        {
            if (_disposed || _configuration.Enabled)
                return;

            GestureSignal? cancelled = _recognizer.CancelCurrent("Touchpad page inactive");
            if (cancelled is not null)
                CompleteSignal(cancelled);
            _cursorGuard.Release();
            _watchdog.Change(Timeout.Infinite, Timeout.Infinite);
            _input.Stop();
        }
    }

    internal void UpdateConfiguration(TouchpadGestureConfiguration configuration)
    {
        lock (_gate)
        {
            GestureSignal? cancelled = _recognizer.CancelCurrent("Gesture settings changed");
            if (cancelled is not null)
                CompleteSignal(cancelled);

            _configuration = configuration.Sanitize();
            _recognizer.SetConfiguration(_configuration);
            if (!_configuration.Enabled)
                _cursorGuard.Release();
        }
    }

    internal void CancelCurrent(string reason)
    {
        lock (_gate)
        {
            GestureSignal? cancelled = _recognizer.CancelCurrent(reason);
            if (cancelled is not null)
                CompleteSignal(cancelled);
            else
                _cursorGuard.Release();
        }
    }

    private void OnFrameReceived(IReadOnlyList<TouchContact> contacts, TouchpadGeometry geometry)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _lastFrame = DateTimeOffset.UtcNow;
            ContactFrameReceived?.Invoke(contacts, geometry);

            GestureSignal? signal = _recognizer.ProcessFrame(contacts, geometry);
            if (signal is null)
                return;

            if (signal.Phase == GesturePhase.Claimed && _configuration.LockCursor)
                _cursorGuard.CaptureAtCurrentPosition();

            CompleteSignal(signal);
        }
    }

    private void CompleteSignal(GestureSignal signal)
    {
        _actions.Handle(signal);
        GestureChanged?.Invoke(signal);

        if (signal.Phase is GesturePhase.Released or GesturePhase.Cancelled)
            _cursorGuard.Release();
    }

    private void OnWatchdog(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !_cursorGuard.IsCaptured)
                return;

            if (DateTimeOffset.UtcNow - _lastFrame < TimeSpan.FromMilliseconds(800))
                return;

            GestureSignal? cancelled = _recognizer.CancelCurrent("Touchpad input timeout");
            if (cancelled is not null)
                CompleteSignal(cancelled);
            else
                _cursorGuard.Release();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _watchdog.Change(Timeout.Infinite, Timeout.Infinite);
            _input.FrameReceived -= OnFrameReceived;
            _input.Dispose();
            _cursorGuard.Dispose();
            _watchdog.Dispose();
        }
    }
}
