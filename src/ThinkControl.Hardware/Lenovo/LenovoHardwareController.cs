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
    LenovoFanControlKind FanControlKind,
    bool CanKeyboardBacklight,
    bool CanCpuTemperature,
    bool CanSensorTelemetry);

public sealed class LenovoHardwareController : IDisposable
{
    private static readonly TimeSpan FirmwareFanRpmPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ManagedFanRpmPollInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PostFanStateChangeReadDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EcThermalPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GenericFanPollInterval = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan EcRetryInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan KeyboardProbeInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StaleFanRpmLimit = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly HardwareDeviceIdentity _identity;
    private readonly SensorHub _sensors = new();
    private readonly KeyboardBacklightService _keyboard = new();
    private readonly LenovoOtherModeFanProvider _otherModeFans = new();
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
    private LenovoFanControlKind _activeFanControlKind = LenovoFanControlKind.None;
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
            TryReturnAllFanProvidersToAutoUnlocked();
            try { _ec?.Dispose(); } catch { }
            try { _keyboard.RefreshBackend(); } catch { }
            _otherModeFans.Refresh();
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
            _activeFanControlKind = LenovoFanControlKind.None;
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
            LenovoOtherModeFanStatus oemFanStatus = _isLenovo
                ? _otherModeFans.ReadStatus()
                : new LenovoOtherModeFanStatus(false, false, [], [], "Not a Lenovo device");
            bool oemFanControl = _identity.IsVerifiedX9 && oemFanStatus.CanControl;
            bool nativeOemFanTelemetry = _identity.IsVerifiedX9 && HasNativeOemFanTelemetry(oemFanStatus);

            // The exact X9 test showed that the classic seven-step EC path does not
            // reproduce Lenovo's smooth/hot Auto range. Once the machine exposes two
            // native Lenovo fan channels (Other Mode or EnergyDrv), treat those as the
            // product boundary: use them for telemetry and do not advertise EC writes
            // merely because PawnIO can still reach 0x2F. The EC may remain open only
            // for the existing read-only thermal fallback while the exact OEM writer is
            // being recovered and validated.
            bool needEcForThermals = !sensorSnapshot.ControlTemperatureC.HasValue;
            bool ecAvailable = !nativeOemFanTelemetry || needEcForThermals
                ? EnsureX9Ec(now)
                : _ec is not null;
            IReadOnlyList<LenovoFanReading> lhmFans = BuildLhmFanTelemetry(sensorSnapshot);

