using System.Management;

namespace ThinkControl.UI.Services;

public static class BatteryCycleCountService
{
    public static int? Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT CycleCount FROM BatteryCycleCount");

            foreach (ManagementObject item in searcher.Get())
            {
                try
                {
                    object? raw = item.Properties["CycleCount"]?.Value;
                    if (raw is null)
                        continue;

                    int value = Convert.ToInt32(raw);
                    if (value is >= 0 and < 100000)
                        return value;
                }
                finally
                {
                    item.Dispose();
                }
            }
        }
        catch
        {
            // Some batteries do not expose a hardware cycle counter through WMI.
        }

        return null;
    }
}
