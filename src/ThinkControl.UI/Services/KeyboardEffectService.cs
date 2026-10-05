using NAudio.Wave;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

public sealed class KeyboardEffectService : IDisposable
{
    private static readonly TimeSpan MinHardwareWriteInterval = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan ReactiveHold = TimeSpan.FromMilliseconds(430);

    private readonly Func<string, CancellationToken, Task<ServiceResponse?>> _writeBacklight;
    private readonly bool _suppressOsd;
    private readonly AppState _state;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _runtimeGate = new();
    private readonly LenovoKeyboardOsdSuppressor _osdSuppressor = new();

    private KeyboardActivityHook? _keyboardHook;
    private CancellationTokenSource? _effectCts;
    private Task? _effectLoop;
    private DateTimeOffset _lastKeyboardActivity = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastHardwareWrite = DateTimeOffset.MinValue;
    private DateTimeOffset _breathingStarted = DateTimeOffset.UtcNow;
    private string? _lastAppliedLevel;
    private WasapiLoopbackCapture? _audioCapture;
    private double _audioRms;
    private double _audioPeakRms;
    private int _audioRestartGeneration;
    private int _selectionGeneration;
    private bool _disposed;

    public KeyboardEffectService(HardwareServiceClient hardware, AppState state)
        : this(hardware.SetKeyboardBacklightAsync, state, suppressOsd: true)
    {
    }

    internal KeyboardEffectService(Func<string, CancellationToken, Task<ServiceResponse?>> writeBacklight,
        AppState state, bool suppressOsd = false)
    {
        _writeBacklight = writeBacklight;
        _suppressOsd = suppressOsd;
        _state = state;
    }

    // Reactive input is deliberately not probed at application startup. Installing a
    // global low-level keyboard hook simply to advertise capability would keep every
    // keystroke flowing through ThinkControl even when the user selected Static.
    public bool ReactiveInputAvailable => _keyboardHook?.IsAvailable == true;

