using NAudio.CoreAudioApi;

namespace ThinkControl.UI.Services;

internal sealed record WindowsVolumeStatus(bool Available, int Percent, bool Muted, string Detail);

internal sealed class WindowsVolumeService
{
    internal WindowsVolumeStatus Read(DataFlow flow = DataFlow.Render)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            int percent = (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            return new(true, Math.Clamp(percent, 0, 100), device.AudioEndpointVolume.Mute, device.FriendlyName);
        }
        catch (Exception ex)
        {
            return new(false, 0, false, $"Windows audio endpoint unavailable: {ex.Message}");
        }
    }

    internal bool Set(int percent, out int applied, DataFlow flow = DataFlow.Render)
    {
        applied = Math.Clamp(percent, 0, 100);
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            float scalar = applied / 100f;
            device.AudioEndpointVolume.MasterVolumeLevelScalar = scalar;
            if (device.AudioEndpointVolume.Mute && scalar > 0)
                device.AudioEndpointVolume.Mute = false;
            applied = (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool SetMuted(bool muted, DataFlow flow = DataFlow.Render)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            device.AudioEndpointVolume.Mute = muted;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
