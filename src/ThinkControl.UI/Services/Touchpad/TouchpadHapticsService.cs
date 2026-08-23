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
    private const uint SpiGetTouchpadParameters = 0x00AE;
    private const uint SpiSetTouchpadParameters = 0x00AF;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;
    private const uint TouchpadParametersVersion1 = 1;

    internal TouchpadHapticStatus Read(
        bool hidTouchpadPresent = false,
        bool hidFeedbackSupported = false,
        bool hidClickForceSupported = false)
    {
        if (!OperatingSystem.IsWindows() || Environment.OSVersion.Version.Build < Windows11_24H2Build)
            return Unsupported("Requires Windows 11 24H2 or newer", hidTouchpadPresent, hidFeedbackSupported, hidClickForceSupported);

        if (!TryReadNative(out TouchpadNativeMethods.TouchpadParametersV1 parameters, out int error))
        {
            string detail = error == 0
                ? "Windows touchpad settings API is unavailable"
                : $"Windows touchpad settings API failed (Win32 {error})";
            return Unsupported(detail, hidTouchpadPresent, hidFeedbackSupported, hidClickForceSupported);
        }

        return ToStatus(parameters, hidTouchpadPresent, hidFeedbackSupported, hidClickForceSupported);
    }

    internal bool SetFeedbackEnabled(bool enabled) =>
        Update(parameters =>
        {
            parameters.FeedbackEnabled = enabled;
            return WriteNative(parameters);
        });

    internal bool SetFeedbackIntensity(int intensity) =>
        Update(parameters =>
        {
            parameters.FeedbackIntensity = checked((uint)Math.Clamp(intensity, 0, 100));
            return WriteNative(parameters);
        });

    internal bool SetClickForceSensitivity(int sensitivity) =>
        Update(parameters =>
        {
            parameters.ClickForceSensitivity = checked((uint)Math.Clamp(sensitivity, 0, 100));
            return WriteNative(parameters);
        });

    private static bool Update(Func<TouchpadNativeMethods.TouchpadParametersV1, bool> update)
    {
        if (!TryReadNative(out TouchpadNativeMethods.TouchpadParametersV1 current, out _))
            return false;
        return update(current);
    }

    private static bool TryReadNative(
        out TouchpadNativeMethods.TouchpadParametersV1 parameters,
        out int error)
    {
        parameters = new TouchpadNativeMethods.TouchpadParametersV1
        {
            VersionNumber = TouchpadParametersVersion1
        };
        error = 0;

        int size = Marshal.SizeOf<TouchpadNativeMethods.TouchpadParametersV1>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            // Marshal through an explicit unmanaged buffer rather than a typed ref.
            // TOUCHPAD_PARAMETERS contains two C bit-field words; treating pvParam as
            // the native void* it really is avoids runtime struct-marshalling quirks.
            Marshal.StructureToPtr(parameters, buffer, false);
            Marshal.SetLastPInvokeError(0);
            if (!SystemParametersInfoW(
                    SpiGetTouchpadParameters,
                    checked((uint)size),
                    buffer,
                    0))
            {
                error = Marshal.GetLastPInvokeError();
                return false;
            }

            parameters = Marshal.PtrToStructure<TouchpadNativeMethods.TouchpadParametersV1>(buffer);
            return parameters.VersionNumber == TouchpadParametersVersion1;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool WriteNative(TouchpadNativeMethods.TouchpadParametersV1 parameters)
    {
        parameters.VersionNumber = TouchpadParametersVersion1;
        int size = Marshal.SizeOf<TouchpadNativeMethods.TouchpadParametersV1>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(parameters, buffer, false);
            return SystemParametersInfoW(
                SpiSetTouchpadParameters,
                checked((uint)size),
                buffer,
                SpifUpdateIniFile | SpifSendChange);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static TouchpadHapticStatus ToStatus(
        TouchpadNativeMethods.TouchpadParametersV1 parameters,
        bool hidTouchpadPresent,
        bool hidFeedbackSupported,
        bool hidClickForceSupported)
    {
        bool feedbackSupported = parameters.FeedbackSupported || hidFeedbackSupported;
        bool clickForceSupported = parameters.ClickForceSupported || hidClickForceSupported;
        return new TouchpadHapticStatus(
            ApiAvailable: true,
            TouchpadPresent: parameters.TouchpadPresent || hidTouchpadPresent || feedbackSupported || clickForceSupported,
            TouchpadEnabled: parameters.TouchpadEnabled,
            FeedbackSupported: feedbackSupported,
            FeedbackEnabled: parameters.FeedbackEnabled,
            FeedbackIntensity: Math.Clamp(unchecked((int)parameters.FeedbackIntensity), 0, 100),
            ClickForceSupported: clickForceSupported,
            ClickForceSensitivity: Math.Clamp(unchecked((int)parameters.ClickForceSensitivity), 0, 100),
            MaxSupportedContacts: Math.Max(0, unchecked((int)parameters.MaxSupportedContacts)),
            ExternalMousePresent: parameters.ExternalMousePresent);
    }

    private static TouchpadHapticStatus Unsupported(
        string error,
        bool hidTouchpadPresent,
        bool hidFeedbackSupported,
        bool hidClickForceSupported) => new(
        ApiAvailable: false,
        TouchpadPresent: hidTouchpadPresent || hidFeedbackSupported || hidClickForceSupported,
        TouchpadEnabled: false,
        FeedbackSupported: hidFeedbackSupported,
        FeedbackEnabled: false,
        FeedbackIntensity: 0,
        ClickForceSupported: hidClickForceSupported,
        ClickForceSensitivity: 0,
        MaxSupportedContacts: 0,
        ExternalMousePresent: false,
        Error: error);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(
        uint action,
        uint parameter,
        IntPtr data,
        uint flags);
}