    public async Task<bool> SetStaticLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        int generation = Interlocked.Increment(ref _selectionGeneration);
        await StopEffectRuntimeAsync(generation).ConfigureAwait(false);
        if (!IsCurrentSelection(generation)) return false;
        StopAudioCapture();
        StopKeyboardHook();
        _state.KeyboardMode = "Static";
        _state.KeyboardBaseLevel = NormalizeLevel(level);
        _breathingStarted = DateTimeOffset.UtcNow;
        return await ApplyLevelAsync(_state.KeyboardBaseLevel, force: true, cancellationToken, generation).ConfigureAwait(false);
    }

    public async Task<bool> SetModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        int generation = Interlocked.Increment(ref _selectionGeneration);
        string normalized = mode switch
        {
            "Auto" => "Auto",
            "Breathing" => "Breathing",
            "Reactive" => "Reactive",
            "Audio" => "Audio",
            _ => "Static"
        };

        await StopEffectRuntimeAsync(generation).ConfigureAwait(false);
        if (!IsCurrentSelection(generation)) return false;
        StopAudioCapture();
        StopKeyboardHook();

        _state.KeyboardMode = normalized;
        _breathingStarted = DateTimeOffset.UtcNow;
        _lastKeyboardActivity = DateTimeOffset.UtcNow;

        if (normalized == "Static")
        {
            return await ApplyLevelAsync(_state.KeyboardBaseLevel, force: true, cancellationToken, generation).ConfigureAwait(false);
        }

        // Auto is exclusively Lenovo's verified firmware mode. It is intentionally
        // not a ThinkControl effect and has no software idle fallback: if the active
        // Lenovo backend cannot set/read back FirmwareAuto, keep the prior hardware
        // state and return the editor to Static rather than starting a hidden loop.
        if (normalized == "Auto")
        {
            if (_state.CanKeyboardBacklight && await TryEnableFirmwareAutoAsync(cancellationToken, generation).ConfigureAwait(false))
                return true;
            if (IsCurrentSelection(generation)) _state.KeyboardMode = "Static";
            return false;
        }

        // Native effects use a provider that explicitly advertises bounded repeated
        // writes. A non-native provider is allowed only after the user enables the
        // session-only Experimental fallback; the same deduplication/rate limit and
        // ordinary Off/Low/High command surface remain in force.
        if (!_state.KeyboardEffectsUsable)
        {
            _state.KeyboardMode = "Static";
            return false;
        }

        if (normalized == "Reactive")
            StartKeyboardHook();
        else if (normalized == "Audio")
            StartAudioCapture();

        await TickEffectAsync(cancellationToken, generation).ConfigureAwait(false);
        if (IsCurrentSelection(generation)) StartEffectRuntime(generation);
        return IsCurrentSelection(generation);
    }

    public void SetBaseLevel(string level) => _state.KeyboardBaseLevel = NormalizeLevel(level);

    public void SetSpeed(double speed) => _state.KeyboardEffectSpeed = speed;

    private bool IsCurrentSelection(int generation) => !_disposed && generation == Volatile.Read(ref _selectionGeneration);

    private void StartEffectRuntime(int generation)
    {
        if (!IsCurrentSelection(generation) || _state.KeyboardMode is "Static" or "Auto")
            return;

        lock (_runtimeGate)
        {
            if (!IsCurrentSelection(generation)) return;
            if (_effectLoop is { IsCompleted: false })
                return;

            _effectCts?.Dispose();
            _effectCts = new CancellationTokenSource();
            CancellationToken token = _effectCts.Token;
            _effectLoop = Task.Run(() => RunEffectAsync(token, generation), token);
        }
    }

    private async Task StopEffectRuntimeAsync(int generation)
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_runtimeGate)
        {
            if (generation != Volatile.Read(ref _selectionGeneration)) return;
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
        // Reenable OSD visibility even if the user disables the experimental
        // feature during a write or immediately switches to a static level.
        lock (_runtimeGate)
            if (generation == Volatile.Read(ref _selectionGeneration)) _osdSuppressor.Disarm();
    }

    private async Task RunEffectAsync(CancellationToken cancellationToken, int generation)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsCurrentSelection(generation))
            {
                await Task.Delay(CurrentEffectInterval(), cancellationToken).ConfigureAwait(false);
                await TickEffectAsync(cancellationToken, generation).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private TimeSpan CurrentEffectInterval() => _state.KeyboardMode switch
    {
        "Reactive" => TimeSpan.FromMilliseconds(90),
        "Audio" => TimeSpan.FromMilliseconds(100),
        "Breathing" => TimeSpan.FromMilliseconds(120),
        _ => TimeSpan.FromSeconds(1)
    };

    private async Task TickEffectAsync(CancellationToken cancellationToken, int generation)
    {
        if (!IsCurrentSelection(generation) || !_state.KeyboardEffectsUsable)
            return;

        string? target = _state.KeyboardMode switch
        {
            "Breathing" => BreathingTarget(),
            "Reactive" => ReactiveTarget(),
            "Audio" => AudioTarget(),
            _ => null
        };

        if (target is not null)
            await ApplyLevelAsync(target, force: false, cancellationToken, generation).ConfigureAwait(false);
    }

    private async Task<bool> TryEnableFirmwareAutoAsync(CancellationToken cancellationToken, int generation)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsCurrentSelection(generation)) return false;
            ServiceResponse? result = await _writeBacklight("FirmwareAuto", cancellationToken).ConfigureAwait(false);
            _lastHardwareWrite = DateTimeOffset.UtcNow;
            if (result?.Success != true || !IsCurrentSelection(generation))
                return false;

            _lastAppliedLevel = null;
            _state.KeyboardStatus = "Lenovo Auto";
            return true;
        }
        finally
        {
            _writeGate.Release();
        }
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
        double peak = Volatile.Read(ref _audioPeakRms);

        // This backend only exposes Off / Low / High, so make Audio visibly reactive
        // across those three safe states instead of leaving silence parked at Low.
        // Relative-to-recent-peak thresholds keep quiet Windows output useful while
        // the small absolute floor prevents idle/noise from flashing the keyboard.
        if (peak >= 0.0035 && rms >= 0.004 && rms / peak >= 0.52)
            return "High";
        if (rms >= 0.0012)
            return "Low";
        return "Off";
    }

    private async Task<bool> ApplyLevelAsync(string level, bool force, CancellationToken cancellationToken, int generation)
    {
        level = NormalizeLevel(level);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!force)
        {
            if (string.Equals(_lastAppliedLevel, level, StringComparison.OrdinalIgnoreCase))
                return IsCurrentSelection(generation);
            if (now - _lastHardwareWrite < MinHardwareWriteInterval)
                return false;
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
            return false;
        }

        try
        {
            // Lenovo's tposd.exe is firmware-notification driven and can show an OSD
            // for every automatic backlight write. Hide only OSD windows created in
            // the short interval around effect writes; explicit static clicks and
            // ordinary Fn+Space feedback remain outside this suppression path.
            lock (_runtimeGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentSelection(generation)) return false;
                if (!force && _suppressOsd) _osdSuppressor.Arm();
            }

            ServiceResponse? result = await _writeBacklight(level, cancellationToken).ConfigureAwait(false);
            _lastHardwareWrite = DateTimeOffset.UtcNow;
            if (result?.Success == true && IsCurrentSelection(generation))
            {
                _lastAppliedLevel = level;
                _state.KeyboardStatus = level;
                return true;
            }
            return false;
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

    private void StartAudioCapture()
    {
        if (_disposed || _state.KeyboardMode != "Audio" || !_state.KeyboardEffectsUsable)
            return;

        WasapiLoopbackCapture? capture = null;
        try
        {
            capture = new WasapiLoopbackCapture();

            lock (_runtimeGate)
            {
                if (_audioCapture is not null)
                {
                    capture.Dispose();
                    return;
                }

                // Publish the instance before StartRecording. Some WASAPI endpoints
                // can produce the first DataAvailable callback immediately; assigning
                // it afterwards made those first buffers look like no active capture.
                _audioCapture = capture;
                capture.DataAvailable += Audio_DataAvailable;
                capture.RecordingStopped += Audio_RecordingStopped;
            }

            // Do not hold _runtimeGate while starting WASAPI. DataAvailable itself
            // takes that gate to validate ownership, and a backend that delivers its
            // first callback synchronously must never be able to deadlock startup.
            capture.StartRecording();
        }
        catch
        {
            if (capture is null)
                return;

            lock (_runtimeGate)
            {
                if (ReferenceEquals(_audioCapture, capture))
                    _audioCapture = null;
            }
            try { capture.DataAvailable -= Audio_DataAvailable; } catch { }
            try { capture.RecordingStopped -= Audio_RecordingStopped; } catch { }
            try { capture.Dispose(); } catch { }
        }
    }

    private void StopAudioCapture()
    {
        Interlocked.Increment(ref _audioRestartGeneration);

        WasapiLoopbackCapture? capture;
        lock (_runtimeGate)
        {
            capture = _audioCapture;
            _audioCapture = null;
        }

        Volatile.Write(ref _audioRms, 0d);
        Volatile.Write(ref _audioPeakRms, 0d);
        if (capture is null)
            return;

        try { capture.DataAvailable -= Audio_DataAvailable; } catch { }
        try { capture.RecordingStopped -= Audio_RecordingStopped; } catch { }
        try { capture.StopRecording(); } catch { }
        try { capture.Dispose(); } catch { }
    }

    private void Audio_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture || e.BytesRecorded <= 0)
            return;

        lock (_runtimeGate)
        {
            if (!ReferenceEquals(capture, _audioCapture))
                return;
        }

        double rms = CalculateRms(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        double previous = Volatile.Read(ref _audioRms);
        double smoothed = previous * 0.55 + rms * 0.45;
        Volatile.Write(ref _audioRms, smoothed);

        double previousPeak = Volatile.Read(ref _audioPeakRms);
        double peak = Math.Max(rms, previousPeak * 0.965);
        Volatile.Write(ref _audioPeakRms, peak);
    }

    private void Audio_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture stopped)
            return;

        bool wasActive;
        lock (_runtimeGate)
        {
            wasActive = ReferenceEquals(stopped, _audioCapture);
            if (wasActive)
                _audioCapture = null;
        }

        if (!wasActive)
            return;

        try { stopped.DataAvailable -= Audio_DataAvailable; } catch { }
        try { stopped.RecordingStopped -= Audio_RecordingStopped; } catch { }
        try { stopped.Dispose(); } catch { }

        Volatile.Write(ref _audioRms, 0d);
        Volatile.Write(ref _audioPeakRms, 0d);

        int generation = Interlocked.Increment(ref _audioRestartGeneration);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250).ConfigureAwait(false);
                if (_disposed || generation != Volatile.Read(ref _audioRestartGeneration) ||
                    _state.KeyboardMode != "Audio" || !_state.KeyboardEffectsUsable)
                {
                    return;
                }

                StartAudioCapture();
            }
            catch
            {
            }
        });
    }

    internal static double CalculateRms(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        // Shared-mode WASAPI very commonly reports WAVE_FORMAT_EXTENSIBLE even when
        // the underlying sample format is 32-bit IEEE float. Treat the recognized
        // extensible PCM/float subtype as its standard equivalent before decoding.
        WaveFormat sampleFormat = format is WaveFormatExtensible extensible
            ? extensible.ToStandardWaveFormat()
            : format;

        if (sampleFormat.Encoding == WaveFormatEncoding.IeeeFloat && sampleFormat.BitsPerSample == 32)
        {
            int samples = bytesRecorded / 4;
            if (samples <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                float value = BitConverter.ToSingle(buffer, i * 4);
                if (float.IsFinite(value))
                    sum += value * value;
            }
            return Math.Sqrt(sum / samples);
        }

        if (sampleFormat.Encoding == WaveFormatEncoding.Pcm && sampleFormat.BitsPerSample == 16)
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

        if (sampleFormat.Encoding == WaveFormatEncoding.Pcm && sampleFormat.BitsPerSample == 24)
        {
            int samples = bytesRecorded / 3;
            if (samples <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                int offset = i * 3;
                int sample = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
                if ((sample & 0x00800000) != 0)
                    sample |= unchecked((int)0xFF000000);
                double value = sample / 8388608d;
                sum += value * value;
            }
            return Math.Sqrt(sum / samples);
        }

        if (sampleFormat.Encoding == WaveFormatEncoding.Pcm && sampleFormat.BitsPerSample == 32)
        {
            int samples = bytesRecorded / 4;
            if (samples <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                int sample = BitConverter.ToInt32(buffer, i * 4);
                double value = sample / 2147483648d;
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
        int generation = Interlocked.Increment(ref _selectionGeneration);
        try { StopEffectRuntimeAsync(generation).GetAwaiter().GetResult(); } catch { }
        StopAudioCapture();
        StopKeyboardHook();
        _osdSuppressor.Dispose();
        // A cancelled IPC request can still finish after the bounded stop wait.
        // This semaphore allocates no WaitHandle; let it be collected after those
        // continuations release it instead of disposing beneath an in-flight write.
    }
}
