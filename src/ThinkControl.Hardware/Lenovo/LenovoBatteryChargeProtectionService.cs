using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ThinkControl.Hardware.X9;

namespace ThinkControl.Hardware.Lenovo;

public sealed record LenovoBatteryChargeProtectionStatus(
    bool Available,
    bool Writable,
    bool Enabled,
    int StartPercent,
    int StopPercent,
    bool CustomThresholds,
    string Provider,
    string Detail);

/// <summary>
/// Exact-X9 bridge for Lenovo's Windows battery start/stop-threshold contract.
///
/// The protocol is the same Lenovo PM Device/PWRMGRV path used by current Lenovo
/// battery tooling: configuration lives under PWRMGRV and the existing Lenovo
/// IBMPmDrv kernel interface receives semantic threshold/mode commands. ThinkControl
/// exposes only bounded start/stop percentages; arbitrary IOCTLs never cross IPC.
///
/// This provider is initially restricted to the verified X9 21Q6/21Q7 identity and
/// additionally requires the installed Lenovo PWRMGRV battery configuration plus a
/// live \\.\IBMPmDrv device. Driver result bit 31 is treated as rejection. Registry
/// state is re-read after every successful transition, and a failed transition makes
/// a best-effort rollback to the exact state observed before the request.
/// </summary>
public static class LenovoBatteryChargeProtectionService
{
    private const string RegistryRoot = @"SOFTWARE\WOW6432Node\Lenovo\PWRMGRV\ConfKeys\Data";
    private const string DriverPath = @"\\.\IBMPmDrv";

    // Lenovo PM Device protocol. These are fixed, reviewed provider constants and
    // are never supplied by the desktop client.
    private const uint IoctlSetChargeMode = 0x0022261C;
    private const uint IoctlSetStart = 0x00222630;
    private const uint IoctlSetStop = 0x00222638;
    private const uint PrimaryBattery = 0x00000100;
    private const uint ThresholdMode = 0x00000101;
    private const uint AutomaticMode = 0x00000000;
    private const uint DriverRejected = 0x80000000;

    // Product surface deliberately follows Lenovo/Vantage-style five-percent steps
    // rather than exposing the driver's wider raw byte range.
    public const int MinimumStartPercent = 40;
    public const int MaximumStartPercent = 90;
    public const int MinimumStopPercent = 45;
    public const int MaximumStopPercent = 95;
    public const int ThresholdStepPercent = 5;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    public static LenovoBatteryChargeProtectionStatus Read(HardwareDeviceIdentity identity)
    {
        if (!identity.IsVerifiedX9)
            return Unavailable("Lenovo charge-threshold probing is restricted to the verified X9 21Q6/21Q7 profile.");

        RegistryBatteryConfig? config;
        try
        {
            config = ReadRegistryConfig();
        }
        catch (Exception ex)
        {
            return Unavailable($"Lenovo PWRMGRV battery configuration could not be read: {Describe(ex)}");
        }

        if (config is null)
            return Unavailable("Lenovo PWRMGRV does not expose a battery start/stop-threshold configuration on this installation.");

        bool enabled = IsEnabled(config);
        int start = NormalizeStoredStart(config.StartPercent);
        int stop = NormalizeStoredStop(config.StopPercent);
        bool writable = CanOpenDriver(out string driverDetail);
        string state = enabled
            ? $"charging starts below {start}% and stops at {stop}%"
            : "thresholds are disabled; normal full charging is active";
        return new LenovoBatteryChargeProtectionStatus(
            Available: true,
            Writable: writable,
            Enabled: enabled,
            StartPercent: start,
            StopPercent: enabled ? stop : 100,
            CustomThresholds: true,
            Provider: "Lenovo PM Device · charge thresholds",
            Detail: $"Lenovo PWRMGRV · {state} · {driverDetail}");
    }

