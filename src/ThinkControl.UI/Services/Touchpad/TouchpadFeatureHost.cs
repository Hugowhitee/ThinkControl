using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Services.Touchpad;

internal sealed class TouchpadFeatureHost : IDisposable
{
    private readonly App _app;
    private readonly TouchpadGestureService _gestures;
    private readonly TouchpadHapticsService _haptics = new();
    private int _pendingBrightness = -1;
    private int _brightnessWorkerRunning;
    private bool _disposed;

    internal TouchpadFeatureHost(App app)
    {
        _app = app;
        TouchpadGestureConfiguration configuration =
            app.UserSettings.Current.TouchpadGestures ??
            (TouchpadGestureConfiguration.Default with { Enabled = false });

        var actions = new GestureActionRouter(
            new NativeInputService(),
            new MediaSessionService(),
            () => app.State.Brightness,
            QueueBrightness,
            StepKeyboardAsync,
            StepPerformance);

        bool x9 = string.Equals(app.State.MachineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(app.State.MachineType, "21Q7", StringComparison.OrdinalIgnoreCase);

        _gestures = new TouchpadGestureService(
            configuration,
            actions,
            fallbackWidthMm: x9 ? 135.0 : 100.0,
            fallbackHeightMm: x9 ? 80.0 : 60.0);
        _gestures.GestureChanged += signal => GestureChanged?.Invoke(signal);
        _gestures.TouchpadDetected += geometry => TouchpadDetected?.Invoke(geometry);
    }

    internal event Action<GestureSignal>? GestureChanged;
    internal event Action<TouchpadGeometry>? TouchpadDetected;

    internal TouchpadGestureConfiguration Configuration => _gestures.Configuration;
    internal TouchpadGeometry? Geometry => _gestures.Geometry;
    internal bool IsInputRunning => _gestures.IsRunning;
    internal TouchpadHapticStatus HapticStatus => _haptics.Read();

    internal bool EnsureInputStarted() => !_disposed && _gestures.Start();

    internal void UpdateConfiguration(TouchpadGestureConfiguration configuration)
    {
        TouchpadGestureConfiguration sanitized = configuration.Sanitize();
        _app.UserSettings.Update(settings => settings with { TouchpadGestures = sanitized });
        _gestures.UpdateConfiguration(sanitized);
        if (sanitized.Enabled)
            EnsureInputStarted();
    }

    internal bool SetHapticEnabled(bool enabled) => _haptics.SetFeedbackEnabled(enabled);
    internal bool SetHapticIntensity(int intensity) => _haptics.SetFeedbackIntensity(intensity);
    internal bool SetClickForceSensitivity(int sensitivity) => _haptics.SetClickForceSensitivity(sensitivity);

    internal void CancelCurrent(string reason) => _gestures.CancelCurrent(reason);

    private void QueueBrightness(int value)
    {
        Interlocked.Exchange(ref _pendingBrightness, Math.Clamp(value, 0, 100));
        if (Interlocked.CompareExchange(ref _brightnessWorkerRunning, 1, 0) != 0)
            return;

        _ = Task.Run(ProcessBrightnessQueueAsync);
    }

    private async Task ProcessBrightnessQueueAsync()
    {
        int lastApplied = -1;
        try
        {
            while (!_disposed)
            {
                int target = Volatile.Read(ref _pendingBrightness);
                if (target < 0 || target == lastApplied)
                    break;

                bool changed = _app.DisplayService.SetBrightness(target);
                if (changed)
                {
                    lastApplied = target;
                    await _app.Dispatcher.InvokeAsync(() => _app.State.Brightness = target);
                }
                else
                {
                    break;
                }

                await Task.Delay(16).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            Interlocked.Exchange(ref _brightnessWorkerRunning, 0);
            int pending = Volatile.Read(ref _pendingBrightness);
            if (!_disposed && pending >= 0 && pending != lastApplied &&
                Interlocked.CompareExchange(ref _brightnessWorkerRunning, 1, 0) == 0)
            {
                _ = Task.Run(ProcessBrightnessQueueAsync);
            }
        }
    }

    private async Task StepKeyboardAsync(int direction)
    {
        if (!_app.State.CanKeyboardBacklight)
            return;

        string status = _app.State.KeyboardStatus;
        int index = status.Contains("Off", StringComparison.OrdinalIgnoreCase) ? 0 :
            status.Contains("Low", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        index = Math.Clamp(index + Math.Sign(direction), 0, 2);
        string level = index switch { 0 => "Off", 1 => "Low", _ => "High" };
        await _app.Dispatcher.InvokeAsync(() => _app.SetKeyboardStaticLevelAsync(level)).Task.Unwrap();
    }

    private bool StepPerformance(int direction)
    {
        ThinkControlPowerMode[] modes =
        [ThinkControlPowerMode.Quiet, ThinkControlPowerMode.Balanced, ThinkControlPowerMode.Performance];
        int current = Array.FindIndex(modes,
            mode => string.Equals(mode.ToString(), _app.State.SelectedMode, StringComparison.OrdinalIgnoreCase));
        if (current < 0)
            current = 1;
        int next = Math.Clamp(current + Math.Sign(direction), 0, modes.Length - 1);
        if (next == current)
            return true;

        bool result = false;
        _app.Dispatcher.Invoke(() => result = _app.SetPowerMode(modes[next]));
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _gestures.Dispose();
    }
}
