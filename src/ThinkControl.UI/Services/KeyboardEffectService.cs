using NAudio.Wave;
using System.Runtime.InteropServices;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

public sealed class KeyboardEffectService : IDisposable
{
    private static readonly TimeSpan MinHardwareWriteInterval = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan ReactiveHold = TimeSpan.FromMilliseconds(430);
    private static readonly TimeSpan IdleLowAfter = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdleOffAfter = TimeSpan.FromSeconds(35);

    private readonly HardwareServiceClient _hardware;
    private readonly AppState _state;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _runtimeGate = new();

    private KeyboardActivityHook? _keyboardHook;
    private CancellationTokenSource? _effectCts;
    private Task? _effectLoop;
    private DateTimeOffset _lastKeyboardActivity = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastHardwareWrite = DateTimeOffset.MinValue;
    private DateTimeOffset _breathingStarted = DateTimeOffset.UtcNow;
    private string? _lastAppliedLevel;
    private WasapiLoopbackCapture? _audioCapture;
    private double _audioRms;
    private bool _disposed;

    public KeyboardEffectService(HardwareServiceClient hardware, AppState state)
    {
        _hardware = hardware;
        _state = state;
    }

    // Reactive input is deliberately not probed at application startup. Installing a
    // global low-level keyboard hook simply to advertise capability would keep every
    // keystroke flowing through ThinkControl even when the user selected Static.
    public bool ReactiveInputAvailable => _keyboardHook?.IsAvailable == true;

