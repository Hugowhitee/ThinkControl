using LibreHardwareMonitor.Hardware;
using System.Management;

namespace ThinkControl.Hardware.X9;

public sealed class CpuTemperatureReader : IDisposable
{
    private readonly object _gate = new();
    private Computer? _computer;
    private bool _openAttempted;

    public (double? Celsius, string Source) Read()
    {
        lock (_gate)
        {
            EnsureOpen();
            double? cpu = null;
            string source = "Unavailable";

            if (_computer is not null)
            {
                try
                {
                    foreach (IHardware hardware in _computer.Hardware)
                    {
                        hardware.Update();
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                                continue;

                            bool preferred = sensor.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                                             sensor.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase);
                            if (preferred || !cpu.HasValue)
                            {
                                cpu = sensor.Value.Value;
                                source = sensor.Name;
                            }

                            if (sensor.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase))
                                return (Math.Round(sensor.Value.Value, 1), sensor.Name);
                        }
                    }
                }
                catch
                {
                }
            }

            if (cpu.HasValue)
                return (Math.Round(cpu.Value, 1), source);

            double? acpi = ReadAcpiThermalZone();
            return acpi.HasValue
                ? (acpi.Value, "ACPI thermal zone")
                : (null, "Unavailable");
        }
    }

    private void EnsureOpen()
    {
        if (_openAttempted)
            return;

        _openAttempted = true;
        try
        {
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
        }
        catch
        {
            try { _computer?.Close(); } catch { }
            _computer = null;
        }
    }

    private static double? ReadAcpiThermalZone()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            foreach (ManagementObject item in searcher.Get())
            {
                double raw = Convert.ToDouble(item["CurrentTemperature"]);
                item.Dispose();
                if (raw is < 2500 or > 4500)
                    continue;

                double celsius = raw / 10d - 273.15;
                if (celsius is >= 0 and <= 120)
                    return Math.Round(celsius, 1);
            }
        }
        catch
        {
        }

        return null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try { _computer?.Close(); } catch { }
            _computer = null;
        }
    }
}
