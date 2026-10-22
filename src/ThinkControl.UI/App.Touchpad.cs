using System.Windows;
using System.Windows.Threading;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services.Touchpad;

namespace ThinkControl.UI;

public partial class App
{
    private static readonly TimeSpan[] TouchpadStartupRetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(7),
        TimeSpan.FromSeconds(15)
    ];

    private TouchpadFeatureHost? _touchpadFeature;
    private DispatcherTimer? _touchpadStartupRetryTimer;
    private int _touchpadStartupRetryCount;

    internal TouchpadFeatureHost TouchpadFeature =>
        _touchpadFeature ??= new TouchpadFeatureHost(this);

    internal bool GetEffectiveTouchpadGesturesEnabled() =>
        _touchpadFeature?.EffectiveGesturesEnabled ??
        UserSettings.Current.TouchpadGestures?.Enabled == true;

    internal void ApplyTouchpadGestureModeOverride(bool enabled) =>
        TouchpadFeature.ApplyTransientGestureEnabled(enabled);

    internal void ClearTouchpadGestureModeOverride() =>
        TouchpadFeature.ApplyTransientGestureEnabled(null);

    internal void StartConfiguredTouchpadInputForStartup()
    {
        if (UserSettings.Current.TouchpadGestures?.Enabled != true)
            return;

        // Tray-only startup has no WPF activation event. Attempt Raw Input
        // registration immediately; if Windows is still preparing the session,
        // retry a few times without introducing a permanent background poller.
        TryStartTouchpadInputWithRecovery(startupCritical: IsTrayOnlyLaunch());
    }

    private void TryStartTouchpadInputWithRecovery(bool startupCritical)
    {
        if (!GetEffectiveTouchpadGesturesEnabled())
        {
            StopTouchpadStartupRetries();
            return;
        }

        bool started;
        try
        {
            started = TouchpadFeature.EnsureInputStarted(startupCritical: startupCritical);
        }
        catch
        {
            // A transient Raw Input/HID initialization error must not stop the
            // entire ThinkControl user session during Windows sign-in.
            started = false;
        }

        if (started)
        {
            StopTouchpadStartupRetries();
            return;
        }

        ScheduleTouchpadStartupRetry();
    }

    private void ScheduleTouchpadStartupRetry()
    {
        if (_touchpadStartupRetryCount >= TouchpadStartupRetryDelays.Length)
        {
            RecordDiagnostic(new DiagnosticEvent(
                DateTimeOffset.UtcNow,
                "touchpad.input_start_failed",
                Capability: "TouchpadGestures",
                Provider: "WindowsRawInput",
                Success: false,
                ErrorCode: "raw_input_registration_unavailable"));
            return;
        }

        _touchpadStartupRetryTimer ??= new DispatcherTimer(DispatcherPriority.Background);
        _touchpadStartupRetryTimer.Tick -= OnTouchpadStartupRetryTick;
        _touchpadStartupRetryTimer.Tick += OnTouchpadStartupRetryTick;
        _touchpadStartupRetryTimer.Interval =
            TouchpadStartupRetryDelays[_touchpadStartupRetryCount++];
        _touchpadStartupRetryTimer.Start();
    }

    private void OnTouchpadStartupRetryTick(object? sender, EventArgs e)
    {
        _touchpadStartupRetryTimer?.Stop();
        TryStartTouchpadInputWithRecovery(startupCritical: true);
    }

    private void StopTouchpadStartupRetries()
    {
        _touchpadStartupRetryTimer?.Stop();
        _touchpadStartupRetryCount = 0;
    }

    private void OnTouchpadApplicationActivated(object? sender, EventArgs e)
    {
        // Activation also recovers registration after device/session changes.
        if (_trayIcon is null || CompactWindow is null)
            return;

        if (GetEffectiveTouchpadGesturesEnabled())
            TryStartTouchpadInputWithRecovery(startupCritical: false);
    }

    private void OnTouchpadApplicationExit(object? sender, ExitEventArgs e)
    {
        StopTouchpadStartupRetries();
        if (_touchpadStartupRetryTimer is not null)
        {
            _touchpadStartupRetryTimer.Tick -= OnTouchpadStartupRetryTick;
            _touchpadStartupRetryTimer = null;
        }
        try { _touchpadFeature?.Dispose(); }
        catch { }
        try { DisposeAudioSafety(); }
        catch { }
    }
}
