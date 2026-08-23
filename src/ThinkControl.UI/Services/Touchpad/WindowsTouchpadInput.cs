using System.Runtime.InteropServices;
using ThinkControl.Core.Touchpad;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class WindowsTouchpadInput : IDisposable
{
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
        return _registered;
    }

    private void ProcessRawInput(IntPtr rawInputHandle)
    {
        if (_disposed)
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
            // A malformed or vendor-specific HID report must never take down the
            // tray process. The next valid report can continue normally.
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

        if (_registered)
        {
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
            TouchpadNativeMethods.RegisterRawInputDevices(
                devices,
                1,
                checked((uint)Marshal.SizeOf<TouchpadNativeMethods.RawInputDevice>()));
        }

        foreach (TouchpadHidDevice device in _devices.Values)
            device.Dispose();
        _devices.Clear();
        _window.Dispose();
    }

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
