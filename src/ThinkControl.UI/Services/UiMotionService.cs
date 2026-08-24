using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ThinkControl.UI.Services;

internal static class UiMotionService
{
    private static bool _enabled;

    internal static void Enable()
    {
        if (_enabled)
            return;
        _enabled = true;

        EventManager.RegisterClassHandler(
            typeof(ButtonBase),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPress),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(ButtonBase),
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnRelease),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(ButtonBase),
            UIElement.MouseLeaveEvent,
            new MouseEventHandler(OnLeave),
            handledEventsToo: true);
    }

    private static void OnPress(object sender, MouseButtonEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || sender is not ButtonBase button || !button.IsEnabled)
            return;
        Animate(button, 0.985, 62);
    }

    private static void OnRelease(object sender, MouseButtonEventArgs e)
    {
        if (sender is ButtonBase button)
            Animate(button, 1.0, 105);
    }

    private static void OnLeave(object sender, MouseEventArgs e)
    {
        if (sender is ButtonBase button)
            Animate(button, 1.0, 105);
    }

    private static void Animate(ButtonBase button, double target, int milliseconds)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            if (button.RenderTransform is ScaleTransform instant)
            {
                instant.ScaleX = target;
                instant.ScaleY = target;
            }
            return;
        }

        ScaleTransform scale;
        if (button.RenderTransform is ScaleTransform existing)
        {
            scale = existing;
        }
        else if (button.RenderTransform == Transform.Identity || button.RenderTransform is null)
        {
            scale = new ScaleTransform(1, 1);
            button.RenderTransform = scale;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        else
        {
            // Do not overwrite feature-specific transforms.
            return;
        }

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scale.ScaleX, target, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scale.ScaleY, target, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
    }
}
