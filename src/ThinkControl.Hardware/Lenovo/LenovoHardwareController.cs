using ThinkControl.Hardware.Sensors;
using ThinkControl.Hardware.X9;

namespace ThinkControl.Hardware.Lenovo;

public sealed record LenovoHardwareStatus(
    HardwareDeviceIdentity Identity,
    double? CpuTemperatureC,
    string CpuTemperatureSource,
    double? ControlTemperatureC,
    string ControlTemperatureSource,
    IReadOnlyList<HardwareSensorReading> Sensors,
    int? FanRpm,
    string FanRpmSource,
    IReadOnlyList<LenovoFanReading> Fans,
    string FanState,
    string KeyboardBacklight,
    string KeyboardBackend,
    string HardwareAccess,
    bool CanFanTelemetry,
    bool CanFanControl,
    bool CanKeyboardBacklight,
    bool CanCpuTemperature,
    bool CanSensorTelemetry);

public sealed class LenovoHardwareController : IDisposable
{
    private static readonly TimeSpan FanRpmPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EcThermalPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GenericFanPollInterval = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan EcRetryInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan KeyboardProbeInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StaleFanRpmLimit = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly HardwareDeviceIdentity _identity;
    private readonly SensorHub _sensors = new();
    private readonly KeyboardBacklightService _keyboard = new();
    private readonly bool _isLenovo;

    private ThinkPadEc? _ec;
    private DateTimeOffset _lastEcFailure = DateTimeOffset.MinValue;
    private string? _lastEcError;
    private DateTimeOffset _lastFanRpmRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFanRpmSuccess = DateTimeOffset.MinValue;
    private DateTimeOffset _lastEcThermalRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGenericFanRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastKeyboardProbe = DateTimeOffset.MinValue;
    private byte? _fanControl;
    private int? _x9FanRpm;
    private int? _x9AuxFanRpm;
    private string _x9FanRpmSource = "Unavailable";
    private int _x9FanRpmFailures;
    private IReadOnlyList<HardwareSensorReading> _x9EcThermals = Array.Empty<HardwareSensorReading>();
    private IReadOnlyList<LenovoFanReading> _genericFans = Array.Empty<LenovoFanReading>();
    private bool _keyboardAvailable;
    private bool _disposed;

    public LenovoHardwareController()
    {
        _identity = DeviceIdentity.Read();
        _isLenovo = _identity.Manufacturer.Contains("LENOVO", StringComparison.OrdinalIgnoreCase);
    }

    public HardwareDeviceIdentity Identity => _identity;

    public void RefreshProviders()
    {
        ThrowIfDisposed();
        _sensors.RefreshProviders();

        lock (_gate)
        {
            try
            {
                if (IsThinkControlFanState(_fanControl) && _ec is not null)
                    _ec.ReturnToBios();
            }
            catch
            {
            }

            try { _ec?.Dispose(); } catch { }
            try { _keyboard.RefreshBackend(); } catch { }
            _ec = null;
            _lastEcFailure = DateTimeOffset.MinValue;
            _lastEcError = null;
            _lastFanRpmRead = DateTimeOffset.MinValue;
            _lastFanRpmSuccess = DateTimeOffset.MinValue;
            _lastEcThermalRead = DateTimeOffset.MinValue;
            _lastGenericFanRead = DateTimeOffset.MinValue;
            _lastKeyboardProbe = DateTimeOffset.MinValue;
            _fanControl = null;
            _x9FanRpm = null;
            _x9AuxFanRpm = null;
            _x9FanRpmSource = "Unavailable";
            _x9FanRpmFailures = 0;
            _x9EcThermals = Array.Empty<HardwareSensorReading>();
            _genericFans = Array.Empty<LenovoFanReading>();
            _keyboardAvailable = false;
        }
    }

    public void RefreshSensorProviders()
    {
        ThrowIfDisposed();
        _sensors.RefreshProviders();
        lock (_gate)
        {
            _lastEcThermalRead = DateTimeOffset.MinValue;
            _x9EcThermals = Array.Empty<HardwareSensorReading>();
        }
    }

