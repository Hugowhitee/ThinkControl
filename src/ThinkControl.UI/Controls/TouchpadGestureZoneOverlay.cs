using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.Core.Touchpad;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Interaction overlay for gesture zones that are spatially distinct from the four
/// edge bands. It deliberately shares the physical corner-lane policy with Core so
/// what the user sees and clicks is also where the real launch recognizer starts.
/// </summary>
internal sealed class TouchpadGestureZoneOverlay : FrameworkElement
{
    private const double PadCornerRadius = 4;
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private TouchpadGeometry _geometry = new(0, 13500, 0, 8000, 135, 80, true);
    private TouchpadCorner? _selectedCorner;
    private TouchpadCorner? _hoverCorner;
    private GestureSignal? _signal;

    internal event Action<TouchpadCorner>? CornerSelected;

    internal TouchpadGestureConfiguration Configuration
    {
        get => _configuration;
        set { _configuration = value.Sanitize(); InvalidateVisual(); }
    }

    internal TouchpadGeometry Geometry
    {
        get => _geometry;
        set { _geometry = value; InvalidateVisual(); }
    }

    internal TouchpadCorner? SelectedCorner
    {
        get => _selectedCorner;
        set { _selectedCorner = value; InvalidateVisual(); }
    }

    internal GestureSignal? Signal
    {
        get => _signal;
        set { _signal = value; InvalidateVisual(); }
    }

    public TouchpadGestureZoneOverlay()
    {
        Cursor = Cursors.Arrow;
        Focusable = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Rect pad = PadRect();
        if (pad.Width <= 0 || pad.Height <= 0)
            return;

        Brush muted = ResourceBrush("Tc.TextMuted", Brushes.Gray);
        Brush faint = ResourceBrush("Tc.TextFaint", Brushes.DimGray);
        Brush text = ResourceBrush("Tc.Text", Brushes.White);
        Brush accent = ResourceBrush("Tc.Accent", Brushes.Red);
        Brush surface = ResourceBrush("Tc.SurfaceAlt", Brushes.Black);

        dc.PushClip(new RectangleGeometry(pad, PadCornerRadius, PadCornerRadius));
        DrawCornerLane(dc, pad, TouchpadCorner.TopLeft, muted, faint, text, accent);
        DrawCornerLane(dc, pad, TouchpadCorner.TopRight, muted, faint, text, accent);
        DrawTrackCenterZone(dc, pad, surface, muted, accent);
        dc.Pop();
    }

