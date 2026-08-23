using System.Management;

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
        string productName = string.Empty;
        string machineType = string.Empty;

        try
        {
            using var computer = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Manufacturer,Model FROM Win32_ComputerSystem");
            foreach (ManagementObject item in computer.Get())
            {
                manufacturer = Convert.ToString(item["Manufacturer"])?.Trim() ?? string.Empty;
                productName = Convert.ToString(item["Model"])?.Trim() ?? string.Empty;
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
                string version = Convert.ToString(item["Version"])?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(version))
                    productName = version;
                item.Dispose();
                break;
            }
        }
        catch
        {
        }

        try
        {
            using var enclosure = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT SMBIOSAssetTag FROM Win32_SystemEnclosure");
            // Deliberately do not consume asset tags or serials. The query remains
            // intentionally unused as a reminder that ThinkControl must not need
            // unique device identifiers for capability resolution.
            _ = enclosure;
        }
        catch
        {
        }

        try
        {
            using var baseBoard = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Product FROM Win32_BaseBoard");
            foreach (ManagementObject item in baseBoard.Get())
            {
                string candidate = Convert.ToString(item["Product"])?.Trim() ?? string.Empty;
                item.Dispose();

                string prefix = candidate.Length >= 4 ? candidate[..4].ToUpperInvariant() : candidate.ToUpperInvariant();
                if (VerifiedMachineTypes.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                {
                    machineType = prefix;
                    break;
                }
            }
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(machineType))
            machineType = TryReadMachineTypeFromModel(productName);

        bool lenovo = manufacturer.Contains("LENOVO", StringComparison.OrdinalIgnoreCase);
        bool verified = lenovo && VerifiedMachineTypes.Contains(machineType, StringComparer.OrdinalIgnoreCase);

        if (verified && !productName.Contains("ThinkPad", StringComparison.OrdinalIgnoreCase))
            productName = "ThinkPad X9-15 Gen 1";

        return new HardwareDeviceIdentity(manufacturer, productName, machineType, verified);
    }

    private static string TryReadMachineTypeFromModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return string.Empty;

        string compact = model.Trim().ToUpperInvariant();
        foreach (string machineType in VerifiedMachineTypes)
        {
            if (compact.Contains(machineType, StringComparison.Ordinal))
                return machineType;
        }

        return compact.Length >= 4 ? compact[..4] : compact;
    }
}
