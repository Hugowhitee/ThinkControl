using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed record TouchpadHapticStatus(
    bool ApiAvailable,
    bool TouchpadPresent,
    bool TouchpadEnabled,
    bool FeedbackSupported,
    bool FeedbackEnabled,
    int FeedbackIntensity,
    bool ClickForceSupported,
    int ClickForceSensitivity,
    int MaxSupportedContacts,
    bool ExternalMousePresent,
    string? Error = null);

internal sealed class TouchpadHapticsService
{
    private const int Windows11_24H2Build = 26100;

    internal TouchpadHapticStatus Read()
    {
        if (!OperatingSystem.IsWindows() || Environment.OSVersion.Version.Build < Windows11_24H2Build)
            return Unsupported("Requires Windows 11 24H2 or newer");

        if (!TryReadNative(out TouchpadNativeMethods.TouchpadParametersV1 parameters))
            return Unsupported("Windows touchpad settings API is unavailable");

        return ToStatus(parameters);
    }

    internal bool SetFeedbackEnabled(bool enabled) =>
        Update(parameters =>
        {
            if (!parameters.FeedbackSupported)
                return false;
            parameters.FeedbackEnabled = enabled;
            return WriteNative(parameters);
        });

    internal bool SetFeedbackIntensity(int intensity) =>
        Update(parameters =>
        {
            if (!parameters.FeedbackSupported)
                return false;
            parameters.FeedbackIntensity = checked((uint)Math.Clamp(intensity, 0, 100));
            return WriteNative(parameters);
        });

    internal bool SetClickForceSensitivity(int sensitivity) =>
        Update(parameters =>
        {
            if (!parameters.ClickForceSupported)
                return false;
            parameters.ClickForceSensitivity = checked((uint)Math.Clamp(sensitivity, 0, 100));
            return WriteNative(parameters);
        });

    private static bool Update(Func<TouchpadNativeMethods.TouchpadParametersV1, bool> update)
    {
        if (!TryReadNative(out TouchpadNativeMethods.TouchpadParametersV1 current))
            return false;
        return update(current);
    }

    private static bool TryReadNative(out TouchpadNativeMethods.TouchpadParametersV1 parameters)
    {
        parameters = new TouchpadNativeMethods.TouchpadParametersV1
        {
            VersionNumber = TouchpadNativeMethods.TouchpadParametersVersion1
        };
        uint size = checked((uint)Marshal.SizeOf<TouchpadNativeMethods.TouchpadParametersV1>());
        return TouchpadNativeMethods.SystemParametersInfoW(
            TouchpadNativeMethods.SpiGetTouchpadParameters,
            size,
            ref parameters,
            0);
    }

    private static bool WriteNative(TouchpadNativeMethods.TouchpadParametersV1 parameters)
    {
        parameters.VersionNumber = TouchpadNativeMethods.TouchpadParametersVersion1;
        uint size = checked((uint)Marshal.SizeOf<TouchpadNativeMethods.TouchpadParametersV1>());
        return TouchpadNativeMethods.SystemParametersInfoW(
            TouchpadNativeMethods.SpiSetTouchpadParameters,
            size,
            ref parameters,
            TouchpadNativeMethods.SpifUpdateIniFile | TouchpadNativeMethods.SpifSendChange);
    }

    private static TouchpadHapticStatus ToStatus(TouchpadNativeMethods.TouchpadParametersV1 parameters) => new(
        ApiAvailable: true,
        TouchpadPresent: parameters.TouchpadPresent,
        TouchpadEnabled: parameters.TouchpadEnabled,
        FeedbackSupported: parameters.FeedbackSupported,
        FeedbackEnabled: parameters.FeedbackEnabled,
        FeedbackIntensity: Math.Clamp(unchecked((int)parameters.FeedbackIntensity), 0, 100),
        ClickForceSupported: parameters.ClickForceSupported,
        ClickForceSensitivity: Math.Clamp(unchecked((int)parameters.ClickForceSensitivity), 0, 100),
        MaxSupportedContacts: Math.Max(0, unchecked((int)parameters.MaxSupportedContacts)),
        ExternalMousePresent: parameters.ExternalMousePresent);

    private static TouchpadHapticStatus Unsupported(string error) => new(
        ApiAvailable: false,
        TouchpadPresent: false,
        TouchpadEnabled: false,
        FeedbackSupported: false,
        FeedbackEnabled: false,
        FeedbackIntensity: 0,
        ClickForceSupported: false,
        ClickForceSensitivity: 0,
        MaxSupportedContacts: 0,
        ExternalMousePresent: false,
        Error: error);
}
