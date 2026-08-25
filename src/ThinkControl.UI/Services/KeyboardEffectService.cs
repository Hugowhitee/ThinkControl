using NAudio.Wave;
using System.Runtime.InteropServices;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

public sealed class KeyboardEffectService : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MinHardwareWriteInterval = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan ReactiveHold = TimeSpan.FromMilliseconds(430);
    private static readonly TimeSpan IdleLowAfter = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdleOffAfter = TimeSpan.FromSeconds(35);

    private readonly HardwareServiceClient _hardware;
    private readonly AppState _state;
    private readonly KeyboardActivityHook _keyboardHook;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

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
        _keyboardHook = new KeyboardActivityHook();
        _keyboardHook.KeyPressed += OnKeyPressed;
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public bool ReactiveInputAvailable => _keyboardHook.IsAvailable;

    public async Task SetStaticLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        StopAudioCapture();
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

        _state.KeyboardMode = normalized;
        _breathingStarted = DateTimeOffset.UtcNow;
        _lastKeyboardActivity = DateTimeOffset.UtcNow;

        if (normalized == "Audio")
            StartAudioCapture();
        else
            StopAudioCapture();

        if (normalized == "Static")
            await ApplyLevelAsync(_state.KeyboardBaseLevel, force: true, cancellationToken).ConfigureAwait(false);
        else
            await TickEffectAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SetBaseLevel(string level) => _state.KeyboardBaseLevel = NormalizeLevel(level);

    public void SetSpeed(double speed) => _state.KeyboardEffectSpeed = speed;

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await TickEffectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

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
            // A direct Off/Low/High click is authoritative. If an effect tick already
            // owns the writer, wait for that bounded service call to finish and then
            // apply the user's requested level. The previous zero-timeout gate could
            // silently drop a static click; Static mode then had no background tick to
            // retry it, making working Lenovo keyboard hardware appear unresponsive.
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
        _cts.Cancel();
        try { _loop.Wait(TimeSpan.FromSeconds(1)); } catch { }
        StopAudioCapture();
        _keyboardHook.KeyPressed -= OnKeyPressed;
        _keyboardHook.Dispose();
        _writeGate.Dispose();
        _cts.Dispose();
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
