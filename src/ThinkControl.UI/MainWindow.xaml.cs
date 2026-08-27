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

    private readonly App _app;
    private bool _forceClose;
    private bool _explicitViewSwitch;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Dashboard.Initialize(app);
        Closing += OnClosing;
        SourceInitialized += (_, _) => ApplyNativeCornerClip();
    }

    /// <summary>
    /// Marks the next hide as an intentional Compact -> Advanced transition. This
    /// prevents Window.Deactivated from starting a second overlapping hide animation.
    /// </summary>
    public void BeginExplicitViewSwitch()
    {
        _explicitViewSwitch = true;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
    }

    public void ShowNearTray(bool animate)
    {
        // Startup decides whether Compact should be shown at all. Once this method
        // is called it represents a real show request and must never silently reject
        // the user because Advanced happened to be the configured startup view.
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

    public void HideAnimated()
    {
        if (!IsVisible)
        {
            _explicitViewSwitch = false;
            return;
        }

        // Explicit layout switching should be deterministic and never race the
        // Deactivated event. Hide immediately; the destination surface supplies
        // the visual continuity instead of cross-fading two top-level windows.
        if (_explicitViewSwitch)
        {
            _explicitViewSwitch = false;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            Hide();
            return;
        }

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
        // During an explicit Compact -> Advanced transition OpenAdvanced owns the
        // hide. Letting Deactivated start a second animation was a race that could
        // leave the shell hidden or visually corrupted.
        if (_explicitViewSwitch)
            return;

        if (IsVisible)
            HideAnimated();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
