using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.Core.Touchpad;
using WpfPoint = System.Windows.Point;
using WpfCursors = System.Windows.Input.Cursors;

namespace ThinkControl.UI.Controls;

public sealed class TouchpadVisualizer : FrameworkElement
{
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private TouchpadGeometry _geometry = new(0, 13500, 0, 8000, 135, 80, true);
    private IReadOnlyList<TouchContact> _contacts = Array.Empty<TouchContact>();
    private TouchpadEdge _selectedEdge = TouchpadEdge.Top;
    private GestureSignal? _signal;

    public TouchpadVisualizer()
    {
        MinHeight = 250;
        Cursor = WpfCursors.Hand;
    }

    public event Action<TouchpadEdge>? EdgeSelected;

    public TouchpadGestureConfiguration Configuration
    {
        get => _configuration;
        set { _configuration = value.Sanitize(); InvalidateVisual(); }
    }

    public TouchpadGeometry Geometry
    {
        get => _geometry;
        set { _geometry = value; InvalidateVisual(); }
    }

    public TouchpadEdge SelectedEdge
    {
        get => _selectedEdge;
        set { _selectedEdge = value; InvalidateVisual(); }
    }

    public void SetTestFrame(IReadOnlyList<TouchContact> contacts, GestureSignal? signal)
    {
        _contacts = contacts;
        _signal = signal;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        Brush surface = ResourceBrush("Tc.SurfaceAlt", Brushes.DimGray);
        Brush border = ResourceBrush("Tc.BorderStrong", Brushes.Gray);
        Brush muted = ResourceBrush("Tc.TextMuted", Brushes.Gray);
        Brush faint = ResourceBrush("Tc.TextFaint", Brushes.Gray);
        Brush accent = ResourceBrush("Tc.Accent", Brushes.Red);
        Brush text = ResourceBrush("Tc.Text", Brushes.White);

        Rect pad = PadRect();
        dc.DrawRoundedRectangle(surface, new Pen(border, 1), pad, 10, 10);

        // Keep the four selectors physically separated. The previous edge-width
        // overlays met at the corners and visually read as intersecting lines.
        DrawSelector(dc, pad, TouchpadEdge.Top, accent, muted, faint, text);
        DrawSelector(dc, pad, TouchpadEdge.Left, accent, muted, faint, text);
        DrawSelector(dc, pad, TouchpadEdge.Right, accent, muted, faint, text);
        DrawSelector(dc, pad, TouchpadEdge.Bottom, accent, muted, faint, text);

        DrawLabel(dc, "TOUCHPAD", new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 - 8),
            11, muted, centered: true);
        string size = _geometry.PhysicalSizeEstimated
            ? $"~{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm"
            : $"{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm";
        DrawLabel(dc, size, new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 + 11),
            9.5, faint, centered: true);

        foreach (TouchContact contact in _contacts.Where(static c => c.IsDown))
        {
            double x = pad.Left + ((contact.X - _geometry.XLogicalMin) / (double)_geometry.XRange) * pad.Width;
            double y = pad.Top + ((contact.Y - _geometry.YLogicalMin) / (double)_geometry.YRange) * pad.Height;
            Brush dot = contact.Confidence ? accent : muted;
            dc.DrawEllipse(dot, null, new WpfPoint(x, y), 4.5, 4.5);
        }

        if (_signal is not null)
        {
            string status = _signal.Phase.ToString();
            if (!string.IsNullOrWhiteSpace(_signal.Reason))
                status += " · " + _signal.Reason;
            DrawLabel(dc, status, new WpfPoint(pad.Left, pad.Bottom + 15), 9.5, muted);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        WpfPoint point = e.GetPosition(this);
        Rect pad = PadRect();
        if (!pad.Contains(point))
            return;

        double left = point.X - pad.Left;
        double right = pad.Right - point.X;
        double top = point.Y - pad.Top;
        double bottom = pad.Bottom - point.Y;
        double min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        TouchpadEdge edge = min == left ? TouchpadEdge.Left :
            min == right ? TouchpadEdge.Right :
            min == top ? TouchpadEdge.Top : TouchpadEdge.Bottom;

        SelectedEdge = edge;
        EdgeSelected?.Invoke(edge);
        e.Handled = true;
    }

    private void DrawSelector(
        DrawingContext dc,
        Rect pad,
        TouchpadEdge edge,
        Brush accent,
        Brush muted,
        Brush faint,
        Brush text)
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        bool selected = edge == _selectedEdge;
        bool enabled = binding.Action != GestureActionKind.Disabled;

        const double gap = 18;
        const double inset = 7;
        double thickness = selected ? 15 : 12;
        Rect zone = edge switch
        {
            TouchpadEdge.Left => new Rect(pad.Left + inset, pad.Top + gap, thickness, Math.Max(1, pad.Height - gap * 2)),
            TouchpadEdge.Right => new Rect(pad.Right - inset - thickness, pad.Top + gap, thickness, Math.Max(1, pad.Height - gap * 2)),
            TouchpadEdge.Top => new Rect(pad.Left + gap, pad.Top + inset, Math.Max(1, pad.Width - gap * 2), thickness),
            _ => new Rect(pad.Left + gap, pad.Bottom - inset - thickness, Math.Max(1, pad.Width - gap * 2), thickness)
        };

        Brush baseBrush = selected ? accent : enabled ? muted : faint;
        Brush fill = TransparentClone(baseBrush, selected ? 0.82 : enabled ? 0.24 : 0.10);
        dc.DrawRoundedRectangle(fill, null, zone, thickness / 2, thickness / 2);

        string label = ActionLabel(binding.Action);
        Brush labelBrush = selected ? accent : enabled ? muted : faint;
        if (edge == TouchpadEdge.Top)
            DrawLabel(dc, label, new WpfPoint(pad.Left + pad.Width / 2, pad.Top + 34), 9.5, labelBrush, true);
        else if (edge == TouchpadEdge.Bottom)
            DrawLabel(dc, label, new WpfPoint(pad.Left + pad.Width / 2, pad.Bottom - 34), 9.5, labelBrush, true);
        else if (edge == TouchpadEdge.Left)
            DrawLabel(dc, label, new WpfPoint(pad.Left + 30, pad.Top + pad.Height / 2), 9.2, labelBrush, centered: true);
        else
            DrawLabel(dc, label, new WpfPoint(pad.Right - 30, pad.Top + pad.Height / 2), 9.2, labelBrush, centered: true);
    }

    private Rect PadRect()
    {
        const double outerX = 34;
        const double outerTop = 20;
        const double bottomReserve = 40;
        double availableWidth = Math.Max(100, ActualWidth - outerX * 2);
        double availableHeight = Math.Max(80, ActualHeight - outerTop - bottomReserve);
        double aspect = _geometry.EffectiveWidthMm / _geometry.EffectiveHeightMm;
        double width = Math.Min(availableWidth, availableHeight * aspect);
        double height = width / aspect;
        if (height > availableHeight)
        {
            height = availableHeight;
            width = height * aspect;
        }
        return new Rect((ActualWidth - width) / 2, outerTop, width, height);
    }

    private void DrawLabel(
        DrawingContext dc,
        string value,
        WpfPoint point,
        double size,
        Brush brush,
        bool centered = false,
        bool rightAligned = false)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formatted = new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            pixelsPerDip);
        double x = centered ? point.X - formatted.Width / 2 : rightAligned ? point.X - formatted.Width : point.X;
        double y = point.Y - formatted.Height / 2;
        dc.DrawText(formatted, new WpfPoint(x, y));
    }

    private Brush ResourceBrush(string key, Brush fallback) =>
        TryFindResource(key) as Brush ?? fallback;

    private static Brush TransparentClone(Brush source, double opacity)
    {
        Brush clone = source.CloneCurrentValue();
        clone.Opacity = opacity;
        clone.Freeze();
        return clone;
    }

    private static string ActionLabel(GestureActionKind action) => action switch
    {
        GestureActionKind.Volume => "Volume",
        GestureActionKind.Brightness => "Brightness",
        GestureActionKind.MediaSeek => "Media seek",
        GestureActionKind.PreviousNextTrack => "Tracks",
        GestureActionKind.PlayPause => "Play / pause",
        GestureActionKind.KeyboardBacklight => "Keyboard light",
        GestureActionKind.PerformanceMode => "Performance",
        GestureActionKind.CustomShortcut => "Shortcut",
        _ => "Off"
    };
}
