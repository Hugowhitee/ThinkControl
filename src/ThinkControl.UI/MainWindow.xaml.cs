using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace ThinkControl.UI;

public partial class MainWindow : Window
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly App _app;
    private bool _forceClose;
    private long _hideGeneration;

    internal bool SuppressExternalAutoHideForShellSmoke { get; set; }
    internal System.Windows.Controls.Button ExpandButtonForShellSmoke => Dashboard.ExpandButtonForShellSmoke;
    internal void PrepareMetricEditorForSnapshot() => Dashboard.PrepareMetricEditorForSnapshot();

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Dashboard.Initialize(app);
        Closing += OnClosing;
        Activated += (_, _) =>
        {
            _app.OnCompactActivated();
            EnsureTopmostNoActivate();
        };
        SourceInitialized += (_, _) =>
        {
            ApplyNativeCornerClip();
            EnsureTopmostNoActivate();
        };
    }

    public void ShowNearTray(bool animate)
    {
        Interlocked.Increment(ref _hideGeneration);
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        SetTransitionPending(false);

        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 14;
        Top = workArea.Bottom - Height - 14;

        // Compact is intentionally different from Advanced: while it is visible it
        // behaves like a persistent utility surface and remains above normal app
        // windows. Set the managed flag before Show and reinforce the native z-order
        // without stealing focus after shell/external-window transitions.
        Topmost = true;
        if (!IsVisible)
            Show();

        WindowState = WindowState.Normal;
        Activate();
        EnsureTopmostNoActivate();

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

    internal void SetTransitionPending(bool pending)
    {
        TransitionOutline.BeginAnimation(OpacityProperty, null);
        if (!pending)
        {
            TransitionOutline.Visibility = Visibility.Collapsed;
            TransitionOutline.Opacity = 0.88;
            return;
        }

        TransitionOutline.Visibility = Visibility.Visible;
        if (!SystemParameters.ClientAreaAnimation)
        {
            TransitionOutline.Opacity = 0.9;
            return;
        }

        var pulse = new DoubleAnimation(0.42, 0.96, TimeSpan.FromMilliseconds(430))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        TransitionOutline.BeginAnimation(OpacityProperty, pulse, HandoffBehavior.SnapshotAndReplace);
    }

    internal void HideForViewTransition()
    {
        Interlocked.Increment(ref _hideGeneration);
        SetTransitionPending(false);
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (IsVisible)
            Hide();
    }

    public void HideAnimated()
    {
        long generation = Interlocked.Increment(ref _hideGeneration);
        SetTransitionPending(false);
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
        Close();
    }

    private void ApplyNativeCornerClip()
    {
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
        }
    }

    private void EnsureTopmostNoActivate()
    {
        if (!IsSourceInitialized || !IsVisible)
            return;

        // Owned ThinkControl surfaces (notably update/attention popups) must remain
        // above Compact. Reasserting HWND_TOPMOST while one is visible can reorder
        // that owned surface behind its owner, so let normal WPF ownership win there.
        if (Application.Current?.Windows.OfType<Window>()
                .Any(window => window.IsVisible && ReferenceEquals(window.Owner, this)) == true)
        {
            return;
        }

        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                _ = SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
        catch
        {
            // WPF Topmost=True remains the managed fallback.
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
        _app.OnCompactDeactivated();
        EnsureTopmostNoActivate();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
