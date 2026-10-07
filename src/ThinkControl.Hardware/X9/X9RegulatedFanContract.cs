using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ThinkControl.Hardware.X9;

// Exact 21Q6/N4CET45W MHQT/MHGT/MHAT contract, reviewed against its ACPI tables.
// This controls OEM ownership only. Fan output remains the reviewed EC running states.
internal sealed class X9RegulatedFanContract : IDisposable
{
    private const uint QueryMode = 0x2224A8, ReadFlags = 0x2224AC, ApplyMode = 0x2224B0;
    private const uint RegulatedFlag = 0x10000000;
    private readonly SafeFileHandle _driver;
    private bool _captured;

    private X9RegulatedFanContract(SafeFileHandle driver) => _driver = driver;

    internal static X9RegulatedFanContract? TryOpen()
    {
        SafeFileHandle driver = Native.CreateFile(@"\\.\IBMPmDrv", 0x80000000, 1, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (driver.IsInvalid) { driver.Dispose(); return null; }
        var contract = new X9RegulatedFanContract(driver);
        try
        {
            if (contract.Query(QueryMode, 0) != 1 || contract.Query(QueryMode, 4) != 1 ||
                contract.Query(QueryMode, 2) != 0x11 || (contract.Query(ReadFlags, 0) & 0x01000000) == 0)
                throw new InvalidOperationException("Reviewed regulated cooling contract is not advertised.");
            return contract;
        }
        catch { contract.Dispose(); return null; }
    }

    internal bool HasOwnership => _captured && Query(QueryMode, 2) == 0x11 && (Query(ReadFlags, 0) & RegulatedFlag) != 0;

    internal void SetState(ThinkPadEc ec, byte state)
    {
        if (state is not (>= 4 and <= 7) and not 0x40) throw new ArgumentOutOfRangeException(nameof(state));
        uint indices = Query(QueryMode, 2), flags = Query(ReadFlags, 0);
        if (indices != 0x11) throw new InvalidOperationException("OEM cooling configuration changed. Select Auto.");
        if (!_captured)
        {
            if ((flags & RegulatedFlag) != 0) throw new InvalidOperationException("Another controller owns regulated cooling. Select Auto.");
            _captured = true; // Responsibility precedes the setter, including partial failures.
            Apply(Command(indices, flags) | 0x40000);
        }
        else if (!HasOwnership) throw new InvalidOperationException("OEM cooling ownership was reclaimed.");
        else if (ec.ReadFanControl() == 0x80) Apply(Command(indices, flags) | 0x40000);
        if (!HasOwnership) throw new InvalidOperationException("OEM cooling ownership was not confirmed.");
        if (state == 0x40) ec.SetVerifiedFirmwareFullSpeed(); else ec.SetManualLevel(state);
    }

    internal void Restore()
    {
        if (!_captured) return;
        ClearRegulatedOwnership();
        _captured = false;
    }

    internal void ClearRegulatedOwnership()
    {
        uint indices = Query(QueryMode, 2), flags = Query(ReadFlags, 0);
        if ((flags & RegulatedFlag) == 0) return; // Already released, including a changed OEM configuration.
        if (indices != 0x11) throw new InvalidOperationException("OEM configuration changed; regulated Auto recovery needs a retry.");
        // Preserve current performance/automatic flags, including external changes.
        if ((flags & RegulatedFlag) != 0) Apply(Command(indices, flags));
        if ((Query(ReadFlags, 0) & RegulatedFlag) != 0) throw new InvalidOperationException("OEM Auto ownership was not confirmed.");
    }

    private static uint Command(uint indices, uint flags) => indices |
        ((flags & 0x02000000) != 0 ? 0x10000u : 0) | ((flags & 0x04000000) != 0 ? 0x20000u : 0);

    private void Apply(uint command)
    {
        if (Query(ApplyMode, command) != 1) throw new InvalidOperationException("OEM regulated cooling request was rejected.");
    }

    private uint Query(uint code, uint input)
    {
        if (!Native.DeviceIoControl(_driver, code, ref input, 4, out uint output, 4, out uint returned, IntPtr.Zero) || returned != 4)
            throw new InvalidOperationException("OEM regulated cooling readback is unavailable.");
        return output;
    }

    public void Dispose() => _driver.Dispose();

    private static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle handle, uint code, ref uint input, uint inputSize, out uint output, uint outputSize, out uint returned, IntPtr overlapped);
    }
}
