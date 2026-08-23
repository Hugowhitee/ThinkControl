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
    string BatteryStatus);

public sealed class SystemStatusService
{
    public SystemStatusSnapshot Read()
    {
        string manufacturer = ReadFirst("Win32_ComputerSystem", "Manufacturer") ?? "";
        string model = ReadFirst("Win32_ComputerSystem", "Model") ?? "ThinkPad";
        string cpu = ReadFirst("Win32_Processor", "Name") ?? "—";
        string gpu = ReadFirst("Win32_VideoController", "Name") ?? "—";
        string bios = ReadFirst("Win32_BIOS", "SMBIOSBIOSVersion") ?? "—";
        string? sku = ReadFirst("Win32_ComputerSystem", "SystemSKUNumber");
        string machineType = ParseMachineType(sku, model);
        string ram = FormatRam(ReadFirstUlong("Win32_ComputerSystem", "TotalPhysicalMemory"));

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

        string deviceName = string.Equals(manufacturer, "LENOVO", StringComparison.OrdinalIgnoreCase)
            ? model
            : model;

        return new SystemStatusSnapshot(deviceName, cpu.Trim(), gpu.Trim(), ram, bios.Trim(), machineType, battery, batteryStatus);
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

    private static string ParseMachineType(string? sku, string model)
    {
        if (!string.IsNullOrWhiteSpace(sku))
        {
            Match match = Regex.Match(sku, @"(?:MT[_ -]?)?(?<mt>[A-Z0-9]{4})(?:[_ -]|$)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups["mt"].Value.ToUpperInvariant();
        }

        Match modelMatch = Regex.Match(model, @"^(?<mt>[A-Z0-9]{4})", RegexOptions.IgnoreCase);
        return modelMatch.Success ? modelMatch.Groups["mt"].Value.ToUpperInvariant() : "—";
    }
}
