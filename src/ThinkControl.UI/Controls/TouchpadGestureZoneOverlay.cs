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
        Brush accent = ResourceBrush("Tc.Accent", Brushes.Red);
        Brush surface = ResourceBrush("Tc.SurfaceAlt", Brushes.Black);

        dc.PushClip(new RectangleGeometry(pad, PadCornerRadius, PadCornerRadius));
        DrawCornerGuide(dc, pad, TouchpadCorner.TopLeft, muted, faint, accent);
        DrawCornerGuide(dc, pad, TouchpadCorner.TopRight, muted, faint, accent);
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
        // Hover is deliberately not a visual selection state. The two idle corner
        // guides remain identical; click/selection and real live recognition are the
        // only states allowed to make one side visually stronger than the other.
    }

    protected override void OnMouseLeave(WpfMouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverCorner = null;
        Cursor = Cursors.Arrow;
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

    private void DrawCornerGuide(
        DrawingContext dc,
        Rect pad,
        TouchpadCorner corner,
        Brush muted,
        Brush faint,
        Brush accent)
    {
        GestureActionKind action = _configuration.LaunchFor(corner);
        bool enabled = action != GestureActionKind.Disabled;
        bool selected = _selectedCorner == corner;
        bool live = _signal?.Corner == corner &&
                    _signal.Phase is GesturePhase.Candidate or GesturePhase.Claimed or GesturePhase.Active;

        // Both idle lanes use the exact same hue and geometry. Configuration only
        // changes their quiet opacity; pointer hover never makes one corner look like
        // a different control. Selection/live ownership is the deliberate exception.
        Brush source = live ? accent : selected ? muted : enabled ? muted : faint;
        double opacity = live ? 1.0 : selected ? 0.86 : enabled ? 0.22 : 0.10;
        double width = live ? 2.25 : selected ? 1.8 : 1.15;
        var boundaryPen = new Pen(TransparentClone(source, opacity), width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        double half = TouchpadCornerZonePolicy.HalfWidthMm;
        double start = TouchpadCornerZonePolicy.StartInsetMm + 1.2;
        double end = TouchpadCornerZonePolicy.LengthMm - half;
        WpfPoint outerA = CornerLanePoint(pad, corner, start, -half);
        WpfPoint innerA = CornerLanePoint(pad, corner, end, -half);
        WpfPoint outerB = CornerLanePoint(pad, corner, start, half);
        WpfPoint innerB = CornerLanePoint(pad, corner, end, half);
        dc.DrawLine(boundaryPen, outerA, innerA);
        dc.DrawLine(boundaryPen, outerB, innerB);
        dc.DrawLine(boundaryPen, innerA, innerB);

        if (selected || live)
        {
            var centerPen = new Pen(
                TransparentClone(live ? accent : muted, live ? 0.92 : 0.48),
                live ? 1.7 : 1.25)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            WpfPoint centerStart = CornerLanePoint(pad, corner, start + 3.0, 0);
            WpfPoint centerEnd = CornerLanePoint(pad, corner, end - 4.0, 0);
            dc.DrawLine(centerPen, centerStart, centerEnd);
        }
    }

    private WpfPoint CornerLanePoint(Rect pad, TouchpadCorner corner, double alongMm, double acrossMm)
    {
        const double invSqrt2 = 0.7071067811865476;
        double localX = (alongMm - acrossMm) * invSqrt2;
        double localY = (alongMm + acrossMm) * invSqrt2;
        double x = localX / _geometry.EffectiveWidthMm * pad.Width;
        double y = localY / _geometry.EffectiveHeightMm * pad.Height;

        // Generate the left geometry once and mirror its final pixel coordinate for
        // the right side. This makes the two rendered guides exact visual mirrors,
        // independent of DPI/layout rounding.
        WpfPoint leftPoint = new(pad.Left + x, pad.Top + y);
        return corner == TouchpadCorner.TopLeft
            ? leftPoint
            : new WpfPoint(pad.Left + pad.Right - leftPoint.X, leftPoint.Y);
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