    protected override void OnMouseMove(WpfMouseEventArgs e)
    {
        base.OnMouseMove(e);
        TouchpadCorner? hover = HitCorner(e.GetPosition(this));
        if (hover == _hoverCorner)
            return;
        _hoverCorner = hover;
        Cursor = hover.HasValue ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(WpfMouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverCorner = null;
        Cursor = Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        TouchpadCorner? corner = HitCorner(e.GetPosition(this));
        if (corner is not TouchpadCorner selected)
            return;

        SelectedCorner = selected;
        CornerSelected?.Invoke(selected);
        e.Handled = true;
    }

    private void DrawCornerLane(
        DrawingContext dc,
        Rect pad,
        TouchpadCorner corner,
        Brush muted,
        Brush faint,
        Brush text,
        Brush accent)
    {
        GestureActionKind action = _configuration.LaunchFor(corner);
        bool enabled = action != GestureActionKind.Disabled;
        bool selected = _selectedCorner == corner;
        bool hovered = _hoverCorner == corner;
        bool live = _signal?.Corner == corner &&
                    _signal.Phase is GesturePhase.Candidate or GesturePhase.Claimed or GesturePhase.Active;

        Brush source = live ? accent : selected || hovered || enabled ? muted : faint;
        double opacity = live ? 1.0 : selected ? 0.82 : hovered ? 0.68 : enabled ? 0.54 : 0.30;
        var pen = new Pen(TransparentClone(source, opacity), live ? 2.4 : selected ? 2.0 : 1.55)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        Geometry outline = BuildCornerOutline(pad, corner);
        dc.DrawGeometry(null, pen, outline);

        WpfPoint glyphCenter = CornerLanePoint(pad, corner, TouchpadCornerZonePolicy.LengthMm * 0.57, 0);
        DrawLaunchGlyph(dc, action, glyphCenter, TransparentClone(live ? accent : enabled ? text : faint, live ? 1 : enabled ? 0.76 : 0.40));
    }

    private Geometry BuildCornerOutline(Rect pad, TouchpadCorner corner)
    {
        double half = TouchpadCornerZonePolicy.HalfWidthMm;
        double start = TouchpadCornerZonePolicy.StartInsetMm;
        double capCenter = TouchpadCornerZonePolicy.LengthMm - half;

        WpfPoint outerA = CornerLanePoint(pad, corner, start, -half);
        WpfPoint innerA = CornerLanePoint(pad, corner, capCenter, -half);
        WpfPoint innerB = CornerLanePoint(pad, corner, capCenter, half);
        WpfPoint outerB = CornerLanePoint(pad, corner, start, half);
        double radiusPx = Math.Max(2, half / _geometry.EffectiveWidthMm * pad.Width);

        var figure = new PathFigure { StartPoint = outerA, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new LineSegment(innerA, true));
        figure.Segments.Add(new ArcSegment(
            innerB,
            new Size(radiusPx, radiusPx),
            0,
            isLargeArc: false,
            SweepDirection.Clockwise,
            isStroked: true));
        figure.Segments.Add(new LineSegment(outerB, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private WpfPoint CornerLanePoint(Rect pad, TouchpadCorner corner, double alongMm, double acrossMm)
    {
        const double invSqrt2 = 0.7071067811865476;
        double localX = (alongMm - acrossMm) * invSqrt2;
        double localY = (alongMm + acrossMm) * invSqrt2;
        double x = localX / _geometry.EffectiveWidthMm * pad.Width;
        double y = localY / _geometry.EffectiveHeightMm * pad.Height;
        return corner == TouchpadCorner.TopLeft
            ? new WpfPoint(pad.Left + x, pad.Top + y)
            : new WpfPoint(pad.Right - x, pad.Top + y);
    }

    private static void DrawLaunchGlyph(DrawingContext dc, GestureActionKind action, WpfPoint point, Brush brush)
    {
        var pen = new Pen(brush, 1.45)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        Rect body = new(point.X - 8, point.Y - 6, 16, 12);

        if (action == GestureActionKind.Disabled)
        {
            dc.DrawEllipse(null, pen, point, 2.1, 2.1);
            return;
        }

        dc.DrawRoundedRectangle(null, pen, body, 1.5, 1.5);
        if (action == GestureActionKind.OpenThinkControl)
        {
            dc.DrawLine(pen, new WpfPoint(body.Left + 5, body.Top), new WpfPoint(body.Left + 5, body.Bottom));
            return;
        }

        // Advanced is a full application surface: use a small header plus two
        // content columns rather than the old unrelated monitor/window glyph.
        dc.DrawLine(pen, new WpfPoint(body.Left, body.Top + 3.5), new WpfPoint(body.Right, body.Top + 3.5));
        dc.DrawLine(pen, new WpfPoint(point.X, body.Top + 3.5), new WpfPoint(point.X, body.Bottom));
    }

    private void DrawTrackCenterZone(DrawingContext dc, Rect pad, Brush surface, Brush muted, Brush accent)
    {
        TouchpadEdge? edge = Enum.GetValues<TouchpadEdge>()
            .FirstOrDefault(candidate => _configuration.BindingFor(candidate).Action == GestureActionKind.PreviousNextTrack);
        if (edge is not TouchpadEdge trackEdge ||
            _configuration.BindingFor(trackEdge).Action != GestureActionKind.PreviousNextTrack ||
            !_configuration.TrackCenterPlayPauseEnabled)
        {
            return;
        }

        // The center target exists only while the option is enabled. Outside this
        // bounded rectangle the entire edge remains a Previous / Next swipe lane.
        Rect zone = TrackCenterRect(pad, trackEdge);
        bool live = _signal?.Edge == trackEdge &&
                    _signal.Action == GestureActionKind.PreviousNextTrack &&
                    _signal.Phase == GesturePhase.Candidate &&
                    TrackCenterGesturePolicy.IsInsideCenterZone(_signal.EdgePosition01);
        Brush source = live ? accent : muted;
        var pen = new Pen(TransparentClone(source, live ? 1 : 0.72), live ? 2.0 : 1.35)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawRoundedRectangle(TransparentClone(surface, 0.96), pen, zone, 3, 3);

        // Intentionally use only the pause bars here. A combined play/pause glyph
        // resembles a skip icon at this size and does not communicate a bounded
        // center target clearly.
        WpfPoint center = new(zone.Left + zone.Width / 2, zone.Top + zone.Height / 2);
        double barLength = Math.Clamp(Math.Min(zone.Width, zone.Height) * 0.46, 8, 14);
        double gap = 3.0;
        dc.DrawLine(pen, new WpfPoint(center.X - gap, center.Y - barLength / 2), new WpfPoint(center.X - gap, center.Y + barLength / 2));
        dc.DrawLine(pen, new WpfPoint(center.X + gap, center.Y - barLength / 2), new WpfPoint(center.X + gap, center.Y + barLength / 2));
    }

    private Rect TrackCenterRect(Rect pad, TouchpadEdge edge)
    {
        double start = TrackCenterGesturePolicy.CenterZoneStart;
        double end = TrackCenterGesturePolicy.CenterZoneEnd;
        double edgeWidthX = Math.Clamp(_configuration.EdgeWidthMm / _geometry.EffectiveWidthMm * pad.Width, 8, pad.Width / 3);
        double edgeWidthY = Math.Clamp(_configuration.EdgeWidthMm / _geometry.EffectiveHeightMm * pad.Height, 8, pad.Height / 3);

        return edge switch
        {
            TouchpadEdge.Top => new Rect(pad.Left + pad.Width * start, pad.Top + 2, pad.Width * (end - start), Math.Max(22, edgeWidthY - 4)),
            TouchpadEdge.Bottom => new Rect(pad.Left + pad.Width * start, pad.Bottom - Math.Max(22, edgeWidthY - 4) - 2, pad.Width * (end - start), Math.Max(22, edgeWidthY - 4)),
            TouchpadEdge.Left => new Rect(pad.Left + 2, pad.Top + pad.Height * start, Math.Max(22, edgeWidthX - 4), pad.Height * (end - start)),
            _ => new Rect(pad.Right - Math.Max(22, edgeWidthX - 4) - 2, pad.Top + pad.Height * start, Math.Max(22, edgeWidthX - 4), pad.Height * (end - start))
        };
    }

    private TouchpadCorner? HitCorner(WpfPoint point)
    {
        Rect pad = PadRect();
        if (!pad.Contains(point))
            return null;

        double localY = (point.Y - pad.Top) / Math.Max(1, pad.Height) * _geometry.EffectiveHeightMm;
        double leftX = (point.X - pad.Left) / Math.Max(1, pad.Width) * _geometry.EffectiveWidthMm;
        if (TouchpadCornerZonePolicy.ContainsLocal(leftX, localY))
            return TouchpadCorner.TopLeft;

        double rightX = (pad.Right - point.X) / Math.Max(1, pad.Width) * _geometry.EffectiveWidthMm;
        return TouchpadCornerZonePolicy.ContainsLocal(rightX, localY)
            ? TouchpadCorner.TopRight
            : null;
    }

    private Rect PadRect()
    {
        const double outerX = 18;
        const double outerTop = 12;
        const double bottomReserve = 18;
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

    private Brush ResourceBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private static Brush TransparentClone(Brush source, double opacity)
    {
        Brush clone = source.CloneCurrentValue();
        clone.Opacity = opacity;
        clone.Freeze();
        return clone;
    }
}
