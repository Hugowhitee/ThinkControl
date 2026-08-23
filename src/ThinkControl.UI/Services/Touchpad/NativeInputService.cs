using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class NativeInputService
{
    private readonly Action<string, int>? _showValue;

    internal NativeInputService(Action<string, int>? showValue = null)
    {
        _showValue = showValue;
    }

    internal bool VolumeUp() => ChangeVolume(+0.025f);
    internal bool VolumeDown() => ChangeVolume(-0.025f);
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
            // Keep the normal Windows media-key path as a compatibility fallback.
            bool sent = SendKey(delta >= 0 ? TouchpadNativeMethods.VkVolumeUp : TouchpadNativeMethods.VkVolumeDown);
            return sent;
        }
    }

    private static bool SendKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            new TouchpadNativeMethods.Input
            {
                Type = TouchpadNativeMethods.InputKeyboard,
                Union = new TouchpadNativeMethods.InputUnion
                {
                    Keyboard = new TouchpadNativeMethods.KeyboardInput { VirtualKey = virtualKey }
                }
            },
            new TouchpadNativeMethods.Input
            {
                Type = TouchpadNativeMethods.InputKeyboard,
                Union = new TouchpadNativeMethods.InputUnion
                {
                    Keyboard = new TouchpadNativeMethods.KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        Flags = TouchpadNativeMethods.KeyeventfKeyup
                    }
                }
            }
        };

        uint sent = TouchpadNativeMethods.SendInput(
            checked((uint)inputs.Length),
            inputs,
            Marshal.SizeOf<TouchpadNativeMethods.Input>());
        return sent == checked((uint)inputs.Length);
    }
}
