namespace ThinkControl.Hardware.X9;

// The inspected 21Q6/N4CET45W firmware recognizes exactly HFSP 0x40.
// The optional regulated contract adds only physically measured running states.
// One owner and lease cover Max, regulated curves, rollback and Auto recovery.
internal sealed class X9FullSpeedSession(Func<byte> read, Action setFullSpeed, Action returnAuto,
    Action<byte>? setRegulated = null, Func<bool>? hasRegulatedOwnership = null, Action? restoreRegulated = null)
{
    internal bool Owned { get; private set; }
    internal bool Regulated { get; private set; }
    internal byte State { get; private set; } = 0x40;
    private DateTimeOffset _lastClient;
    private int _readFailures;

    internal void Renew(DateTimeOffset now) => _lastClient = now;

    internal void Start(DateTimeOffset now) => SetState(0x40, regulated: false, now);

    internal void SetRegulatedState(byte state, DateTimeOffset now)
    {
        if (state is not (>= 4 and <= 7) and not 0x40 || setRegulated is null)
            throw new InvalidOperationException("That running fan state is unavailable.");
        SetState(state, regulated: true, now);
    }

    private void SetState(byte state, bool regulated, DateTimeOffset now)
    {
        if (Owned && Regulated != regulated) Stop();
        if (Owned)
        {
            if (read() != State || (Regulated && hasRegulatedOwnership?.Invoke() != true))
                throw new InvalidOperationException("Fan control was reclaimed. Select Auto before retrying.");
            if (state == State) { Renew(now); return; }
        }
        else if (read() != 0x80)
            throw new InvalidOperationException("Fan control is already active outside this session. Select Auto before Max cooling.");
        Owned = true; // Retain responsibility even when a write/rollback fails.
        Regulated = regulated;
        Renew(now);
        try
        {
            if (regulated)
            {
                // A verified Auto bridge releases the full-speed latch before downshifting.
                if (State == 0x40 && read() == 0x40 && state != 0x40) returnAuto();
                setRegulated!(state);
            }
            else setFullSpeed();
            if (read() != state || (regulated && hasRegulatedOwnership?.Invoke() != true))
                throw new InvalidOperationException("Fan state readback was not confirmed.");
            State = state;
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
        Exception? ownershipError = null;
        try { if (Regulated) restoreRegulated?.Invoke(); }
        catch (Exception ex) { ownershipError = ex; }
        // An OEM readback failure must not prevent the independent EC Auto handoff.
        returnAuto();
        if (read() != 0x80) throw new InvalidOperationException("Lenovo Auto readback was not confirmed.");
        if (ownershipError is not null) throw ownershipError;
        Owned = false;
        Regulated = false;
        _readFailures = 0;
    }

    internal void Check(DateTimeOffset now)
    {
        if (!Owned) return;
        if (now - _lastClient >= TimeSpan.FromSeconds(45)) { Stop(); return; }
        try
        {
            if (read() == State && (!Regulated || hasRegulatedOwnership?.Invoke() == true)) { _readFailures = 0; return; }
        }
        catch { }
        // A transient EC sample cannot cause a rewrite of Max. Repeated loss of
        // readback hands ownership to firmware instead of maintaining a false label.
        if (++_readFailures >= 2) Stop();
    }
}