    public async Task SetStaticLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        await StopEffectRuntimeAsync().ConfigureAwait(false);
        StopAudioCapture();
        StopKeyboardHook();
        _state.KeyboardMode = "Static";
        _state.KeyboardBaseLevel = NormalizeLevel(level);
        _breathingStarted = DateTimeOffset.UtcNow;
        await ApplyLevelAsync(_state.KeyboardBaseLevel, force: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        string normalized = mode switch
        {
            "Auto" => "Auto",
            "Breathing" => "Breathing",
            "Reactive" => "Reactive",
            "Audio" => "Audio",
            _ => "Static"
        };

        await StopEffectRuntimeAsync().ConfigureAwait(false);
        StopAudioCapture();
        StopKeyboardHook();

        _state.KeyboardMode = normalized;
        _breathingStarted = DateTimeOffset.UtcNow;
        _lastKeyboardActivity = DateTimeOffset.UtcNow;

        if (normalized == "Static")
        {
            await ApplyLevelAsync(_state.KeyboardBaseLevel, force: true, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (normalized == "Reactive")
            StartKeyboardHook();
        else if (normalized == "Audio")
            StartAudioCapture();

        await TickEffectAsync(cancellationToken).ConfigureAwait(false);
        StartEffectRuntime();
    }

    public void SetBaseLevel(string level) => _state.KeyboardBaseLevel = NormalizeLevel(level);

    public void SetSpeed(double speed) => _state.KeyboardEffectSpeed = speed;

    private void StartEffectRuntime()
    {
        if (_disposed || _state.KeyboardMode == "Static")
            return;

        lock (_runtimeGate)
        {
            if (_effectLoop is { IsCompleted: false })
                return;

            _effectCts?.Dispose();
            _effectCts = new CancellationTokenSource();
            CancellationToken token = _effectCts.Token;
            _effectLoop = Task.Run(() => RunEffectAsync(token), token);
        }
    }

    private async Task StopEffectRuntimeAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_runtimeGate)
        {
            cts = _effectCts;
            loop = _effectLoop;
            _effectCts = null;
            _effectLoop = null;
        }

        try { cts?.Cancel(); } catch { }
        if (loop is not null)
        {
            try { await loop.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            catch { }
        }
        cts?.Dispose();
    }

    private async Task RunEffectAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(CurrentEffectInterval(), cancellationToken).ConfigureAwait(false);
                await TickEffectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private TimeSpan CurrentEffectInterval() => _state.KeyboardMode switch
    {
        // Auto only reacts to 15/35 second idle thresholds; a one-second cadence is
        // indistinguishable to the user and avoids an 8 Hz background wakeup.
        "Auto" => TimeSpan.FromSeconds(1),
        // User-selected animated/reactive modes can sample faster while active, but
        // still coalesce hardware writes through MinHardwareWriteInterval below.
        "Reactive" => TimeSpan.FromMilliseconds(90),
        "Audio" => TimeSpan.FromMilliseconds(100),
        "Breathing" => TimeSpan.FromMilliseconds(120),
        _ => TimeSpan.FromSeconds(1)
    };

    private async Task TickEffectAsync(CancellationToken cancellationToken)
    {
        if (!_state.CanKeyboardBacklight)
            return;

        string? target = _state.KeyboardMode switch
        {
            "Auto" => AutoTarget(),
            "Breathing" => BreathingTarget(),
            "Reactive" => ReactiveTarget(),
            "Audio" => AudioTarget(),
            _ => null
        };

        if (target is not null)
            await ApplyLevelAsync(target, force: false, cancellationToken).ConfigureAwait(false);
    }

    private string AutoTarget()
    {
        TimeSpan idle = GetIdleTime();
        if (idle >= IdleOffAfter)
            return "Off";
        if (idle >= IdleLowAfter)
            return "Low";
        return "High";
    }

    private string BreathingTarget()
    {
        double speed = Math.Clamp(_state.KeyboardEffectSpeed, 0.5, 2.0);
        double halfCycleMs = 1050d / speed;
        double elapsed = (DateTimeOffset.UtcNow - _breathingStarted).TotalMilliseconds;
        long phase = (long)Math.Floor(elapsed / halfCycleMs);

        // The X9 firmware appears to fade between discrete levels itself. Deliberately
        // alternate Low/High instead of hammering Off/Low/High at animation-frame rate.
        return phase % 2 == 0 ? "Low" : "High";
    }

    private string ReactiveTarget()
    {
        if (DateTimeOffset.UtcNow - _lastKeyboardActivity <= ReactiveHold)
            return "High";
        return NormalizeLevel(_state.KeyboardBaseLevel) == "High" ? "Low" : NormalizeLevel(_state.KeyboardBaseLevel);
    }

    private string AudioTarget()
    {
        double rms = Volatile.Read(ref _audioRms);
        if (rms >= 0.16)
            return "High";
        if (rms >= 0.035)
            return "Low";
        return NormalizeLevel(_state.KeyboardBaseLevel) == "Off" ? "Off" : "Low";
    }

    private async Task ApplyLevelAsync(string level, bool force, CancellationToken cancellationToken)
    {
        level = NormalizeLevel(level);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!force)
        {
            if (string.Equals(_lastAppliedLevel, level, StringComparison.OrdinalIgnoreCase))
                return;
            if (now - _lastHardwareWrite < MinHardwareWriteInterval)
                return;
        }

        if (force)
        {
            // A direct Off/Low/High click is authoritative. If an effect write is
            // already in flight, wait for that bounded service call and then apply
            // the user's explicit level. Dropping a static click leaves no effect
            // loop to retry it and makes working keyboard hardware look broken.
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (!await _writeGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var result = await _hardware.SetKeyboardBacklightAsync(level, cancellationToken).ConfigureAwait(false);
            _lastHardwareWrite = DateTimeOffset.UtcNow;
            if (result?.Success == true)
            {
                _lastAppliedLevel = level;
                _state.KeyboardStatus = level;
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void StartKeyboardHook()
    {
        if (_keyboardHook is not null || _disposed)
            return;

        try
        {
            var hook = new KeyboardActivityHook();
            hook.KeyPressed += OnKeyPressed;
            _keyboardHook = hook;
        }
        catch
        {
            StopKeyboardHook();
        }
    }

    private void StopKeyboardHook()
    {
        KeyboardActivityHook? hook = _keyboardHook;
        _keyboardHook = null;
        if (hook is null)
            return;

        try { hook.KeyPressed -= OnKeyPressed; } catch { }
        try { hook.Dispose(); } catch { }
    }

    private void OnKeyPressed() => _lastKeyboardActivity = DateTimeOffset.UtcNow;

    private static string NormalizeLevel(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "off" => "Off",
        "low" => "Low",
        _ => "High"
    };

    private static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        uint elapsed = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    private void StartAudioCapture()
    {
        if (_audioCapture is not null)
            return;

        try
        {
            var capture = new WasapiLoopbackCapture();
            capture.DataAvailable += Audio_DataAvailable;
            capture.RecordingStopped += Audio_RecordingStopped;
            capture.StartRecording();
            _audioCapture = capture;
        }
        catch
        {
            StopAudioCapture();
        }
    }

    private void StopAudioCapture()
    {
        WasapiLoopbackCapture? capture = _audioCapture;
        _audioCapture = null;
        Volatile.Write(ref _audioRms, 0d);
        if (capture is null)
            return;

        try { capture.DataAvailable -= Audio_DataAvailable; } catch { }
        try { capture.RecordingStopped -= Audio_RecordingStopped; } catch { }
        try { capture.StopRecording(); } catch { }
        try { capture.Dispose(); } catch { }
    }

    private void Audio_DataAvailable(object? sender, WaveInEventArgs e)
    {
        WasapiLoopbackCapture? capture = _audioCapture;
        if (capture is null || e.BytesRecorded <= 0)
            return;

        double rms = CalculateRms(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        double previous = Volatile.Read(ref _audioRms);
        double smoothed = previous * 0.70 + rms * 0.30;
        Volatile.Write(ref _audioRms, smoothed);
    }

    private void Audio_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        Volatile.Write(ref _audioRms, 0d);
    }

    private static double CalculateRms(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            int samples = bytesRecorded / 4;
            if (samples <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                float value = BitConverter.ToSingle(buffer, i * 4);
                sum += value * value;
            }
            return Math.Sqrt(sum / samples);
        }

        if (format.BitsPerSample == 16)
        {
            int samples = bytesRecorded / 2;
            if (samples <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                short sample = BitConverter.ToInt16(buffer, i * 2);
                double value = sample / 32768d;
                sum += value * value;
            }
            return Math.Sqrt(sum / samples);
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try { StopEffectRuntimeAsync().GetAwaiter().GetResult(); } catch { }
        StopAudioCapture();
        StopKeyboardHook();
        _writeGate.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