    public static bool TrySetThresholds(
        HardwareDeviceIdentity identity,
        int startPercent,
        int stopPercent,
        out bool changed,
        out string? detail)
    {
        changed = false;
        detail = null;
        if (!TryValidatePair(startPercent, stopPercent, out string? validation))
        {
            detail = validation;
            return false;
        }

        LenovoBatteryChargeProtectionStatus status = Read(identity);
        if (!status.Available)
        {
            detail = status.Detail;
            return false;
        }
        if (!status.Writable)
        {
            detail = $"{status.Detail} · the installed Lenovo PM Device is not writable, so ThinkControl left charging unchanged.";
            return false;
        }
        if (status.Enabled && status.StartPercent == startPercent && status.StopPercent == stopPercent)
        {
            detail = $"Battery thresholds are already {startPercent}–{stopPercent}%.";
            return true;
        }

        RegistryBatteryConfig? before = ReadRegistryConfig();
        if (before is null)
        {
            detail = "The Lenovo battery configuration disappeared before the threshold transition.";
            return false;
        }

        try
        {
            WriteRegistryConfig(before.SubKeyName, startPercent, stopPercent, enabled: true);
            if (!ApplyDriverThresholds(startPercent, stopPercent, enabled: true, out string? driverError))
                throw new BatteryThresholdException(driverError ?? "Lenovo PM Device rejected the threshold transition.");

            RegistryBatteryConfig? verified = ReadRegistryConfig(before.SubKeyName);
            if (verified is null || !IsEnabled(verified) ||
                NormalizeStoredStart(verified.StartPercent) != startPercent ||
                NormalizeStoredStop(verified.StopPercent) != stopPercent)
            {
                throw new BatteryThresholdException("Lenovo threshold registry readback did not match the requested pair.");
            }

            changed = true;
            detail = $"Battery charge window set to {startPercent}–{stopPercent}% · Lenovo PM Device accepted the transition and PWRMGRV readback verified the pair.";
            BroadcastLenovoPowerSettingChange();
            return true;
        }
        catch (Exception ex)
        {
            RestorePreviousState(before);
            detail = $"Battery threshold transition failed safely: {Describe(ex)}. The previous Lenovo charging configuration was requested again.";
            return false;
        }
    }

    public static bool TryDisable(
        HardwareDeviceIdentity identity,
        out bool changed,
        out string? detail)
    {
        changed = false;
        detail = null;
        LenovoBatteryChargeProtectionStatus status = Read(identity);
        if (!status.Available)
        {
            detail = status.Detail;
            return false;
        }
        if (!status.Writable)
        {
            detail = $"{status.Detail} · the installed Lenovo PM Device is not writable, so ThinkControl left charging unchanged.";
            return false;
        }
        if (!status.Enabled)
        {
            detail = "Battery thresholds are already disabled; normal full charging remains active.";
            return true;
        }

        RegistryBatteryConfig? before = ReadRegistryConfig();
        if (before is null)
        {
            detail = "The Lenovo battery configuration disappeared before the threshold release.";
            return false;
        }

        try
        {
            // Keep the stored percentages so enabling protection later can recover the
            // previous pair, but clear both control flags. At the driver level Lenovo
            // requires both latched thresholds to be cleared before Automatic mode.
            WriteRegistryConfig(before.SubKeyName, before.StartPercent, before.StopPercent, enabled: false);
            if (!ApplyDriverThresholds(0, 0, enabled: false, out string? driverError))
                throw new BatteryThresholdException(driverError ?? "Lenovo PM Device rejected the return to automatic charging.");

            RegistryBatteryConfig? verified = ReadRegistryConfig(before.SubKeyName);
            if (verified is null || IsEnabled(verified))
                throw new BatteryThresholdException("Lenovo threshold registry readback still reports an enabled threshold after release.");

            changed = true;
            detail = "Battery charge thresholds disabled · Lenovo PM Device returned to automatic/full charging and PWRMGRV readback verified the release.";
            BroadcastLenovoPowerSettingChange();
            return true;
        }
        catch (Exception ex)
        {
            RestorePreviousState(before);
            detail = $"Battery threshold release failed safely: {Describe(ex)}. The previous Lenovo charging configuration was requested again.";
            return false;
        }
    }