            if (ecAvailable && _ec is not null)
            {
                if (oemFanStatus.Fans.Count == 0 &&
                    (IsThinkControlFanState(_fanControl) || lhmFans.Count == 0))
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

            bool needGenericFanFallback = oemFanStatus.Fans.Count == 0 && lhmFans.Count == 0 &&
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

            IReadOnlyList<LenovoFanReading> fans = BuildFanTelemetry(oemFanStatus, ecAvailable, lhmFans);
            LenovoFanReading? primaryFan = fans.FirstOrDefault();
            LenovoFanControlKind fanControlKind = ResolveFanControlKind(oemFanControl, nativeOemFanTelemetry, ecAvailable);

            string fanState = _activeFanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm
                ? "ThinkControl managed · Lenovo OEM target RPM"
                : nativeOemFanTelemetry
                    ? "Lenovo managed · OEM fan telemetry"
                    : _identity.IsVerifiedX9 && ecAvailable && _fanControl.HasValue
                        ? ThinkPadFanProtocol.DescribeControl(_fanControl.Value)
                        : fans.Count > 0
                            ? "Lenovo managed · read-only telemetry"
                            : "Lenovo managed · telemetry unavailable";

            bool canFanTelemetry = fans.Count > 0;
            bool canSensorTelemetry = mergedSensors.Count > 0;
            string hardwareAccess = BuildHardwareAccess(
                fanControlKind,
                oemFanStatus.Detail,
                nativeOemFanTelemetry,
                ecAvailable,
                canFanTelemetry,
                _keyboardAvailable,
                canSensorTelemetry);

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
                CanFanControl: fanControlKind != LenovoFanControlKind.None,
                FanControlKind: fanControlKind,
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
            LenovoOtherModeFanStatus oem = _otherModeFans.ReadStatus();
            if (oem.CanControl)
            {
                error = "Raw EC steps are disabled while the X9 exposes Lenovo's constrained OEM target-RPM provider. Use the percentage/curve path instead.";
                return false;
            }
            if (HasNativeOemFanTelemetry(oem))
            {
                error = "Raw EC steps are disabled because the X9 exposes native Lenovo fan telemetry but the matching OEM writer is not yet validated. Lenovo Auto keeps fan ownership while ThinkControl recovers the native target-RPM command.";
                return false;
            }

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
                _activeFanControlKind = LenovoFanControlKind.ThinkPadEcDiscrete;
                InvalidateFanRpmAfterStateChange(now);
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

    public bool SetFanPercent(int percent, out string? detail, out string? error)
    {
        detail = null;
        error = null;
        ThrowIfDisposed();

        if (!_identity.IsVerifiedX9)
        {
            error = "OEM target-RPM fan control is not enabled for this device identity.";
            return false;
        }

        lock (_gate)
        {
            LenovoOtherModeFanStatus status = _otherModeFans.ReadStatus();
            if (!status.CanControl)
            {
                error = HasNativeOemFanTelemetry(status)
                    ? "Native Lenovo fan telemetry is active, but the exact X9 OEM target-RPM writer has not passed validation yet. Lenovo Auto keeps ownership."
                    : "Lenovo Other Mode did not expose two constrained writable X9 fan channels.";
                return false;
            }

            // Never let legacy EC manual ownership and the OEM target-RPM contract
            // compete. If a prior diagnostic EC state is still active, hand that path
            // back to firmware before issuing the OEM request.
            if (IsThinkControlFanState(_fanControl) && _ec is not null)
            {
                try
                {
                    _ec.ReturnToBios();
                    _fanControl = ThinkPadRegisters.BiosControl;
                }
                catch (Exception ex)
                {
                    error = $"Could not release legacy EC fan ownership before OEM target-RPM control: {ex.Message}";
                    return false;
                }
            }

            if (!_otherModeFans.SetPercent(percent, out detail, out error))
                return false;

            _activeFanControlKind = LenovoFanControlKind.LenovoOtherModeTargetRpm;
            _x9FanRpm = null;
            _x9AuxFanRpm = null;
            _x9FanRpmSource = "Lenovo OEM target-RPM provider";
            return true;
        }
    }

    public bool ReturnFanToAuto(out string? error)
    {
        error = null;
        ThrowIfDisposed();

        if (!_identity.IsVerifiedX9)
        {
            error = "This device does not use ThinkControl's verified X9 fan provider; its firmware remains in control.";
            return false;
        }

        lock (_gate)
        {
            bool success = true;
            var failures = new List<string>(2);

            if (_activeFanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)
            {
                if (!_otherModeFans.ReturnToAuto(out string? oemError))
                {
                    success = false;
                    failures.Add(oemError ?? "Lenovo OEM fan Auto handoff failed");
                }
            }

            if (IsThinkControlFanState(_fanControl) && _ec is not null)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                try
                {
                    _ec.ReturnToBios();
                    _fanControl = ThinkPadRegisters.BiosControl;
                    InvalidateFanRpmAfterStateChange(now);
                }
                catch (Exception ex)
                {
                    success = false;
                    failures.Add($"Legacy EC Auto handoff failed: {ex.Message}");
                }
            }

            if (success)
                _activeFanControlKind = LenovoFanControlKind.None;
            error = failures.Count == 0 ? null : string.Join(" · ", failures);
            return success;
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

    private LenovoFanControlKind ResolveFanControlKind(bool oemFanControl, bool nativeOemFanTelemetry, bool ecAvailable)
    {
        if (_identity.IsVerifiedX9 && oemFanControl)
            return LenovoFanControlKind.LenovoOtherModeTargetRpm;
        if (_identity.IsVerifiedX9 && nativeOemFanTelemetry)
            return LenovoFanControlKind.None;
        if (_identity.IsVerifiedX9 && ecAvailable)
            return LenovoFanControlKind.ThinkPadEcDiscrete;
        return LenovoFanControlKind.None;
    }

    private static bool HasNativeOemFanTelemetry(LenovoOtherModeFanStatus status) =>
        status.Fans.Count >= 2 &&
        status.Fans.All(fan => fan.Source.StartsWith("Lenovo ", StringComparison.OrdinalIgnoreCase));

    private void ReadX9FanRpm(DateTimeOffset now)
    {
        TimeSpan pollInterval = FanRpmPollIntervalForCurrentState();
        if (_ec is null || now - _lastFanRpmRead < pollInterval)
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

    private TimeSpan FanRpmPollIntervalForCurrentState() =>
        IsThinkControlFanState(_fanControl) ? ManagedFanRpmPollInterval : FirmwareFanRpmPollInterval;

    private void InvalidateFanRpmAfterStateChange(DateTimeOffset now)
    {
        _x9FanRpm = null;
        _x9AuxFanRpm = null;
        _x9FanRpmSource = "Settling after fan-state change";
        _x9FanRpmFailures = 0;
        _lastFanRpmSuccess = DateTimeOffset.MinValue;
        _lastFanRpmRead = now - FanRpmPollIntervalForCurrentState() + PostFanStateChangeReadDelay;
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
        LenovoOtherModeFanStatus oemFanStatus,
        bool ecAvailable,
        IReadOnlyList<LenovoFanReading> lhmFans)
    {
        if (oemFanStatus.Fans.Count > 0)
            return oemFanStatus.Fans;

        bool managedX9 = _identity.IsVerifiedX9 && ecAvailable && IsThinkControlFanState(_fanControl);
        if (managedX9)
        {
            if (_x9FanRpm.HasValue && _x9AuxFanRpm.HasValue)
            {
                return
                [
                    new LenovoFanReading("x9-ec-main", _x9FanRpm.Value, "Fan 1", _x9FanRpmSource),
                    new LenovoFanReading("x9-ec-auxiliary", _x9AuxFanRpm.Value, "Fan 2", _x9FanRpmSource)
                ];
            }

            return lhmFans.Count >= 2 ? lhmFans : Array.Empty<LenovoFanReading>();
        }

        if (lhmFans.Count > 0)
            return lhmFans;

        if (_identity.IsVerifiedX9 && ecAvailable && _x9FanRpm.HasValue)
        {
            return
            [
                new LenovoFanReading("x9-ec-shared", _x9FanRpm.Value, "System fan tachometer", _x9FanRpmSource)
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

    private string BuildHardwareAccess(
        LenovoFanControlKind fanControlKind,
        string oemDetail,
        bool nativeOemFanTelemetry,
        bool ecAvailable,
        bool fanTelemetry,
        bool keyboardAvailable,
        bool sensorTelemetry)
    {
        if (!_isLenovo)
            return "Windows features · Lenovo hardware providers not applicable";

        if (_identity.IsVerifiedX9)
        {
            if (fanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)
                return keyboardAvailable
                    ? $"Full · X9 Lenovo OEM target-RPM fan control + keyboard · {oemDetail}"
                    : $"Full fan control · X9 Lenovo OEM target-RPM provider · {oemDetail}";
            if (nativeOemFanTelemetry)
                return keyboardAvailable
                    ? $"Read-only · X9 Lenovo OEM fan telemetry + keyboard · native fan writer pending validation · {oemDetail}"
                    : $"Read-only · X9 Lenovo OEM fan telemetry · native fan writer pending validation · {oemDetail}";
            if (fanControlKind == LenovoFanControlKind.ThinkPadEcDiscrete && keyboardAvailable)
                return sensorTelemetry
                    ? "Fallback · verified X9 discrete EC telemetry/control + Lenovo keyboard"
                    : "Fallback control · verified X9 discrete EC + Lenovo keyboard";
            if (fanControlKind == LenovoFanControlKind.ThinkPadEcDiscrete)
                return sensorTelemetry
                    ? "Fallback · verified X9 discrete EC telemetry/control · keyboard unavailable"
                    : "Fallback · verified X9 discrete EC · keyboard unavailable";
            if (sensorTelemetry || fanTelemetry)
                return $"Telemetry ready · fan writes unavailable · {oemDetail} · {_lastEcError ?? "EC fallback pending"}";
            if (keyboardAvailable)
                return $"Partial · verified X9 keyboard · {_lastEcError ?? "fan providers unavailable"}";
            return $"Limited · verified X9 · {oemDetail} · {_lastEcError ?? "hardware providers unavailable"}";
        }

        if (fanTelemetry && keyboardAvailable)
            return "Experimental · Lenovo keyboard + read-only fan telemetry";
        if (keyboardAvailable)
            return "Experimental · verified-by-readback Lenovo keyboard provider";
        if (fanTelemetry || sensorTelemetry)
            return "Read-only · hardware telemetry · firmware-managed control";

        return "Limited · Lenovo device · Windows features available";
    }

    private void TryReturnAllFanProvidersToAutoUnlocked()
    {
        if (_activeFanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)
        {
            try { _otherModeFans.ReturnToAuto(out _); } catch { }
        }
        if (IsThinkControlFanState(_fanControl) && _ec is not null)
        {
            try { _ec.ReturnToBios(); } catch { }
        }
        _activeFanControlKind = LenovoFanControlKind.None;
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

            TryReturnAllFanProvidersToAutoUnlocked();
            try { _ec?.Dispose(); } catch { }
            _keyboard.Dispose();
            _sensors.Dispose();
            _disposed = true;
        }
    }
}