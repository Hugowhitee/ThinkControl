using System.Windows;
using System.Windows.Media;
using ThinkControl.Core.Touchpad;
using WpfPoint = System.Windows.Point;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Non-interactive auxiliary overlay for gesture visuals that are not selectable
/// editor zones. The six selectable edge/corner zones are owned entirely by
/// <see cref="TouchpadVisualizer"/>; this overlay only renders the optional bounded
/// track-center play/pause target.
/// </summary>
internal sealed class TouchpadGestureZoneOverlay : FrameworkElement
{
    private const double PadCornerRadius = 4;
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private TouchpadGeometry _geometry = new(0, 13500, 0, 8000, 135, 80, true);
    private GestureSignal? _signal;

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

    internal GestureSignal? Signal
    {
        get => _signal;
        set { _signal = value; InvalidateVisual(); }
    }

    public TouchpadGestureZoneOverlay()
    {
        Focusable = false;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Rect pad = PadRect();
        if (pad.Width <= 0 || pad.Height <= 0)
            return;

        Brush muted = ResourceBrush("Tc.TextMuted", Brushes.Gray);
        Brush accent = ResourceBrush("Tc.Accent", Brushes.Red);
        Brush surface = ResourceBrush("Tc.SurfaceAlt", Brushes.Black);

        dc.PushClip(new RectangleGeometry(pad, PadCornerRadius, PadCornerRadius));
        DrawTrackCenterZone(dc, pad, surface, muted, accent);
        dc.Pop();
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
        const double gap = 3.0;
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