    public static bool TryValidatePair(int startPercent, int stopPercent, out string? error)
    {
        error = null;
        if (startPercent < MinimumStartPercent || startPercent > MaximumStartPercent ||
            stopPercent < MinimumStopPercent || stopPercent > MaximumStopPercent)
        {
            error = $"Battery thresholds must stay within {MinimumStartPercent}–{MaximumStartPercent}% start and {MinimumStopPercent}–{MaximumStopPercent}% stop.";
            return false;
        }
        if (startPercent % ThresholdStepPercent != 0 || stopPercent % ThresholdStepPercent != 0)
        {
            error = $"Battery thresholds use {ThresholdStepPercent}% steps, matching the Lenovo control surface.";
            return false;
        }
        if (startPercent >= stopPercent)
        {
            error = "The charge-start threshold must be lower than the charge-stop threshold.";
            return false;
        }
        return true;
    }

    private static bool ApplyDriverThresholds(int startPercent, int stopPercent, bool enabled, out string? error)
    {
        error = null;
        using SafeFileHandle handle = OpenDriver();
        if (handle.IsInvalid)
        {
            error = $"\\.\\IBMPmDrv could not be opened ({Marshal.GetLastWin32Error()}).";
            return false;
        }

        if (!enabled)
        {
            // The embedded controller can retain the old stop threshold if Automatic
            // mode is selected first. Clear both latches before handing ownership back.
            return SendDriverCommand(handle, IoctlSetStop, PrimaryBattery, 0, out error) &&
                   SendDriverCommand(handle, IoctlSetStart, PrimaryBattery, 0, out error) &&
                   SendDriverCommand(handle, IoctlSetChargeMode, AutomaticMode, 0, out error);
        }

        return SendDriverCommand(handle, IoctlSetChargeMode, ThresholdMode, 0, out error) &&
               SendDriverCommand(handle, IoctlSetStop, PrimaryBattery | (uint)stopPercent, stopPercent, out error) &&
               SendDriverCommand(handle, IoctlSetStart, PrimaryBattery | (uint)startPercent, startPercent, out error);
    }

    private static bool SendDriverCommand(
        SafeFileHandle handle,
        uint ioctl,
        uint payload,
        int semanticPercent,
        out string? error)
    {
        error = null;
        uint input = payload;
        uint result = 0;
        bool ok = DeviceIoControl(
            handle,
            ioctl,
            ref input,
            sizeof(uint),
            ref result,
            sizeof(uint),
            out uint bytesReturned,
            IntPtr.Zero);
        if (!ok)
        {
            error = $"Lenovo PM Device IOCTL 0x{ioctl:X8} failed with Win32 {Marshal.GetLastWin32Error()}.";
            return false;
        }
        if (bytesReturned >= sizeof(uint) && (result & DriverRejected) != 0)
        {
            string semantic = semanticPercent > 0 ? $" ({semanticPercent}%)" : string.Empty;
            error = $"Lenovo PM Device rejected IOCTL 0x{ioctl:X8}{semantic}.";
            return false;
        }
        return true;
    }

    private static RegistryBatteryConfig? ReadRegistryConfig(string? preferredSubKey = null)
    {
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(RegistryRoot, writable: false);
        if (root is null)
            return null;

        IEnumerable<string> names = preferredSubKey is null
            ? root.GetSubKeyNames()
            : [preferredSubKey];
        foreach (string name in names)
        {
            using RegistryKey? battery = root.OpenSubKey(name, writable: false);
            if (battery is null || !TryReadDword(battery, "ChargeStartPercentage", out int start))
                continue;
            if (!TryReadDword(battery, "ChargeStopPercentage", out int stop))
                continue;
            _ = TryReadDword(battery, "ChargeStartControl", out int startControl);
            _ = TryReadDword(battery, "ChargeStopControl", out int stopControl);
            return new RegistryBatteryConfig(name, start, stop, startControl, stopControl);
        }
        return null;
    }

