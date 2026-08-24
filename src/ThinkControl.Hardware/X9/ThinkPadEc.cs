namespace ThinkControl.Hardware.X9;

internal sealed class ThinkPadEc : IDisposable
{
    private const byte EcDataPort = 0x62;
    private const byte EcCommandPort = 0x66;
    private const byte EcReadCommand = 0x80;
    private const byte EcWriteCommand = 0x81;
    private const byte OutputBufferFull = 0x01;
    private const byte InputBufferFull = 0x02;

    private const int ReadMaxRetries = 5;
    private const int ReadWaitSpins = 50;
    private const int TickMs = 10;
    private const int TransactionTimeoutMs = 100;
    private const int InitialBufferTimeoutMs = 1000;
    private const int MutexTimeoutMs = 1500;

    private readonly PawnIoEcTransport _ports;
    private readonly Mutex _thinkPadMutex = CreateOrOpenMutex("Access_Thinkpad_EC");
    private readonly Mutex _globalEcMutex = CreateOrOpenMutex(@"Global\Access_EC");
    private bool _manualControlEngaged;
    private byte? _lastManualLevel;
    private bool _disposed;

    internal ThinkPadEc()
    {
        // Do not infer PawnIO readiness from its Windows service entry alone.
        // The transport proves that the device can be opened and that LHM's signed
        // LPC/ACPI EC module actually loads before any ThinkPad EC transaction runs.
        _ports = new PawnIoEcTransport();
    }

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
        WritePort(EcCommandPort, EcReadCommand);
        if (!WaitForReadWriteReady()) return false;
        WritePort(EcDataPort, register);
        if (!WaitForReadWriteReady() || !WaitForReadReady()) return false;
        value = ReadPort(EcDataPort);
        return true;
    }

    private bool WaitForReadWriteReady()
    {
        for (int i = 0; i < ReadWaitSpins; i++)
        {
            if ((ReadPort(EcCommandPort) & InputBufferFull) == 0)
                return true;
            Thread.Sleep(1);
        }
        return false;
    }

    private bool WaitForReadReady()
    {
        for (int retry = 0; retry < ReadMaxRetries; retry++)
        {
            if ((ReadPort(EcCommandPort) & OutputBufferFull) != 0)
                return true;
            Thread.Sleep(1);
        }

        // Same compatibility fallback used by the earlier working plugin: a few
        // ThinkPad ECs complete the transaction after IBF clears without reliably
        // asserting OBF for every register read.
        for (int i = 0; i < ReadWaitSpins; i++)
        {
            if ((ReadPort(EcCommandPort) & InputBufferFull) == 0)
                return true;
            Thread.Sleep(1);
        }
        return false;
    }

    private void WriteByteUnlocked(byte register, byte value)
    {
        byte status = WaitForBothBuffersClear(InitialBufferTimeoutMs);
        if ((status & OutputBufferFull) != 0)
            _ = ReadPort(EcDataPort);

        if (!WaitForOutputBufferClear(TransactionTimeoutMs))
            throw new TimeoutException("EC output buffer stayed busy before write command.");

        WritePort(EcCommandPort, EcWriteCommand);
        if (!WaitForBothBuffersClearSuccess(TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept write command for 0x{register:X2}.");

        WritePort(EcDataPort, register);
        if (!WaitForBothBuffersClearSuccess(TransactionTimeoutMs))
            throw new TimeoutException($"EC did not accept register 0x{register:X2}.");

        WritePort(EcDataPort, value);
    }

    private byte WaitForBothBuffersClear(int timeoutMs)
    {
        byte status = 0;
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += TickMs)
        {
            status = ReadPort(EcCommandPort);
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
            byte status = ReadPort(EcCommandPort);
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
            if ((ReadPort(EcCommandPort) & OutputBufferFull) == 0)
                return true;
            Thread.Sleep(TickMs);
        }
        return false;
    }

    private byte ReadPort(byte port) => _ports.ReadPort(port);
    private void WritePort(byte port, byte value) => _ports.WritePort(port, value);

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