    public void RefreshKeyboardProvider()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            _keyboard.RefreshBackend();
            _lastKeyboardProbe = DateTimeOffset.MinValue;
            _keyboardAvailable = false;
        }
    }

    public LenovoHardwareStatus ReadStatus()
    {
        ThrowIfDisposed();
        SensorHubSnapshot sensorSnapshot = _sensors.Read();

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool ecAvailable = EnsureX9Ec(now);
            IReadOnlyList<LenovoFanReading> lhmFans = BuildLhmFanTelemetry(sensorSnapshot);

            if (ecAvailable && _ec is not null)
            {
                // Preserve the released shared-tach path while Lenovo owns the fans.
                // Selector 0x31 is exercised only after the user explicitly enters a
                // ThinkControl-managed state, where dual-fan synchronization is the
                // behavior under investigation. If no richer telemetry exists in Auto,
                // the old low-rate shared tachometer remains available.
                if (IsThinkControlFanState(_fanControl) || lhmFans.Count == 0)
                    ReadX9FanRpm(now);

                if (!sensorSnapshot.ControlTemperatureC.HasValue)
                    ReadX9EcThermals(now);
                else
                    _x9EcThermals = Array.Empty<HardwareSensorReading>();
            }
            else
            {
                _x9EcThermals = Array.Empty<HardwareSensorReading>();
            }

            IReadOnlyList<HardwareSensorReading> mergedSensors = BuildSensorTelemetry(sensorSnapshot);
            HardwareSensorReading? ecControl = !sensorSnapshot.ControlTemperatureC.HasValue
                ? _x9EcThermals.Where(sensor => sensor.SensorType.Equals("Temperature", StringComparison.OrdinalIgnoreCase)).MaxBy(sensor => sensor.Value)
                : null;
            double? controlTemperature = sensorSnapshot.ControlTemperatureC ?? ecControl?.Value;
            string controlTemperatureSource = sensorSnapshot.ControlTemperatureC.HasValue
                ? sensorSnapshot.ControlTemperatureSource
                : ecControl is not null
                    ? $"ThinkPad X9 EC · hottest read-only thermal sensor · {_ec?.PortLabel ?? "detected port"}"
                    : "Unavailable";

            bool needGenericFanFallback = lhmFans.Count == 0 &&
                (!_identity.IsVerifiedX9 || !ecAvailable || !_x9FanRpm.HasValue);
            if (_isLenovo && needGenericFanFallback && now - _lastGenericFanRead >= GenericFanPollInterval)
                ReadGenericLenovoFanTelemetry(now);

            if (_isLenovo && now - _lastKeyboardProbe >= KeyboardProbeInterval)
            {
                _keyboardAvailable = SafeKeyboardProbe();
                _lastKeyboardProbe = now;
            }

            string keyboardState = "Unavailable";
            if (_keyboardAvailable && _keyboard.TryGet(out KeyboardBacklightLevel level))
            {
                keyboardState = level == KeyboardBacklightLevel.FirmwareAuto
                    ? "Lenovo Auto"
                    : level.ToString();
            }

            IReadOnlyList<LenovoFanReading> fans = BuildFanTelemetry(ecAvailable, lhmFans);
            LenovoFanReading? primaryFan = fans.FirstOrDefault();

            string fanState = _identity.IsVerifiedX9 && ecAvailable && _fanControl.HasValue
                ? ThinkPadFanProtocol.DescribeControl(_fanControl.Value)
                : fans.Count > 0
                    ? "Lenovo managed · read-only telemetry"
                    : "Lenovo managed · telemetry unavailable";

            bool canFanTelemetry = fans.Count > 0;
            bool canSensorTelemetry = mergedSensors.Count > 0;
            string hardwareAccess = BuildHardwareAccess(ecAvailable, canFanTelemetry, _keyboardAvailable, canSensorTelemetry);

            return new LenovoHardwareStatus(
                _identity,
                sensorSnapshot.CpuTemperatureC,
                sensorSnapshot.CpuTemperatureSource,
                controlTemperature,
                controlTemperatureSource,
                mergedSensors,
                primaryFan?.Rpm,
                primaryFan?.Source ?? "Unavailable",
                fans,
                fanState,
                keyboardState,
                _keyboard.BackendLabel,
                hardwareAccess,
                CanFanTelemetry: canFanTelemetry,
                CanFanControl: _identity.IsVerifiedX9 && ecAvailable,
                CanKeyboardBacklight: _isLenovo && _keyboardAvailable,
                CanCpuTemperature: sensorSnapshot.CpuTemperatureC.HasValue,
                CanSensorTelemetry: canSensorTelemetry);
        }
    }

    public bool SetFanLevel(int level, out string? error)
    {
        error = null;
        ThrowIfDisposed();

        if (!_identity.IsVerifiedX9)
        {
            error = "Direct fan writes are available only for the verified ThinkPad X9-15 Gen 1 21Q6/21Q7 profile. This device remains Lenovo-managed.";
            return false;
        }

        if (level is < 1 or > 7)
        {
            error = "Manual EC step must be between 1 and 7.";
            return false;
        }

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!EnsureX9Ec(now) || _ec is null)
            {
                error = _lastEcError ?? "PawnIO/EC access is unavailable.";
                return false;
            }

            try
            {
                _ec.SetManualLevel((byte)level);
                _fanControl = (byte)level;
                _lastFanRpmRead = now - FanRpmPollInterval + TimeSpan.FromSeconds(4);
                return true;
            }
            catch (Exception ex)
            {
                MarkEcFailed(ex, now);
                error = $"Fan step could not be verified: {ex.Message}";
                return false;
            }
        }
    }

    public bool ReturnFanToAuto(out string? error)
    {
        error = null;
        ThrowIfDisposed();

        if (!_identity.IsVerifiedX9)
        {
            error = "This device does not use ThinkControl's verified X9 EC fan provider; its firmware remains in control.";
            return false;
        }

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!EnsureX9Ec(now) || _ec is null)
            {
                error = _lastEcError ?? "PawnIO/EC access is unavailable.";
                return false;
            }

            try
            {
                _ec.ReturnToBios();
                _fanControl = ThinkPadRegisters.BiosControl;
                _x9AuxFanRpm = null;
                _lastFanRpmRead = now - FanRpmPollInterval + TimeSpan.FromSeconds(4);
                return true;
            }
            catch (Exception ex)
            {
                MarkEcFailed(ex, now);
                error = $"Lenovo Auto could not be verified: {ex.Message}";
                return false;
            }
        }
    }

    public bool SetKeyboardBacklight(string value, out string? error)
    {
        error = null;
        ThrowIfDisposed();

        if (!_isLenovo)
        {
            error = "Lenovo keyboard hardware control is unavailable on this manufacturer.";
            return false;
        }

        if (!Enum.TryParse(value, true, out KeyboardBacklightLevel level))
        {
            error = "Keyboard backlight state must be Off, Low, High or FirmwareAuto.";
            return false;
        }

        lock (_gate)
        {
            if (!SafeKeyboardProbe())
            {
                _keyboardAvailable = false;
                error = "No compatible Lenovo keyboard-backlight driver contract passed the read probe.";
                return false;
            }

            bool success = _keyboard.SetAndVerify(level);
            if (!success)
            {
                error = level == KeyboardBacklightLevel.FirmwareAuto
                    ? "Lenovo OEM Auto is not exposed by a verified set-and-readback contract on this keyboard backend."
                    : $"Lenovo keyboard backlight did not verify {level}.";
                return false;
            }

            _keyboardAvailable = true;
            _lastKeyboardProbe = DateTimeOffset.UtcNow;
            return true;
        }
    }

    private void ReadX9FanRpm(DateTimeOffset now)
    {
        if (_ec is null || now - _lastFanRpmRead < FanRpmPollInterval)
            return;

        _lastFanRpmRead = now;
        try
        {
            if (IsThinkControlFanState(_fanControl))
            {
                ThinkPadFanRpmPair pair = _ec.ReadFanRpms();
                _x9FanRpm = pair.MainRpm;
                _x9AuxFanRpm = pair.AuxiliaryRpm;
                _x9FanRpmSource = $"ThinkPad X9 EC dual tachometers · selector 0x31 + 0x84/0x85 · {_ec.PortLabel}";
            }
            else
            {
                _x9FanRpm = _ec.ReadFanRpm();
                _x9AuxFanRpm = null;
                _x9FanRpmSource = $"ThinkPad X9 EC shared tachometer 0x84/0x85 · {_ec.PortLabel}";
            }

            _lastFanRpmSuccess = now;
            _x9FanRpmFailures = 0;
        }
        catch (Exception ex)
        {
            _x9FanRpmFailures++;
            if (_x9FanRpmFailures >= 2 || now - _lastFanRpmSuccess > StaleFanRpmLimit)
            {
                _x9FanRpm = null;
                _x9AuxFanRpm = null;
                _x9FanRpmSource = $"Unavailable · X9 tachometer read failed: {ex.GetType().Name}";
            }
        }
    }

    private void ReadX9EcThermals(DateTimeOffset now)
    {
        if (_ec is null || now - _lastEcThermalRead < EcThermalPollInterval)
            return;

        _lastEcThermalRead = now;
        try
        {
            string port = _ec.PortLabel;
            _x9EcThermals = _ec.ReadThermalSensors()
                .Select(reading => new HardwareSensorReading(
                    $"x9-ec-thermal-{reading.Register:X2}",
                    "ThinkPad X9 embedded controller",
                    "EmbeddedController",
                    $"Thermal 0x{reading.Register:X2}",
                    "Temperature",
                    reading.Celsius,
                    "°C",
                    false,
                    $"ThinkPad X9 EC · read-only unmapped thermal · {port}"))
                .OrderBy(sensor => sensor.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            _x9EcThermals = Array.Empty<HardwareSensorReading>();
        }
    }

    private IReadOnlyList<HardwareSensorReading> BuildSensorTelemetry(SensorHubSnapshot snapshot)
    {
        if (_x9EcThermals.Count == 0)
            return snapshot.Sensors;

        HardwareSensorReading? hottest = _x9EcThermals.MaxBy(sensor => sensor.Value);
        var combined = new List<HardwareSensorReading>(snapshot.Sensors.Count + _x9EcThermals.Count);
        combined.AddRange(snapshot.Sensors);
        foreach (HardwareSensorReading sensor in _x9EcThermals)
        {
            combined.Add(hottest is not null && sensor.Id == hottest.Id
                ? sensor with { ControlTemperature = true }
                : sensor);
        }
        return combined;
    }

    private void ReadGenericLenovoFanTelemetry(DateTimeOffset now)
    {
        _lastGenericFanRead = now;
        _genericFans = LenovoFanTelemetryService.Read();
    }

    private static IReadOnlyList<LenovoFanReading> BuildLhmFanTelemetry(SensorHubSnapshot sensors)
    {
        return sensors.Sensors
            .Where(sensor => string.Equals(sensor.SensorType, "Fan", StringComparison.OrdinalIgnoreCase))
            .Where(sensor => sensor.Value is >= 0 and <= 20000)
            .GroupBy(sensor => sensor.Id, StringComparer.OrdinalIgnoreCase)
            .Select((group, index) =>
            {
                HardwareSensorReading sensor = group.First();
                return new LenovoFanReading(
                    $"lhm-pawnio-{index + 1}",
                    (int)Math.Round(sensor.Value),
                    string.IsNullOrWhiteSpace(sensor.Name) ? $"Fan {index + 1}" : sensor.Name,
                    sensor.Source);
            })
            .ToArray();
    }

    private IReadOnlyList<LenovoFanReading> BuildFanTelemetry(
        bool ecAvailable,
        IReadOnlyList<LenovoFanReading> lhmFans)
    {
        if (_identity.IsVerifiedX9 && ecAvailable && _x9FanRpm.HasValue && _x9AuxFanRpm.HasValue)
        {
            return
            [
                new LenovoFanReading(
                    "x9-ec-main",
                    _x9FanRpm.Value,
                    "Fan 1",
                    _x9FanRpmSource),
                new LenovoFanReading(
                    "x9-ec-auxiliary",
                    _x9AuxFanRpm.Value,
                    "Fan 2",
                    _x9FanRpmSource)
            ];
        }

        if (lhmFans.Count > 0)
            return lhmFans;

        if (_identity.IsVerifiedX9 && ecAvailable && _x9FanRpm.HasValue)
        {
            return
            [
                new LenovoFanReading(
                    "x9-ec-shared",
                    _x9FanRpm.Value,
                    "System fan tachometer",
                    _x9FanRpmSource)
            ];
        }

        if (_genericFans.Count > 0)
            return _genericFans;

        return Array.Empty<LenovoFanReading>();
    }

    private bool EnsureX9Ec(DateTimeOffset now)
    {
        if (!_identity.IsVerifiedX9)
            return false;

        if (_ec is not null)
            return true;

        if (now - _lastEcFailure < EcRetryInterval)
            return false;

        try
        {
            var candidate = new ThinkPadEc();
            byte control = candidate.ReadFanControl();
            if (!(control <= ThinkPadRegisters.MaxManualLevel || ThinkPadFanProtocol.IsFullSpeed(control) || control == ThinkPadRegisters.BiosControl))
            {
                candidate.Dispose();
                _lastEcError = $"EC probe returned unexpected fan state 0x{control:X2}.";
                _lastEcFailure = now;
                return false;
            }

            _ec = candidate;
            _fanControl = control;
            _lastEcError = null;
            return true;
        }
        catch (Exception ex)
        {
            _lastEcFailure = now;
            _lastEcError = $"PawnIO/EC unavailable: {ex.Message}";
            try { _ec?.Dispose(); } catch { }
            _ec = null;
            return false;
        }
    }

    private void MarkEcFailed(Exception ex, DateTimeOffset now)
    {
        _lastEcFailure = now;
        _lastEcError = ex.Message;
        try { _ec?.Dispose(); } catch { }
        _ec = null;
        _x9FanRpm = null;
        _x9AuxFanRpm = null;
        _x9FanRpmSource = "Unavailable";
        _x9EcThermals = Array.Empty<HardwareSensorReading>();
    }

    private bool SafeKeyboardProbe()
    {
        try { return _keyboard.IsAvailable; }
        catch { return false; }
    }

    private string BuildHardwareAccess(bool ecAvailable, bool fanTelemetry, bool keyboardAvailable, bool sensorTelemetry)
    {
        if (!_isLenovo)
            return "Windows features · Lenovo hardware providers not applicable";

        if (_identity.IsVerifiedX9)
        {
            if (ecAvailable && keyboardAvailable)
                return sensorTelemetry
                    ? "Full · verified X9 EC telemetry/control + Lenovo keyboard"
                    : "Full control · verified X9 EC + Lenovo keyboard";
            if (ecAvailable)
                return sensorTelemetry
                    ? "Partial · verified X9 EC telemetry/control · keyboard unavailable"
                    : "Partial · verified X9 EC · keyboard unavailable";
            if (sensorTelemetry || fanTelemetry)
                return $"Telemetry ready · PawnIO/Windows sensors · fan writes unavailable · {_lastEcError ?? "EC probe pending"}";
            if (keyboardAvailable)
                return $"Partial · verified X9 keyboard · {_lastEcError ?? "EC unavailable"}";
            return $"Limited · verified X9 · {_lastEcError ?? "hardware providers unavailable"}";
        }

        if (fanTelemetry && keyboardAvailable)
            return "Experimental · Lenovo keyboard + read-only fan telemetry";
        if (keyboardAvailable)
            return "Experimental · verified-by-readback Lenovo keyboard provider";
        if (fanTelemetry || sensorTelemetry)
            return "Read-only · hardware telemetry · firmware-managed control";

        return "Limited · Lenovo device · Windows features available";
    }

    private static bool IsThinkControlFanState(byte? value) =>
        value is >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel ||
        value.HasValue && ThinkPadFanProtocol.IsFullSpeed(value.Value);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LenovoHardwareController));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            if (IsThinkControlFanState(_fanControl) && _ec is not null)
            {
                try { _ec.ReturnToBios(); } catch { }
            }

            try { _ec?.Dispose(); } catch { }
            _keyboard.Dispose();
            _sensors.Dispose();
            _disposed = true;
        }
    }
}
