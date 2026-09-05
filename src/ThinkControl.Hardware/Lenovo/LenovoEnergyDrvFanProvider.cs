using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ThinkControl.Hardware.Lenovo;

internal sealed record LenovoEnergyDrvFanStatus(
    bool Available,
    bool Complete,
    IReadOnlyList<LenovoFanReading> Fans,
    string Detail);

/// <summary>
/// Read-only probe for Lenovo's EnergyDrv fan-speed query contract.
///
/// Public reverse-engineering of Lenovo Energy Management shows QueryFanSpeed
/// using IOCTL 0x83102570 with a zero-based fan index and one UInt32 result.
/// The same Lenovo library has a separate ChangeFanSpeed IOCTL (0x8310257C),
/// but its command encoding has not been physically recovered for the X9 and is
/// intentionally NOT implemented here. This provider therefore gives us OEM
/// telemetry/evidence without guessing a write contract.
/// </summary>
internal sealed class LenovoEnergyDrvFanProvider
{
    private const string DevicePath = @"\\.\EnergyDrv";
    private const uint QueryFanSpeedIoctl = 0x83102570;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int ErrorInvalidData = 13;
    private const int ExpectedFanCount = 2;
    private const int MaximumPlausibleRpm = 20_000;
    private static readonly TimeSpan ReadInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FailureRetryInterval = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private DateTimeOffset _lastRead = DateTimeOffset.MinValue;
    private DateTimeOffset _retryAfter = DateTimeOffset.MinValue;
    private LenovoEnergyDrvFanStatus _cached = new(
        false,
        false,
        [],
        "Lenovo EnergyDrv fan telemetry not probed");

    internal void Refresh()
    {
        lock (_gate)
        {
            _lastRead = DateTimeOffset.MinValue;
            _retryAfter = DateTimeOffset.MinValue;
            _cached = new LenovoEnergyDrvFanStatus(
                false,
                false,
                [],
                "Lenovo EnergyDrv fan telemetry not probed");
        }
    }

    internal LenovoEnergyDrvFanStatus ReadStatus(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (now < _retryAfter)
                return _cached;
            if (now - _lastRead < ReadInterval)
                return _cached;

            _lastRead = now;
            _cached = ReadUncached();
            _retryAfter = _cached.Available
                ? DateTimeOffset.MinValue
                : now + FailureRetryInterval;
            return _cached;
        }
    }

    private static LenovoEnergyDrvFanStatus ReadUncached()
    {
        using SafeFileHandle? handle = OpenReadOnlyQueryHandle(out string access, out int openError);
        if (handle is null || handle.IsInvalid)
        {
            return new LenovoEnergyDrvFanStatus(
                false,
                false,
                [],
                $"EnergyDrv unavailable · CreateFile error {openError}");
        }

        var fans = new List<LenovoFanReading>(ExpectedFanCount);
        var failures = new List<string>(ExpectedFanCount);
        for (int index = 0; index < ExpectedFanCount; index++)
        {
            if (!TryQueryFanSpeed(handle, index, out int rpm, out int error))
            {
                failures.Add($"Fan {index + 1} query error {error}");
                continue;
            }

            if (rpm is < 0 or > MaximumPlausibleRpm)
            {
                failures.Add($"Fan {index + 1} implausible value {rpm}");
                continue;
            }

            fans.Add(new LenovoFanReading(
                $"lenovo-energydrv-{index + 1}",
                rpm,
                $"Fan {index + 1}",
                "Lenovo EnergyDrv · QueryFanSpeed 0x83102570"));
        }

        bool complete = fans.Count == ExpectedFanCount;
        string detail = complete
            ? $"Lenovo EnergyDrv OEM fan telemetry · 2 channels · {access}"
            : fans.Count > 0
                ? $"Lenovo EnergyDrv partial fan telemetry · {fans.Count}/2 channels · {string.Join(" · ", failures)}"
                : $"Lenovo EnergyDrv opened but fan queries failed · {string.Join(" · ", failures)}";

        return new LenovoEnergyDrvFanStatus(
            Available: fans.Count > 0,
            Complete: complete,
            Fans: fans,
            Detail: detail);
    }

    private static SafeFileHandle? OpenReadOnlyQueryHandle(out string access, out int error)
    {
        // Historical Lenovo code opens EnergyDrv for the query path with read access,
        // while some driver builds also permit a zero-access query handle. Try the
        // least-privileged form first and never request GENERIC_WRITE here.
        SafeFileHandle handle = CreateFileW(
            DevicePath,
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (!handle.IsInvalid)
        {
            access = "query-only handle";
            error = 0;
            return handle;
        }

        handle.Dispose();
        handle = CreateFileW(
            DevicePath,
            GenericRead,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (!handle.IsInvalid)
        {
            access = "GENERIC_READ";
            error = 0;
            return handle;
        }

        error = Marshal.GetLastWin32Error();
        handle.Dispose();
        access = "unavailable";
        return null;
    }

    private static bool TryQueryFanSpeed(
        SafeFileHandle handle,
        int index,
        out int rpm,
        out int error)
    {
        uint input = checked((uint)index);
        bool ok = DeviceIoControl(
            handle,
            QueryFanSpeedIoctl,
            ref input,
            sizeof(uint),
            out uint output,
            sizeof(uint),
            out uint bytesReturned,
            IntPtr.Zero);

        if (!ok)
        {
            rpm = 0;
            error = Marshal.GetLastWin32Error();
            return false;
        }

        if (bytesReturned < sizeof(uint) || output > int.MaxValue)
        {
            rpm = 0;
            error = ErrorInvalidData;
            return false;
        }

        rpm = (int)output;
        error = 0;
        return true;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        ref uint inBuffer,
        uint inBufferSize,
        out uint outBuffer,
        uint outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}
