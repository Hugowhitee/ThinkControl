using System.Windows.Threading;
using ThinkControl.Core.Audio;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class TouchpadFeatureHost : IDisposable
{
    private const int ContinuousVolumeLeadLimit = 8;
    private const int ContinuousBrightnessLeadLimit = 10;

    private readonly App _app;
    private readonly TouchpadGestureService _gestures;
    private readonly TouchpadHapticsService _haptics = new();
    private readonly NativeInputService _nativeInput;
    private readonly GestureActionRouter _actions;
    private readonly GestureOsdService _osd;
    private int _pendingVolume = -1;
    private int _volumeWorkerRunning;
    private int _confirmedVolume = -1;
    private int _volumeGestureActive;
    private int _pendingBrightness = -1;
    private int _brightnessWorkerRunning;
    private int _confirmedBrightness = -1;
    private int _brightnessGestureActive;
    private int _inputStartScheduled;
    private bool _disposed;

    internal TouchpadFeatureHost(App app)
    {
        _app = app;

        GestureOsdService? osd = null;
        _nativeInput = new NativeInputService(
            (label, value) => app.Dispatcher.BeginInvoke(new Action(() => osd?.Show(label, value))),
            () => !AudioSafetyPolicy.BlocksTouchpadAudio(app.AudioSafety.Mode));
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
            () => app.AudioSafety.Mode,
            mode => app.Dispatcher.BeginInvoke(new Action(() =>
                _osd.Show(AudioSafetyPolicy.DisplayName(mode) == "Silent" ? "Silent · media locked" : "Media locked", ReadVolumePercent()))),
            _nativeInput.TryGetVolumePercent,
            QueueGestureVolume,
            () => app.State.Brightness,
            QueueGestureBrightness,
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

    internal bool EnsureInputStarted(bool startupCritical = false)
    {
        if (_disposed)
            return false;
        if (_gestures.IsRunning)
            return true;
        if (Interlocked.CompareExchange(ref _inputStartScheduled, 1, 0) != 0)
            return true;

        // Tray startup is the hotkey/gesture service path, not a page-render path.
        // Register Raw Input immediately so a logged-in user does not have to wait for
        // WPF window construction, diagnostics or hardware/service discovery before
        // edge gestures work. Ordinary visible/page starts still defer the HID probe
        // until ContextIdle so first paint wins.
        if (startupCritical)
        {
            try
            {
                return !_disposed && _gestures.Start();
            }
            finally
            {
                Interlocked.Exchange(ref _inputStartScheduled, 0);
            }
        }

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

    internal void CancelAudioActions()
    {
        Interlocked.Exchange(ref _pendingVolume, -1);
        if (!_disposed)
            _gestures.CancelCurrent("Audio safety mode changed");
    }

    private bool ApplyOsdValue(string label, int value)
    {
        if (label.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Muted", StringComparison.OrdinalIgnoreCase))
        {
            QueueVolume(value);
            return !AudioSafetyPolicy.BlocksTouchpadAudio(_app.AudioSafety.Mode);
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
        if (action == GestureActionKind.Volume)
        {
            Interlocked.Exchange(ref _pendingVolume, -1);
            if (active)
            {
                Interlocked.Exchange(ref _confirmedVolume, -1);
                Volatile.Write(ref _volumeGestureActive, 1);
            }
            else
            {
                Volatile.Write(ref _volumeGestureActive, 0);
                Interlocked.Exchange(ref _confirmedVolume, -1);
            }
            return;
        }

        if (action == GestureActionKind.Brightness)
        {
            Interlocked.Exchange(ref _pendingBrightness, -1);
            if (active)
            {
                Interlocked.Exchange(ref _confirmedBrightness, Math.Clamp(_app.State.Brightness, 0, 100));
                Volatile.Write(ref _brightnessGestureActive, 1);
            }
            else
            {
                Volatile.Write(ref _brightnessGestureActive, 0);
                Interlocked.Exchange(ref _confirmedBrightness, -1);
            }
        }
    }

    private void QueueGestureVolume(int value)
    {
        if (Volatile.Read(ref _volumeGestureActive) == 0)
            return;

        int confirmed = Volatile.Read(ref _confirmedVolume);
        if (confirmed < 0)
        {
            if (_nativeInput.TryGetVolumePercent() is not int live)
                return;
            confirmed = live;
            Interlocked.Exchange(ref _confirmedVolume, confirmed);
        }

        int bounded = Math.Clamp(
            value,
            Math.Max(0, confirmed - ContinuousVolumeLeadLimit),
            Math.Min(100, confirmed + ContinuousVolumeLeadLimit));
        QueueVolume(bounded);
    }

    private void QueueGestureBrightness(int value)
    {
        if (Volatile.Read(ref _brightnessGestureActive) == 0)
            return;

        int confirmed = Volatile.Read(ref _confirmedBrightness);
        if (confirmed < 0)
        {
            confirmed = Math.Clamp(_app.State.Brightness, 0, 100);
            Interlocked.Exchange(ref _confirmedBrightness, confirmed);
        }

        int bounded = Math.Clamp(
            value,
            Math.Max(0, confirmed - ContinuousBrightnessLeadLimit),
            Math.Min(100, confirmed + ContinuousBrightnessLeadLimit));
        QueueBrightness(bounded);
    }

    private void QueueVolume(int value)
    {
        if (AudioSafetyPolicy.BlocksTouchpadAudio(_app.AudioSafety.Mode))
        {
            Interlocked.Exchange(ref _pendingVolume, -1);
            return;
        }

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
                if (AudioSafetyPolicy.BlocksTouchpadAudio(_app.AudioSafety.Mode))
                {
                    Interlocked.Exchange(ref _pendingVolume, -1);
                    break;
                }

                int target = Volatile.Read(ref _pendingVolume);
                if (target < 0 || target == lastApplied)
                    break;

                if (!_nativeInput.TrySetVolume(target, out int applied))
                {
                    Interlocked.CompareExchange(ref _pendingVolume, -1, target);
                    break;
                }

                lastApplied = applied;
                Interlocked.Exchange(ref _confirmedVolume, applied);
                Interlocked.CompareExchange(ref _pendingVolume, -1, target);
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
                !AudioSafetyPolicy.BlocksTouchpadAudio(_app.AudioSafety.Mode) &&
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
                    Interlocked.Exchange(ref _confirmedBrightness, target);
                    Interlocked.CompareExchange(ref _pendingBrightness, -1, target);
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
        Volatile.Write(ref _volumeGestureActive, 0);
        Volatile.Write(ref _brightnessGestureActive, 0);
        Interlocked.Exchange(ref _pendingVolume, -1);
        Interlocked.Exchange(ref _pendingBrightness, -1);
        Interlocked.Exchange(ref _confirmedVolume, -1);
        Interlocked.Exchange(ref _confirmedBrightness, -1);
        _gestures.Dispose();
        _nativeInput.Dispose();
        _osd.Dispose();
    }
}
