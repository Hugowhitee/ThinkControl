using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkControl.Core.Touchpad;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfCursors = System.Windows.Input.Cursors;

namespace ThinkControl.UI.Controls;

public sealed class TouchpadVisualizer : FrameworkElement
{
    private const double PadCornerRadius = 4;
    private static readonly TimeSpan TrailLifetime = TimeSpan.FromMilliseconds(720);
    private static readonly TimeSpan ReleasedValueHold = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan ReleasedValueFade = TimeSpan.FromMilliseconds(420);
    private const int MaxTrailPoints = 34;
    private const double MaxConnectedJumpMm = 18.0;

    private readonly List<TrailPoint> _trail = [];
    private readonly HashSet<int> _previousActiveContactIds = [];
    private readonly DispatcherTimer _trailTimer;
    private readonly DispatcherTimer _feedbackTimer;
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private TouchpadGeometry _geometry = new(0, 13500, 0, 8000, 135, 80, true);
    private IReadOnlyList<TouchContact> _contacts = Array.Empty<TouchContact>();
    private TouchpadZoneSelection _selectedZone = TouchpadZoneSelection.ForEdge(TouchpadEdge.Top);
    private TouchpadZoneSelection? _hoverZone;
    private GestureSignal? _signal;
    private TouchpadEdge? _activeFeedbackEdge;
    private string? _activeFeedbackText;
    private TouchpadEdge? _releasedFeedbackEdge;
    private string? _releasedFeedbackText;
    private DateTimeOffset _releasedFeedbackStarted;

