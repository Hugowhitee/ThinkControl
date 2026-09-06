using Microsoft.Win32;
using System.Management;
using System.Text.RegularExpressions;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI.Services;

public sealed record SystemStatusSnapshot(
    string DeviceName,
    string CpuName,
    string GpuName,
    string RamText,
    string BiosVersion,
    string MachineType,
    int BatteryPercent,
    string BatteryStatus,
    string Manufacturer);

public sealed record StartupSystemIdentity(
    string DeviceName,
    string MachineType,
    string Manufacturer);

public sealed class SystemStatusService
{
    private const string BiosRegistryPath = @"HARDWARE\DESCRIPTION\System\BIOS";
    private static readonly string[] VerifiedX9MachineTypes = ["21Q6", "21Q7"];
    private readonly object _cacheGate = new();
    private StaticSystemIdentity? _cachedIdentity;
    private int _fastStartupReadPending;

    /// <summary>
    /// Marks the next Read() as the process-startup preflight. That one read uses only
    /// cheap registry/power data so shell/tray creation cannot be held behind WMI.
    /// The following normal Read(), already called from RefreshStatusAsync on a worker,
    /// performs and caches the full CPU/GPU/BIOS inventory.
    /// </summary>
    public void UseFastStartupReadOnce() => Interlocked.Exchange(ref _fastStartupReadPending, 1);

    /// <summary>
    /// Reads only the cheap firmware identity values Windows already exposes in the
    /// registry. Shell/tray creation and enabled touchpad gestures must not wait for
    /// the full WMI CPU/GPU/BIOS inventory.
    /// </summary>
    public StartupSystemIdentity ReadStartupIdentity()
    {
        try
        {
            using RegistryKey? bios = Registry.LocalMachine.OpenSubKey(BiosRegistryPath, writable: false);
            string manufacturer = ReadRegistryString(bios, "SystemManufacturer") ?? string.Empty;
            string productName = ReadRegistryString(bios, "SystemProductName") ?? string.Empty;
            string productVersion = ReadRegistryString(bios, "SystemVersion") ?? string.Empty;
            string sku = ReadRegistryString(bios, "SystemSKU") ?? string.Empty;

            string machineType = ParseMachineType(sku, productName, productVersion);
            string deviceName = SelectDeviceName(productVersion, productName);
            if (string.IsNullOrWhiteSpace(deviceName))
                deviceName = "Windows laptop";

            return new StartupSystemIdentity(
                deviceName.Trim(),
                machineType,
                manufacturer.Trim());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return new StartupSystemIdentity("Windows laptop", "—", string.Empty);
        }
    }

    public SystemStatusSnapshot Read()
    {
        if (Interlocked.Exchange(ref _fastStartupReadPending, 0) == 1)
            return BuildFastStartupSnapshot();

        StaticSystemIdentity identity = GetStaticIdentity();
        (int battery, string batteryStatus) = ReadPowerState();

        return new SystemStatusSnapshot(
            identity.DeviceName,
            identity.CpuName,
            identity.GpuName,
            identity.RamText,
            identity.BiosVersion,
            identity.MachineType,
            battery,
            batteryStatus,
            identity.Manufacturer);
    }

    private SystemStatusSnapshot BuildFastStartupSnapshot()
    {
        StartupSystemIdentity identity = ReadStartupIdentity();
        (int battery, string batteryStatus) = ReadPowerState();
        return new SystemStatusSnapshot(
            identity.DeviceName,
            "—",
            "—",
            "—",
            "—",
            identity.MachineType,
            battery,
            batteryStatus,
            identity.Manufacturer);
    }

    private static (int Battery, string Status) ReadPowerState()
    {
        Forms.PowerStatus power = Forms.SystemInformation.PowerStatus;
        int battery = power.BatteryLifePercent is >= 0 and <= 1
            ? (int)Math.Round(power.BatteryLifePercent * 100)
            : 0;
        string batteryStatus = power.PowerLineStatus switch
        {
            Forms.PowerLineStatus.Online => battery >= 100 ? "Fully charged" : "Charging / AC",
            Forms.PowerLineStatus.Offline => "On battery",
            _ => "Power state unknown"
        };
        return (battery, batteryStatus);
    }

