using System.Runtime.InteropServices;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using ThinkControl.Core.Audio;

namespace ThinkControl.UI.Services;

internal sealed record AudioSafetyTransitionResult(bool Success, AudioSafetyMode Mode, string Detail);

/// <summary>
/// Canonical user-session owner for ThinkControl's audio safety state.
/// The mode is intentionally not persisted: process restart always starts at Normal,
/// while any mute state ThinkControl changed is restored on an orderly exit.
/// </summary>
internal sealed class AudioSafetyService : IDisposable
{
    private readonly WindowsVolumeService _volume = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly object _ownershipGate = new();
    private readonly Dictionary<string, bool> _priorMuteByEndpoint = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dispatcher _uiDispatcher;

    private MMDeviceEnumerator? _silentDeviceEnumerator;
    private SilentDeviceNotificationClient? _silentDeviceNotificationClient;
    private MMDevice? _silentObservedDevice;
    private SilentVolumeKeyBlocker? _silentVolumeKeyBlocker;

    private int _mode = (int)AudioSafetyMode.Normal;
    private int _enforcementRunning;
    private int _enforcementPending;
    private bool _disposed;

    internal AudioSafetyService()
    {
        // WH_KEYBOARD_LL callbacks are delivered to the installing thread. Capture
        // the WPF dispatcher here so the Silent key guard is always installed on a
        // thread with a message loop even though mode transitions use ConfigureAwait(false).
        _uiDispatcher = Dispatcher.CurrentDispatcher;
    }

    internal event Action<AudioSafetyMode>? ModeChanged;

    internal AudioSafetyMode Mode => (AudioSafetyMode)Volatile.Read(ref _mode);
    internal string DisplayName => AudioSafetyPolicy.DisplayName(Mode);
    internal bool CanChangeOutput => !AudioSafetyPolicy.BlocksExplicitOutputChanges(Mode);

    internal WindowsVolumeStatus ReadOutput() => _volume.Read(DataFlow.Render, respectAudioSafety: false);

    internal bool TrySetOutputVolume(int percent, out int applied)
    {
        applied = Math.Clamp(percent, 0, 100);
        return CanChangeOutput && _volume.Set(percent, out applied);
    }

    internal bool TrySetOutputMuted(bool muted) =>
        CanChangeOutput && _volume.SetMuted(muted);

