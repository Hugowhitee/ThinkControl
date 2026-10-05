using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ThinkControl.UI.Controls;

/// <summary>Retains the magnitude of small precision touchpad wheel messages.</summary>
public sealed class AdvancedScrollViewer : ScrollViewer
{
    private double _pendingWheelDistance;
    private bool _wheelFlushQueued;

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;
        if (e.Delta == 0)
        {
            e.Handled = true;
            return;
        }

        // Ordinary wheel notches and child controls keep WPF's standard behavior.
        // WPF otherwise turns every small touchpad delta into a full wheel notch.
        if (Math.Abs((long)e.Delta) >= Mouse.MouseWheelDeltaForOneLine ||
            CanContentScroll || Keyboard.Modifiers != ModifierKeys.None)
        {
            base.OnMouseWheel(e);
            return;
        }

        int lines = SystemParameters.WheelScrollLines;
        double notchDistance = lines < 0 ? ViewportHeight : lines * 16d;
        _pendingWheelDistance -= notchDistance * e.Delta / Mouse.MouseWheelDeltaForOneLine;
        if (!_wheelFlushQueued)
        {
            _wheelFlushQueued = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                double distance = _pendingWheelDistance;
                _pendingWheelDistance = 0;
                _wheelFlushQueued = false;
                if (IsLoaded && IsVisible)
                    ScrollToVerticalOffset(VerticalOffset + distance);
            }));
        }
        e.Handled = true;
    }
}
