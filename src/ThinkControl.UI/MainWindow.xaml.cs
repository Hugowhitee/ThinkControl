using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace ThinkControl.UI;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _forceClose;
    private bool _initialShowHandled;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Dashboard.Initialize(app);
        Closing += OnClosing;
    }

    public void ShowNearTray(bool animate)
    {
        if (!_initialShowHandled)
        {
            _initialShowHandled = true;
            if (_app.ShouldSuppressInitialCompactLaunch)
                return;
        }

        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 14;
        Top = workArea.Bottom - Height - 14;

        if (!IsVisible)
            Show();

        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;

        if (animate)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(125))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }
        else
        {
            Opacity = 1;
        }
    }

    public void HideAnimated()
    {
        if (!IsVisible)
            return;

        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(95));
        animation.Completed += (_, _) =>
        {
            Hide();
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        };
        BeginAnimation(OpacityProperty, animation);
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
        if (IsVisible)
            HideAnimated();
    }
}