    private static void WriteRegistryConfig(string subKeyName, int start, int stop, bool enabled)
    {
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(RegistryRoot, writable: true)
            ?? throw new BatteryThresholdException("Lenovo PWRMGRV configuration is not writable.");
        using RegistryKey? battery = root.OpenSubKey(subKeyName, writable: true)
            ?? throw new BatteryThresholdException("The Lenovo battery configuration key is no longer writable.");
        battery.SetValue("ChargeStartPercentage", start, RegistryValueKind.DWord);
        battery.SetValue("ChargeStopPercentage", stop, RegistryValueKind.DWord);
        battery.SetValue("ChargeStartControl", enabled ? 1 : 0, RegistryValueKind.DWord);
        battery.SetValue("ChargeStopControl", enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    private static void RestorePreviousState(RegistryBatteryConfig before)
    {
        try
        {
            WriteRegistryConfig(before.SubKeyName, before.StartPercent, before.StopPercent, IsEnabled(before));
            _ = ApplyDriverThresholds(
                NormalizeStoredStart(before.StartPercent),
                NormalizeStoredStop(before.StopPercent),
                IsEnabled(before),
                out _);
            BroadcastLenovoPowerSettingChange();
        }
        catch
        {
            // Recovery is best effort. The caller reports the failed transition and
            // never claims the requested state became active.
        }
    }

    private static bool CanOpenDriver(out string detail)
    {
        using SafeFileHandle handle = OpenDriver();
        if (!handle.IsInvalid)
        {
            detail = "IBMPmDrv ready";
            return true;
        }
        int error = Marshal.GetLastWin32Error();
        detail = $"IBMPmDrv unavailable (Win32 {error})";
        return false;
    }

    private static SafeFileHandle OpenDriver() => CreateFileW(
        DriverPath,
        GenericRead | GenericWrite,
        FileShareRead | FileShareWrite,
        IntPtr.Zero,
        OpenExisting,
        FileAttributeNormal,
        IntPtr.Zero);

    private static bool TryReadDword(RegistryKey key, string name, out int value)
    {
        value = 0;
        object? raw = key.GetValue(name);
        try
        {
            if (raw is null)
                return false;
            value = Convert.ToInt32(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsEnabled(RegistryBatteryConfig config) =>
        config.StartControl != 0 && config.StopControl != 0 &&
        config.StartPercent < config.StopPercent && config.StopPercent < 100;

    private static int NormalizeStoredStart(int value) =>
        value is >= MinimumStartPercent and <= MaximumStartPercent ? value : 75;

    private static int NormalizeStoredStop(int value) =>
        value is >= MinimumStopPercent and <= MaximumStopPercent ? value : 85;

    private static LenovoBatteryChargeProtectionStatus Unavailable(string detail) => new(
        false, false, false, 75, 100, true,
        "Lenovo PM Device · charge thresholds", detail);

    private static string Describe(Exception ex) => ex is BatteryThresholdException
        ? ex.Message
        : ex is Win32Exception win32
            ? $"{win32.GetType().Name}/{win32.NativeErrorCode}"
            : ex.GetType().Name;

    private static void BroadcastLenovoPowerSettingChange()
    {
        const uint WmSettingChange = 0x001A;
        const uint SmtoAbortIfHung = 0x0002;
        IntPtr hwndBroadcast = new(0xFFFF);
        _ = SendMessageTimeoutW(
            hwndBroadcast,
            WmSettingChange,
            UIntPtr.Zero,
            "SOFTWARE\\Lenovo\\PWRMGRV",
            SmtoAbortIfHung,
            250,
            out _);
    }

    private sealed record RegistryBatteryConfig(
        string SubKeyName,
        int StartPercent,
        int StopPercent,
        int StartControl,
        int StopControl);

    private sealed class BatteryThresholdException(string message) : Exception(message);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref uint lpInBuffer,
        int nInBufferSize,
        ref uint lpOutBuffer,
        int nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);
}
