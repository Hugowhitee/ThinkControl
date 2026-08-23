using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services.Touchpad;

internal static class TouchpadNativeMethods
{
    internal const ushort HidUsagePageGeneric = 0x0001;
    internal const ushort HidUsagePageDigitizer = 0x000D;
    internal const ushort HidUsageGenericX = 0x0030;
    internal const ushort HidUsageGenericY = 0x0031;
    internal const ushort HidUsageTouchPad = 0x0005;
    internal const ushort HidUsageTipSwitch = 0x0042;
    internal const ushort HidUsageConfidence = 0x0047;
    internal const ushort HidUsageWidth = 0x0048;
    internal const ushort HidUsageHeight = 0x0049;
    internal const ushort HidUsageContactId = 0x0051;
    internal const ushort HidUsagePressure = 0x0030;

    internal const uint RidevInputSink = 0x00000100;
    internal const uint RidevRemove = 0x00000001;
    internal const uint RidInput = 0x10000003;
    internal const uint RimTypeHid = 2;
    internal const uint RidiPreparsedData = 0x20000005;
    internal const int WmInput = 0x00FF;
    internal const int HidpInput = 0;
    internal const int HidpStatusSuccess = 0x00110000;

    internal const uint InputKeyboard = 1;
    internal const uint KeyeventfKeyup = 0x0002;
    internal const ushort VkVolumeMute = 0xAD;
    internal const ushort VkVolumeDown = 0xAE;
    internal const ushort VkVolumeUp = 0xAF;
    internal const ushort VkMediaNextTrack = 0xB0;
    internal const ushort VkMediaPrevTrack = 0xB1;
    internal const ushort VkMediaPlayPause = 0xB3;

    internal const uint SpiGetTouchpadParameters = 0x00AE;
    internal const uint SpiSetTouchpadParameters = 0x00AF;
    internal const uint SpifUpdateIniFile = 0x0001;
    internal const uint SpifSendChange = 0x0002;
    internal const uint TouchpadParametersVersion1 = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        internal ushort UsagePage;
        internal ushort Usage;
        internal uint Flags;
        internal IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        internal uint Type;
        internal uint Size;
        internal IntPtr Device;
        internal IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawHid
    {
        internal uint SizeHid;
        internal uint Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidpCaps
    {
        internal ushort Usage;
        internal ushort UsagePage;
        internal ushort InputReportByteLength;
        internal ushort OutputReportByteLength;
        internal ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] internal ushort[] Reserved;
        internal ushort NumberLinkCollectionNodes;
        internal ushort NumberInputButtonCaps;
        internal ushort NumberInputValueCaps;
        internal ushort NumberInputDataIndices;
        internal ushort NumberOutputButtonCaps;
        internal ushort NumberOutputValueCaps;
        internal ushort NumberOutputDataIndices;
        internal ushort NumberFeatureButtonCaps;
        internal ushort NumberFeatureValueCaps;
        internal ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct HidpValueCaps
    {
        internal ushort UsagePage;
        internal byte ReportId;
        [MarshalAs(UnmanagedType.U1)] internal bool IsAlias;
        internal ushort BitField;
        internal ushort LinkCollection;
        internal ushort LinkUsage;
        internal ushort LinkUsagePage;
        [MarshalAs(UnmanagedType.U1)] internal bool IsRange;
        [MarshalAs(UnmanagedType.U1)] internal bool IsStringRange;
        [MarshalAs(UnmanagedType.U1)] internal bool IsDesignatorRange;
        [MarshalAs(UnmanagedType.U1)] internal bool IsAbsolute;
        [MarshalAs(UnmanagedType.U1)] internal bool HasNull;
        internal byte Reserved;
        internal ushort BitSize;
        internal ushort ReportCount;
        internal ushort Reserved2a;
        internal ushort Reserved2b;
        internal ushort Reserved2c;
        internal ushort Reserved2d;
        internal ushort Reserved2e;
        internal uint UnitsExp;
        internal uint Units;
        internal int LogicalMin;
        internal int LogicalMax;
        internal int PhysicalMin;
        internal int PhysicalMax;
        internal ushort UsageMin;
        internal ushort UsageMax;
        internal ushort StringMin;
        internal ushort StringMax;
        internal ushort DesignatorMin;
        internal ushort DesignatorMax;
        internal ushort DataIndexMin;
        internal ushort DataIndexMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort Scan;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Union;
    }

    // C's two BOOL bit-field groups occupy one 32-bit word each. Keeping them as
    // uints avoids relying on unsupported managed bit-field marshalling.
    [StructLayout(LayoutKind.Sequential)]
    internal struct TouchpadParametersV1
    {
        internal uint VersionNumber;
        internal uint MaxSupportedContacts;
        internal uint LegacyTouchpadFeatures;
        internal uint StatusFlags;
        internal uint SettingFlags;
        internal int SensitivityLevel;
        internal uint CursorSpeed;
        internal uint FeedbackIntensity;
        internal uint ClickForceSensitivity;
        internal uint RightClickZoneWidth;
        internal uint RightClickZoneHeight;

        internal bool TouchpadPresent => (StatusFlags & (1u << 0)) != 0;
        internal bool ExternalMousePresent => (StatusFlags & (1u << 2)) != 0;
        internal bool TouchpadEnabled => (StatusFlags & (1u << 3)) != 0;
        internal bool FeedbackSupported => (StatusFlags & (1u << 5)) != 0;
        internal bool ClickForceSupported => (StatusFlags & (1u << 6)) != 0;
        internal bool FeedbackEnabled
        {
            readonly get => (SettingFlags & (1u << 1)) != 0;
            set
            {
                if (value) SettingFlags |= 1u << 1;
                else SettingFlags &= ~(1u << 1);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint deviceCount,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetValueCaps(
        int reportType,
        [In, Out] HidpValueCaps[] valueCaps,
        ref ushort valueCapsLength,
        IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetUsageValue(
        int reportType,
        ushort usagePage,
        ushort linkCollection,
        ushort usage,
        out uint value,
        IntPtr preparsedData,
        IntPtr report,
        uint reportLength);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetUsages(
        int reportType,
        ushort usagePage,
        ushort linkCollection,
        [In, Out] ushort[] usageList,
        ref uint usageLength,
        IntPtr preparsedData,
        IntPtr report,
        uint reportLength);

    [DllImport("hid.dll")]
    internal static extern int HidP_MaxUsageListLength(
        int reportType,
        ushort usagePage,
        IntPtr preparsedData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClipCursor(out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClipCursor(ref Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClipCursor(IntPtr rect);

    [DllImport("user32.dll")]
    internal static extern int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool show);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint inputCount, [In] Input[] inputs, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfoW(
        uint action,
        uint parameter,
        ref TouchpadParametersV1 data,
        uint flags);
}
