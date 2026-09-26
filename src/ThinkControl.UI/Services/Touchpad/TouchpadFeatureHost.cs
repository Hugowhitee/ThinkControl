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
    private readonly object _volumeWriteGate = new();
    private readonly object _brightnessWriteGate = new();
    private int _pendingVolume = -1;
    private int _volumeWorkerRunning;
    private int _confirmedVolume = -1;
    private int _volumeGestureActive;
    private int _volumeGestureGeneration;
    private int _pendingVolumeGeneration = -1;
    private int _pendingBrightness = -1;
    private int _brightnessWorkerRunning;
    private int _confirmedBrightness = -1;
    private int _brightnessGestureActive;
    private int _brightnessGestureGeneration;
    private int _pendingBrightnessGeneration = -1;
    private int _inputStartScheduled;
    private bool? _transientGestureEnabled;
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
            {
                string label = AudioSafetyPolicy.DisplayName(mode) == "Silent"
                    ? "Silent · output muted"
                    : "Gesture lock";
                if (ReadVolumePercent() is int volume)
                    _osd.Show(label, volume);
                else
                    _osd.ShowStatus(label);
            })),
            _nativeInput.TryGetVolumePercent,
            QueueGestureVolume,
            ReadGestureBrightnessBaseline,
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
    internal int? ReadVolumePercent() => _nativeInput.GetVolumePercent();
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

    internal bool EffectiveGesturesEnabled => _gestures.Configuration.Enabled;

    internal void UpdateConfiguration(
        TouchpadGestureConfiguration configuration,
        bool releaseGestureModeOwnership = false)
    {
        TouchpadGestureConfiguration sanitized = configuration.Sanitize();

        if (releaseGestureModeOwnership)
        {
            _transientGestureEnabled = null;
            _app.Modes.ReleaseFacet(ThinkControlModeFacet.TouchpadGestures);
        }

        bool persistedEnabled = releaseGestureModeOwnership
            ? sanitized.Enabled
            : _app.UserSettings.Current.TouchpadGestures?.Enabled == true;
        TouchpadGestureConfiguration persisted = (sanitized with { Enabled = persistedEnabled }).Sanitize();
        _app.UserSettings.Update(settings => settings with { TouchpadGestures = persisted });

        bool runtimeEnabled = _transientGestureEnabled ?? persisted.Enabled;
        TouchpadGestureConfiguration runtime = (sanitized with { Enabled = runtimeEnabled }).Sanitize();
        _gestures.UpdateConfiguration(runtime);
        if (runtime.Enabled)
            EnsureInputStarted();
        else
            StopInputIfGesturesDisabled();
    }

    internal void ApplyTransientGestureEnabled(bool? enabled)
    {
        _transientGestureEnabled = enabled;
        bool persistedEnabled = _app.UserSettings.Current.TouchpadGestures?.Enabled == true;
        TouchpadGestureConfiguration runtime =
            (_gestures.Configuration with { Enabled = enabled ?? persistedEnabled }).Sanitize();
        _gestures.UpdateConfiguration(runtime);
        if (runtime.Enabled)
            EnsureInputStarted();
        else
            StopInputIfGesturesDisabled();
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
            lock (_volumeWriteGate)
            {
                Interlocked.Increment(ref _volumeGestureGeneration);
                Interlocked.Exchange(ref _pendingVolume, -1);
                Interlocked.Exchange(ref _pendingVolumeGeneration, -1);
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
            }
            return;
        }

        if (action == GestureActionKind.Brightness)
        {
            lock (_brightnessWriteGate)
            {
                Interlocked.Increment(ref _brightnessGestureGeneration);
                Interlocked.Exchange(ref _pendingBrightness, -1);
                Interlocked.Exchange(ref _pendingBrightnessGeneration, -1);
                if (active)
                {
                    Interlocked.Exchange(ref _confirmedBrightness, -1);
                    Volatile.Write(ref _brightnessGestureActive, 1);
                }
                else
                {
                    Volatile.Write(ref _brightnessGestureActive, 0);
                    Interlocked.Exchange(ref _confirmedBrightness, -1);
                }
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
        int generation = Volatile.Read(ref _volumeGestureGeneration);
        QueueVolume(bounded, generation);
    }

    private int? ReadGestureBrightnessBaseline()
    {
        if (_app.DisplayService.GetBrightness() is not int observed)
            return null;

        int live = Math.Clamp(observed, 0, 100);
        if (Volatile.Read(ref _brightnessGestureActive) != 0)
            Interlocked.Exchange(ref _confirmedBrightness, live);
        return live;
    }

    private void QueueGestureBrightness(int value)
    {
        if (Volatile.Read(ref _brightnessGestureActive) == 0)
            return;

        int confirmed = Volatile.Read(ref _confirmedBrightness);
        if (confirmed < 0)
        {
            if (_app.DisplayService.GetBrightness() is not int live)
                return;
            confirmed = Math.Clamp(live, 0, 100);
            Interlocked.Exchange(ref _confirmedBrightness, confirmed);
        }

        int bounded = Math.Clamp(
            value,
            Math.Max(0, confirmed - ContinuousBrightnessLeadLimit),
            Math.Min(100, confirmed + ContinuousBrightnessLeadLimit));
        int generation = Volatile.Read(ref _brightnessGestureGeneration);
        QueueBrightness(bounded, generation);
    }

    private void QueueVolume(int value, int gestureGeneration = -1)
    {
        if (AudioSafetyPolicy.BlocksTouchpadAudio(_app.AudioSafety.Mode))
        {
            lock (_volumeWriteGate)
            {
                Interlocked.Exchange(ref _pendingVolume, -1);
                Interlocked.Exchange(ref _pendingVolumeGeneration, -1);
            }
            return;
        }

        lock (_volumeWriteGate)
        {
            if (gestureGeneration >= 0 &&
                (Volatile.Read(ref _volumeGestureActive) == 0 ||
                 gestureGeneration != Volatile.Read(ref _volumeGestureGeneration)))
            {
                return;
            }

            Interlocked.Exchange(ref _pendingVolume, Math.Clamp(value, 0, 100));
            Interlocked.Exchange(ref _pendingVolumeGeneration, gestureGeneration);
        }

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

                int applied;
                int target;
                int generation;
                bool accepted;
                lock (_volumeWriteGate)
                {
                    target = Volatile.Read(ref _pendingVolume);
                    generation = Volatile.Read(ref _pendingVolumeGeneration);
                    if (target < 0 || target == lastApplied)
                        break;

                    if (generation >= 0 &&
                        (Volatile.Read(ref _volumeGestureActive) == 0 ||
                         generation != Volatile.Read(ref _volumeGestureGeneration)))
                    {
                        Interlocked.Exchange(ref _pendingVolume, -1);
                        Interlocked.Exchange(ref _pendingVolumeGeneration, -1);
                        break;
                    }

                    accepted = _nativeInput.TrySetVolume(target, out applied);
                    if (!accepted)
                    {
                        if (target == Volatile.Read(ref _pendingVolume) &&
                            generation == Volatile.Read(ref _pendingVolumeGeneration))
                        {
                            Interlocked.Exchange(ref _pendingVolume, -1);
                            Interlocked.Exchange(ref _pendingVolumeGeneration, -1);
                        }
                    }
                    else
                    {
                        lastApplied = applied;
                        if (generation >= 0 &&
                            generation == Volatile.Read(ref _volumeGestureGeneration) &&
                            Volatile.Read(ref _volumeGestureActive) != 0)
                        {
                            Interlocked.Exchange(ref _confirmedVolume, applied);
                        }

                        if (target == Volatile.Read(ref _pendingVolume) &&
                            generation == Volatile.Read(ref _pendingVolumeGeneration))
                        {
                            Interlocked.Exchange(ref _pendingVolume, -1);
                            Interlocked.Exchange(ref _pendingVolumeGeneration, -1);
                        }
                    }
                }

                if (!accepted)
                    break;

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

    private void QueueBrightness(int value, int gestureGeneration = -1)
    {
        lock (_brightnessWriteGate)
        {
            if (gestureGeneration >= 0 &&
                (Volatile.Read(ref _brightnessGestureActive) == 0 ||
                 gestureGeneration != Volatile.Read(ref _brightnessGestureGeneration)))
            {
                return;
            }

            Interlocked.Exchange(ref _pendingBrightness, Math.Clamp(value, 0, 100));
            Interlocked.Exchange(ref _pendingBrightnessGeneration, gestureGeneration);
        }

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
                int target;
                int generation;
                int applied = -1;
                bool accepted;
                lock (_brightnessWriteGate)
                {
                    target = Volatile.Read(ref _pendingBrightness);
                    generation = Volatile.Read(ref _pendingBrightnessGeneration);
                    if (target < 0 || target == lastApplied)
                        break;

                    if (generation >= 0 &&
                        (Volatile.Read(ref _brightnessGestureActive) == 0 ||
                         generation != Volatile.Read(ref _brightnessGestureGeneration)))
                    {
                        Interlocked.Exchange(ref _pendingBrightness, -1);
                        Interlocked.Exchange(ref _pendingBrightnessGeneration, -1);
                        break;
                    }

                    bool changed = _app.DisplayService.SetBrightness(target);
                    int? observed = changed ? _app.DisplayService.GetBrightness() : null;
                    accepted = observed is int;
                    if (observed is int observedBrightness)
                    {
                        applied = Math.Clamp(observedBrightness, 0, 100);
                        lastApplied = applied;
                        if (generation >= 0 &&
                            generation == Volatile.Read(ref _brightnessGestureGeneration) &&
                            Volatile.Read(ref _brightnessGestureActive) != 0)
                        {
                            Interlocked.Exchange(ref _confirmedBrightness, applied);
                        }
                    }

                    if (target == Volatile.Read(ref _pendingBrightness) &&
                        generation == Volatile.Read(ref _pendingBrightnessGeneration))
                    {
                        Interlocked.Exchange(ref _pendingBrightness, -1);
                        Interlocked.Exchange(ref _pendingBrightnessGeneration, -1);
                    }
                }

                if (!accepted)
                {
                    // A successful WMI invocation is not proof that the panel moved.
                    // Without observable readback, fail closed and do not advance the
                    // gesture's confirmed-state window from a speculative target.
                    break;
                }

                int displayed = applied;
                await _app.Dispatcher.InvokeAsync(() =>
                {
                    _app.State.Brightness = displayed;
                    _osd.Show("Brightness", displayed);
                });

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
        lock (_volumeWriteGate)
        {
            Interlocked.Increment(ref _volumeGestureGeneration);
            Interlocked.Exchange(ref _pendingVolume, -1);
            Interlocked.Exchange(ref _pendingVolumeGeneration, -1);
            Interlocked.Exchange(ref _confirmedVolume, -1);
        }
        lock (_brightnessWriteGate)
        {
            Interlocked.Increment(ref _brightnessGestureGeneration);
            Interlocked.Exchange(ref _pendingBrightness, -1);
            Interlocked.Exchange(ref _pendingBrightnessGeneration, -1);
            Interlocked.Exchange(ref _confirmedBrightness, -1);
        }
        _gestures.Dispose();
        _nativeInput.Dispose();
        _osd.Dispose();
    }
}
