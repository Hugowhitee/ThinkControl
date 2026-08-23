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

public sealed class SystemStatusService
{
    private static readonly string[] VerifiedX9MachineTypes = ["21Q6", "21Q7"];

    public SystemStatusSnapshot Read()
    {
        string manufacturer = ReadFirst("Win32_ComputerSystem", "Manufacturer") ?? "";
        string model = ReadFirst("Win32_ComputerSystem", "Model") ?? "ThinkPad";
        string productVersion = ReadFirst("Win32_ComputerSystemProduct", "Version") ?? "";
        string cpu = ReadFirst("Win32_Processor", "Name") ?? "—";
        string gpu = ReadFirst("Win32_VideoController", "Name") ?? "—";
        string bios = ReadFirst("Win32_BIOS", "SMBIOSBIOSVersion") ?? "—";
        string? sku = ReadFirst("Win32_ComputerSystem", "SystemSKUNumber");
        string machineType = ParseMachineType(sku, model, productVersion);
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

        string deviceName = SelectDeviceName(productVersion, model);
        return new SystemStatusSnapshot(
            deviceName,
            cpu.Trim(),
            gpu.Trim(),
            ram,
            bios.Trim(),
            machineType,
            battery,
            batteryStatus,
            manufacturer.Trim());
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
        // Exact verified identifiers get first priority. Lenovo SystemSKUNumber
        // often looks like LENOVO_MT_21Q6_BU_Think_..., so taking the first four
        // letters would incorrectly classify an X9 as "LENO".
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
}
