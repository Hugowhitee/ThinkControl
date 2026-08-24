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
    private const int TransactionTimeoutMs = 1000;
    private const int PollSleepMs = 10;
    private const int MutexTimeoutMs = 1500;

    private readonly PawnIoEcTransport _ports;
    private readonly Mutex _thinkPadMutex = CreateOrOpenMutex(@"Global\Access_Thinkpad_EC");
    private readonly Mutex _globalEcMutex = CreateOrOpenMutex(@"Global\Access_EC");
    private ushort _commandPort;
    private ushort _dataPort;
    private bool _manualControlEngaged;
    private byte? _lastManualLevel;
    private bool _disposed;

    internal ThinkPadEc()
    {
        // Do not infer PawnIO readiness from its Windows service entry alone.
        // The transport proves device/module access first; then a read-only EC
        // capability probe selects the modern ThinkPad Type 1 ports before the
        // older ACPI Type 2 fallback. Detection uses the same global ThinkPad EC
        // mutex as established TPFanCtrl implementations so another utility cannot
        // interleave an EC transaction while ThinkControl is probing.
        _ports = new PawnIoEcTransport();
        try
        {
            WithEcLock(() =>
            {
                DetectPortPair();
                return 0;
            });
        }
        catch
        {
            _ports.Dispose();
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

    /// <summary>
    /// Reads the classic ThinkPad EC thermal banks without assigning semantic CPU/
    /// GPU labels. These values are useful as a conservative X9 fallback when a
    /// richer sensor provider is temporarily unavailable. Callers may display them
    /// as generic EC thermal sensors or use the hottest valid value for safety, but
    /// must not relabel an unmapped register as CPU Package.
    /// </summary>
    internal IReadOnlyList<(byte Register, byte Celsius)> ReadThermalSensors()
    {
        return WithEcLock<IReadOnlyList<(byte Register, byte Celsius)>>(() =>
        {
            var readings = new List<(byte Register, byte Celsius)>(12);
            ReadThermalBankUnlocked(EcThermalBank0Start, 8, readings);
            ReadThermalBankUnlocked(EcThermalBank1Start, 4, readings);
            return readings;
        });
    }

    internal void SetManualLevel(byte level)
    {
        if (level < ThinkPadRegisters.MinManualLevel || level > ThinkPadRegisters.MaxManualLevel)
            throw new ArgumentOutOfRangeException(nameof(level), "Manual fan level must be between 1 and 7.");

        if (_manualControlEngaged && _lastManualLevel == level)
            return;

        _manualControlEngaged = true;
        try
        {
            WithEcLock(() =>
            {
                WriteByteUnlocked(ThinkPadRegisters.FanControl, level);
                return 0;
            });

            Thread.Sleep(45);
            byte readBack = ReadFanControl();
            if (readBack != level)
                throw new InvalidOperationException($"Fan write verification failed. Requested {level}, EC returned 0x{readBack:X2}.");

            _lastManualLevel = level;
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
        _lastManualLevel = null;
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

            // Auto, manual 1-7 and the known 0x40-0x47 override states are strong
            // non-zero evidence that this is the real ThinkPad EC port pair. 0x00 is
            // a legitimate fan-off state but also the most likely false value from a
            // wrong port, so only that ambiguous state requires a second read-only
            // thermal sanity check. This avoids making generic register 0x78 a hard
            // X9 compatibility requirement while still rejecting all-zero probes.
            if (control == ThinkPadRegisters.BiosControl ||
                control is >= ThinkPadRegisters.MinManualLevel and <= ThinkPadRegisters.MaxManualLevel ||
                control is >= 0x40 and <= 0x47)
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

    private void ReadThermalBankUnlocked(byte start, int count, ICollection<(byte Register, byte Celsius)> output)
    {
        for (int i = 0; i < count; i++)
        {
            byte register = unchecked((byte)(start + i));
            if (!TryReadByte(register, out byte value))
                continue;
            if (value is < 5 or > 125)
                continue;
            output.Add((register, value));
        }
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
            _lastManualLevel = null;
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

    private bool TryReadByte(byte register, out byte value)
    {
        value = 0;

        // Match the long-established ThinkPad ACPI EC transaction sequence used by
        // TPFanCtrl: begin only when both buffers are clear, then wait for IBF after
        // each byte written to the EC input queue. Many newer ThinkPad BIOS builds
        // do not assert OBF consistently for ordinary register reads, so requiring
        // OBF here can turn a valid read into a false timeout.
        if (!WaitForFlagsClear(InputBufferFull | OutputBufferFull, TransactionTimeoutMs))
            return false;

        WritePort(_commandPort, EcReadCommand);
        if (!WaitForFlagsClear(InputBufferFull, TransactionTimeoutMs))
            return false;

        WritePort(_dataPort, register);
        if (!WaitForFlagsClear(InputBufferFull, TransactionTimeoutMs))
            return false;

        value = ReadPort(_dataPort);
        return true;
    }

    private void WriteByteUnlocked(byte register, byte value)
    {
        // Use the same proven EC queue discipline as the read path. Waiting only
        // for IBF after command/address/data avoids rejecting valid modern ECs that
        // leave unrelated OBF state observable while accepting a write.
        if (!WaitForFlagsClear(InputBufferFull | OutputBufferFull, TransactionTimeoutMs))
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
