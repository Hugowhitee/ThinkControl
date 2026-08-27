using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class TouchpadFeatureHost : IDisposable
{
    private readonly App _app;
    private readonly TouchpadGestureService _gestures;
    private readonly TouchpadHapticsService _haptics = new();
    private readonly NativeInputService _nativeInput;
    private readonly GestureActionRouter _actions;
    private readonly GestureOsdService _osd;
    private int _pendingVolume = -1;
    private int _volumeWorkerRunning;
    private int _pendingBrightness = -1;
    private int _brightnessWorkerRunning;
    private int _lastQueuedKeyboardIndex = -1;
    private int _lastQueuedPerformanceIndex = -1;
    private bool _disposed;

    internal TouchpadFeatureHost(App app)
    {
        _app = app;

        GestureOsdService? osd = null;
        _nativeInput = new NativeInputService((label, value) =>
            app.Dispatcher.BeginInvoke(new Action(() => osd?.Show(label, value))));
        _osd = osd = new GestureOsdService(
            () => app.UserSettings.Current,
            ApplyOsdValue,
            _nativeInput.ToggleMute);

        TouchpadGestureConfiguration configuration =
            app.UserSettings.Current.TouchpadGestures ??
            (TouchpadGestureConfiguration.Default with { Enabled = false });

        _actions = new GestureActionRouter(
            _nativeInput,
            new MediaSessionService(),
            () => app.UserSettings.Current.TouchpadGestures ?? configuration,
            _nativeInput.GetVolumePercent,
            QueueVolume,
            () => app.State.Brightness,
            QueueBrightness,
            GetKeyboardIndex,
            QueueKeyboardIndex,
            GetPerformanceIndex,
            QueuePerformanceIndex,
            SetGestureActive,
            next => app.Dispatcher.BeginInvoke(new Action(() => _osd.ShowTrack(next))),
            () => app.Dispatcher.BeginInvoke(new Action(_osd.ShowTrackCenter)));

        bool x9 = string.Equals(app.State.MachineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(app.State.MachineType, "21Q7", StringComparison.OrdinalIgnoreCase);

        _gestures = new TouchpadGestureService(
            configuration,
            _actions,
            fallbackWidthMm: x9 ? 135.0 : 100.0,
            fallbackHeightMm: x9 ? 80.0 : 60.0);
        _gestures.GestureChanged += signal => GestureChanged?.Invoke(signal);
        _gestures.TouchpadDetected += geometry => TouchpadDetected?.Invoke(geometry);
        _gestures.ContactFrameReceived += (contacts, geometry) => ContactFrameReceived?.Invoke(contacts, geometry);
    }

    internal event Action<GestureSignal>? GestureChanged;
    internal event Action<TouchpadGeometry>? TouchpadDetected;
    internal event Action<IReadOnlyList<TouchContact>, TouchpadGeometry>? ContactFrameReceived;

    internal TouchpadGestureConfiguration Configuration => _gestures.Configuration;
    internal TouchpadGeometry? Geometry => _gestures.Geometry;
    internal bool IsInputRunning => _gestures.IsRunning;
    internal double CurrentSeekDeltaSeconds => _actions.CurrentSeekDeltaSeconds;
    internal int ReadVolumePercent() => _nativeInput.GetVolumePercent();
    internal int? CurrentVolumeTarget
    {
        get
        {
            int value = Volatile.Read(ref _pendingVolume);
            return value >= 0 ? value : null;
        }
    }
    internal int? CurrentBrightnessTarget
    {
        get
        {
            int value = Volatile.Read(ref _pendingBrightness);
            return value >= 0 ? value : null;
        }
    }
    internal TouchpadHapticStatus HapticStatus => _haptics.Read(
        hidTouchpadPresent: _gestures.Geometry is not null,
        hidFeedbackSupported: _gestures.HapticFeedbackSupported,
        hidClickForceSupported: _gestures.ClickForceSupported);

    internal bool EnsureInputStarted() => !_disposed && _gestures.Start();
    internal void StopInputIfGesturesDisabled()
    {
        if (!_disposed)
            _gestures.StopIfDisabled();
    }

    internal void UpdateConfiguration(TouchpadGestureConfiguration configuration)
    {
        TouchpadGestureConfiguration sanitized = configuration.Sanitize();
        _app.UserSettings.Update(settings => settings with { TouchpadGestures = sanitized });
        _gestures.UpdateConfiguration(sanitized);
        if (sanitized.Enabled)
            EnsureInputStarted();
    }

    internal bool SetHapticEnabled(bool enabled) => _haptics.SetFeedbackEnabled(enabled);
    internal bool SetHapticIntensity(int intensity) => _haptics.SetFeedbackIntensity(intensity);
    internal bool SetClickForceSensitivity(int sensitivity) => _haptics.SetClickForceSensitivity(sensitivity);

    internal void CancelCurrent(string reason) => _gestures.CancelCurrent(reason);

    private bool ApplyOsdValue(string label, int value)
    {
        if (label.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Muted", StringComparison.OrdinalIgnoreCase))
        {
            QueueVolume(value);
            return true;
        }

        if (label.Contains("Brightness", StringComparison.OrdinalIgnoreCase))
        {
            QueueBrightness(value);
            return true;
        }

        return false;
    }

    private void SetGestureActive(GestureActionKind action, bool active)
    {
        if (active)
        {
            if (action == GestureActionKind.KeyboardBacklight)
                Interlocked.Exchange(ref _lastQueuedKeyboardIndex, -1);
            else if (action == GestureActionKind.PerformanceMode)
                Interlocked.Exchange(ref _lastQueuedPerformanceIndex, -1);
            return;
        }
    }

    private void QueueVolume(int value)
    {
        Interlocked.Exchange(ref _pendingVolume, Math.Clamp(value, 0, 100));
        if (Interlocked.CompareExchange(ref _volumeWorkerRunning, 1, 0) != 0)
            return;

        _ = Task.Run(ProcessVolumeQueueAsync);
    }

    private async Task ProcessVolumeQueueAsync()
    {
        int lastApplied = -1;
        try
        {
            while (!_disposed)
            {
                int target = Volatile.Read(ref _pendingVolume);
                if (target < 0 || target == lastApplied)
                    break;

                if (!_nativeInput.SetVolume(target))
                {
                    Interlocked.CompareExchange(ref _pendingVolume, -1, target);
                    break;
                }

                lastApplied = target;
                await Task.Delay(36).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            Interlocked.Exchange(ref _volumeWorkerRunning, 0);
            int pending = Volatile.Read(ref _pendingVolume);
            if (!_disposed && pending >= 0 && pending != lastApplied &&
                Interlocked.CompareExchange(ref _volumeWorkerRunning, 1, 0) == 0)
            {
                _ = Task.Run(ProcessVolumeQueueAsync);
            }
        }
    }

    private void QueueBrightness(int value)
    {
        Interlocked.Exchange(ref _pendingBrightness, Math.Clamp(value, 0, 100));
        if (Interlocked.CompareExchange(ref _brightnessWorkerRunning, 1, 0) != 0)
            return;

        _ = Task.Run(ProcessBrightnessQueueAsync);
    }

    private async Task ProcessBrightnessQueueAsync()
    {
        int lastApplied = -1;
        try
        {
            while (!_disposed)
            {
                int target = Volatile.Read(ref _pendingBrightness);
                if (target < 0 || target == lastApplied)
                    break;

                bool changed = _app.DisplayService.SetBrightness(target);
                if (changed)
                {
                    lastApplied = target;
                    await _app.Dispatcher.InvokeAsync(() =>
                    {
                        _app.State.Brightness = target;
                        _osd.Show("Brightness", target);
                    });
                }
                else
                {
                    Interlocked.CompareExchange(ref _pendingBrightness, -1, target);
                    break;
                }

                await Task.Delay(36).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            Interlocked.Exchange(ref _brightnessWorkerRunning, 0);
            int pending = Volatile.Read(ref _pendingBrightness);
            if (!_disposed && pending >= 0 && pending != lastApplied &&
                Interlocked.CompareExchange(ref _brightnessWorkerRunning, 1, 0) == 0)
            {
                _ = Task.Run(ProcessBrightnessQueueAsync);
            }
        }
    }

    private int GetKeyboardIndex()
    {
        string status = _app.State.KeyboardStatus;
        return status.Contains("Off", StringComparison.OrdinalIgnoreCase) ? 0 :
            status.Contains("Low", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private void QueueKeyboardIndex(int index)
    {
        if (!_app.State.CanKeyboardBacklight || _disposed)
            return;

        int target = Math.Clamp(index, 0, 2);
        if (Interlocked.Exchange(ref _lastQueuedKeyboardIndex, target) == target)
            return;

        _ = ApplyKeyboardIndexAsync(target);
    }

    private async Task ApplyKeyboardIndexAsync(int index)
    {
        string level = index switch { 0 => "Off", 1 => "Low", _ => "High" };
        try
        {
            await _app.Dispatcher.InvokeAsync(() => _app.SetKeyboardStaticLevelAsync(level)).Task.Unwrap();
            bool verified = await _app.Dispatcher.InvokeAsync(() =>
                _app.State.KeyboardStatus.Contains(level, StringComparison.OrdinalIgnoreCase));
            if (!verified)
            {
                Interlocked.Exchange(ref _lastQueuedKeyboardIndex, -1);
                return;
            }

            await _app.Dispatcher.InvokeAsync(() => _osd.Show("Keyboard light", index * 50));
        }
        catch
        {
            Interlocked.Exchange(ref _lastQueuedKeyboardIndex, -1);
        }
    }

    private int GetPerformanceIndex()
    {
        ThinkControlPowerMode[] modes =
        [ThinkControlPowerMode.Quiet, ThinkControlPowerMode.Balanced, ThinkControlPowerMode.Performance];
        int current = Array.FindIndex(modes,
            mode => string.Equals(mode.ToString(), _app.State.SelectedMode, StringComparison.OrdinalIgnoreCase));
        return current < 0 ? 1 : current;
    }

    private void QueuePerformanceIndex(int index)
    {
        if (_disposed)
            return;

        ThinkControlPowerMode[] modes =
        [ThinkControlPowerMode.Quiet, ThinkControlPowerMode.Balanced, ThinkControlPowerMode.Performance];
        int target = Math.Clamp(index, 0, modes.Length - 1);
        if (Interlocked.Exchange(ref _lastQueuedPerformanceIndex, target) == target)
            return;

        _app.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_app.SetPowerMode(modes[target]))
                _osd.Show(modes[target].ToString(), target * 50);
            else
                Interlocked.Exchange(ref _lastQueuedPerformanceIndex, -1);
        }));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Interlocked.Exchange(ref _pendingVolume, -1);
        Interlocked.Exchange(ref _pendingBrightness, -1);
        _gestures.Dispose();
        _nativeInput.Dispose();
        _osd.Dispose();
    }
}
