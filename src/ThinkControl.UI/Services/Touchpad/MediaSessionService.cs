using Windows.Media.Control;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class MediaSessionService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _seekGate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private TimeSpan _anchorPosition;
    private TimeSpan _minimum;
    private TimeSpan _maximum;
    private bool _seekReady;
    private double _pendingOffsetSeconds;
    private double _lastAppliedOffsetSeconds;
    private int _workerRunning;
    private int _generation;

    internal async Task<bool> BeginSeekAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _session = _manager.GetCurrentSession();
            if (_session is null)
            {
                EndSeek();
                return false;
            }

            GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = _session.GetTimelineProperties();
            _anchorPosition = timeline.Position;
            _minimum = timeline.MinSeekTime > TimeSpan.Zero ? timeline.MinSeekTime : timeline.StartTime;
            _maximum = timeline.MaxSeekTime > _minimum ? timeline.MaxSeekTime : timeline.EndTime;
            _seekReady = _maximum > _minimum;
            lock (_seekGate)
            {
                _pendingOffsetSeconds = 0;
                _lastAppliedOffsetSeconds = 0;
                _generation++;
            }
            return _seekReady;
        }
        catch
        {
            EndSeek();
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void QueueSeekDelta(double deltaSeconds)
    {
        if (!_seekReady || !double.IsFinite(deltaSeconds) || Math.Abs(deltaSeconds) < 0.01)
            return;

        lock (_seekGate)
            _pendingOffsetSeconds = Math.Clamp(_pendingOffsetSeconds + deltaSeconds, -3600, 3600);

        if (Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
            _ = Task.Run(ProcessSeekQueueAsync);
    }

    private async Task ProcessSeekQueueAsync()
    {
        int generation;
        lock (_seekGate)
            generation = _generation;

        try
        {
            while (_seekReady)
            {
                // Coalesce high-frequency Raw Input frames. Spotify and several
                // browser media sessions behave much better with ~12 seek writes/s
                // than with one playback-position request per touch frame.
                await Task.Delay(80).ConfigureAwait(false);

                double offset;
                lock (_seekGate)
                {
                    if (generation != _generation)
                        return;
                    offset = _pendingOffsetSeconds;
                    if (Math.Abs(offset - _lastAppliedOffsetSeconds) < 0.15)
                        return;
                }

                bool applied = await ApplyOffsetAsync(offset).ConfigureAwait(false);
                if (!applied)
                    return;

                lock (_seekGate)
                    _lastAppliedOffsetSeconds = offset;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _workerRunning, 0);
            bool restart;
            lock (_seekGate)
                restart = _seekReady && generation == _generation && Math.Abs(_pendingOffsetSeconds - _lastAppliedOffsetSeconds) >= 0.15;
            if (restart && Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
                _ = Task.Run(ProcessSeekQueueAsync);
        }
    }

    private async Task<bool> ApplyOffsetAsync(double offsetSeconds)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_seekReady || _session is null)
                return false;

            TimeSpan target = _anchorPosition + TimeSpan.FromSeconds(offsetSeconds);
            if (target < _minimum)
                target = _minimum;
            if (target > _maximum)
                target = _maximum;

            return await _session.TryChangePlaybackPositionAsync(target.Ticks);
        }
        catch
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void EndSeek()
    {
        lock (_seekGate)
        {
            _generation++;
            _pendingOffsetSeconds = 0;
            _lastAppliedOffsetSeconds = 0;
        }
        _seekReady = false;
        _session = null;
    }
}