    private StaticSystemIdentity GetStaticIdentity()
    {
        lock (_cacheGate)
        {
            if (_cachedIdentity is not null)
                return _cachedIdentity;

            string manufacturer = ReadFirst("Win32_ComputerSystem", "Manufacturer") ?? "";
            string model = ReadFirst("Win32_ComputerSystem", "Model") ?? "ThinkPad";
            string productVersion = ReadFirst("Win32_ComputerSystemProduct", "Version") ?? "";
            string cpu = ReadFirst("Win32_Processor", "Name") ?? "—";
            string gpu = ReadFirst("Win32_VideoController", "Name") ?? "—";
            string bios = ReadFirst("Win32_BIOS", "SMBIOSBIOSVersion") ?? "—";
            string? sku = ReadFirst("Win32_ComputerSystem", "SystemSKUNumber");
            string machineType = ParseMachineType(sku, model, productVersion);
            string ram = FormatRam(ReadFirstUlong("Win32_ComputerSystem", "TotalPhysicalMemory"));

            _cachedIdentity = new StaticSystemIdentity(
                SelectDeviceName(productVersion, model),
                cpu.Trim(),
                gpu.Trim(),
                ram,
                bios.Trim(),
                machineType,
                manufacturer.Trim());
            return _cachedIdentity;
        }
    }

    private static string? ReadRegistryString(RegistryKey? key, string valueName)
    {
        object? value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            string[] values when values.Length > 0 => values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
            _ => null
        };
    }

    private static string? ReadFirst(string className, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementObject result in results)
                return result[property]?.ToString();
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static ulong? ReadFirstUlong(string className, string property)
    {
        string? raw = ReadFirst(className, property);
        return ulong.TryParse(raw, out ulong value) ? value : null;
    }

    private static string FormatRam(ulong? bytes)
    {
        if (bytes is null || bytes == 0)
            return "—";

        double gib = bytes.Value / 1024d / 1024d / 1024d;
        return $"{Math.Round(gib):0} GB";
    }

    private static string SelectDeviceName(string productVersion, string model)
    {
        string version = productVersion.Trim();
        if (!string.IsNullOrWhiteSpace(version) &&
            !string.Equals(version, "ThinkPad", StringComparison.OrdinalIgnoreCase) &&
            (version.Contains("ThinkPad", StringComparison.OrdinalIgnoreCase) ||
             version.Contains("ThinkBook", StringComparison.OrdinalIgnoreCase) ||
             version.Contains("Yoga", StringComparison.OrdinalIgnoreCase) ||
             version.Contains("IdeaPad", StringComparison.OrdinalIgnoreCase) ||
             version.Contains("Legion", StringComparison.OrdinalIgnoreCase) ||
             version.Contains("LOQ", StringComparison.OrdinalIgnoreCase)))
        {
            return version;
        }

        return model.Trim();
    }

    private static string ParseMachineType(params string?[] candidates)
    {
        foreach (string verified in VerifiedX9MachineTypes)
        {
            foreach (string? candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    candidate.Contains(verified, StringComparison.OrdinalIgnoreCase))
                {
                    return verified;
                }
            }
        }

        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            Match explicitMatch = Regex.Match(
                candidate,
                @"(?:MTM?|TYPE)[_ -]?(?<mt>[0-9][A-Z0-9]{3})(?:[_ -]|$)",
                RegexOptions.IgnoreCase);
            if (explicitMatch.Success)
                return explicitMatch.Groups["mt"].Value.ToUpperInvariant();

            Match tokenMatch = Regex.Match(
                candidate,
                @"(?<![A-Z0-9])(?<mt>[0-9][A-Z0-9]{3})(?![A-Z0-9])",
                RegexOptions.IgnoreCase);
            if (tokenMatch.Success)
                return tokenMatch.Groups["mt"].Value.ToUpperInvariant();
        }

        return "—";
    }

    private sealed record StaticSystemIdentity(
        string DeviceName,
        string CpuName,
        string GpuName,
        string RamText,
        string BiosVersion,
        string MachineType,
        string Manufacturer);
}
