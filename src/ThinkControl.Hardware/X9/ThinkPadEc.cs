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
    private const byte EcThermalProbe = 0x78;

    private const int ReadMaxRetries = 5;
    private const int ReadWaitSpins = 50;
    private const int TickMs = 10;
    private const int TransactionTimeoutMs = 100;
    private const int InitialBufferTimeoutMs = 1000;
    private const int MutexTimeoutMs = 1500;

    private readonly PawnIoEcTransport _ports;
    private readonly Mutex _thinkPadMutex = CreateOrOpenMutex("Access_Thinkpad_EC");
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
        // older ACPI Type 2 fallback. No EC register is written during detection.
        _ports = new PawnIoEcTransport();
        try
        {
            DetectPortPair();
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
            if (!TryReadByte(EcThermalProbe, out byte thermal))
            {
                detail = $"EC thermal probe timed out on 0x{commandPort:X}/0x{dataPort:X}.";
                return false;
            }

            if (!TryReadByte(ThinkPadRegisters.FanControl, out byte control))
            {
                detail = $"Fan-control read timed out on 0x{commandPort:X}/0x{dataPort:X}.";
                return false;
            }

            bool plausibleThermal = thermal is >= 5 and <= 125;
            bool plausibleControl = control <= 0x07 || control is >= 0x40 and <= 0x47 || control == ThinkPadRegisters.BiosControl;
            if (!plausibleThermal || !plausibleControl)
            {
                detail = $"Read-only validation returned thermal {thermal} and fan state 0x{control:X2} on 0x{commandPort:X}/0x{dataPort:X}.";
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
        if (!WaitForReadWriteReady()) return false;
        WritePort(_commandPort, EcReadCommand);
        if (!WaitForReadWriteReady()) return false;
        WritePort(_dataPort, register);
        if (!WaitForReadWriteReady() || !WaitForReadReady()) return false;
        value = ReadPort(_dataPort);
        return true;
    }

    private bool WaitForReadWriteReady()
    {
        for (int i = 0; i < ReadWaitSpins; i++)
        {
            if ((ReadPort(_commandPort) & InputBufferFull) == 0)
                return true;
            Thread.Sleep(1);
        }
        return false;
    }

    private bool WaitForReadReady()
    {
        for (int retry = 0; retry < ReadMaxRetries; retry++)
        {
            if ((ReadPort(_commandPort) & OutputBufferFull) != 0)
                return true;
            Thread.Sleep(1);
        }

        // Some ThinkPad ECs complete a read after IBF clears without reliably
        // asserting OBF for every register transaction.
        for (int i = 0; i < ReadWaitSpins; i++)
        {
            if ((ReadPort(_commandPort) & InputBufferFull) == 0)
                return true;
            Thread.Sleep(1);
        }
        return false;
    }

    private void WriteByteUnlocked(byte register, byte value)
    {
        byte status = WaitForBothBuffersClear(InitialBufferTimeoutMs);
        if ((status & OutputBufferFull) != 0)
            _ = ReadPort(_dataPort);

        if (!WaitForOutputBufferClear(TransactionTimeoutMs))
            throw new TimeoutException("EC output buffer stayed busy before write command.");

        WritePort(_commandPort, EcWriteCommand);
        if (!WaitForBothBuffersClearSuccess(TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept write command for 0x{register:X2}.");

        WritePort(_dataPort, register);
        if (!WaitForBothBuffersClearSuccess(TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept register 0x{register:X2}.");

        WritePort(_dataPort, value);
    }

    private byte WaitForBothBuffersClear(int timeoutMs)
    {
        byte status = 0;
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += TickMs)
        {
            status = ReadPort(_commandPort);
            if ((status & (InputBufferFull | OutputBufferFull)) == 0)
                break;
            Thread.Sleep(TickMs);
        }
        return status;
    }

    private bool WaitForBothBuffersClearSuccess(int timeoutMs)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += TickMs)
        {
            byte status = ReadPort(_commandPort);
            if ((status & (InputBufferFull | OutputBufferFull)) == 0)
                return true;
            Thread.Sleep(TickMs);
        }
        return false;
    }

    private bool WaitForOutputBufferClear(int timeoutMs)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += TickMs)
        {
            if ((ReadPort(_commandPort) & OutputBufferFull) == 0)
                return true;
            Thread.Sleep(TickMs);
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
