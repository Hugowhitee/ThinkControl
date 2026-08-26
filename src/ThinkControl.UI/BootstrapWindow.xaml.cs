using System.Windows;
using System.Windows.Media.Animation;

namespace ThinkControl.UI;

public partial class BootstrapWindow : Window
{
    public BootstrapWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => StartMotion();
    }

    private void StartMotion()
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;

        double travel = Math.Max(190, ProgressTrack.ActualWidth + 92);
        var animation = new DoubleAnimation
        {
            From = -92,
            To = travel,
            Duration = TimeSpan.FromMilliseconds(1050),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        ProgressTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }
}
