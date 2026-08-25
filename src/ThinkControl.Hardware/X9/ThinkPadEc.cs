namespace ThinkControl.Hardware.X9;

internal sealed class ThinkPadEc : IDisposable
{
    private const ushort EcType1CommandPort = 0x1604;
    private const ushort EcType1DataPort = 0x1600;
    private const ushort EcType2CommandPort = 0x66;
    private const ushort EcType2DataPort = 0x62;

    private const byte EcReadCommand = 0x80;
    private const byte EcWriteCommand = 0x81;
    private const byte OutputBufferFull = 0x01;
    private const byte InputBufferFull = 0x02;
    private const byte EcThermalBank0Start = 0x78;
    private const byte EcThermalBank1Start = 0xC0;

    private const int ReadMaxRetries = 5;
    private const int ReadObfProbeMs = 5;
    private const int ReadFallbackTimeoutMs = 50;
    private const int TransactionTimeoutMs = 1000;
    private const int OptionalThermalTimeoutMs = 120;
    private const int PollSleepMs = 10;
    private const int MutexTimeoutMs = 1500;
    private static readonly TimeSpan ThermalFailureBackoff = TimeSpan.FromSeconds(30);

    private readonly PawnIoEcTransport _ports;
    private readonly Mutex _thinkPadMutex;
    private readonly Mutex _globalEcMutex;
    private ushort _commandPort;
    private ushort _dataPort;
    private DateTimeOffset _thermalBackoffUntil = DateTimeOffset.MinValue;
    private bool _manualControlEngaged;
    private byte? _lastManualControl;
    private bool _disposed;

    internal ThinkPadEc()
    {
        var ports = new PawnIoEcTransport();
        Mutex? thinkPadMutex = null;
        Mutex? globalEcMutex = null;
        try
        {
            thinkPadMutex = CreateOrOpenMutex(@"Global\Access_Thinkpad_EC");
            globalEcMutex = CreateOrOpenMutex(@"Global\Access_EC");
            _ports = ports;
            _thinkPadMutex = thinkPadMutex;
            _globalEcMutex = globalEcMutex;

            WithEcLock(() =>
            {
                DetectPortPair();
                return 0;
            });
        }
        catch
        {
            ports.Dispose();
            globalEcMutex?.Dispose();
            thinkPadMutex?.Dispose();
            throw;
        }
    }

    internal string PortLabel => $"0x{_commandPort:X}/0x{_dataPort:X}";

    internal byte ReadFanControl() => WithEcLock(() => ReadByteUnlocked(ThinkPadRegisters.FanControl));

    internal int ReadFanRpm()
    {
        return WithEcLock(() =>
        {
            byte low = ReadByteUnlocked(ThinkPadRegisters.FanSpeedLow);
            Thread.Sleep(5);
            byte high = ReadByteUnlocked(ThinkPadRegisters.FanSpeedHigh);
            int rpm = ThinkPadFanProtocol.CombineRpm(low, high);
            if (!ThinkPadFanProtocol.IsPlausibleRpm(rpm))
                throw new InvalidOperationException($"Implausible fan RPM value {rpm}.");
            return rpm;
        });
    }

    internal IReadOnlyList<(byte Register, byte Celsius)> ReadThermalSensors()
    {
        if (DateTimeOffset.UtcNow < _thermalBackoffUntil)
            return Array.Empty<(byte Register, byte Celsius)>();

        return WithEcLock<IReadOnlyList<(byte Register, byte Celsius)>>(() =>
        {
            var readings = new List<(byte Register, byte Celsius)>(12);
            bool complete = ReadThermalBankUnlocked(EcThermalBank0Start, 8, readings) &&
                            ReadThermalBankUnlocked(EcThermalBank1Start, 4, readings);
            if (!complete)
            {
                readings.Clear();
                _thermalBackoffUntil = DateTimeOffset.UtcNow + ThermalFailureBackoff;
            }
            else
            {
                _thermalBackoffUntil = DateTimeOffset.MinValue;
            }
            return readings;
        });
    }

    internal void SetManualLevel(byte level)
    {
        if (level < ThinkPadRegisters.MinManualLevel || level > ThinkPadRegisters.MaxManualLevel)
            throw new ArgumentOutOfRangeException(nameof(level), "Manual fan level must be between 1 and 7.");

        SetFanControlVerified(
            level,
            readBack => readBack == level,
            $"manual fan level {level}");
    }

