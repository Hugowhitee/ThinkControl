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
        Activated += (_, _) => _app.OnCompactActivated();
        SourceInitialized += (_, _) => ApplyNativeCornerClip();
    }

    public void ShowNearTray(bool animate)
    {
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
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
