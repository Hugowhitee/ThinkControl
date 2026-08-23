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
    private const double PadCornerRadius = 4;

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
        dc.DrawRoundedRectangle(surface, null, pad, PadCornerRadius, PadCornerRadius);

        // The recognizer accepts a contact anywhere inside EdgeWidthMm from a
        // physical edge. Render those exact full-edge bands instead of decorative
        // rounded pills, including the real overlapping corner candidate areas.
        dc.PushClip(new RectangleGeometry(pad, PadCornerRadius, PadCornerRadius));
        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>().Where(edge => edge != _selectedEdge))
            DrawEdgeBand(dc, pad, edge, accent, muted, faint);
        DrawEdgeBand(dc, pad, _selectedEdge, accent, muted, faint);
        dc.Pop();

        dc.DrawRoundedRectangle(null, new Pen(border, 1), pad, PadCornerRadius, PadCornerRadius);
        DrawEdgeLabels(dc, pad, accent, muted, faint);

        DrawLabel(dc, "START AT AN EDGE", new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 - 8),
            10.5, muted, centered: true);
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

        TouchpadEdge? edge = HitEdge(pad, point);
        if (edge is null)
            return;

        SelectedEdge = edge.Value;
        EdgeSelected?.Invoke(edge.Value);
        e.Handled = true;
    }

    private void DrawEdgeBand(
        DrawingContext dc,
        Rect pad,
        TouchpadEdge edge,
        Brush accent,
        Brush muted,
        Brush faint)
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        bool selected = edge == _selectedEdge;
        bool enabled = binding.Action != GestureActionKind.Disabled;
        Rect zone = EdgeBandRect(pad, edge);

        Brush source = selected ? accent : enabled ? muted : faint;
        double opacity = selected ? 0.48 : enabled ? 0.13 : 0.045;
        dc.DrawRectangle(TransparentClone(source, opacity), null, zone);

        // A crisp inner threshold makes the activation width readable without
        // turning the zone into a fake button.
        Pen threshold = new(TransparentClone(source, selected ? 0.92 : enabled ? 0.34 : 0.18), selected ? 1.5 : 1.0);
        switch (edge)
        {
            case TouchpadEdge.Left:
                dc.DrawLine(threshold, new WpfPoint(zone.Right, zone.Top), new WpfPoint(zone.Right, zone.Bottom));
                break;
            case TouchpadEdge.Right:
                dc.DrawLine(threshold, new WpfPoint(zone.Left, zone.Top), new WpfPoint(zone.Left, zone.Bottom));
                break;
            case TouchpadEdge.Top:
                dc.DrawLine(threshold, new WpfPoint(zone.Left, zone.Bottom), new WpfPoint(zone.Right, zone.Bottom));
                break;
            case TouchpadEdge.Bottom:
                dc.DrawLine(threshold, new WpfPoint(zone.Left, zone.Top), new WpfPoint(zone.Right, zone.Top));
                break;
        }
    }

    private void DrawEdgeLabels(DrawingContext dc, Rect pad, Brush accent, Brush muted, Brush faint)
    {
        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
        {
            TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
            bool selected = edge == _selectedEdge;
            bool enabled = binding.Action != GestureActionKind.Disabled;
            Brush labelBrush = selected ? accent : enabled ? muted : faint;
            string label = ActionLabel(binding.Action);
            Rect band = EdgeBandRect(pad, edge);

            WpfPoint point = edge switch
            {
                TouchpadEdge.Top => new(pad.Left + pad.Width / 2, band.Bottom + 12),
                TouchpadEdge.Bottom => new(pad.Left + pad.Width / 2, band.Top - 12),
                TouchpadEdge.Left => new(band.Right + 24, pad.Top + pad.Height / 2),
                _ => new(band.Left - 24, pad.Top + pad.Height / 2)
            };
            DrawLabel(dc, label, point, edge is TouchpadEdge.Left or TouchpadEdge.Right ? 9.0 : 9.4, labelBrush, centered: true);
        }
    }

    private Rect EdgeBandRect(Rect pad, TouchpadEdge edge)
    {
        double xWidth = Math.Clamp(_configuration.EdgeWidthMm / _geometry.EffectiveWidthMm * pad.Width, 4, pad.Width / 3);
        double yWidth = Math.Clamp(_configuration.EdgeWidthMm / _geometry.EffectiveHeightMm * pad.Height, 4, pad.Height / 3);

        return edge switch
        {
            TouchpadEdge.Left => new Rect(pad.Left, pad.Top, xWidth, pad.Height),
            TouchpadEdge.Right => new Rect(pad.Right - xWidth, pad.Top, xWidth, pad.Height),
            TouchpadEdge.Top => new Rect(pad.Left, pad.Top, pad.Width, yWidth),
            _ => new Rect(pad.Left, pad.Bottom - yWidth, pad.Width, yWidth)
        };
    }

    private TouchpadEdge? HitEdge(Rect pad, WpfPoint point)
    {
        var candidates = Enum.GetValues<TouchpadEdge>()
            .Where(edge => EdgeBandRect(pad, edge).Contains(point))
            .ToArray();
        if (candidates.Length == 0)
            return null;
        if (candidates.Length == 1)
            return candidates[0];

        // Corners genuinely belong to two recognizer candidates. For selection UI,
        // choose whichever physical edge the click is closest to.
        return candidates
            .OrderBy(edge => edge switch
            {
                TouchpadEdge.Left => point.X - pad.Left,
                TouchpadEdge.Right => pad.Right - point.X,
                TouchpadEdge.Top => point.Y - pad.Top,
                _ => pad.Bottom - point.Y
            })
            .First();
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