    internal async Task<AudioSafetyTransitionResult> SetModeAsync(AudioSafetyMode requested)
    {
        if (_disposed)
            return new(false, Mode, "Audio safety is shutting down.");

        requested = Enum.IsDefined(requested) ? requested : AudioSafetyMode.Normal;
        await _transitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return new(false, Mode, "Audio safety is shutting down.");

            AudioSafetyMode current = Mode;
            if (current == requested)
            {
                if (requested == AudioSafetyMode.Silent)
                    EnsureSilentOutput();
                return new(true, current, Describe(current));
            }

            // Enforcement workers use this same transition gate. Therefore an
            // in-flight Silent convergence either finishes before this restore or
            // observes the new mode after the transition; it cannot re-mute an
            // endpoint after we have restored its owned prior state.
            if (current == AudioSafetyMode.Silent && requested != AudioSafetyMode.Silent)
                RestoreOwnedMuteStates();

            if (requested == AudioSafetyMode.Silent && !EnterSilent())
                return new(false, current, "Silent could not secure the Windows output and volume keys. Audio safety was left unchanged.");

            Volatile.Write(ref _mode, (int)requested);
            AudioSafetyRuntimeState.SetMode(requested);
            ModeChanged?.Invoke(requested);

            // Close the tiny transition window between the initial mute and making
            // Silent the canonical mode. A keyboard/app change in that window is
            // converged immediately after the state becomes active.
            if (requested == AudioSafetyMode.Silent)
                EnsureSilentOutput();

            return new(true, requested, Describe(requested));
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    /// <summary>
    /// Requests event-driven Silent convergence. Repeated requests are never lost:
    /// a callback that arrives while enforcement is already running sets a pending
    /// bit and the worker performs another pass before it exits. This matters for a
    /// held/repeated physical volume key racing the re-mute operation.
    /// </summary>
    internal void EnsureSilentOutput()
    {
        if (_disposed || Mode != AudioSafetyMode.Silent)
            return;

        Interlocked.Exchange(ref _enforcementPending, 1);
        StartEnforcementWorker();
    }

    private void StartEnforcementWorker()
    {
        if (_disposed || Mode != AudioSafetyMode.Silent ||
            Interlocked.CompareExchange(ref _enforcementRunning, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(EnforcementLoopAsync);
    }

    private async Task EnforcementLoopAsync()
    {
        int transientFailures = 0;
        try
        {
            while (!_disposed && Mode == AudioSafetyMode.Silent)
            {
                if (Interlocked.Exchange(ref _enforcementPending, 0) == 0)
                    break;

                bool success = false;
                await _transitionGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!_disposed && Mode == AudioSafetyMode.Silent)
                        success = EnforceCurrentOutputMute();
                }
                finally
                {
                    _transitionGate.Release();
                }

                if (success)
                {
                    transientFailures = 0;
                    continue;
                }

                // Device switches can briefly report no ready default endpoint.
                // Retry a few times from this event-triggered worker; this is bounded
                // convergence, not a permanent polling timer.
                if (!_disposed && Mode == AudioSafetyMode.Silent && transientFailures < 3)
                {
                    int delayMs = transientFailures switch
                    {
                        0 => 40,
                        1 => 120,
                        _ => 350
                    };
                    transientFailures++;
                    await Task.Delay(delayMs).ConfigureAwait(false);
                    Interlocked.Exchange(ref _enforcementPending, 1);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Orderly application shutdown may dispose the gate before a queued
            // convergence worker gets CPU time. No output write is needed then.
        }
        finally
        {
            Interlocked.Exchange(ref _enforcementRunning, 0);

            // Close the classic coalescing race: an event can set pending after the
            // loop's final exchange but before running becomes zero.
            if (!_disposed && Mode == AudioSafetyMode.Silent &&
                Volatile.Read(ref _enforcementPending) != 0)
            {
                StartEnforcementWorker();
            }
        }
    }

    private bool EnterSilent()
    {
        try
        {
            EnsureSilentObserverInfrastructure();
            MMDevice device = GetObservedDefaultOutput();
            RememberPriorMute(device);
            if (!device.AudioEndpointVolume.Mute)
                device.AudioEndpointVolume.Mute = true;

            if (!device.AudioEndpointVolume.Mute || !ActivateVolumeKeyBlocker())
                throw new InvalidOperationException("Silent could not secure mute/key ownership.");

            return true;
        }
        catch
        {
            RestoreOwnedMuteStates();
            return false;
        }
    }

    private bool EnforceCurrentOutputMute()
    {
        try
        {
            MMDevice device = GetObservedDefaultOutput();
            RememberPriorMute(device);
            if (!device.AudioEndpointVolume.Mute)
                device.AudioEndpointVolume.Mute = true;
            return device.AudioEndpointVolume.Mute;
        }
        catch
        {
            // Silent remains the requested policy. Endpoint and volume callbacks
            // normally correct changes immediately; this worker also performs a
            // short bounded retry burst for transient endpoint replacement.
            return false;
        }
    }

    private void EnsureSilentObserverInfrastructure()
    {
        lock (_ownershipGate)
        {
            if (_silentDeviceEnumerator is not null && _silentDeviceNotificationClient is not null)
                return;

            var enumerator = new MMDeviceEnumerator();
            var notificationClient = new SilentDeviceNotificationClient(EnsureSilentOutput);
            int result = enumerator.RegisterEndpointNotificationCallback(notificationClient);
            if (result < 0)
            {
                enumerator.Dispose();
                Marshal.ThrowExceptionForHR(result);
            }

            _silentDeviceEnumerator = enumerator;
            _silentDeviceNotificationClient = notificationClient;
        }
    }

    private MMDevice GetObservedDefaultOutput()
    {
        EnsureSilentObserverInfrastructure();

        MMDeviceEnumerator enumerator;
        lock (_ownershipGate)
            enumerator = _silentDeviceEnumerator
                ?? throw new InvalidOperationException("Silent device observer is unavailable.");

        MMDevice current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        lock (_ownershipGate)
        {
            if (_silentObservedDevice is not null &&
                _silentObservedDevice.ID.Equals(current.ID, StringComparison.OrdinalIgnoreCase))
            {
                current.Dispose();
                return _silentObservedDevice;
            }

            _silentObservedDevice?.Dispose();
            _silentObservedDevice = current;

            // IAudioEndpointVolumeCallback receives mute/volume changes caused by
            // Windows controls, apps and the standard VK_VOLUME_* path. Keep the
            // callback non-blocking; it only queues the serialized enforcement worker.
            _silentObservedDevice.AudioEndpointVolume.OnVolumeNotification += _ => EnsureSilentOutput();
            return _silentObservedDevice;
        }
    }

    private bool ActivateVolumeKeyBlocker()
    {
        bool Activate()
        {
            if (_silentVolumeKeyBlocker?.IsAvailable == true)
                return true;

            _silentVolumeKeyBlocker?.Dispose();
            _silentVolumeKeyBlocker = new SilentVolumeKeyBlocker();
            if (_silentVolumeKeyBlocker.IsAvailable)
                return true;

            _silentVolumeKeyBlocker.Dispose();
            _silentVolumeKeyBlocker = null;
            return false;
        }

        try
        {
            if (_uiDispatcher.CheckAccess())
                return Activate();

            if (_uiDispatcher.HasShutdownStarted || _uiDispatcher.HasShutdownFinished)
                return false;

            return _uiDispatcher.Invoke(Activate);
        }
        catch
        {
            return false;
        }
    }

    private void ReleaseVolumeKeyBlocker()
    {
        void Release()
        {
            _silentVolumeKeyBlocker?.Dispose();
            _silentVolumeKeyBlocker = null;
        }

        try
        {
            if (_uiDispatcher.CheckAccess() ||
                _uiDispatcher.HasShutdownStarted ||
                _uiDispatcher.HasShutdownFinished)
            {
                Release();
            }
            else
            {
                _uiDispatcher.Invoke(Release);
            }
        }
        catch
        {
            // The process is already tearing down. The OS removes low-level hooks
            // when the owning process exits.
            _silentVolumeKeyBlocker = null;
        }
    }

    private void ReleaseSilentOutputObserver()
    {
        MMDevice? observed;
        MMDeviceEnumerator? enumerator;
        SilentDeviceNotificationClient? notificationClient;

        lock (_ownershipGate)
        {
            observed = _silentObservedDevice;
            enumerator = _silentDeviceEnumerator;
            notificationClient = _silentDeviceNotificationClient;
            _silentObservedDevice = null;
            _silentDeviceEnumerator = null;
            _silentDeviceNotificationClient = null;
        }

        try { observed?.Dispose(); } catch { }

        if (enumerator is not null)
        {
            try
            {
                if (notificationClient is not null)
                    enumerator.UnregisterEndpointNotificationCallback(notificationClient);
            }
            catch
            {
            }

            try { enumerator.Dispose(); } catch { }
        }
    }

    private void RememberPriorMute(MMDevice device)
    {
        lock (_ownershipGate)
        {
            if (!_priorMuteByEndpoint.ContainsKey(device.ID))
                _priorMuteByEndpoint[device.ID] = device.AudioEndpointVolume.Mute;
        }
    }

    private void RestoreOwnedMuteStates()
    {
        ReleaseVolumeKeyBlocker();
        ReleaseSilentOutputObserver();

        KeyValuePair<string, bool>[] owned;
        lock (_ownershipGate)
        {
            owned = _priorMuteByEndpoint.ToArray();
            _priorMuteByEndpoint.Clear();
        }

        if (owned.Length == 0)
            return;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach ((string id, bool priorMuted) in owned)
            {
                try
                {
                    using MMDevice device = enumerator.GetDevice(id);
                    device.AudioEndpointVolume.Mute = priorMuted;
                }
                catch
                {
                    // A removed endpoint cannot be restored now. No fallback endpoint
                    // is changed because that would violate ownership.
                }
            }
        }
        catch
        {
        }
    }

    private static string Describe(AudioSafetyMode mode) => mode switch
    {
        AudioSafetyMode.MediaLock => "Gesture lock blocks ThinkControl touchpad volume, track and seek actions. Keyboard and Windows/app audio controls still work.",
        AudioSafetyMode.Silent => "Silent keeps Windows output muted. Volume keys are ignored until you leave Silent; app/Windows unmute attempts are re-muted immediately.",
        _ => "ThinkControl touchpad media and volume actions work normally."
    };

    public void Dispose()
    {
        if (_disposed)
            return;

        _transitionGate.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            RestoreOwnedMuteStates();
            Volatile.Write(ref _mode, (int)AudioSafetyMode.Normal);
            AudioSafetyRuntimeState.SetMode(AudioSafetyMode.Normal);
        }
        finally
        {
            _transitionGate.Release();
        }

        _transitionGate.Dispose();
    }

    private sealed class SilentDeviceNotificationClient : IMMNotificationClient
    {
        private readonly Action _changed;

        internal SilentDeviceNotificationClient(Action changed) => _changed = changed;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => Queue();
        public void OnDeviceAdded(string pwstrDeviceId) => Queue();
        public void OnDeviceRemoved(string deviceId) => Queue();

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render)
                Queue();
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            // Properties do not define mute/default-device ownership. Endpoint
            // volume and default-device callbacks cover the relevant state changes.
        }

        private void Queue()
        {
            try { _changed(); } catch { }
        }
    }
}
