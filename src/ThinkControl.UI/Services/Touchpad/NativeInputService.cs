using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class NativeInputService : IDisposable
{
    private const ushort VkLeftWindows = 0x5B;
    private const ushort VkTab = 0x09;
    private const ushort VkD = 0x44;

    private readonly Action<string, int>? _showValue;
    private readonly object _audioGate = new();
    private MMDeviceEnumerator? _audioEnumerator;
    private MMDevice? _audioDevice;
    private bool _disposed;

    internal NativeInputService(Action<string, int>? showValue = null)
    {
        _showValue = showValue;
    }

    internal int GetVolumePercent()
    {
        lock (_audioGate)
        {
            try
            {
                MMDevice device = OpenDefaultAudioEndpoint(refresh: true);
                return Math.Clamp((int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100), 0, 100);
            }
            catch
            {
                ResetAudioEndpoint();
                return 50;
            }
        }
    }

    internal bool SetVolume(int percent)
    {
        lock (_audioGate)
        {
            try
            {
                MMDevice device = OpenDefaultAudioEndpoint(refresh: false);
                float next = Math.Clamp(percent, 0, 100) / 100f;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = next;
                if (device.AudioEndpointVolume.Mute && next > 0)
                    device.AudioEndpointVolume.Mute = false;
                _showValue?.Invoke("Volume", (int)Math.Round(next * 100));
                return true;
            }
            catch
            {
                // Endpoint changes are uncommon during one gesture. If Windows did
                // switch devices, discard the cached COM object and let the next
                // gesture/read reopen the current default endpoint cleanly.
                ResetAudioEndpoint();
                return false;
            }
        }
    }

    internal bool ToggleMute()
    {
        lock (_audioGate)
        {
            try
            {
                MMDevice device = OpenDefaultAudioEndpoint(refresh: false);
                device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
                _showValue?.Invoke(device.AudioEndpointVolume.Mute ? "Muted" : "Volume", (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));
                return true;
            }
            catch
            {
                ResetAudioEndpoint();
                return SendKey(TouchpadNativeMethods.VkVolumeMute);
            }
        }
    }

    internal bool NextTrack() => SendKey(TouchpadNativeMethods.VkMediaNextTrack);
    internal bool PreviousTrack() => SendKey(TouchpadNativeMethods.VkMediaPrevTrack);
    internal bool TogglePlayPause() => SendKey(TouchpadNativeMethods.VkMediaPlayPause);
    internal bool ShowTaskView() => SendChord(VkLeftWindows, VkTab);
    internal bool ShowDesktop() => SendChord(VkLeftWindows, VkD);

    private MMDevice OpenDefaultAudioEndpoint(bool refresh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _audioEnumerator ??= new MMDeviceEnumerator();
        if (refresh || _audioDevice is null)
        {
            _audioDevice?.Dispose();
            _audioDevice = _audioEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        return _audioDevice;
    }

    private void ResetAudioEndpoint()
    {
        try { _audioDevice?.Dispose(); } catch { }
        _audioDevice = null;
    }

    private static bool SendChord(ushort modifier, ushort key)
    {
        TouchpadNativeMethods.Input[] inputs =
        [
            KeyInput(modifier, keyUp: false),
            KeyInput(key, keyUp: false),
            KeyInput(key, keyUp: true),
            KeyInput(modifier, keyUp: true)
        ];
        return SendInputs(inputs);
    }

    private static bool SendKey(ushort virtualKey)
    {
        TouchpadNativeMethods.Input[] inputs =
        [
            KeyInput(virtualKey, keyUp: false),
            KeyInput(virtualKey, keyUp: true)
        ];
        return SendInputs(inputs);
    }

    private static TouchpadNativeMethods.Input KeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = TouchpadNativeMethods.InputKeyboard,
        Union = new TouchpadNativeMethods.InputUnion
        {
            Keyboard = new TouchpadNativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? TouchpadNativeMethods.KeyeventfKeyup : 0
            }
        }
    };

    private static bool SendInputs(TouchpadNativeMethods.Input[] inputs)
    {
        uint sent = TouchpadNativeMethods.SendInput(
            checked((uint)inputs.Length),
            inputs,
            Marshal.SizeOf<TouchpadNativeMethods.Input>());
        return sent == checked((uint)inputs.Length);
    }

    public void Dispose()
    {
        lock (_audioGate)
        {
            if (_disposed)
                return;
            _disposed = true;
            ResetAudioEndpoint();
            try { _audioEnumerator?.Dispose(); } catch { }
            _audioEnumerator = null;
        }
    }
}