    internal void SetFullSpeed()
    {
        // The upstream Linux thinkpad_acpi driver defines bit 0x40 as EC full-speed
        // and writes it together with level 7 (0x47) as a safe fallback. We use the
        // same state only on the already model-gated X9 provider and require the EC
        // to read back the full-speed bit. If the firmware ignores that bit, 100%
        // remains unavailable instead of being falsely reported as level 7.
        SetFanControlVerified(
            ThinkPadRegisters.FullSpeedControl,
            ThinkPadFanProtocol.IsFullSpeed,
            "full-speed override");
    }

    private void SetFanControlVerified(byte requested, Func<byte, bool> acceptsReadBack, string label)
    {
        if (_manualControlEngaged && _lastManualControl == requested)
            return;

        _manualControlEngaged = true;
        try
        {
            WithEcLock(() =>
            {
                WriteByteUnlocked(ThinkPadRegisters.FanControl, requested);
                return 0;
            });

            Thread.Sleep(45);
            byte readBack = ReadFanControl();
            if (!acceptsReadBack(readBack))
                throw new InvalidOperationException(
                    $"Fan write verification failed for {label}. Requested 0x{requested:X2}, EC returned 0x{readBack:X2}.");

            _lastManualControl = readBack;
        }
        catch
        {
            TryReturnToBiosAfterFailedManualWrite();
            throw;
        }
    }

    internal void ReturnToBios()
    {
        WithEcLock(() =>
        {
            WriteByteUnlocked(ThinkPadRegisters.FanControl, ThinkPadRegisters.BiosControl);
            return 0;
        });

        Thread.Sleep(45);
        byte readBack = ReadFanControl();
        if (readBack != ThinkPadRegisters.BiosControl)
            throw new InvalidOperationException($"BIOS fan-control verification failed. EC returned 0x{readBack:X2}.");

        _manualControlEngaged = false;
        _lastManualControl = null;
    }

    private void DetectPortPair()
    {
        if (TryValidatePortPair(EcType1CommandPort, EcType1DataPort, out _))
            return;
        if (TryValidatePortPair(EcType2CommandPort, EcType2DataPort, out string legacyDetail))
            return;

        throw new InvalidOperationException(
            "PawnIO and its LPC module are ready, but neither supported ThinkPad EC port pair passed a read-only validation. " +
            $"Modern 0x1604/0x1600 and legacy 0x66/0x62 were both rejected. {legacyDetail}");
    }

    private bool TryValidatePortPair(ushort commandPort, ushort dataPort, out string detail)
    {
        _commandPort = commandPort;
        _dataPort = dataPort;
        detail = string.Empty;

        try
        {
            if (!TryReadByte(ThinkPadRegisters.FanControl, out byte control))
            {
                detail = $"Fan-control read timed out on 0x{commandPort:X}/0x{dataPort:X}.";
                return false;
            }

            if (control == ThinkPadRegisters.BiosControl ||
                control is >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel ||
                ThinkPadFanProtocol.IsFullSpeed(control))
            {
                return true;
            }

            if (control != 0x00)
            {
                detail = $"Read-only validation returned unknown fan state 0x{control:X2} on 0x{commandPort:X}/0x{dataPort:X}.";
                return false;
            }

            if (!TryReadByte(EcThermalBank0Start, out byte thermal))
            {
                detail = $"Ambiguous fan-off state 0x00 was read, but the thermal sanity probe timed out on 0x{commandPort:X}/0x{dataPort:X}.";
                return false;
            }

            if (thermal is < 5 or > 125)
            {
                detail = $"Ambiguous fan-off state 0x00 and implausible thermal value {thermal} were read on 0x{commandPort:X}/0x{dataPort:X}.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            detail = $"EC validation on 0x{commandPort:X}/0x{dataPort:X} failed: {ex.Message}";
            return false;
        }
    }

    private bool ReadThermalBankUnlocked(byte start, int count, ICollection<(byte Register, byte Celsius)> output)
    {
        for (int i = 0; i < count; i++)
        {
            byte register = unchecked((byte)(start + i));
            if (!TryReadByte(register, out byte value, OptionalThermalTimeoutMs))
                return false;
            if (value is < 5 or > 125)
                continue;
            output.Add((register, value));
        }
        return true;
    }

    private void TryReturnToBiosAfterFailedManualWrite()
    {
        try
        {
            WithEcLock(() =>
            {
                WriteByteUnlocked(ThinkPadRegisters.FanControl, ThinkPadRegisters.BiosControl);
                return 0;
            });
            _manualControlEngaged = false;
            _lastManualControl = null;
        }
        catch
        {
        }
    }

    private byte ReadByteUnlocked(byte register)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= ReadMaxRetries; attempt++)
        {
            try
            {
                if (TryReadByte(register, out byte value))
                    return value;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            Thread.Sleep(1);
        }

        throw new TimeoutException($"EC read 0x{register:X2} failed after {ReadMaxRetries} attempts.", lastError);
    }

