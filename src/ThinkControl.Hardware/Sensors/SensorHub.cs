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
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OpenRetryInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan AcpiFallbackInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EmptyProviderRecycleInterval = TimeSpan.FromMinutes(5);
    private const int MaxSensorCount = 256;
    private const int EmptyCriticalRefreshesBeforeRecycle = 4;

    private readonly object _gate = new();
    private Computer? _computer;
    private bool _disposed;
    private DateTimeOffset _lastOpenAttempt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastAcpiRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastProviderRecycle = DateTimeOffset.MinValue;
    private int _emptyCriticalRefreshes;
    private HardwareSensorReading? _cachedAcpiThermal;
    private SensorHubSnapshot _last = EmptySnapshot();

    public SensorHubSnapshot Read()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastRefresh < RefreshInterval)
                return _last;

            EnsureOpen(now);
            var readings = new List<HardwareSensorReading>(80);

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
                    CloseComputer();
                }
            }

            HardwareSensorReading? cpu = SelectCpuTemperature(readings);
            if (cpu is null && GetAcpiThermalZone(now) is HardwareSensorReading acpi)
            {
                // ACPI thermal zones are real telemetry, but Windows does not
                // guarantee that a zone represents the CPU package. Keep the value
                // visible only as a system thermal-zone reading. Most importantly,
                // do not execute a root\wmi query every hot sensor refresh.
                readings.Add(acpi);
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

    public void RefreshProviders()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            CloseComputer();
            _last = EmptySnapshot();
            _lastOpenAttempt = DateTimeOffset.MinValue;
            _lastRefresh = DateTimeOffset.MinValue;
            _lastAcpiRead = DateTimeOffset.MinValue;
            _cachedAcpiThermal = null;
            _lastProviderRecycle = DateTimeOffset.MinValue;
            _emptyCriticalRefreshes = 0;
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
                IsMotherboardEnabled = true,

                // SMART/storage discovery is one of the few LHM paths that can wake
                // devices and create noticeable latency spikes on a laptop. ThinkControl
                // does not use it for cooling, so it stays out of the always-on service.
                IsStorageEnabled = false,
                IsMemoryEnabled = false,

                // Battery telemetry is intentionally owned by ThinkControl's native/
                // WMI battery service. Duplicating it through LHM adds no useful data.
                IsBatteryEnabled = false,

                IsNetworkEnabled = false,
                IsControllerEnabled = false,
                IsPsuEnabled = false,
                IsPowerMonitorEnabled = false
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

        CloseComputer();
        _lastProviderRecycle = now;
        _lastOpenAttempt = DateTimeOffset.MinValue;
        _emptyCriticalRefreshes = 0;
    }

    private static void VisitHardware(IHardware hardware, ICollection<HardwareSensorReading> output)
    {
        if (output.Count >= MaxSensorCount)
            return;

        bool updated = true;
        try { hardware.Update(); }
        catch { updated = false; }

        if (updated)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (output.Count >= MaxSensorCount)
                    break;
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
        }

        // Some parent hardware objects can fail their own optional update while a
        // useful child still works. Do not throw away the complete subtree.
        foreach (IHardware child in hardware.SubHardware)
        {
            if (output.Count >= MaxSensorCount)
                break;
            VisitHardware(child, output);
        }
    }

    private HardwareSensorReading? GetAcpiThermalZone(DateTimeOffset now)
    {
        if (now - _lastAcpiRead < AcpiFallbackInterval)
            return _cachedAcpiThermal;

        _lastAcpiRead = now;
        _cachedAcpiThermal = TryReadAcpiThermalZone(out HardwareSensorReading? reading)
            ? reading
            : null;
        return _cachedAcpiThermal;
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
                false,
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

    private static SensorHubSnapshot EmptySnapshot() => new(
        Array.Empty<HardwareSensorReading>(),
        null,
        "Unavailable",
        null,
        "Unavailable");

    private static bool IsTemperature(HardwareSensorReading reading) =>
        string.Equals(reading.SensorType, "Temperature", StringComparison.OrdinalIgnoreCase);

    private static bool IsCpuHardware(string type) => type.Equals("Cpu", StringComparison.OrdinalIgnoreCase);
    private static bool IsGpuHardware(string type) => type.Contains("Gpu", StringComparison.OrdinalIgnoreCase);
    private static bool IsPlausibleTemperature(double celsius) => celsius is >= -20 and <= 125;

    private static int HardwareSortKey(string hardwareType) => hardwareType switch
    {
        "Cpu" => 0,
        "GpuIntel" or "GpuNvidia" or "GpuAmd" => 1,
        "Motherboard" => 2,
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
