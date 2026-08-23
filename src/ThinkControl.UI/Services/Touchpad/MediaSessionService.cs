using Windows.Media.Control;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class MediaSessionService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private TimeSpan _anchorPosition;
    private TimeSpan _minimum;
    private TimeSpan _maximum;
    private bool _seekReady;

    // Deliberately relative: a full-width gesture is useful for navigation, but
    // touching 80% across the pad never means "jump to 80% of the movie".
    private const double SecondsPerMillimetre = 0.75;

    internal async Task<bool> BeginSeekAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _session = _manager.GetCurrentSession();
            if (_session is null)
            {
                _seekReady = false;
                return false;
            }

            GlobalSystemMediaTransportControlsSessionTimelineProperties timeline =
                _session.GetTimelineProperties();
            _anchorPosition = timeline.Position;
            _minimum = timeline.MinSeekTime > TimeSpan.Zero ? timeline.MinSeekTime : timeline.StartTime;
            _maximum = timeline.MaxSeekTime > _minimum ? timeline.MaxSeekTime : timeline.EndTime;
            _seekReady = _maximum > _minimum;
            return _seekReady;
        }
        catch
        {
            _seekReady = false;
            _session = null;
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<bool> SeekRelativeAsync(double totalTravelMm)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_seekReady || _session is null)
                return false;

            TimeSpan delta = TimeSpan.FromSeconds(totalTravelMm * SecondsPerMillimetre);
            TimeSpan target = _anchorPosition + delta;
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
        _seekReady = false;
        _session = null;
    }
}