    public TouchpadVisualizer()
    {
        MinHeight = 300;
        Cursor = WpfCursors.Arrow;
        TouchpadActionVisualCatalog.ValidateCurrentActionSet();

        _trailTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(45)
        };
        _trailTimer.Tick += (_, _) =>
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - TrailLifetime;
            _trail.RemoveAll(point => point.Timestamp < cutoff);
            if (_trail.Count == 0 && !_contacts.Any(static contact => contact.IsDown))
                _trailTimer.Stop();
            InvalidateVisual();
        };

        _feedbackTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(32)
        };
        _feedbackTimer.Tick += (_, _) =>
        {
            if (ReleasedFeedbackOpacity() <= 0)
            {
                ClearReleasedGestureFeedback();
                return;
            }
            InvalidateVisual();
        };

        Unloaded += (_, _) =>
        {
            _trailTimer.Stop();
            _feedbackTimer.Stop();
        };
    }

    public event Action<TouchpadZoneSelection>? ZoneSelected;

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

    public TouchpadZoneSelection SelectedZone
    {
        get => _selectedZone;
        set { _selectedZone = value.Sanitize(); InvalidateVisual(); }
    }

    public void SetTestFrame(IReadOnlyList<TouchContact> contacts, GestureSignal? signal)
    {
        _contacts = contacts;
        _signal = signal;

        if (signal is { Phase: GesturePhase.Claimed or GesturePhase.Active })
            ClearReleasedGestureFeedback();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var currentIds = contacts
            .Where(static contact => contact.IsDown && contact.Confidence)
            .Select(static contact => contact.ContactId)
            .ToHashSet();

        foreach (TouchContact contact in contacts.Where(static contact => contact.IsDown && contact.Confidence))
        {
            TrailPoint? previous = FindLastTrailPoint(contact.ContactId);
            bool startsSegment = !_previousActiveContactIds.Contains(contact.ContactId) || previous is null;

            if (!startsSegment && previous is TrailPoint last && PhysicalJumpMm(last, contact) > MaxConnectedJumpMm)
                startsSegment = true;

            bool movedEnough = previous is null ||
                Math.Abs(previous.Value.X - contact.X) + Math.Abs(previous.Value.Y - contact.Y) >= 8;
            if (startsSegment || movedEnough)
                _trail.Add(new TrailPoint(contact.ContactId, contact.X, contact.Y, now, startsSegment));
        }

        _previousActiveContactIds.Clear();
        foreach (int id in currentIds)
            _previousActiveContactIds.Add(id);

        while (_trail.Count > MaxTrailPoints)
            _trail.RemoveAt(0);

        if ((_trail.Count > 0 || contacts.Any(static contact => contact.IsDown)) && !_trailTimer.IsEnabled)
            _trailTimer.Start();
        InvalidateVisual();
    }

    public void ShowActiveGestureValue(TouchpadEdge? edge, string? value)
    {
        if (edge is not TouchpadEdge resolved || string.IsNullOrWhiteSpace(value))
        {
            ClearActiveGestureFeedback();
            return;
        }
        _activeFeedbackEdge = resolved;
        _activeFeedbackText = value.Trim();
        InvalidateVisual();
    }

    public void ClearActiveGestureFeedback()
    {
        _activeFeedbackEdge = null;
        _activeFeedbackText = null;
        InvalidateVisual();
    }

    public void ShowReleasedGestureValue(TouchpadEdge? edge, string? value)
    {
        if (edge is not TouchpadEdge resolved)
        {
            ClearReleasedGestureFeedback();
            return;
        }
        ShowReleasedGestureValue(resolved, value);
    }

    public void ShowReleasedGestureValue(TouchpadEdge edge, string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ClearReleasedGestureFeedback();
            return;
        }

        _releasedFeedbackEdge = edge;
        _releasedFeedbackText = text;
        _releasedFeedbackStarted = DateTimeOffset.UtcNow;
        if (!_feedbackTimer.IsEnabled)
            _feedbackTimer.Start();
        InvalidateVisual();
    }

    public void ClearReleasedGestureFeedback()
    {
        _releasedFeedbackEdge = null;
        _releasedFeedbackText = null;
        _feedbackTimer.Stop();
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

        Rect pad = PadRect();
        dc.DrawRoundedRectangle(surface, null, pad, PadCornerRadius, PadCornerRadius);

        dc.PushClip(new RectangleGeometry(pad, PadCornerRadius, PadCornerRadius));
        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
            DrawEdgeBand(dc, pad, edge, accent, muted, faint);
        DrawCornerZone(dc, pad, TouchpadCorner.TopLeft, accent, muted, faint);
        DrawCornerZone(dc, pad, TouchpadCorner.TopRight, accent, muted, faint);
        DrawTrackLane(dc, pad, muted, accent);
        DrawTrail(dc, pad, accent);
        dc.Pop();

        dc.DrawRoundedRectangle(null, new Pen(border, 1), pad, PadCornerRadius, PadCornerRadius);
        DrawEdgeLabels(dc, pad, accent, muted, faint);
        DrawCornerLabel(dc, pad, TouchpadCorner.TopLeft, accent, muted, faint);
        DrawCornerLabel(dc, pad, TouchpadCorner.TopRight, accent, muted, faint);

        DrawLabel(dc, ZoneName(_selectedZone).ToUpperInvariant(),
            new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 - 8),
            TypographyScale.Caption, muted, centered: true);
        string size = _geometry.PhysicalSizeEstimated
            ? $"~{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm"
            : $"{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm";
        DrawLabel(dc, $"{size} · click a zone to edit", new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 + 12),
            TypographyScale.Caption, faint, centered: true);

        foreach (TouchContact contact in _contacts.Where(static c => c.IsDown))
        {
            WpfPoint point = ContactPoint(pad, contact.X, contact.Y);
            Brush dot = contact.Confidence ? accent : muted;
            dc.DrawEllipse(TransparentClone(dot, 0.18), null, point, 10, 10);
            dc.DrawEllipse(dot, null, point, 4.8, 4.8);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        WpfPoint point = e.GetPosition(this);
        Rect pad = PadRect();
        TouchpadZoneSelection? hover = pad.Contains(point) ? HitZone(pad, point) : null;
        if (hover != _hoverZone)
        {
            _hoverZone = hover;
            Cursor = hover.HasValue ? WpfCursors.Hand : WpfCursors.Arrow;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverZone = null;
        Cursor = WpfCursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        WpfPoint point = e.GetPosition(this);
        Rect pad = PadRect();
        if (!pad.Contains(point))
            return;

        TouchpadZoneSelection? zone = HitZone(pad, point);
        if (zone is not TouchpadZoneSelection selected)
            return;

        SelectedZone = selected;
        ZoneSelected?.Invoke(selected);
        e.Handled = true;
    }

    internal void ValidateCornerSymmetryForSnapshot()
    {
        Rect pad = PadRect();
        if (pad.Width <= 0 || pad.Height <= 0)
            throw new InvalidOperationException("Touchpad visualizer has no arranged pad geometry for symmetry validation.");

        CornerGuide left = CornerGuideFor(pad, TouchpadCorner.TopLeft);
        CornerGuide right = CornerGuideFor(pad, TouchpadCorner.TopRight);
        WpfPoint[] leftPoints =
        [
            left.OuterA, left.InnerA, left.OuterB, left.InnerB,
            left.CenterStart, left.CenterEnd, left.GuardTop, left.GuardSide, left.InnerTip
        ];
        WpfPoint[] rightPoints =
        [
            right.OuterA, right.InnerA, right.OuterB, right.InnerB,
            right.CenterStart, right.CenterEnd, right.GuardTop, right.GuardSide, right.InnerTip
        ];

        for (int i = 0; i < leftPoints.Length; i++)
        {
            if (Math.Abs(leftPoints[i].Y - rightPoints[i].Y) > 0.01 ||
                Math.Abs((leftPoints[i].X + rightPoints[i].X) - (pad.Left + pad.Right)) > 0.01)
            {
                throw new InvalidOperationException($"Touchpad corner guide point {i} is not an exact horizontal mirror.");
            }
        }

        double leftLengthA = Distance(left.OuterA, left.InnerA);
        double rightLengthA = Distance(right.OuterA, right.InnerA);
        double leftLengthB = Distance(left.OuterB, left.InnerB);
        double rightLengthB = Distance(right.OuterB, right.InnerB);
        if (Math.Abs(leftLengthA - rightLengthA) > 0.01 || Math.Abs(leftLengthB - rightLengthB) > 0.01)
            throw new InvalidOperationException("Touchpad corner guide line lengths drifted left/right.");

        Rect leftBounds = Bounds(leftPoints);
        Rect rightBounds = Bounds(rightPoints);
        if (Math.Abs(leftBounds.Width - rightBounds.Width) > 0.01 || Math.Abs(leftBounds.Height - rightBounds.Height) > 0.01)
            throw new InvalidOperationException("Touchpad corner guide bounds drifted left/right.");

        double leftAngle = Math.Atan2(left.CenterEnd.Y - left.CenterStart.Y, left.CenterEnd.X - left.CenterStart.X);
        double rightAngle = Math.Atan2(right.CenterEnd.Y - right.CenterStart.Y, right.CenterEnd.X - right.CenterStart.X);
        if (Math.Abs(leftAngle - (Math.PI - rightAngle)) > 0.0001)
            throw new InvalidOperationException("Touchpad corner guide angles are not mirrored.");

        Geometry leftFill = CornerZoneGeometry(pad, TouchpadCorner.TopLeft);
        Geometry rightFill = CornerZoneGeometry(pad, TouchpadCorner.TopRight);
        const int samples = 12;
        for (int ix = 0; ix <= samples; ix++)
        {
            for (int iy = 0; iy <= samples; iy++)
            {
                WpfPoint leftSample = new(
                    pad.Left + leftFill.Bounds.Width * ix / samples,
                    pad.Top + leftFill.Bounds.Height * iy / samples);
                WpfPoint rightSample = new(pad.Left + pad.Right - leftSample.X, leftSample.Y);
                if (leftFill.FillContains(leftSample) != rightFill.FillContains(rightSample))
                    throw new InvalidOperationException("Touchpad corner fill occupancy is not an exact horizontal mirror.");
            }
        }
    }

    private TrailPoint? FindLastTrailPoint(int contactId)
    {
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            if (_trail[i].ContactId == contactId)
                return _trail[i];
        }
        return null;
    }

    private double PhysicalJumpMm(TrailPoint previous, TouchContact current)
    {
        double dx = Math.Abs(current.X - previous.X) / (double)Math.Max(1, _geometry.XRange) * _geometry.EffectiveWidthMm;
        double dy = Math.Abs(current.Y - previous.Y) / (double)Math.Max(1, _geometry.YRange) * _geometry.EffectiveHeightMm;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void DrawTrail(DrawingContext dc, Rect pad, Brush accent)
    {
        if (_trail.Count == 0)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 1; i < _trail.Count; i++)
        {
            TrailPoint previous = _trail[i - 1];
            TrailPoint current = _trail[i];
            if (current.StartsSegment || previous.ContactId != current.ContactId)
                continue;

            double dxMm = Math.Abs(current.X - previous.X) / (double)Math.Max(1, _geometry.XRange) * _geometry.EffectiveWidthMm;
            double dyMm = Math.Abs(current.Y - previous.Y) / (double)Math.Max(1, _geometry.YRange) * _geometry.EffectiveHeightMm;
            if (Math.Sqrt(dxMm * dxMm + dyMm * dyMm) > MaxConnectedJumpMm)
                continue;

            double age = Math.Clamp((now - current.Timestamp).TotalMilliseconds / TrailLifetime.TotalMilliseconds, 0, 1);
            double opacity = 0.55 * (1 - age);
            if (opacity <= 0.02)
                continue;
            var pen = new Pen(TransparentClone(accent, opacity), 2.6)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(pen,
                ContactPoint(pad, previous.X, previous.Y),
                ContactPoint(pad, current.X, current.Y));
        }
    }

    private bool IsActiveEdge(TouchpadEdge edge) =>
        _signal is { Edge: not null, Corner: null, Phase: GesturePhase.Claimed or GesturePhase.Active } && _signal.Edge == edge;

    private bool IsCandidateEdge(TouchpadEdge edge) =>
        _signal is { Edge: not null, Corner: null, Phase: GesturePhase.Candidate } && _signal.Edge == edge;

    private bool IsActiveCorner(TouchpadCorner corner) =>
        _signal is { Corner: not null, Phase: GesturePhase.Claimed or GesturePhase.Active } && _signal.Corner == corner;

    private bool IsCandidateCorner(TouchpadCorner corner) =>
        _signal is { Corner: not null, Phase: GesturePhase.Candidate } && _signal.Corner == corner;

    private void DrawEdgeBand(DrawingContext dc, Rect pad, TouchpadEdge edge, Brush accent, Brush muted, Brush faint)
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        bool active = IsActiveEdge(edge);
        bool candidate = IsCandidateEdge(edge);
        bool selected = _selectedZone.Edge == edge;
        bool hovered = _hoverZone?.Edge == edge;
        bool enabled = binding.Action != GestureActionKind.Disabled;
        Rect zone = EdgeBandRect(pad, edge);
        TouchpadActionVisualSpec spec = TouchpadActionVisualCatalog.Get(binding.Action);

        if (spec.Motion == TouchpadGestureMotionKind.Inward)
        {
            DrawInwardLane(dc, pad, edge, accent, muted, active, candidate, selected, hovered);
            return;
        }

        Brush fillSource = active || candidate ? accent : muted;
        double opacity = ZoneFillOpacity(active, candidate, selected, hovered, enabled);
        Geometry visibleBand = EdgeBandVisualGeometry(pad, edge);
        dc.DrawGeometry(TransparentClone(fillSource, opacity), null, visibleBand);

        Pen threshold = ZoneBoundaryPen(active, candidate, selected, hovered, enabled, accent, muted, faint);
        dc.PushClip(visibleBand);
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
        dc.Pop();
    }

    private void DrawCornerZone(DrawingContext dc, Rect pad, TouchpadCorner corner, Brush accent, Brush muted, Brush faint)
    {
        GestureActionKind action = _configuration.LaunchFor(corner);
        bool enabled = action != GestureActionKind.Disabled;
        bool reverseClose = _configuration.ReverseCloseFor(corner);
        bool active = IsActiveCorner(corner);
        bool candidate = IsCandidateCorner(corner);
        bool selected = _selectedZone.Corner == corner;
        bool hovered = _hoverZone?.Corner == corner;
        bool outwardLive = (active || candidate) &&
                           _signal?.Corner == corner &&
                           _signal.CornerDirection == CornerGestureDirection.Outward;
        CornerGuide guide = CornerGuideFor(pad, corner);

        Brush fillSource = active || candidate ? accent : muted;
        double fillOpacity = ZoneFillOpacity(active, candidate, selected, hovered, enabled);
        Geometry fill = CornerZoneGeometry(pad, corner);
        dc.DrawGeometry(TransparentClone(fillSource, fillOpacity), null, fill);

        Pen boundary = ZoneBoundaryPen(active, candidate, selected, hovered, enabled, accent, muted, faint);
        dc.DrawGeometry(null, boundary, CornerBoundaryGeometry(pad, corner));

        if (enabled || selected || hovered || candidate || active)
        {
            Brush source = active || candidate ? accent : muted;
            double arrowOpacity = active ? 0.96 : candidate ? 0.78 : selected ? 0.62 : hovered ? 0.48 : enabled ? 0.34 : 0.22;
            var arrowPen = new Pen(TransparentClone(source, arrowOpacity), active ? 2.1 : candidate ? 1.8 : 1.45)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            if (outwardLive)
                DrawArrow(dc, guide.CenterEnd, guide.CenterStart, arrowPen);
            else
                DrawArrow(dc, guide.CenterStart, guide.CenterEnd, arrowPen);

            if (reverseClose && !outwardLive)
                DrawArrowHead(dc, guide.CenterEnd, guide.CenterStart, new Pen(TransparentClone(source, arrowOpacity * 0.62), arrowPen.Thickness));
        }
    }

    private void DrawTrackLane(DrawingContext dc, Rect pad, Brush muted, Brush accent)
    {
        if (!_configuration.TrackCenterPlayPauseEnabled)
            return;

        TouchpadEdge? trackEdge = Enum.GetValues<TouchpadEdge>()
            .Where(edge => _configuration.BindingFor(edge).Action == GestureActionKind.PreviousNextTrack)
            .Select(static edge => (TouchpadEdge?)edge)
            .FirstOrDefault();
        if (trackEdge is not TouchpadEdge edge)
            return;

        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        Rect band = EdgeBandRect(pad, edge);
        Rect centerZone = TrackCenterRect(pad, edge);
        Geometry visibleBand = EdgeBandVisualGeometry(pad, edge);
        bool vertical = edge is TouchpadEdge.Left or TouchpadEdge.Right;
        bool selected = _selectedZone.Edge == edge;
        bool hovered = _hoverZone?.Edge == edge;
        bool active = IsActiveEdge(edge);
        bool centerLive = _signal?.Edge == edge &&
                          _signal.Action == GestureActionKind.PreviousNextTrack &&
                          _signal.Phase == GesturePhase.Candidate &&
                          TrackCenterGesturePolicy.IsInsideCenterZone(_signal.EdgePosition01);

        double centerOpacity = centerLive ? 0.26 : selected ? 0.16 : hovered ? 0.14 : 0.10;
        var separator = new Pen(
            TransparentClone(centerLive ? accent : muted, centerLive ? 0.82 : selected || hovered ? 0.46 : 0.32),
            centerLive ? 1.5 : 1.0)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat
        };

        dc.PushClip(visibleBand);
        dc.DrawRectangle(TransparentClone(centerLive ? accent : muted, centerOpacity), null, centerZone);
        if (vertical)
        {
            dc.DrawLine(separator, new WpfPoint(band.Left, centerZone.Top), new WpfPoint(band.Right, centerZone.Top));
            dc.DrawLine(separator, new WpfPoint(band.Left, centerZone.Bottom), new WpfPoint(band.Right, centerZone.Bottom));
        }
        else
        {
            dc.DrawLine(separator, new WpfPoint(centerZone.Left, band.Top), new WpfPoint(centerZone.Left, band.Bottom));
            dc.DrawLine(separator, new WpfPoint(centerZone.Right, band.Top), new WpfPoint(centerZone.Right, band.Bottom));
        }
        dc.Pop();

        TouchpadActionVisualSpec spec = TouchpadActionVisualCatalog.Get(GestureActionKind.PreviousNextTrack);
        TouchpadVisualCue firstCue;
        TouchpadVisualCue lastCue;
        if (vertical)
        {
            firstCue = binding.Inverted ? spec.Negative : spec.Positive;
            lastCue = binding.Inverted ? spec.Positive : spec.Negative;
        }
        else
        {
            firstCue = binding.Inverted ? spec.Positive : spec.Negative;
            lastCue = binding.Inverted ? spec.Negative : spec.Positive;
        }

        double physicalDelta = vertical
            ? -(_signal?.DeltaMm ?? 0)
            : (_signal?.DeltaMm ?? 0);
        bool firstActive = active && (vertical ? physicalDelta > 0.01 : physicalDelta < -0.01);
        bool lastActive = active && (vertical ? physicalDelta < -0.01 : physicalDelta > 0.01);
        Brush idleIcon = TransparentClone(muted, selected ? 0.96 : hovered ? 0.86 : 0.70);
        Brush centerIcon = centerLive ? accent : idleIcon;

        WpfPoint firstPoint = TrackLanePoint(pad, edge, 0.22);
        WpfPoint centerPoint = TrackLanePoint(pad, edge, 0.50);
        WpfPoint lastPoint = TrackLanePoint(pad, edge, 0.78);
        DrawCue(dc, firstCue, firstPoint, firstActive ? accent : idleIcon, vertical);
        DrawMaterialIcon(
            dc,
            SemanticIconKeys.PlayPause,
            new Rect(centerPoint.X - 8.25, centerPoint.Y - 8.25, 16.5, 16.5),
            centerIcon);
        DrawCue(dc, lastCue, lastPoint, lastActive ? accent : idleIcon, vertical);
    }

    private void DrawCornerLabel(
        DrawingContext dc,
        Rect pad,
        TouchpadCorner corner,
        Brush accent,
        Brush muted,
        Brush faint)
    {
        GestureActionKind action = _configuration.LaunchFor(corner);
        if (action == GestureActionKind.Disabled)
            return;

        bool active = IsActiveCorner(corner);
        bool candidate = IsCandidateCorner(corner);
        bool selected = _selectedZone.Corner == corner;
        bool hovered = _hoverZone?.Corner == corner;
        Brush brush = active || candidate
            ? accent
            : selected || hovered
                ? ResourceBrush("Tc.Text", muted)
                : muted;
        TouchpadActionVisualSpec spec = TouchpadActionVisualCatalog.Get(action);
        WpfPoint iconPoint = CornerLanePoint(pad, corner, TouchpadCornerZonePolicy.LengthMm + 7.0, 0);
        DrawCue(dc, spec.Center, iconPoint, brush, vertical: false);

        string label = action == GestureActionKind.OpenAdvanced ? "Advanced" : "Compact";
        DrawLabel(
            dc,
            label,
            new WpfPoint(iconPoint.X, iconPoint.Y + 18),
            TypographyScale.Caption,
            active || candidate || selected || hovered ? brush : faint,
            centered: true);
    }

    private static double ZoneFillOpacity(bool active, bool candidate, bool selected, bool hovered, bool enabled) =>
        active ? 0.24 : candidate ? 0.14 : selected ? 0.12 : hovered ? 0.10 : enabled ? 0.065 : 0.025;

    private static Pen ZoneBoundaryPen(
        bool active,
        bool candidate,
        bool selected,
        bool hovered,
        bool enabled,
        Brush accent,
        Brush muted,
        Brush faint)
    {
        Brush source = active || candidate ? accent : enabled || selected || hovered ? muted : faint;
        double opacity = active ? 1.0 : candidate ? 0.72 : selected ? 0.62 : hovered ? 0.55 : enabled ? 0.28 : 0.14;
        double width = active ? 2.2 : candidate ? 1.8 : selected ? 1.4 : 1.0;
        return new Pen(TransparentClone(source, opacity), width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }

    private static void DrawInwardLane(
        DrawingContext dc,
        Rect pad,
        TouchpadEdge edge,
        Brush accent,
        Brush muted,
        bool active,
        bool candidate,
        bool selected,
        bool hovered)
    {
        Brush source = active || candidate ? accent : muted;
        double opacity = active ? 1 : candidate ? 0.78 : selected ? 0.62 : hovered ? 0.5 : 0.30;
        var pen = new Pen(TransparentClone(source, opacity), active ? 2.4 : 1.7)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        WpfPoint start;
        WpfPoint end;
        WpfPoint segmentA;
        WpfPoint segmentB;
        switch (edge)
        {
            case TouchpadEdge.Top:
                start = new WpfPoint(pad.Left + pad.Width / 2, pad.Top + 4);
                end = new WpfPoint(start.X, start.Y + 34);
                segmentA = new WpfPoint(start.X - 18, start.Y);
                segmentB = new WpfPoint(start.X + 18, start.Y);
                break;
            case TouchpadEdge.Bottom:
                start = new WpfPoint(pad.Left + pad.Width / 2, pad.Bottom - 4);
                end = new WpfPoint(start.X, start.Y - 34);
                segmentA = new WpfPoint(start.X - 18, start.Y);
                segmentB = new WpfPoint(start.X + 18, start.Y);
                break;
            case TouchpadEdge.Left:
                start = new WpfPoint(pad.Left + 4, pad.Top + pad.Height / 2);
                end = new WpfPoint(start.X + 34, start.Y);
                segmentA = new WpfPoint(start.X, start.Y - 18);
                segmentB = new WpfPoint(start.X, start.Y + 18);
                break;
            default:
                start = new WpfPoint(pad.Right - 4, pad.Top + pad.Height / 2);
                end = new WpfPoint(start.X - 34, start.Y);
                segmentA = new WpfPoint(start.X, start.Y - 18);
                segmentB = new WpfPoint(start.X, start.Y + 18);
                break;
        }

        dc.DrawLine(new Pen(TransparentClone(source, opacity * 0.32), 7)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        }, start, end);
        dc.DrawLine(pen, segmentA, segmentB);
        DrawArrow(dc, start, end, pen);
    }

    private static void DrawArrow(DrawingContext dc, WpfPoint start, WpfPoint end, Pen pen)
    {
        dc.DrawLine(pen, start, end);
        DrawArrowHead(dc, start, end, pen);
    }

    private static void DrawArrowHead(DrawingContext dc, WpfPoint start, WpfPoint end, Pen pen)
    {
        WpfPoint direction = new(end.X - start.X, end.Y - start.Y);
        double length = Math.Max(1, Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
        double ux = direction.X / length;
        double uy = direction.Y / length;
        WpfPoint arrowA = new(end.X - ux * 8 - uy * 5, end.Y - uy * 8 + ux * 5);
        WpfPoint arrowB = new(end.X - ux * 8 + uy * 5, end.Y - uy * 8 - ux * 5);
        dc.DrawLine(pen, arrowA, end);
        dc.DrawLine(pen, arrowB, end);
    }

    private void DrawEdgeLabels(DrawingContext dc, Rect pad, Brush accent, Brush muted, Brush faint)
    {
        double releasedOpacity = ReleasedFeedbackOpacity();
        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
        {
            TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
            bool active = IsActiveEdge(edge);
            bool candidate = IsCandidateEdge(edge);
            bool selected = _selectedZone.Edge == edge;
            bool hovered = _hoverZone?.Edge == edge;
            bool enabled = binding.Action != GestureActionKind.Disabled;
            Brush labelBrush = active || candidate ? accent : selected ? ResourceBrush("Tc.Text", muted) : hovered ? ResourceBrush("Tc.Text", muted) : enabled ? muted : faint;
            Rect band = EdgeBandRect(pad, edge);
            bool integratedTrack = binding.Action == GestureActionKind.PreviousNextTrack && _configuration.TrackCenterPlayPauseEnabled;

            WpfPoint point = integratedTrack
                ? TrackLanePoint(pad, edge, 0.50)
                : edge switch
                {
                    TouchpadEdge.Top => new(pad.Left + pad.Width / 2, band.Bottom + 13),
                    TouchpadEdge.Bottom => new(pad.Left + pad.Width / 2, band.Top - 13),
                    TouchpadEdge.Left => new(band.Right + 25, pad.Top + pad.Height / 2),
                    _ => new(band.Left - 25, pad.Top + pad.Height / 2)
                };

            if (!integratedTrack && TouchpadActionVisualCatalog.Get(binding.Action).Motion == TouchpadGestureMotionKind.Inward)
            {
                point = edge switch
                {
                    TouchpadEdge.Top => new WpfPoint(pad.Left + pad.Width / 2, pad.Top + 58),
                    TouchpadEdge.Bottom => new WpfPoint(pad.Left + pad.Width / 2, pad.Bottom - 58),
                    TouchpadEdge.Left => new WpfPoint(pad.Left + 58, pad.Top + pad.Height / 2),
                    _ => new WpfPoint(pad.Right - 58, pad.Top + pad.Height / 2)
                };
            }

            if (!integratedTrack)
                DrawActionGlyph(dc, edge, point, binding, labelBrush, accent, active, candidate);

            if (_activeFeedbackEdge == edge && !string.IsNullOrWhiteSpace(_activeFeedbackText))
                DrawValueBadge(dc, pad, edge, point, _activeFeedbackText!, accent, 1, live: true);
            else if (!active && releasedOpacity > 0 && _releasedFeedbackEdge == edge && !string.IsNullOrWhiteSpace(_releasedFeedbackText))
                DrawValueBadge(dc, pad, edge, point, _releasedFeedbackText!, accent, releasedOpacity, live: false);
        }
    }

    private void DrawActionGlyph(
        DrawingContext dc,
        TouchpadEdge edge,
        WpfPoint point,
        TouchpadEdgeBinding binding,
        Brush brush,
        Brush accent,
        bool active,
        bool candidate)
    {
        TouchpadActionVisualSpec spec = TouchpadActionVisualCatalog.Get(binding.Action);
        bool vertical = edge is TouchpadEdge.Left or TouchpadEdge.Right;

        if (spec.Directional)
        {
            TouchpadVisualCue firstCue;
            TouchpadVisualCue lastCue;
            if (vertical)
            {
                firstCue = binding.Inverted ? spec.Negative : spec.Positive;
                lastCue = binding.Inverted ? spec.Positive : spec.Negative;
            }
            else
            {
                firstCue = binding.Inverted ? spec.Positive : spec.Negative;
                lastCue = binding.Inverted ? spec.Negative : spec.Positive;
            }

            double physicalDelta = vertical
                ? -(_signal?.DeltaMm ?? 0)
                : (_signal?.DeltaMm ?? 0);
            bool firstActive = active && (vertical ? physicalDelta > 0.01 : physicalDelta < -0.01);
            bool lastActive = active && (vertical ? physicalDelta < -0.01 : physicalDelta > 0.01);

            WpfPoint firstPoint = vertical
                ? new(point.X, point.Y - spec.Spread)
                : new(point.X - spec.Spread, point.Y);
            WpfPoint lastPoint = vertical
                ? new(point.X, point.Y + spec.Spread)
                : new(point.X + spec.Spread, point.Y);

            DrawCue(dc, firstCue, firstPoint, firstActive ? accent : brush, vertical);
            DrawCue(dc, lastCue, lastPoint, lastActive ? accent : brush, vertical);
        }

        bool showCenter = !spec.CenterRequiresTrackOption || _configuration.TrackCenterPlayPauseEnabled;
        if (showCenter)
        {
            bool centerActive = spec.Action == GestureActionKind.PreviousNextTrack
                ? candidate
                : active || candidate;
            DrawCue(dc, spec.Center, point, centerActive ? accent : brush, vertical);
        }
        else
        {
            DrawSmallDot(dc, point, brush);
        }
    }

    private void DrawCue(
        DrawingContext dc,
        TouchpadVisualCue cue,
        WpfPoint point,
        Brush brush,
        bool vertical)
    {
        switch (cue.Kind)
        {
            case TouchpadVisualCueKind.None:
                return;
            case TouchpadVisualCueKind.ResourceIcon:
                if (!string.IsNullOrWhiteSpace(cue.Value))
                    DrawMaterialIcon(dc, cue.Value, new Rect(point.X - 9.25, point.Y - 9.25, 18.5, 18.5), brush);
                return;
            case TouchpadVisualCueKind.Text:
                DrawLabel(dc, cue.Value ?? string.Empty, point, TypographyScale.Body, brush, centered: true);
                return;
            case TouchpadVisualCueKind.Disabled:
                DrawDisabledGlyph(dc, point, brush);
                return;
        }
    }

    private static void DrawDisabledGlyph(DrawingContext dc, WpfPoint point, Brush brush)
    {
        var pen = new Pen(brush, 1.5);
        dc.DrawEllipse(null, pen, point, 8, 8);
        dc.DrawLine(pen, new WpfPoint(point.X - 5.5, point.Y + 5.5), new WpfPoint(point.X + 5.5, point.Y - 5.5));
    }

    private static void DrawSmallDot(DrawingContext dc, WpfPoint point, Brush brush) =>
        dc.DrawEllipse(brush, null, point, 2.3, 2.3);

    private double ReleasedFeedbackOpacity()
    {
        if (_releasedFeedbackEdge is null || string.IsNullOrWhiteSpace(_releasedFeedbackText))
            return 0;

        TimeSpan age = DateTimeOffset.UtcNow - _releasedFeedbackStarted;
        if (age <= ReleasedValueHold)
            return 1;
        TimeSpan fadeAge = age - ReleasedValueHold;
        if (fadeAge >= ReleasedValueFade)
            return 0;
        return 1 - fadeAge.TotalMilliseconds / ReleasedValueFade.TotalMilliseconds;
    }

    private void DrawValueBadge(DrawingContext dc, Rect pad, TouchpadEdge edge, WpfPoint anchor, string value, Brush accent, double opacity, bool live)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        Brush textBrush = TransparentClone(ResourceBrush("Tc.Text", Brushes.White), opacity);
        var text = new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Text, Segoe UI"),
            live ? TypographyScale.Secondary : TypographyScale.Caption,
            textBrush,
            pixelsPerDip);
        double width = text.Width + (live ? 18 : 14);
        double height = text.Height + (live ? 10 : 8);
        double x = edge switch
        {
            TouchpadEdge.Left => Math.Min(pad.Right - width - 8, anchor.X + 18),
            TouchpadEdge.Right => Math.Max(pad.Left + 8, anchor.X - width - 18),
            _ => anchor.X - width / 2
        };
        double y = edge switch
        {
            TouchpadEdge.Top => Math.Min(pad.Bottom - height - 8, anchor.Y + 20),
            TouchpadEdge.Bottom => Math.Max(pad.Top + 8, anchor.Y - height - 20),
            _ => anchor.Y - height - 24
        };
        var badge = new Rect(x, y, width, height);
        Brush surface = ResourceBrush("Tc.Surface", Brushes.Black);
        Brush background = live ? TransparentClone(surface, 0.97 * opacity) : TransparentClone(accent, 0.13 * opacity);
        Pen border = new(TransparentClone(accent, (live ? 1.0 : 0.72) * opacity), live ? 1.5 : 1);
        dc.DrawRoundedRectangle(background, border, badge, 6, 6);
        dc.DrawText(text, new WpfPoint(badge.Left + (live ? 9 : 7), badge.Top + (live ? 5 : 4)));
    }

    private void DrawMaterialIcon(DrawingContext dc, string resourceKey, Rect bounds, Brush brush)
    {
        if (TryFindResource(resourceKey) is not Geometry source)
            return;

        Geometry geometry = source.CloneCurrentValue();
        double scale = Math.Min(bounds.Width, bounds.Height) / 960d;
        geometry.Transform = new MatrixTransform(
            scale, 0,
            0, scale,
            bounds.Left, bounds.Top + 960d * scale);
        dc.DrawGeometry(brush, null, geometry);
    }

    private static string ZoneName(TouchpadZoneSelection zone)
    {
        if (zone.Corner is TouchpadCorner corner)
            return corner == TouchpadCorner.TopLeft ? "Top-left corner" : "Top-right corner";
        return EdgeName(zone.Edge ?? TouchpadEdge.Top);
    }

    private static string EdgeName(TouchpadEdge edge) => edge switch
    {
        TouchpadEdge.Left => "Left edge",
        TouchpadEdge.Right => "Right edge",
        TouchpadEdge.Top => "Top edge",
        _ => "Bottom edge"
    };

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

    private Geometry EdgeBandVisualGeometry(Rect pad, TouchpadEdge edge)
    {
        Geometry visible = new RectangleGeometry(EdgeBandRect(pad, edge));
        if (edge is TouchpadEdge.Top or TouchpadEdge.Left)
            visible = new CombinedGeometry(GeometryCombineMode.Exclude, visible, CornerZoneGeometry(pad, TouchpadCorner.TopLeft));
        if (edge is TouchpadEdge.Top or TouchpadEdge.Right)
            visible = new CombinedGeometry(GeometryCombineMode.Exclude, visible, CornerZoneGeometry(pad, TouchpadCorner.TopRight));
        return visible;
    }

    private Rect TrackCenterRect(Rect pad, TouchpadEdge edge)
    {
        Rect band = EdgeBandRect(pad, edge);
        double start = TrackCenterGesturePolicy.CenterZoneStart;
        double end = TrackCenterGesturePolicy.CenterZoneEnd;

        return edge switch
        {
            TouchpadEdge.Top or TouchpadEdge.Bottom => new Rect(
                pad.Left + pad.Width * start,
                band.Top,
                pad.Width * (end - start),
                band.Height),
            _ => new Rect(
                band.Left,
                pad.Top + pad.Height * start,
                band.Width,
                pad.Height * (end - start))
        };
    }

    private WpfPoint TrackLanePoint(Rect pad, TouchpadEdge edge, double position01)
    {
        Rect band = EdgeBandRect(pad, edge);
        double position = Math.Clamp(position01, 0, 1);
        return edge is TouchpadEdge.Top or TouchpadEdge.Bottom
            ? new WpfPoint(pad.Left + pad.Width * position, band.Top + band.Height / 2)
            : new WpfPoint(band.Left + band.Width / 2, pad.Top + pad.Height * position);
    }

    private TouchpadZoneSelection? HitZone(Rect pad, WpfPoint point)
    {
        TouchpadCorner? corner = HitCorner(pad, point);
        if (corner is TouchpadCorner selectedCorner)
            return TouchpadZoneSelection.ForCorner(selectedCorner);

        TouchpadEdge? edge = HitEdge(pad, point);
        return edge is TouchpadEdge selectedEdge
            ? TouchpadZoneSelection.ForEdge(selectedEdge)
            : null;
    }

    private TouchpadCorner? HitCorner(Rect pad, WpfPoint point)
    {
        double localY = (point.Y - pad.Top) / Math.Max(1, pad.Height) * _geometry.EffectiveHeightMm;
        double leftX = (point.X - pad.Left) / Math.Max(1, pad.Width) * _geometry.EffectiveWidthMm;
        if (TouchpadCornerZonePolicy.ContainsLocal(leftX, localY))
            return TouchpadCorner.TopLeft;

        double rightX = (pad.Right - point.X) / Math.Max(1, pad.Width) * _geometry.EffectiveWidthMm;
        return TouchpadCornerZonePolicy.ContainsLocal(rightX, localY)
            ? TouchpadCorner.TopRight
            : null;
    }

    private TouchpadEdge? HitEdge(Rect pad, WpfPoint point)
    {
        TouchpadEdge[] candidates = Enum.GetValues<TouchpadEdge>()
            .Where(edge => EdgeBandRect(pad, edge).Contains(point))
            .ToArray();
        if (candidates.Length == 0)
            return null;
        if (candidates.Length == 1)
            return candidates[0];

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

    private CornerGuide CornerGuideFor(Rect pad, TouchpadCorner corner)
    {
        double half = TouchpadCornerZonePolicy.HalfWidthMm;
        double start = TouchpadCornerZonePolicy.LaneStartAlongMm;
        double end = TouchpadCornerZonePolicy.InnerCapCenterMm;
        WpfPoint outerA = CornerLanePoint(pad, corner, start, -half);
        WpfPoint innerA = CornerLanePoint(pad, corner, end, -half);
        WpfPoint outerB = CornerLanePoint(pad, corner, start, half);
        WpfPoint innerB = CornerLanePoint(pad, corner, end, half);
        WpfPoint centerStart = CornerLanePoint(pad, corner, start + 1.4, 0);
        WpfPoint centerEnd = CornerLanePoint(pad, corner, end - 1.8, 0);
        WpfPoint guardTop = CornerLocalPoint(pad, corner, TouchpadCornerZonePolicy.OuterGuardRadiusMm, 0);
        WpfPoint guardSide = CornerLocalPoint(pad, corner, 0, TouchpadCornerZonePolicy.OuterGuardRadiusMm);
        WpfPoint innerTip = CornerLanePoint(pad, corner, TouchpadCornerZonePolicy.LengthMm, 0);
        return new CornerGuide(
            outerA,
            innerA,
            outerB,
            innerB,
            centerStart,
            centerEnd,
            guardTop,
            guardSide,
            innerTip);
    }

    private Geometry CornerZoneGeometry(Rect pad, TouchpadCorner corner)
    {
        Geometry left = BuildLeftCornerZoneGeometry(pad);
        return corner == TouchpadCorner.TopLeft
            ? left
            : MirrorCornerGeometry(left, pad);
    }

    private Geometry BuildLeftCornerZoneGeometry(Rect pad)
    {
        CornerGuide guide = CornerGuideFor(pad, TouchpadCorner.TopLeft);
        WpfPoint origin = CornerOrigin(pad, TouchpadCorner.TopLeft);
        double guardRadius = CornerPhysicalPixels(pad, TouchpadCornerZonePolicy.OuterGuardRadiusMm);
        double capRadius = CornerPhysicalPixels(pad, TouchpadCornerZonePolicy.HalfWidthMm);

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(origin, isFilled: true, isClosed: true);
            context.LineTo(guide.GuardTop, isStroked: false, isSmoothJoin: false);
            context.ArcTo(
                guide.OuterA,
                new Size(guardRadius, guardRadius),
                rotationAngle: 0,
                isLargeArc: false,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: false,
                isSmoothJoin: true);
            context.LineTo(guide.InnerA, isStroked: false, isSmoothJoin: true);
            context.ArcTo(
                guide.InnerB,
                new Size(capRadius, capRadius),
                rotationAngle: 0,
                isLargeArc: false,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: false,
                isSmoothJoin: true);
            context.LineTo(guide.OuterB, isStroked: false, isSmoothJoin: true);
            context.ArcTo(
                guide.GuardSide,
                new Size(guardRadius, guardRadius),
                rotationAngle: 0,
                isLargeArc: false,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: false,
                isSmoothJoin: true);
            context.LineTo(origin, isStroked: false, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }

    private Geometry CornerBoundaryGeometry(Rect pad, TouchpadCorner corner)
    {
        Geometry left = BuildLeftCornerBoundaryGeometry(pad);
        return corner == TouchpadCorner.TopLeft
            ? left
            : MirrorCornerGeometry(left, pad);
    }

    private Geometry BuildLeftCornerBoundaryGeometry(Rect pad)
    {
        CornerGuide guide = CornerGuideFor(pad, TouchpadCorner.TopLeft);
        double guardRadius = CornerPhysicalPixels(pad, TouchpadCornerZonePolicy.OuterGuardRadiusMm);
        double capRadius = CornerPhysicalPixels(pad, TouchpadCornerZonePolicy.HalfWidthMm);

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(guide.GuardTop, isFilled: false, isClosed: false);
            context.ArcTo(
                guide.OuterA,
                new Size(guardRadius, guardRadius),
                rotationAngle: 0,
                isLargeArc: false,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: true);
            context.LineTo(guide.InnerA, isStroked: true, isSmoothJoin: true);
            context.ArcTo(
                guide.InnerB,
                new Size(capRadius, capRadius),
                rotationAngle: 0,
                isLargeArc: false,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: true);
            context.LineTo(guide.OuterB, isStroked: true, isSmoothJoin: true);
            context.ArcTo(
                guide.GuardSide,
                new Size(guardRadius, guardRadius),
                rotationAngle: 0,
                isLargeArc: false,
                sweepDirection: SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry MirrorCornerGeometry(Geometry source, Rect pad)
    {
        Geometry mirrored = source.CloneCurrentValue();
        mirrored.Transform = new ScaleTransform(-1, 1, pad.Left + pad.Width / 2, pad.Top);
        mirrored.Freeze();
        return mirrored;
    }

    private WpfPoint CornerOrigin(Rect pad, TouchpadCorner corner) =>
        corner == TouchpadCorner.TopLeft
            ? new WpfPoint(pad.Left, pad.Top)
            : new WpfPoint(pad.Right, pad.Top);

    private double CornerPhysicalPixels(Rect pad, double millimetres) =>
        millimetres * pad.Width / Math.Max(1, _geometry.EffectiveWidthMm);

    private WpfPoint CornerLocalPoint(Rect pad, TouchpadCorner corner, double localXmm, double localYmm)
    {
        double x = localXmm / _geometry.EffectiveWidthMm * pad.Width;
        double y = localYmm / _geometry.EffectiveHeightMm * pad.Height;
        WpfPoint leftPoint = new(pad.Left + x, pad.Top + y);
        return corner == TouchpadCorner.TopLeft
            ? leftPoint
            : new WpfPoint(pad.Left + pad.Right - leftPoint.X, leftPoint.Y);
    }

    private WpfPoint CornerLanePoint(Rect pad, TouchpadCorner corner, double alongMm, double acrossMm)
    {
        const double invSqrt2 = 0.7071067811865476;
        double localX = (alongMm - acrossMm) * invSqrt2;
        double localY = (alongMm + acrossMm) * invSqrt2;
        return CornerLocalPoint(pad, corner, localX, localY);
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

    private WpfPoint ContactPoint(Rect pad, int x, int y) => new(
        pad.Left + ((x - _geometry.XLogicalMin) / (double)_geometry.XRange) * pad.Width,
        pad.Top + ((y - _geometry.YLogicalMin) / (double)_geometry.YRange) * pad.Height);

    private void DrawLabel(DrawingContext dc, string value, WpfPoint point, double size, Brush brush, bool centered = false, bool rightAligned = false)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formatted = new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Text, Segoe UI"),
            size,
            brush,
            pixelsPerDip);
        double x = centered ? point.X - formatted.Width / 2 : rightAligned ? point.X - formatted.Width : point.X;
        double y = point.Y - formatted.Height / 2;
        dc.DrawText(formatted, new WpfPoint(x, y));
    }

    private Brush ResourceBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private static Brush TransparentClone(Brush source, double opacity)
    {
        Brush clone = source.CloneCurrentValue();
        clone.Opacity = opacity;
        clone.Freeze();
        return clone;
    }

    private static double Distance(WpfPoint a, WpfPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Rect Bounds(IReadOnlyList<WpfPoint> points)
    {
        double left = points.Min(static point => point.X);
        double top = points.Min(static point => point.Y);
        double right = points.Max(static point => point.X);
        double bottom = points.Max(static point => point.Y);
        return new Rect(left, top, right - left, bottom - top);
    }

    private readonly record struct CornerGuide(
        WpfPoint OuterA,
        WpfPoint InnerA,
        WpfPoint OuterB,
        WpfPoint InnerB,
        WpfPoint CenterStart,
        WpfPoint CenterEnd,
        WpfPoint GuardTop,
        WpfPoint GuardSide,
        WpfPoint InnerTip);

    private readonly record struct TrailPoint(int ContactId, int X, int Y, DateTimeOffset Timestamp, bool StartsSegment);
}
