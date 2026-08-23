using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.Core.Touchpad;

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
        MinHeight = 260;
        Cursor = Cursors.Hand;
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
        Brush text = ResourceBrush("Tc.Text", Brushes.White);
        Brush muted = ResourceBrush("Tc.TextMuted", Brushes.Gray);
        Brush accent = ResourceBrush("Tc.Accent", Brushes.Red);

        Rect pad = PadRect();
        dc.DrawRoundedRectangle(surface, new Pen(border, 1), pad, 8, 8);

        DrawEdge(dc, pad, TouchpadEdge.Left, accent, border, muted);
        DrawEdge(dc, pad, TouchpadEdge.Right, accent, border, muted);
        DrawEdge(dc, pad, TouchpadEdge.Top, accent, border, muted);
        DrawEdge(dc, pad, TouchpadEdge.Bottom, accent, border, muted);

        DrawLabel(dc, "TOUCHPAD", new Point(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 - 7),
            11, muted, centered: true);
        string size = _geometry.PhysicalSizeEstimated
            ? $"~{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm"
            : $"{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm";
        DrawLabel(dc, size, new Point(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 + 11),
            9.5, muted, centered: true);

        foreach (TouchContact contact in _contacts.Where(static c => c.IsDown))
        {
            double x = pad.Left + ((contact.X - _geometry.XLogicalMin) / (double)_geometry.XRange) * pad.Width;
            double y = pad.Top + ((contact.Y - _geometry.YLogicalMin) / (double)_geometry.YRange) * pad.Height;
            Brush dot = contact.Confidence ? accent : muted;
            dc.DrawEllipse(dot, null, new Point(x, y), 5, 5);
        }

        if (_signal is not null)
        {
            string status = _signal.Phase.ToString();
            if (!string.IsNullOrWhiteSpace(_signal.Reason))
                status += " · " + _signal.Reason;
            DrawLabel(dc, status, new Point(pad.Left, pad.Bottom + 14), 10, muted);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Point point = e.GetPosition(this);
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

    private void DrawEdge(
        DrawingContext dc,
        Rect pad,
        TouchpadEdge edge,
        Brush accent,
        Brush border,
        Brush muted)
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        double widthPx = edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? Math.Clamp(_configuration.EdgeWidthMm / _geometry.EffectiveWidthMm * pad.Width, 4, pad.Width * 0.2)
            : Math.Clamp(_configuration.EdgeWidthMm / _geometry.EffectiveHeightMm * pad.Height, 4, pad.Height * 0.22);

        Rect zone = edge switch
        {
            TouchpadEdge.Left => new Rect(pad.Left, pad.Top, widthPx, pad.Height),
            TouchpadEdge.Right => new Rect(pad.Right - widthPx, pad.Top, widthPx, pad.Height),
            TouchpadEdge.Top => new Rect(pad.Left, pad.Top, pad.Width, widthPx),
            _ => new Rect(pad.Left, pad.Bottom - widthPx, pad.Width, widthPx)
        };

        bool selected = edge == _selectedEdge;
        bool enabled = binding.Action != GestureActionKind.Disabled;
        Brush fill = TransparentClone(selected ? accent : border, selected ? 0.24 : enabled ? 0.10 : 0.035);
        Pen pen = new(selected ? accent : border, selected ? 1.4 : 0.7);
        dc.DrawRectangle(fill, pen, zone);

        string label = ActionLabel(binding.Action);
        if (edge == TouchpadEdge.Top)
            DrawLabel(dc, label, new Point(pad.Left + pad.Width / 2, pad.Top + widthPx + 8), 10, selected ? accent : muted, true);
        else if (edge == TouchpadEdge.Bottom)
            DrawLabel(dc, label, new Point(pad.Left + pad.Width / 2, pad.Bottom - widthPx - 18), 10, selected ? accent : muted, true);
        else
        {
            double x = edge == TouchpadEdge.Left ? pad.Left + widthPx + 7 : pad.Right - widthPx - 7;
            DrawLabel(dc, label, new Point(x, pad.Top + pad.Height / 2), 9.5, selected ? accent : muted,
                centered: edge == TouchpadEdge.Left, rightAligned: edge == TouchpadEdge.Right);
        }
    }

    private Rect PadRect()
    {
        const double outerX = 34;
        const double outerTop = 24;
        const double bottomReserve = 46;
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
        Point point,
        double size,
        Brush brush,
        bool centered = false,
        bool rightAligned = false)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formatted = new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            pixelsPerDip);
        double x = centered ? point.X - formatted.Width / 2 : rightAligned ? point.X - formatted.Width : point.X;
        double y = point.Y - formatted.Height / 2;
        dc.DrawText(formatted, new Point(x, y));
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
