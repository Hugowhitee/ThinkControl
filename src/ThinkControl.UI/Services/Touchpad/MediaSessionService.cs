using Windows.Media.Control;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class MediaSessionService
{
    private static readonly TimeSpan SeekCadence = TimeSpan.FromMilliseconds(180);
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
                // Spotify and several GSMTC bridges do not tolerate a remote seek per
                // touch frame. Keep only the accumulated target and send at roughly
                // 5-6 Hz while the finger is down.
                await Task.Delay(SeekCadence).ConfigureAwait(false);

                double offset;
                lock (_seekGate)
                {
                    if (generation != _generation)
                        return;
                    offset = _pendingOffsetSeconds;
                    if (Math.Abs(offset - _lastAppliedOffsetSeconds) < 0.35)
                        return;
                }

                bool applied = await ApplyOffsetAsync(offset, generation).ConfigureAwait(false);
                if (!applied)
                    return;

                lock (_seekGate)
                {
                    if (generation != _generation)
                        return;
                    _lastAppliedOffsetSeconds = offset;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _workerRunning, 0);
            bool restart;
            lock (_seekGate)
                restart = _seekReady && generation == _generation && Math.Abs(_pendingOffsetSeconds - _lastAppliedOffsetSeconds) >= 0.35;
            if (restart && Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
                _ = Task.Run(ProcessSeekQueueAsync);
        }
    }

    private async Task<bool> ApplyOffsetAsync(double offsetSeconds, int generation)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_seekGate)
            {
                if (!_seekReady || _session is null || generation != _generation)
                    return false;
            }

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

            bool changed = await _session.TryChangePlaybackPositionAsync(target.Ticks);
            if (!changed)
                return false;

            // Do not replace the gesture anchor with the new target: queued offsets
            // are cumulative from gesture start. Keeping one anchor avoids compounding
            // Spotify's delayed timeline updates into ever-larger seeks.
            return true;
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
        double lastApplied;
        int finalGeneration;

        lock (_seekGate)
        {
            finalOffset = _pendingOffsetSeconds;
            lastApplied = _lastAppliedOffsetSeconds;

            // Invalidate the cadence worker before issuing the final target. Any
            // worker that already passed its outer generation check re-checks the
            // token after acquiring _gate and therefore cannot overwrite the final
            // released-finger position afterwards.
            finalGeneration = ++_generation;
        }

        if (_seekReady && Math.Abs(finalOffset - lastApplied) >= 0.12)
            _ = await ApplyOffsetAsync(finalOffset, finalGeneration).ConfigureAwait(false);

        // A new gesture can begin while the final GSMTC write above is awaiting.
        // In that case BeginSeekAsync increments _generation and owns the new session.
        // The old teardown must not clear its offsets/readiness/session.
        lock (_seekGate)
        {
            if (finalGeneration != _generation)
                return;

            _generation++;
            _pendingOffsetSeconds = 0;
            _lastAppliedOffsetSeconds = 0;
            _seekReady = false;
            _session = null;
        }
    }

    internal void EndSeek() => ResetSeekState();

    private void ResetSeekState()
    {
        lock (_seekGate)
        {
            _generation++;
            _pendingOffsetSeconds = 0;
            _lastAppliedOffsetSeconds = 0;
            _seekReady = false;
            _session = null;
        }
    }
}
