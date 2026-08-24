using System.Runtime.InteropServices;
using ThinkControl.Core.Touchpad;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class WindowsTouchpadInput : IDisposable
{
    private const uint RidiDeviceInfo = 0x2000000B;
    private const uint MaxProbeDevices = 1024;

    private readonly MessageWindow _window;
    private readonly Dictionary<IntPtr, TouchpadHidDevice> _devices = new();
    private readonly double _fallbackWidthMm;
    private readonly double _fallbackHeightMm;
    private bool _registered;
    private bool _disposed;

    internal WindowsTouchpadInput(double fallbackWidthMm = 100, double fallbackHeightMm = 60)
    {
        _fallbackWidthMm = fallbackWidthMm;
        _fallbackHeightMm = fallbackHeightMm;
        _window = new MessageWindow(ProcessRawInput);
    }

    internal event Action<IReadOnlyList<TouchContact>, TouchpadGeometry>? FrameReceived;
    internal event Action<TouchpadGeometry>? TouchpadDetected;

    internal TouchpadGeometry? Geometry { get; private set; }
    internal bool IsStarted => _registered;
    internal bool HapticFeedbackSupported { get; private set; }
    internal bool ClickForceSupported { get; private set; }

    internal bool Start()
    {
        if (_disposed)
            return false;
        if (_registered)
            return true;

        var devices = new[]
        {
            new TouchpadNativeMethods.RawInputDevice
            {
                UsagePage = TouchpadNativeMethods.HidUsagePageDigitizer,
                Usage = TouchpadNativeMethods.HidUsageTouchPad,
                Flags = TouchpadNativeMethods.RidevInputSink,
                Target = _window.Handle
            }
        };

        _registered = TouchpadNativeMethods.RegisterRawInputDevices(
            devices,
            1,
            checked((uint)Marshal.SizeOf<TouchpadNativeMethods.RawInputDevice>()));

        if (_registered)
            ProbeConnectedTouchpads();

        return _registered;
    }

    internal void Stop()
    {
        if (!_registered)
            return;

        var devices = new[]
        {
            new TouchpadNativeMethods.RawInputDevice
            {
                UsagePage = TouchpadNativeMethods.HidUsagePageDigitizer,
                Usage = TouchpadNativeMethods.HidUsageTouchPad,
                Flags = TouchpadNativeMethods.RidevRemove,
                Target = IntPtr.Zero
            }
        };

        try
        {
            TouchpadNativeMethods.RegisterRawInputDevices(
                devices,
                1,
                checked((uint)Marshal.SizeOf<TouchpadNativeMethods.RawInputDevice>()));
        }
        catch
        {
        }
        finally
        {
            _registered = false;
        }
    }

    private void ProbeConnectedTouchpads()
    {
        uint entrySize = checked((uint)Marshal.SizeOf<RawInputDeviceListEntry>());
        for (int attempt = 0; attempt < 3; attempt++)
        {
            uint count = 0;
            if (GetRawInputDeviceCount(IntPtr.Zero, ref count, entrySize) != 0 ||
                count == 0 || count > MaxProbeDevices)
            {
                return;
            }

            var entries = new RawInputDeviceListEntry[checked((int)count)];
            uint capacity = count;
            uint copied = FillRawInputDeviceList(entries, ref capacity, entrySize);
            if (copied == uint.MaxValue)
                continue;

            int limit = Math.Min(entries.Length, checked((int)copied));
            for (int index = 0; index < limit; index++)
            {
                RawInputDeviceListEntry entry = entries[index];
                if (entry.Type != TouchpadNativeMethods.RimTypeHid ||
                    !IsPrecisionTouchpad(entry.Device))
                {
                    continue;
                }

                TouchpadHidDevice? device = GetOrCreateDevice(entry.Device);
                if (device is null)
                    continue;

                Geometry ??= device.Geometry;
                TouchpadDetected?.Invoke(Geometry);
            }

            return;
        }
    }

    private static bool IsPrecisionTouchpad(IntPtr rawDevice)
    {
        var info = new RawInputDeviceInfo
        {
            Size = checked((uint)Marshal.SizeOf<RawInputDeviceInfo>())
        };
        uint size = info.Size;
        uint result = GetRawInputDeviceInfoForProbe(
            rawDevice,
            RidiDeviceInfo,
            ref info,
            ref size);

        return result != uint.MaxValue &&
               info.Type == TouchpadNativeMethods.RimTypeHid &&
               info.Hid.UsagePage == TouchpadNativeMethods.HidUsagePageDigitizer &&
               info.Hid.Usage == TouchpadNativeMethods.HidUsageTouchPad;
    }

    private void ProcessRawInput(IntPtr rawInputHandle)
    {
        if (_disposed || !_registered)
            return;

        try
        {
            uint size = 0;
            uint headerSize = checked((uint)Marshal.SizeOf<TouchpadNativeMethods.RawInputHeader>());
            if (TouchpadNativeMethods.GetRawInputData(
                    rawInputHandle,
                    TouchpadNativeMethods.RidInput,
                    IntPtr.Zero,
                    ref size,
                    headerSize) != 0 || size == 0)
            {
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                uint read = TouchpadNativeMethods.GetRawInputData(
                    rawInputHandle,
                    TouchpadNativeMethods.RidInput,
                    buffer,
                    ref size,
                    headerSize);
                if (read != size)
                    return;

                TouchpadNativeMethods.RawInputHeader header =
                    Marshal.PtrToStructure<TouchpadNativeMethods.RawInputHeader>(buffer);
                if (header.Type != TouchpadNativeMethods.RimTypeHid)
                    return;

                IntPtr hidPointer = IntPtr.Add(buffer, checked((int)headerSize));
                TouchpadNativeMethods.RawHid hid =
                    Marshal.PtrToStructure<TouchpadNativeMethods.RawHid>(hidPointer);
                if (hid.SizeHid == 0 || hid.Count == 0)
                    return;

                TouchpadHidDevice? device = GetOrCreateDevice(header.Device);
                if (device is null)
                    return;

                if (!ReferenceEquals(Geometry, device.Geometry))
                {
                    Geometry = device.Geometry;
                    TouchpadDetected?.Invoke(device.Geometry);
                }

                IntPtr reportBase = IntPtr.Add(hidPointer, Marshal.SizeOf<TouchpadNativeMethods.RawHid>());
                for (int index = 0; index < hid.Count; index++)
                {
                    IntPtr report = IntPtr.Add(reportBase, checked((int)(index * hid.SizeHid)));
                    IReadOnlyList<TouchContact> contacts = device.ParseReport(report, hid.SizeHid);
                    FrameReceived?.Invoke(contacts, device.Geometry);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }
    }

    private TouchpadHidDevice? GetOrCreateDevice(IntPtr rawDevice)
    {
        if (_devices.TryGetValue(rawDevice, out TouchpadHidDevice? existing))
            return existing;

        TouchpadHidDevice? created = TouchpadHidDevice.Create(
            rawDevice,
            _fallbackWidthMm,
            _fallbackHeightMm);
        if (created is not null)
        {
            _devices.Add(rawDevice, created);
            HapticFeedbackSupported |= created.SupportsHapticFeedback;
            ClickForceSupported |= created.SupportsClickForce;
        }
        return created;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Stop();
        foreach (TouchpadHidDevice device in _devices.Values)
            device.Dispose();
        _devices.Clear();
        _window.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceListEntry
    {
        internal IntPtr Device;
        internal uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHidInfo
    {
        internal uint VendorId;
        internal uint ProductId;
        internal uint VersionNumber;
        internal ushort UsagePage;
        internal ushort Usage;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct RawInputDeviceInfo
    {
        [FieldOffset(0)] internal uint Size;
        [FieldOffset(4)] internal uint Type;
        [FieldOffset(8)] internal RawInputHidInfo Hid;
    }

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceList", SetLastError = true)]
    private static extern uint GetRawInputDeviceCount(
        IntPtr deviceList,
        ref uint deviceCount,
        uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceList", SetLastError = true)]
    private static extern uint FillRawInputDeviceList(
        [Out] RawInputDeviceListEntry[] deviceList,
        ref uint deviceCount,
        uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", ExactSpelling = true, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoForProbe(
        IntPtr device,
        uint command,
        ref RawInputDeviceInfo data,
        ref uint size);

    private sealed class MessageWindow : Forms.NativeWindow, IDisposable
    {
        private readonly Action<IntPtr> _input;

        internal MessageWindow(Action<IntPtr> input)
        {
            _input = input;
            CreateHandle(new Forms.CreateParams
            {
                Caption = "ThinkControl.TouchpadInput",
                Style = unchecked((int)0x80000000)
            });
        }

        protected override void WndProc(ref Forms.Message message)
        {
            if (message.Msg == TouchpadNativeMethods.WmInput)
                _input(message.LParam);
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
                DestroyHandle();
        }
    }
}
