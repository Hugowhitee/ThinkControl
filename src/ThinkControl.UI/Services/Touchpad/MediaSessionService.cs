using Windows.Media.Control;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class MediaSessionService
{
    private static readonly TimeSpan SeekCadence = TimeSpan.FromMilliseconds(170);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _seekGate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private TimeSpan _anchorPosition;
    private DateTimeOffset _anchorUpdatedAt;
    private TimeSpan _minimum;
    private TimeSpan _maximum;
    private bool _playingAtAnchor;
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
                ResetSeekState();
                return false;
            }

            GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = _session.GetTimelineProperties();
            GlobalSystemMediaTransportControlsSessionPlaybackInfo playback = _session.GetPlaybackInfo();
            _anchorPosition = timeline.Position;
            _anchorUpdatedAt = timeline.LastUpdatedTime;
            _playingAtAnchor = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
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
            ResetSeekState();
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
            _pendingOffsetSeconds = Math.Clamp(_pendingOffsetSeconds + deltaSeconds, -7200, 7200);

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
                // Spotify, browsers and several GSMTC bridges become unstable when
                // a new remote seek arrives while the previous one is still being
                // processed. Keep only the latest accumulated target and deliberately
                // limit remote writes to roughly 6/s. The gesture remains smooth in
                // ThinkControl because all raw frames still update the pending target.
                await Task.Delay(SeekCadence).ConfigureAwait(false);

                double offset;
                lock (_seekGate)
                {
                    if (generation != _generation)
                        return;
                    offset = _pendingOffsetSeconds;
                    if (Math.Abs(offset - _lastAppliedOffsetSeconds) < 0.30)
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
                restart = _seekReady && generation == _generation && Math.Abs(_pendingOffsetSeconds - _lastAppliedOffsetSeconds) >= 0.30;
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

            TimeSpan liveAnchor = _anchorPosition;
            if (_playingAtAnchor && _anchorUpdatedAt != default)
            {
                TimeSpan elapsed = DateTimeOffset.UtcNow - _anchorUpdatedAt;
                if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromMinutes(10))
                    liveAnchor += elapsed;
            }

            TimeSpan target = liveAnchor + TimeSpan.FromSeconds(offsetSeconds);
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

    internal async Task EndSeekAsync()
    {
        double finalOffset;
        int generation;
        lock (_seekGate)
        {
            finalOffset = _pendingOffsetSeconds;
            generation = _generation;
        }

        // One final write guarantees the released finger position wins even when
        // the cadence worker was still in its debounce window.
        if (_seekReady && Math.Abs(finalOffset - _lastAppliedOffsetSeconds) >= 0.12)
            _ = await ApplyOffsetAsync(finalOffset).ConfigureAwait(false);

        lock (_seekGate)
        {
            if (generation == _generation)
                _generation++;
            _pendingOffsetSeconds = 0;
            _lastAppliedOffsetSeconds = 0;
        }
        _seekReady = false;
        _session = null;
    }

    internal void EndSeek() => ResetSeekState();

    private void ResetSeekState()
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
