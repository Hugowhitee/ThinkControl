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
    private static readonly TimeSpan FanStatePollInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan FanRpmPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan GenericFanPollInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan EcRetryInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan KeyboardProbeInterval = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly HardwareDeviceIdentity _identity;
    private readonly SensorHub _sensors = new();
    private readonly KeyboardBacklightService _keyboard = new();
    private readonly bool _isLenovo;

    private ThinkPadEc? _ec;
    private DateTimeOffset _lastEcFailure = DateTimeOffset.MinValue;
    private string? _lastEcError;
    private DateTimeOffset _lastFanStateRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFanRpmRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGenericFanRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastKeyboardProbe = DateTimeOffset.MinValue;
    private byte? _fanControl;
    private int? _x9FanRpm;
    private string _x9FanRpmSource = "Unavailable";
    private IReadOnlyList<LenovoFanReading> _genericFans = Array.Empty<LenovoFanReading>();
    private bool _keyboardAvailable;
    private bool _disposed;

    public LenovoHardwareController()
    {
        _identity = DeviceIdentity.Read();
        _isLenovo = _identity.Manufacturer.Contains("LENOVO", StringComparison.OrdinalIgnoreCase);
    }

    public HardwareDeviceIdentity Identity => _identity;

    public LenovoHardwareStatus ReadStatus()
    {
        ThrowIfDisposed();

        // Sensor discovery is intentionally outside the EC gate. FanControl and
        // LibreHardwareMonitor can expose useful CPU/fan telemetry through PawnIO
        // even if ThinkControl's stricter verified EC write probe is unavailable.
        SensorHubSnapshot sensorSnapshot = _sensors.Read();

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool ecAvailable = EnsureX9Ec(now);

            if (ecAvailable && _ec is not null)
            {
                ReadX9FanState(now, ref ecAvailable);
                if (ecAvailable)
                    ReadX9FanRpm(now);
            }

            if (_isLenovo && now - _lastGenericFanRead >= GenericFanPollInterval)
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

            IReadOnlyList<LenovoFanReading> fans = BuildFanTelemetry(ecAvailable, sensorSnapshot);
            LenovoFanReading? primaryFan = fans.FirstOrDefault();

            string fanState = _identity.IsVerifiedX9 && ecAvailable && _fanControl.HasValue
                ? ThinkPadFanProtocol.DescribeControl(_fanControl.Value)
                : fans.Count > 0
                    ? "Lenovo managed · read-only telemetry"
                    : "Lenovo managed · telemetry unavailable";

            bool canFanTelemetry = fans.Count > 0;
            string hardwareAccess = BuildHardwareAccess(ecAvailable, canFanTelemetry, _keyboardAvailable, sensorSnapshot.Sensors.Count > 0);

            return new LenovoHardwareStatus(
                _identity,
                sensorSnapshot.CpuTemperatureC,
                sensorSnapshot.CpuTemperatureSource,
                sensorSnapshot.ControlTemperatureC,
                sensorSnapshot.ControlTemperatureSource,
                sensorSnapshot.Sensors,
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
                CanSensorTelemetry: sensorSnapshot.Sensors.Count > 0);
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
            error = "Fan level must be between 1 and 7. Level 0 and 0x40 override states are intentionally blocked.";
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
                _lastFanStateRead = now;
                _lastFanRpmRead = now - FanRpmPollInterval + TimeSpan.FromSeconds(4);
                return true;
            }
            catch (Exception ex)
            {
                MarkEcFailed(ex, now);
                error = $"Fan level could not be verified: {ex.Message}";
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
                _lastFanStateRead = now;
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

        if (!Enum.TryParse(value, true, out KeyboardBacklightLevel level) ||
            level == KeyboardBacklightLevel.FirmwareAuto)
        {
            error = "The privileged service accepts only Off, Low and High. Auto/effects are user-session ThinkControl policies.";
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
                error = $"Lenovo keyboard backlight did not verify {level}.";
                return false;
            }

            _keyboardAvailable = true;
            _lastKeyboardProbe = DateTimeOffset.UtcNow;
            return true;
        }
    }

    private void ReadX9FanState(DateTimeOffset now, ref bool ecAvailable)
    {
        if (_ec is null || (_fanControl.HasValue && now - _lastFanStateRead < FanStatePollInterval))
            return;

        try
        {
            _fanControl = _ec.ReadFanControl();
            _lastFanStateRead = now;
        }
        catch (Exception ex)
        {
            MarkEcFailed(ex, now);
            ecAvailable = false;
        }
    }

    private void ReadX9FanRpm(DateTimeOffset now)
    {
        if (_ec is null || (_x9FanRpm.HasValue && now - _lastFanRpmRead < FanRpmPollInterval))
            return;

        try
        {
            _x9FanRpm = _ec.ReadFanRpm();
            _x9FanRpmSource = "ThinkPad X9 EC tachometer 0x84/0x85";
            _lastFanRpmRead = now;
        }
        catch
        {
        }
    }

    private void ReadGenericLenovoFanTelemetry(DateTimeOffset now)
    {
        _lastGenericFanRead = now;
        _genericFans = LenovoFanTelemetryService.Read();
    }

    private IReadOnlyList<LenovoFanReading> BuildFanTelemetry(bool ecAvailable, SensorHubSnapshot sensors)
    {
        if (_genericFans.Count > 0)
            return _genericFans;

        LenovoFanReading[] lhmFans = sensors.Sensors
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
        if (lhmFans.Length > 0)
            return lhmFans;

        if (_identity.IsVerifiedX9 && ecAvailable && _x9FanRpm.HasValue)
        {
            return
            [
                new LenovoFanReading(
                    "x9-ec-main",
                    _x9FanRpm.Value,
                    "Fan",
                    _x9FanRpmSource)
            ];
        }

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
            if (!(control <= 0x07 || control is >= 0x40 and <= 0x47 || control == ThinkPadRegisters.BiosControl))
            {
                candidate.Dispose();
                _lastEcError = $"EC probe returned unexpected fan state 0x{control:X2}.";
                _lastEcFailure = now;
                return false;
            }

            _ec = candidate;
            _fanControl = control;
            _lastFanStateRead = now;
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
                    ? "Full · verified X9 EC + PawnIO sensors + Lenovo keyboard"
                    : "Full control · verified X9 EC + Lenovo keyboard";
            if (ecAvailable)
                return sensorTelemetry
                    ? "Partial · verified X9 EC + PawnIO sensors · keyboard unavailable"
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

            if (_fanControl is >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel && _ec is not null)
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
