using NAudio.CoreAudioApi;
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
    private int _mode = (int)AudioSafetyMode.Normal;
    private int _enforcementRunning;
    private bool _disposed;

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
                    EnforceCurrentOutputMute();
                return new(true, current, Describe(current));
            }

            // Enforcement workers use this same transition gate. Therefore an
            // in-flight Silent convergence either finishes before this restore or
            // observes the new mode after the transition; it cannot re-mute an
            // endpoint after we have restored its owned prior state.
            if (current == AudioSafetyMode.Silent && requested != AudioSafetyMode.Silent)
                RestoreOwnedMuteStates();

            if (requested == AudioSafetyMode.Silent && !EnterSilent())
                return new(false, current, "Silent could not acquire the current Windows output. Audio safety was left unchanged.");

            Volatile.Write(ref _mode, (int)requested);
            AudioSafetyRuntimeState.SetMode(requested);
            ModeChanged?.Invoke(requested);
            return new(true, requested, Describe(requested));
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    /// <summary>
    /// Reuses the application's existing status cadence to cover a default-output
    /// switch while Silent is active. No second polling loop is created. Enforcement
    /// is serialized with mode transitions so leaving Silent cannot race a late mute.
    /// </summary>
    internal void EnsureSilentOutput()
    {
        if (_disposed || Mode != AudioSafetyMode.Silent ||
            Interlocked.CompareExchange(ref _enforcementRunning, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _transitionGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!_disposed && Mode == AudioSafetyMode.Silent)
                        EnforceCurrentOutputMute();
                }
                finally
                {
                    _transitionGate.Release();
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
            }
        });
    }

    private bool EnterSilent()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            RememberPriorMute(device);
            if (!device.AudioEndpointVolume.Mute)
                device.AudioEndpointVolume.Mute = true;
            return device.AudioEndpointVolume.Mute;
        }
        catch
        {
            lock (_ownershipGate)
                _priorMuteByEndpoint.Clear();
            return false;
        }
    }

    private void EnforceCurrentOutputMute()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            RememberPriorMute(device);
            if (!device.AudioEndpointVolume.Mute)
                device.AudioEndpointVolume.Mute = true;
        }
        catch
        {
            // Silent remains the requested policy. The next bounded application
            // status tick retries after transient endpoint/device transitions.
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
        AudioSafetyMode.MediaLock => "Touchpad volume, track and seek actions are locked. Windows audio remains available for deliberate use.",
        AudioSafetyMode.Silent => "ThinkControl media actions are locked and the active Windows output is kept muted for this session.",
        _ => "Touchpad media and volume actions work normally."
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
}
