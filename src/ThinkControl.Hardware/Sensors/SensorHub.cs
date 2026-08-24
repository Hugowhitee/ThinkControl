using LibreHardwareMonitor.Hardware;
using System.Management;

namespace ThinkControl.Hardware.Sensors;

public sealed record HardwareSensorReading(
    string Id,
    string HardwareName,
    string HardwareType,
    string Name,
    string SensorType,
    double Value,
    string Unit,
    bool ControlTemperature,
    string Source);

public sealed record SensorHubSnapshot(
    IReadOnlyList<HardwareSensorReading> Sensors,
    double? CpuTemperatureC,
    string CpuTemperatureSource,
    double? ControlTemperatureC,
    string ControlTemperatureSource);

public sealed class SensorHub : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OpenRetryInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EmptyProviderRecycleInterval = TimeSpan.FromSeconds(45);
    private const int MaxSensorCount = 256;
    private const int EmptyCriticalRefreshesBeforeRecycle = 3;

    private readonly object _gate = new();
    private Computer? _computer;
    private bool _disposed;
    private DateTimeOffset _lastOpenAttempt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastProviderRecycle = DateTimeOffset.MinValue;
    private int _emptyCriticalRefreshes;
    private SensorHubSnapshot _last = new(
        Array.Empty<HardwareSensorReading>(),
        null,
        "Unavailable",
        null,
        "Unavailable");

    public SensorHubSnapshot Read()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastRefresh < RefreshInterval)
                return _last;

            EnsureOpen(now);
            var readings = new List<HardwareSensorReading>(96);

            if (_computer is not null)
            {
                try
                {
                    foreach (IHardware hardware in _computer.Hardware)
                    {
                        VisitHardware(hardware, readings);
                        if (readings.Count >= MaxSensorCount)
                            break;
                    }
                }
                catch
                {
                    // Keep read-only telemetry best-effort. If LHM's provider was
                    // invalidated (for example PawnIO was installed after service
                    // startup), recycle it so the next status poll can rediscover.
                    CloseComputer();
                }
            }

            HardwareSensorReading? cpu = SelectCpuTemperature(readings);
            if (cpu is null && TryReadAcpiThermalZone(out HardwareSensorReading? acpi))
            {
                readings.Add(acpi!);
                cpu = acpi;
            }

            RecycleStaleProviderIfNeeded(now, readings);

            HardwareSensorReading? gpu = SelectGpuTemperature(readings);
            HardwareSensorReading? control = MaxTemperature(cpu, gpu);

            if (cpu is not null)
                MarkControl(readings, cpu.Id);
            if (gpu is not null)
                MarkControl(readings, gpu.Id);

            _last = new SensorHubSnapshot(
                readings
                    .OrderBy(r => HardwareSortKey(r.HardwareType))
                    .ThenBy(r => r.HardwareName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => SensorSortKey(r.SensorType))
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                cpu is null ? null : Math.Round(cpu.Value, 1),
                cpu?.Source ?? "Unavailable",
                control is null ? null : Math.Round(control.Value, 1),
                control?.Source ?? "Unavailable");
            _lastRefresh = now;
            return _last;
        }
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        if (_computer is not null || now - _lastOpenAttempt < OpenRetryInterval)
            return;

        _lastOpenAttempt = now;
        Computer? candidate = null;
        try
        {
            candidate = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsBatteryEnabled = true,
                IsNetworkEnabled = true,
                IsControllerEnabled = true,
                IsPsuEnabled = true,
                IsPowerMonitorEnabled = true
            };
            candidate.Open();
            _computer = candidate;
        }
        catch
        {
            try { candidate?.Close(); } catch { }
            _computer = null;
        }
    }

    private void RecycleStaleProviderIfNeeded(DateTimeOffset now, IReadOnlyCollection<HardwareSensorReading> readings)
    {
        bool hasCriticalTelemetry = readings.Any(reading =>
            reading.SensorType.Equals("Temperature", StringComparison.OrdinalIgnoreCase) ||
            reading.SensorType.Equals("Fan", StringComparison.OrdinalIgnoreCase));

        if (hasCriticalTelemetry)
        {
            _emptyCriticalRefreshes = 0;
            return;
        }

        if (_computer is null)
            return;

        _emptyCriticalRefreshes++;
        if (_emptyCriticalRefreshes < EmptyCriticalRefreshesBeforeRecycle ||
            now - _lastProviderRecycle < EmptyProviderRecycleInterval)
        {
            return;
        }

        // Computer/Open providers capture low-level availability when opened. If
        // PawnIO was installed while the ThinkControl service was already running,
        // a provider can remain alive but never surface the newly available sensor
        // path. Periodically recycle only after several missing critical reads.
        CloseComputer();
        _lastProviderRecycle = now;
        _lastOpenAttempt = DateTimeOffset.MinValue;
        _emptyCriticalRefreshes = 0;
    }

    private static void VisitHardware(IHardware hardware, ICollection<HardwareSensorReading> output)
    {
        if (output.Count >= MaxSensorCount)
            return;

        try { hardware.Update(); }
        catch { return; }

        foreach (ISensor sensor in hardware.Sensors)
        {
            if (output.Count >= MaxSensorCount)
                return;
            if (!sensor.Value.HasValue || !float.IsFinite(sensor.Value.Value))
                continue;

            string type = sensor.SensorType.ToString();
            output.Add(new HardwareSensorReading(
                sensor.Identifier.ToString(),
                Clean(hardware.Name, "Hardware"),
                hardware.HardwareType.ToString(),
                Clean(sensor.Name, type),
                type,
                Math.Round(sensor.Value.Value, PrecisionFor(type)),
                UnitFor(type),
                false,
                $"LibreHardwareMonitor/PawnIO · {Clean(hardware.Name, hardware.HardwareType.ToString())} · {Clean(sensor.Name, type)}"));
        }

        foreach (IHardware child in hardware.SubHardware)
            VisitHardware(child, output);
    }

    private static HardwareSensorReading? SelectCpuTemperature(IEnumerable<HardwareSensorReading> readings)
    {
        HardwareSensorReading[] temperatures = readings
            .Where(r => IsTemperature(r) && IsCpuHardware(r.HardwareType) && IsPlausibleTemperature(r.Value))
            .ToArray();
        if (temperatures.Length == 0)
            return null;

        HardwareSensorReading? package = temperatures.FirstOrDefault(r =>
            r.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase) ||
            r.Name.Equals("Package", StringComparison.OrdinalIgnoreCase));
        if (package is not null)
            return package;

        HardwareSensorReading? coreMax = temperatures.FirstOrDefault(r =>
            r.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase));
        return coreMax ?? temperatures.MaxBy(r => r.Value);
    }

    private static HardwareSensorReading? SelectGpuTemperature(IEnumerable<HardwareSensorReading> readings)
    {
        HardwareSensorReading[] temperatures = readings
            .Where(r => IsTemperature(r) && IsGpuHardware(r.HardwareType) && IsPlausibleTemperature(r.Value))
            .ToArray();
        if (temperatures.Length == 0)
            return null;

        HardwareSensorReading? core = temperatures.FirstOrDefault(r =>
            r.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
            r.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase));
        return core ?? temperatures.MaxBy(r => r.Value);
    }

    private static HardwareSensorReading? MaxTemperature(params HardwareSensorReading?[] candidates) =>
        candidates.Where(r => r is not null).Cast<HardwareSensorReading>().MaxBy(r => r.Value);

    private static void MarkControl(List<HardwareSensorReading> readings, string id)
    {
        int index = readings.FindIndex(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (index >= 0)
            readings[index] = readings[index] with { ControlTemperature = true };
    }

    private static bool TryReadAcpiThermalZone(out HardwareSensorReading? reading)
    {
        reading = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT InstanceName,CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            double hottest = double.MinValue;
            string name = "ACPI thermal zone";
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    if (item["CurrentTemperature"] is null)
                        continue;
                    double raw = Convert.ToDouble(item["CurrentTemperature"]);
                    if (raw is < 2500 or > 4500)
                        continue;

                    double celsius = raw / 10d - 273.15;
                    if (!IsPlausibleTemperature(celsius) || celsius <= hottest)
                        continue;

                    hottest = celsius;
                    name = Convert.ToString(item["InstanceName"])?.Trim() ?? name;
                }
            }

            if (!double.IsFinite(hottest) || hottest == double.MinValue)
                return false;

            reading = new HardwareSensorReading(
                "acpi/thermal-zone",
                "ACPI thermal zone",
                "Acpi",
                name,
                "Temperature",
                Math.Round(hottest, 1),
                "°C",
                true,
                "Windows ACPI thermal zone");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CloseComputer()
    {
        try { _computer?.Close(); } catch { }
        _computer = null;
    }

    private static bool IsTemperature(HardwareSensorReading reading) =>
        string.Equals(reading.SensorType, "Temperature", StringComparison.OrdinalIgnoreCase);

    private static bool IsCpuHardware(string type) => type.Equals("Cpu", StringComparison.OrdinalIgnoreCase);
    private static bool IsGpuHardware(string type) => type.Contains("Gpu", StringComparison.OrdinalIgnoreCase);
    private static bool IsPlausibleTemperature(double celsius) => celsius is >= -20 and <= 125;

    private static int HardwareSortKey(string hardwareType) => hardwareType switch
    {
        "Cpu" => 0,
        "GpuIntel" or "GpuNvidia" or "GpuAmd" => 1,
        "Memory" => 2,
        "Storage" => 3,
        "Battery" => 4,
        "Motherboard" => 5,
        _ => 10
    };

    private static int SensorSortKey(string sensorType) => sensorType switch
    {
        "Temperature" => 0,
        "Fan" => 1,
        "Power" => 2,
        "Load" => 3,
        "Clock" => 4,
        "Voltage" => 5,
        "Current" => 6,
        _ => 10
    };

    private static string UnitFor(string sensorType) => sensorType switch
    {
        "Temperature" => "°C",
        "Fan" => "RPM",
        "Power" => "W",
        "Voltage" => "V",
        "Current" => "A",
        "Clock" => "MHz",
        "Frequency" => "Hz",
        "Load" or "Control" or "Level" => "%",
        "Energy" => "Wh",
        "Data" => "GB",
        "SmallData" => "MB",
        "Throughput" => "B/s",
        "TimeSpan" => "s",
        _ => string.Empty
    };

    private static int PrecisionFor(string sensorType) => sensorType switch
    {
        "Fan" or "Clock" or "Frequency" => 0,
        "Temperature" or "Load" or "Control" or "Level" => 1,
        "Power" or "Voltage" or "Current" or "Energy" => 2,
        _ => 2
    };

    private static string Clean(string? value, string fallback)
    {
        string cleaned = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SensorHub));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            CloseComputer();
        }
    }
}
