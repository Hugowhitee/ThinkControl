using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ThinkControl.UI;

public partial class MainWindow : Window
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    private readonly App _app;
    private bool _forceClose;
    private long _hideGeneration;

    internal bool SuppressExternalAutoHideForShellSmoke { get; set; }
    internal System.Windows.Controls.Button ExpandButtonForShellSmoke => Dashboard.ExpandButtonForShellSmoke;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Dashboard.Initialize(app);
        Closing += OnClosing;
        Activated += (_, _) => _app.OnCompactActivated();
        SourceInitialized += (_, _) => ApplyNativeCornerClip();
        Dispatcher.UnhandledException += Dispatcher_UnhandledException;
    }

    public void ShowNearTray(bool animate)
    {
        // Every show invalidates a previously queued/animated hide. Without this,
        // a completed Deactivated fade can hide a flyout that has already been
        // re-opened by the tray or an internal view transition.
        Interlocked.Increment(ref _hideGeneration);
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 14;
        Top = workArea.Bottom - Height - 14;

        if (!IsVisible)
            Show();

        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;

        if (animate && SystemParameters.ClientAreaAnimation)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(125))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
            Opacity = 1;
        }
    }

    /// <summary>
    /// Immediate hide used only while App.ViewTransitions owns both primary
    /// surfaces. There is deliberately no fade and no local transition flag: the
    /// App coordinator is the single authority for that lifecycle operation.
    /// </summary>
    internal void HideForViewTransition()
    {
        Interlocked.Increment(ref _hideGeneration);
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (IsVisible)
            Hide();
    }

    public void HideAnimated()
    {
        long generation = Interlocked.Increment(ref _hideGeneration);
        if (!IsVisible)
            return;

        if (!SystemParameters.ClientAreaAnimation)
        {
            Hide();
            return;
        }

        BeginAnimation(OpacityProperty, null);
        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(95))
        {
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            // A newer show/hide request owns the window now. Never let an old
            // animation completion hide the newly active Compact surface.
            if (generation != Volatile.Read(ref _hideGeneration))
                return;

            if (IsVisible)
                Hide();
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        };
        BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Dispatcher.UnhandledException -= Dispatcher_UnhandledException;
        Close();
    }

    private void ApplyNativeCornerClip()
    {
        // WindowChrome's WPF corner radius only affects the drawn frame. On a
        // custom-chrome window the HWND itself can still expose a square background
        // pixel outside that curve. Ask DWM to round the actual native surface as
        // well so fill, border and hit-test shape end on the same corner.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int preference = DwmWindowCornerRound;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 and unsupported DWM configurations keep the existing
            // WindowChrome fallback; corner polish must never affect startup.
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose)
            return;

        e.Cancel = true;
        HideAnimated();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (SuppressExternalAutoHideForShellSmoke)
            return;

        if (_app.KeepCompactVisibleForInternalWindow())
            return;

        _app.OnCompactDeactivated();
    }

    private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Persist the exception before WPF follows its normal unhandled-exception
        // behavior. Do not mark it handled: hiding a real crash behind a catch would
        // recreate the diagnostic ambiguity that made alpha.23 difficult to trust.
        _app.RecordShellException("dispatcher", e.Exception);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
