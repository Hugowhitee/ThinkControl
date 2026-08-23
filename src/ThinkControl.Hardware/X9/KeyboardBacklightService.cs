using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace ThinkControl.Hardware.X9;

public enum KeyboardBacklightLevel
{
    Off = 0,
    Low = 1,
    High = 2,
    FirmwareAuto = 3
}

/// <summary>
/// Capability-probed Lenovo keyboard-backlight driver access.
///
/// ThinkControl never chooses a write contract from the marketing model name alone.
/// A backend is usable only when its signed Lenovo device can be opened and its
/// read operation returns one of that backend's known states. Every write is then
/// read back before success is reported.
///
/// Contracts:
/// - IBMPmDrv: ThinkPad Lenovo Power Management Driver.
/// - EnergyDrv: Lenovo ACPI-Compliant Virtual Power Controller used by multiple
///   ThinkBook / IdeaPad / LOQ-family machines. Two read encodings are known in
///   the ecosystem, so both are probed independently and fail closed.
/// </summary>
public sealed class KeyboardBacklightService : IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    private static readonly DriverConfig[] Drivers =
    [
        new(
            "Lenovo PM Driver · ThinkPad",
            @"\\.\IBMPmDrv",
            0x00222680,
            null,
            0x00050200,
            0x00050201,
            0x00050202,
            null,
            0x00222684,
            0x00000000,
            0x00000001,
            0x00000002),

        new(
            "Lenovo EnergyDrv · standard",
            @"\\.\EnergyDrv",
            0x83102144,
            0x00000032,
            0x00000001,
            0x00000003,
            0x00000005,
            null,
            0x83102144,
            0x00000033,
            0x00010033,
            0x00020033),

        new(
            "Lenovo EnergyDrv · alternate",
            @"\\.\EnergyDrv",
            0x83102144,
            0x00000032,
            0x00010001,
            0x00010003,
            0x00010005,
            0x00010007,
            0x83102144,
            0x00000033,
            0x00010033,
            0x00020033)
    ];

    private SafeFileHandle? _handle;
    private DriverConfig? _driver;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        byte[]? inBuffer,
        int inBufferSize,
        byte[]? outBuffer,
        int outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    public string BackendLabel => _driver?.Name ?? "Not exposed";

    public bool IsAvailable
    {
        get
        {
            EnsureOpen();
            return _handle is { IsInvalid: false, IsClosed: false } &&
                   _driver is not null &&
                   TryGet(out _);
        }
    }

    public bool TryGet(out KeyboardBacklightLevel level)
    {
        level = KeyboardBacklightLevel.Off;
        EnsureOpen();
        return _handle is { IsInvalid: false, IsClosed: false } &&
               _driver is not null &&
               TryGet(_driver, _handle, out level);
    }

    public bool SetAndVerify(KeyboardBacklightLevel level)
    {
        if (level is KeyboardBacklightLevel.FirmwareAuto)
            return false;

        EnsureOpen();
        if (_handle is null || _handle.IsInvalid || _handle.IsClosed || _driver is null)
            return false;

        uint payload = level switch
        {
            KeyboardBacklightLevel.Off => _driver.SetOff,
            KeyboardBacklightLevel.Low => _driver.SetLow,
            KeyboardBacklightLevel.High => _driver.SetHigh,
            _ => _driver.SetOff
        };

        byte[] input = BitConverter.GetBytes(payload);
        var output = new byte[16];
        if (!DeviceIoControl(
                _handle,
                _driver.SetIoctl,
                input,
                input.Length,
                output,
                output.Length,
                out _,
                IntPtr.Zero))
        {
            return false;
        }

        Thread.Sleep(55);
        return TryGet(out KeyboardBacklightLevel current) && current == level;
    }

    private void EnsureOpen()
    {
        if (_handle is { IsInvalid: false, IsClosed: false } && _driver is not null)
            return;

        _handle?.Dispose();
        _handle = null;
        _driver = null;

        foreach (DriverConfig candidate in Drivers)
        {
            SafeFileHandle handle = CreateFile(
                candidate.Principal,
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid || handle.IsClosed)
            {
                handle.Dispose();
                continue;
            }

            // The read is the compatibility gate. Unknown return encodings do not
            // select the backend and therefore can never reach SetAndVerify.
            if (TryGet(candidate, handle, out _))
            {
                _driver = candidate;
                _handle = handle;
                return;
            }

            handle.Dispose();
        }
    }

    private static bool TryGet(
        DriverConfig driver,
        SafeFileHandle handle,
        out KeyboardBacklightLevel level)
    {
        level = KeyboardBacklightLevel.Off;
        byte[]? input = driver.GetInput.HasValue
            ? BitConverter.GetBytes(driver.GetInput.Value)
            : null;
        var output = new byte[16];

        if (!DeviceIoControl(
                handle,
                driver.GetIoctl,
                input,
                input?.Length ?? 0,
                output,
                output.Length,
                out int returned,
                IntPtr.Zero) || returned < 4)
        {
            return false;
        }

        uint raw = BitConverter.ToUInt32(output, 0);
        if (raw == driver.GetOff) level = KeyboardBacklightLevel.Off;
        else if (raw == driver.GetLow) level = KeyboardBacklightLevel.Low;
        else if (raw == driver.GetHigh) level = KeyboardBacklightLevel.High;
        else if (driver.GetAuto.HasValue && raw == driver.GetAuto.Value) level = KeyboardBacklightLevel.FirmwareAuto;
        else return false;

        return true;
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
        _driver = null;
    }

    private sealed record DriverConfig(
        string Name,
        string Principal,
        uint GetIoctl,
        uint? GetInput,
        uint GetOff,
        uint GetLow,
        uint GetHigh,
        uint? GetAuto,
        uint SetIoctl,
        uint SetOff,
        uint SetLow,
        uint SetHigh);
}
