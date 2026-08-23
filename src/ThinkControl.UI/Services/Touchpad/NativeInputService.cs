using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class NativeInputService
{
    internal bool VolumeUp() => SendKey(TouchpadNativeMethods.VkVolumeUp);
    internal bool VolumeDown() => SendKey(TouchpadNativeMethods.VkVolumeDown);
    internal bool ToggleMute() => SendKey(TouchpadNativeMethods.VkVolumeMute);
    internal bool NextTrack() => SendKey(TouchpadNativeMethods.VkMediaNextTrack);
    internal bool PreviousTrack() => SendKey(TouchpadNativeMethods.VkMediaPrevTrack);
    internal bool TogglePlayPause() => SendKey(TouchpadNativeMethods.VkMediaPlayPause);

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
