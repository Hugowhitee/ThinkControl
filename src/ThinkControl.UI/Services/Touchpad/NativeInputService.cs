using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class NativeInputService
{
    private const ushort VkLeftWindows = 0x5B;
    private const ushort VkTab = 0x09;
    private const ushort VkD = 0x44;

    private readonly Action<string, int>? _showValue;

    internal NativeInputService(Action<string, int>? showValue = null)
    {
        _showValue = showValue;
    }

    internal bool VolumeUp() => ChangeVolume(+0.025f);
    internal bool VolumeDown() => ChangeVolume(-0.025f);

    internal bool SetVolume(int percent)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            float next = Math.Clamp(percent, 0, 100) / 100f;
            device.AudioEndpointVolume.MasterVolumeLevelScalar = next;
            if (device.AudioEndpointVolume.Mute && next > 0)
                device.AudioEndpointVolume.Mute = false;
            _showValue?.Invoke("Volume", (int)Math.Round(next * 100));
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool ToggleMute()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
            _showValue?.Invoke(device.AudioEndpointVolume.Mute ? "Muted" : "Volume", (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100));
            return true;
        }
        catch
        {
            return SendKey(TouchpadNativeMethods.VkVolumeMute);
        }
    }

    internal bool NextTrack() => SendKey(TouchpadNativeMethods.VkMediaNextTrack);
    internal bool PreviousTrack() => SendKey(TouchpadNativeMethods.VkMediaPrevTrack);
    internal bool TogglePlayPause() => SendKey(TouchpadNativeMethods.VkMediaPlayPause);
    internal bool ShowTaskView() => SendChord(VkLeftWindows, VkTab);
    internal bool ShowDesktop() => SendChord(VkLeftWindows, VkD);

    private bool ChangeVolume(float delta)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            AudioEndpointVolume volume = device.AudioEndpointVolume;
            float next = Math.Clamp(volume.MasterVolumeLevelScalar + delta, 0f, 1f);
            volume.MasterVolumeLevelScalar = next;
            if (volume.Mute && next > 0)
                volume.Mute = false;
            _showValue?.Invoke("Volume", (int)Math.Round(next * 100));
            return true;
        }
        catch
        {
            return SendKey(delta >= 0 ? TouchpadNativeMethods.VkVolumeUp : TouchpadNativeMethods.VkVolumeDown);
        }
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
}
