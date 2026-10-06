namespace ThinkControl.Hardware.X9;

// The inspected 21Q6/N4CET45W firmware recognizes exactly HFSP 0x40.
// This is a two-state contract, never a percentage or a seven-step curve.
internal sealed class X9FullSpeedSession(Func<byte> read, Action setFullSpeed, Action returnAuto)
{
    internal bool Owned { get; private set; }
    private DateTimeOffset _lastClient;
    private int _readFailures;

    internal void Renew(DateTimeOffset now) => _lastClient = now;

    internal void Start(DateTimeOffset now)
    {
        if (Owned)
        {
            if (read() != 0x40) throw new InvalidOperationException("Max cooling was reclaimed by firmware; select Auto before retrying.");
            Renew(now);
            return;
        }
        if (read() != 0x80)
            throw new InvalidOperationException("Fan control is already active outside this session. Select Auto before Max cooling.");
        Owned = true; // Retain responsibility even when a write/rollback fails.
        Renew(now);
        try
        {
            setFullSpeed();
            if (read() != 0x40) throw new InvalidOperationException("Max cooling readback was not confirmed.");
            _readFailures = 0;
        }
        catch
        {
            try { Stop(); } catch { }
            throw;
        }
    }

    internal void Stop()
    {
        returnAuto();
        if (read() != 0x80) throw new InvalidOperationException("Lenovo Auto readback was not confirmed.");
        Owned = false;
        _readFailures = 0;
    }

    internal void Check(DateTimeOffset now)
    {
        if (!Owned) return;
        if (now - _lastClient >= TimeSpan.FromSeconds(45)) { Stop(); return; }
        try
        {
            if (read() == 0x40) { _readFailures = 0; return; }
        }
        catch { }
        // A transient EC sample cannot cause a rewrite of Max. Repeated loss of
        // readback hands ownership to firmware instead of maintaining a false label.
        if (++_readFailures >= 2) Stop();
    }
}