    private bool TryReadByte(byte register, out byte value, int timeoutMs = TransactionTimeoutMs)
    {
        value = 0;
        if (!PrepareForTransaction(timeoutMs))
            return false;

        WritePort(_commandPort, EcReadCommand);
        if (!WaitForFlagsClear(InputBufferFull, timeoutMs))
            return false;

        WritePort(_dataPort, register);
        if (!WaitForFlagsClear(InputBufferFull, timeoutMs))
            return false;

        if (!WaitForReadReady(timeoutMs))
            return false;

        value = ReadPort(_dataPort);
        return true;
    }

    private void WriteByteUnlocked(byte register, byte value)
    {
        if (!PrepareForTransaction(TransactionTimeoutMs))
            throw new TimeoutException("EC buffers stayed busy before write command.");

        WritePort(_commandPort, EcWriteCommand);
        if (!WaitForFlagsClear(InputBufferFull, TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept write command for 0x{register:X2}.");

        WritePort(_dataPort, register);
        if (!WaitForFlagsClear(InputBufferFull, TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept register 0x{register:X2}.");

        WritePort(_dataPort, value);
        if (!WaitForFlagsClear(InputBufferFull, TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept value 0x{value:X2} for register 0x{register:X2}.");
    }

    private bool PrepareForTransaction(int timeoutMs)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += PollSleepMs)
        {
            byte status = ReadPort(_commandPort);
            if ((status & InputBufferFull) != 0)
            {
                Thread.Sleep(PollSleepMs);
                continue;
            }

            if ((status & OutputBufferFull) != 0)
            {
                _ = ReadPort(_dataPort);
                Thread.Sleep(1);
                continue;
            }

            return true;
        }
        return false;
    }

    private bool WaitForFlagsClear(byte flags, int timeoutMs)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += PollSleepMs)
        {
            if ((ReadPort(_commandPort) & flags) == 0)
                return true;
            Thread.Sleep(PollSleepMs);
        }
        return false;
    }

    private bool WaitForReadReady(int timeoutMs)
    {
        int obfBudget = Math.Min(timeoutMs, ReadObfProbeMs);
        for (int elapsed = 0; elapsed < obfBudget; elapsed++)
        {
            if ((ReadPort(_commandPort) & OutputBufferFull) != 0)
                return true;
            Thread.Sleep(1);
        }

        int fallbackBudget = Math.Min(Math.Max(timeoutMs - obfBudget, 1), ReadFallbackTimeoutMs);
        for (int elapsed = 0; elapsed < fallbackBudget; elapsed++)
        {
            if ((ReadPort(_commandPort) & InputBufferFull) == 0)
            {
                Thread.Sleep(1);
                return true;
            }
            Thread.Sleep(1);
        }

        return false;
    }

    private byte ReadPort(ushort port) => _ports.ReadPort(port);
    private void WritePort(ushort port, byte value) => _ports.WritePort(port, value);

    private T WithEcLock<T>(Func<T> action)
    {
        ThrowIfDisposed();
        bool thinkPadLocked = false;
        bool globalLocked = false;
        try
        {
            thinkPadLocked = Wait(_thinkPadMutex);
            if (!thinkPadLocked)
                throw new TimeoutException("Could not acquire ThinkPad EC mutex.");

            globalLocked = Wait(_globalEcMutex);
            if (!globalLocked)
                throw new TimeoutException("Could not acquire shared EC mutex.");

            return action();
        }
        finally
        {
            if (globalLocked) _globalEcMutex.ReleaseMutex();
            if (thinkPadLocked) _thinkPadMutex.ReleaseMutex();
        }
    }

    private static Mutex CreateOrOpenMutex(string name)
    {
        try { return Mutex.OpenExisting(name); }
        catch (WaitHandleCannotBeOpenedException) { return new Mutex(false, name); }
        catch (UnauthorizedAccessException) { return new Mutex(false, name); }
    }

    private static bool Wait(Mutex mutex)
    {
        try { return mutex.WaitOne(MutexTimeoutMs, false); }
        catch (AbandonedMutexException) { return true; }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ThinkPadEc));
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (_manualControlEngaged)
            {
                try { ReturnToBios(); } catch { }
            }
        }
        finally
        {
            _disposed = true;
            _ports.Dispose();
            _globalEcMutex.Dispose();
            _thinkPadMutex.Dispose();
        }
    }
}
