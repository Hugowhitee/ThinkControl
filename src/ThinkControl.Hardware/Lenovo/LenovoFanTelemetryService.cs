using System.Management;

namespace ThinkControl.Hardware.Lenovo;

internal sealed record LenovoFanReading(int Rpm, string Label, string Source);

/// <summary>
/// Read-only fan telemetry discovery for Lenovo and generic Windows surfaces.
/// No setters are invoked here.
/// </summary>
internal static class LenovoFanTelemetryService
{
    internal static IReadOnlyList<LenovoFanReading> Read()
    {
        IReadOnlyList<LenovoFanReading> readings = ReadLenovoFanMethod();
        if (readings.Count > 0)
            return readings;

        readings = ReadLenovoDesktopFanClasses();
        if (readings.Count > 0)
            return readings;

        return ReadCimTachometers();
    }

    private static IReadOnlyList<LenovoFanReading> ReadLenovoFanMethod()
    {
        var result = new List<LenovoFanReading>(3);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT * FROM LENOVO_FAN_METHOD");

            foreach (ManagementObject fanMethod in searcher.Get())
            {
                using (fanMethod)
                {
                    // Lenovo firmware normally exposes fan IDs 0/1. Probe ID 2 as
                    // well for newer multi-fan designs, but stop at the first invalid
                    // method response and accept only plausible RPM values.
                    for (int fanId = 0; fanId < 3; fanId++)
                    {
                        if (!TryInvokeCurrentFanSpeed(fanMethod, fanId, out int rpm))
                            continue;

                        if (!IsPlausibleRpm(rpm))
                            continue;

                        result.Add(new LenovoFanReading(
                            rpm,
                            $"Fan #{fanId + 1}",
                            "Lenovo WMI · LENOVO_FAN_METHOD"));
                    }
                }

                if (result.Count > 0)
                    break;
            }
        }
        catch
        {
            // Missing vendor WMI is normal on many ThinkPads.
        }

        return result;
    }

    private static bool TryInvokeCurrentFanSpeed(ManagementObject fanMethod, int fanId, out int rpm)
    {
        rpm = 0;
        try
        {
            using ManagementBaseObject inParams = fanMethod.GetMethodParameters("Fan_GetCurrentFanSpeed");
            if (inParams.Properties["FanID"] is null)
                return false;

            inParams["FanID"] = fanId;
            using ManagementBaseObject? outParams = fanMethod.InvokeMethod(
                "Fan_GetCurrentFanSpeed",
                inParams,
                null);

            if (outParams is null || outParams.Properties["CurrentFanSpeed"] is null)
                return false;

            rpm = Convert.ToInt32(outParams["CurrentFanSpeed"]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<LenovoFanReading> ReadLenovoDesktopFanClasses()
    {
        var result = new List<LenovoFanReading>(2);
        (string ClassName, string Label)[] classes =
        {
            ("Lenovo_DT_GetCPUFan", "CPU fan"),
            ("Lenovo_DT_GetSYSFan", "System fan")
        };

        foreach ((string className, string label) in classes)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    $"SELECT * FROM {className}");

                foreach (ManagementObject item in searcher.Get())
                {
                    using (item)
                    {
                        if (item.Properties["return"] is null || item["return"] is null)
                            continue;

                        int rpm = Convert.ToInt32(item["return"]);
                        if (!IsPlausibleRpm(rpm))
                            continue;

                        result.Add(new LenovoFanReading(rpm, label, $"Lenovo WMI · {className}"));
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        return result;
    }

    private static IReadOnlyList<LenovoFanReading> ReadCimTachometers()
    {
        var result = new List<LenovoFanReading>(3);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Name,CurrentReading FROM CIM_Tachometer");

            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    if (item["CurrentReading"] is null)
                        continue;

                    int rpm = Convert.ToInt32(item["CurrentReading"]);
                    if (!IsPlausibleRpm(rpm))
                        continue;

                    string name = Convert.ToString(item["Name"])?.Trim() ?? "Fan";
                    result.Add(new LenovoFanReading(rpm, name, "Windows CIM_Tachometer"));
                    if (result.Count >= 3)
                        break;
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private static bool IsPlausibleRpm(int rpm) => rpm is >= 0 and <= 12000;
}
