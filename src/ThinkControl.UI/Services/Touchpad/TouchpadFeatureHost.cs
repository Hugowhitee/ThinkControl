using System.Windows.Threading;
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
    private int _inputStartScheduled;
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
            SetGestureActive,
            next => app.Dispatcher.BeginInvoke(new Action(() => _osd.ShowTrack(next))),
            result => app.Dispatcher.BeginInvoke(new Action(() => _osd.ShowTrackCenter(result))),
            () => app.Dispatcher.BeginInvoke(new Action(app.ShowThinkControlFromTray)),
            () => app.Dispatcher.BeginInvoke(new Action(() => app.OpenAdvancedSafely("Home"))),
            () => app.Dispatcher.BeginInvoke(new Action(app.HideThinkControlToTray)));

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

    internal bool EnsureInputStarted()
    {
        if (_disposed)
            return false;
        if (_gestures.IsRunning)
            return true;

        // Raw-input registration includes a connected-device/HID probe. It is useful
        // work, but it must not sit synchronously inside a page VisibilityChanged or
        // shell transition. Queue one start after the current render/input work so
        // Advanced becomes visible first. Enabled gestures still start automatically
        // at app activation; this only changes when that setup blocks the WPF thread.
        if (Interlocked.CompareExchange(ref _inputStartScheduled, 1, 0) != 0)
            return true;

        _app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            try
            {
                if (!_disposed)
                    _gestures.Start();
            }
            finally
            {
                Interlocked.Exchange(ref _inputStartScheduled, 0);
            }
        }));
        return true;
    }

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

    private static void SetGestureActive(GestureActionKind action, bool active)
    {
        // Kept as the router's lifecycle hook so future bounded per-action state can
        // be reset in one place. The removed keyboard/performance gesture workers no
        // longer need hidden mutable state here.
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
