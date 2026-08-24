using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using LibreHardwareMonitor.PawnIo;

namespace ThinkControl.Hardware.X9;

/// <summary>
/// Small, explicit PawnIO transport for the verified X9 EC provider.
///
/// LibreHardwareMonitor's public LpcAcpiEc wrapper deliberately hides whether the
/// embedded PawnIO module actually loaded. A failed module load can therefore look
/// like zero-valued port reads, which is not a strong enough capability gate for
/// fan writes. ThinkControl performs the same device/module handshake used by
/// established PawnIO clients and fails closed before any EC transaction is tried.
/// </summary>
internal sealed class PawnIoEcTransport : IDisposable
{
    private const string DevicePath = @"\\?\GLOBALROOT\Device\PawnIO";
    private const string LpcModuleResource = "LibreHardwareMonitor.Resources.PawnIo.LpcACPIEC.bin";
    private const int FunctionNameLength = 32;
    private const uint DeviceType = 41394u << 16;
    private const uint IoctlLoadBinary = DeviceType | (0x821u << 2);
    private const uint IoctlExecute = DeviceType | (0x841u << 2);

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    private readonly SafeFileHandle _device;
    private bool _disposed;

    internal PawnIoEcTransport()
    {
        if (!PawnIo.IsInstalled)
            throw new InvalidOperationException("PawnIO is not installed. Open Hardware setup to install the verified low-level component.");

        if (PawnIo.Version is Version installed && installed < new Version(2, 1, 0))
        {
            throw new InvalidOperationException(
                $"PawnIO {installed} is too old for the verified X9 EC provider. Hardware setup installs PawnIO 2.2.0.");
        }

        _device = CreateFile(
            DevicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);

        if (_device.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            _device.Dispose();
            throw new InvalidOperationException(DescribeOpenFailure(error), new Win32Exception(error));
        }

        try
        {
            byte[] module = ReadEmbeddedModule();
            if (!DeviceIoControl(_device, IoctlLoadBinary, module, (uint)module.Length, null, 0, out _, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"PawnIO device opened, but the LibreHardwareMonitor LPC/ACPI EC module could not be loaded (Win32 {error}: {new Win32Exception(error).Message}).");
            }

            // A harmless legacy status-port read proves that the loaded module can
            // execute. Actual ThinkPad EC port-pair detection happens read-only in
            // ThinkPadEc and includes the modern 0x1604/0x1600 pair.
            _ = ReadPort(0x66);
        }
        catch
        {
            _device.Dispose();
            throw;
        }
    }

    internal byte ReadPort(ushort port)
    {
        long[] result = Execute("ioctl_pio_read", [(long)port], 1);
        if (result.Length != 1)
            throw new InvalidOperationException("PawnIO LPC read returned no data.");
        return unchecked((byte)result[0]);
    }

    internal void WritePort(ushort port, byte value)
    {
        _ = Execute("ioctl_pio_write", [(long)port, value], 0);
    }

    private long[] Execute(string functionName, long[] input, int outputLength)
    {
        ThrowIfDisposed();

        byte[] name = Encoding.ASCII.GetBytes(functionName);
        byte[] request = new byte[FunctionNameLength + (input.Length * sizeof(long))];
        Buffer.BlockCopy(name, 0, request, 0, Math.Min(name.Length, FunctionNameLength - 1));
        if (input.Length > 0)
            Buffer.BlockCopy(input, 0, request, FunctionNameLength, input.Length * sizeof(long));

        byte[]? response = outputLength > 0 ? new byte[outputLength * sizeof(long)] : null;
        bool ok = DeviceIoControl(
            _device,
            IoctlExecute,
            request,
            (uint)request.Length,
            response,
            (uint)(response?.Length ?? 0),
            out uint bytesReturned,
            IntPtr.Zero);

        if (!ok)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"PawnIO LPC operation '{functionName}' failed (Win32 {error}: {new Win32Exception(error).Message}).");
        }

        if (outputLength == 0)
            return Array.Empty<long>();

        if (response is null || bytesReturned < sizeof(long))
            throw new InvalidOperationException($"PawnIO LPC operation '{functionName}' returned no result data.");

        int count = Math.Min(outputLength, checked((int)(bytesReturned / sizeof(long))));
        long[] output = new long[count];
        Buffer.BlockCopy(response, 0, output, 0, count * sizeof(long));
        return output;
    }

    private static byte[] ReadEmbeddedModule()
    {
        Assembly assembly = typeof(PawnIo).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(LpcModuleResource);
        if (stream is null)
            throw new InvalidOperationException("LibreHardwareMonitor LPC/ACPI EC PawnIO module is missing from the application payload.");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        byte[] module = memory.ToArray();
        if (module.Length == 0)
            throw new InvalidOperationException("LibreHardwareMonitor LPC/ACPI EC PawnIO module is empty.");
        return module;
    }

    private static string DescribeOpenFailure(int error) => error switch
    {
        2 or 3 => "PawnIO is registered but its device is not available. Repair PawnIO in Hardware setup, then retry providers.",
        5 => "PawnIO is installed but access to its device was denied. Repair the low-level component or its driver permissions in Hardware setup.",
        _ => $"PawnIO device could not be opened (Win32 {error}: {new Win32Exception(error).Message})."
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PawnIoEcTransport));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _device.Dispose();
    }

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
        byte[] inputBuffer,
        uint inputBufferSize,
        [Out] byte[]? outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}
