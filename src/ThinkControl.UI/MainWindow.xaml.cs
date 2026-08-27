using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace ThinkControl.UI;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _forceClose;
    private bool _initialShowHandled;
    private bool _explicitShowRequested;
    private bool _explicitViewSwitch;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Dashboard.Initialize(app);
        Closing += OnClosing;
    }

    /// <summary>
    /// Startup suppression is only for the automatic first shell presentation.
    /// A user explicitly requesting Compact must always be allowed to see it,
    /// even when Advanced is configured as the default opening view.
    /// </summary>
    public void AllowExplicitShow() => _explicitShowRequested = true;

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
        if (!_initialShowHandled)
        {
            _initialShowHandled = true;
            bool explicitRequest = _explicitShowRequested;
            _explicitShowRequested = false;
            if (!explicitRequest && _app.ShouldSuppressInitialCompactLaunch)
                return;
        }
        else
        {
            _explicitShowRequested = false;
        }

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
}
