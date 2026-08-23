namespace ThinkControl.Hardware.X9;

public sealed record X9HardwareStatus(
    HardwareDeviceIdentity Identity,
    double? CpuTemperatureC,
    string CpuTemperatureSource,
    int? FanRpm,
    string FanRpmSource,
    string FanState,
    string KeyboardBacklight,
    string KeyboardBackend,
    string HardwareAccess,
    bool CanFanTelemetry,
    bool CanFanControl,
    bool CanKeyboardBacklight,
    bool CanCpuTemperature);

public sealed class X9HardwareController : IDisposable
{
    private static readonly TimeSpan FanStatePollInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan FanRpmPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EcRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KeyboardProbeInterval = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly HardwareDeviceIdentity _identity;
    private readonly CpuTemperatureReader _cpu = new();
    private readonly KeyboardBacklightService _keyboard = new();

    private ThinkPadEc? _ec;
    private DateTimeOffset _lastEcFailure = DateTimeOffset.MinValue;
    private string? _lastEcError;
    private DateTimeOffset _lastFanStateRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFanRpmRead = DateTimeOffset.MinValue;
    private DateTimeOffset _lastKeyboardProbe = DateTimeOffset.MinValue;
    private byte? _fanControl;
    private int? _fanRpm;
    private bool _keyboardAvailable;
    private bool _disposed;

    public X9HardwareController()
    {
        _identity = DeviceIdentity.Read();
    }

    public HardwareDeviceIdentity Identity => _identity;

    public X9HardwareStatus ReadStatus()
    {
        ThrowIfDisposed();
        (double? temperature, string temperatureSource) = _cpu.Read();

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool ecAvailable = EnsureEc(now);

            if (ecAvailable && _ec is not null)
            {
                if (!_fanControl.HasValue || now - _lastFanStateRead >= FanStatePollInterval)
                {
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

                // Tachometer reads are deliberately sparse. The X9 has previously
                // shown audible cadence when its EC is poked too frequently.
                if (ecAvailable && (!_fanRpm.HasValue || now - _lastFanRpmRead >= FanRpmPollInterval))
                {
                    try
                    {
                        _fanRpm = _ec.ReadFanRpm();
                        _lastFanRpmRead = now;
                    }
                    catch
                    {
                        // Keep the last good RPM value. Fan state/control can still
                        // be healthy when one tachometer transaction is missed.
                    }
                }
            }

            if (now - _lastKeyboardProbe >= KeyboardProbeInterval)
            {
                _keyboardAvailable = _identity.IsVerifiedX9 && SafeKeyboardProbe();
                _lastKeyboardProbe = now;
            }

            string keyboardState = "Unavailable";
            if (_keyboardAvailable && _keyboard.TryGet(out KeyboardBacklightLevel level))
                keyboardState = level == KeyboardBacklightLevel.FirmwareAuto ? "Lenovo Auto" : level.ToString();

            string fanState = _fanControl.HasValue
                ? ThinkPadFanProtocol.DescribeControl(_fanControl.Value)
                : "Lenovo managed · state unavailable";

            string hardwareAccess = BuildHardwareAccess(ecAvailable, _keyboardAvailable);
            return new X9HardwareStatus(
                _identity,
                temperature,
                temperatureSource,
                _fanRpm,
                _fanRpm.HasValue ? "ThinkPad EC tachometer 0x84/0x85" : "Unavailable",
                fanState,
                keyboardState,
                _keyboard.BackendLabel,
                hardwareAccess,
                CanFanTelemetry: _identity.IsVerifiedX9 && ecAvailable,
                CanFanControl: _identity.IsVerifiedX9 && ecAvailable,
                CanKeyboardBacklight: _identity.IsVerifiedX9 && _keyboardAvailable,
                CanCpuTemperature: temperature.HasValue);
        }
    }

    public bool SetFanLevel(int level, out string? error)
    {
        error = null;
        ThrowIfDisposed();

        if (!_identity.IsVerifiedX9)
        {
            error = $"Fan writes are blocked for unverified profile {_identity.MachineType}.";
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
            if (!EnsureEc(now) || _ec is null)
            {
                error = _lastEcError ?? "PawnIO/EC access is unavailable.";
                return false;
            }

            try
            {
                _ec.SetManualLevel((byte)level);
                _fanControl = (byte)level;
                _lastFanStateRead = now;

                // Keep the previous RPM while the physical fan settles. The next
                // status poll may refresh it after four seconds rather than forcing
                // a tach read directly after every command.
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
            error = $"Fan writes are blocked for unverified profile {_identity.MachineType}.";
            return false;
        }

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!EnsureEc(now) || _ec is null)
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

        if (!_identity.IsVerifiedX9)
        {
            error = $"Keyboard writes are blocked for unverified profile {_identity.MachineType}.";
            return false;
        }

        if (!Enum.TryParse(value, true, out KeyboardBacklightLevel level) || level == KeyboardBacklightLevel.FirmwareAuto)
        {
            error = "The privileged service accepts only verified hardware levels Off, Low and High. Auto/effects are user-session ThinkControl policies.";
            return false;
        }

        lock (_gate)
        {
            if (!SafeKeyboardProbe())
            {
                _keyboardAvailable = false;
                error = "Lenovo keyboard backlight driver is not available.";
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

    private bool EnsureEc(DateTimeOffset now)
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

    private string BuildHardwareAccess(bool ecAvailable, bool keyboardAvailable)
    {
        if (!_identity.IsVerifiedX9)
            return $"Read-only · unverified ThinkPad profile {_identity.MachineType}";

        if (ecAvailable && keyboardAvailable)
            return "Full · verified X9 profile · EC + Lenovo keyboard";

        if (ecAvailable)
            return "Partial · verified X9 · EC ready · keyboard backend unavailable";

        if (keyboardAvailable)
            return $"Partial · verified X9 · keyboard ready · {_lastEcError ?? "EC unavailable"}";

        return $"Limited · verified X9 · {_lastEcError ?? "hardware backends unavailable"}";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(X9HardwareController));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            // Safety invariant: manual fan ownership never survives service exit.
            if (_fanControl is >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel && _ec is not null)
            {
                try { _ec.ReturnToBios(); } catch { }
            }

            try { _ec?.Dispose(); } catch { }
            _keyboard.Dispose();
            _cpu.Dispose();
            _disposed = true;
        }
    }
}
