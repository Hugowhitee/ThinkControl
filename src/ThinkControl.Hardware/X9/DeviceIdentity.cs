using System.Management;
using System.Text.RegularExpressions;

namespace ThinkControl.Hardware.X9;

public sealed record HardwareDeviceIdentity(
    string Manufacturer,
    string ProductName,
    string MachineType,
    bool IsVerifiedX9)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ProductName)
        ? "Lenovo ThinkPad"
        : ProductName;
}

public static class DeviceIdentity
{
    private static readonly string[] VerifiedMachineTypes = ["21Q6", "21Q7"];

    public static HardwareDeviceIdentity Read()
    {
        string manufacturer = string.Empty;
        string model = string.Empty;
        string systemSku = string.Empty;
        string productVersion = string.Empty;

        try
        {
            using var computer = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Manufacturer,Model,SystemSKUNumber FROM Win32_ComputerSystem");
            foreach (ManagementObject item in computer.Get())
            {
                manufacturer = Convert.ToString(item["Manufacturer"])?.Trim() ?? string.Empty;
                model = Convert.ToString(item["Model"])?.Trim() ?? string.Empty;
                systemSku = Convert.ToString(item["SystemSKUNumber"])?.Trim() ?? string.Empty;
                item.Dispose();
                break;
            }
        }
        catch
        {
        }

        try
        {
            using var product = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Version FROM Win32_ComputerSystemProduct");
            foreach (ManagementObject item in product.Get())
            {
                productVersion = Convert.ToString(item["Version"])?.Trim() ?? string.Empty;
                item.Dispose();
                break;
            }
        }
        catch
        {
        }

        string machineType = FirstVerifiedMachineType(systemSku, model, productVersion);
        bool lenovo = manufacturer.Contains("LENOVO", StringComparison.OrdinalIgnoreCase);
        bool verified = lenovo && VerifiedMachineTypes.Contains(machineType, StringComparer.OrdinalIgnoreCase);

        string productName = !string.IsNullOrWhiteSpace(productVersion) ? productVersion : model;
        if (verified && !productName.Contains("ThinkPad", StringComparison.OrdinalIgnoreCase))
            productName = "ThinkPad X9-15 Gen 1";

        return new HardwareDeviceIdentity(manufacturer, productName, machineType, verified);
    }

    private static string FirstVerifiedMachineType(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            foreach (string verified in VerifiedMachineTypes)
            {
                if (candidate.Contains(verified, StringComparison.OrdinalIgnoreCase))
                    return verified;
            }

            Match match = Regex.Match(candidate, @"(?:MT[_ -]?)?(?<mt>[A-Z0-9]{4})(?:[_ -]|$)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups["mt"].Value.ToUpperInvariant();
        }

        return string.Empty;
    }
}
