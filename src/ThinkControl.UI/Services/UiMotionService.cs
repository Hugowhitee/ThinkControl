using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseEventHandler = System.Windows.Input.MouseEventHandler;
using WpfPoint = System.Windows.Point;

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
            typeof(WpfButtonBase),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnPress),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(WpfButtonBase),
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnRelease),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(WpfButtonBase),
            UIElement.MouseLeaveEvent,
            new WpfMouseEventHandler(OnLeave),
            handledEventsToo: true);
    }

    private static void OnPress(object sender, WpfMouseButtonEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || sender is not WpfButtonBase button || !button.IsEnabled)
            return;
        Animate(button, 0.985, 62);
    }

    private static void OnRelease(object sender, WpfMouseButtonEventArgs e)
    {
        if (sender is WpfButtonBase button)
            Animate(button, 1.0, 105);
    }

    private static void OnLeave(object sender, WpfMouseEventArgs e)
    {
        if (sender is WpfButtonBase button)
            Animate(button, 1.0, 105);
    }

    private static void Animate(WpfButtonBase button, double target, int milliseconds)
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
            button.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
        }
        else
        {
            return;
        }

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scale.ScaleX, target, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scale.ScaleY, target, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
    }
}
