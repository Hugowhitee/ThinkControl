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
    private TouchpadEdge _selectedEdge = TouchpadEdge.Top;
    private TouchpadEdge? _hoverEdge;
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
        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>().Where(edge => edge != _selectedEdge))
            DrawEdgeBand(dc, pad, edge, accent, muted, faint);
        DrawEdgeBand(dc, pad, _selectedEdge, accent, muted, faint);
        DrawTrail(dc, pad, accent);
        dc.Pop();

        dc.DrawRoundedRectangle(null, new Pen(border, 1), pad, PadCornerRadius, PadCornerRadius);
        DrawEdgeLabels(dc, pad, accent, muted, faint);

        DrawLabel(dc, EdgeName(_selectedEdge).ToUpperInvariant(),
            new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 - 8),
            12, muted, centered: true);
        string size = _geometry.PhysicalSizeEstimated
            ? $"~{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm"
            : $"{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm";
        DrawLabel(dc, $"{size} · click an edge to edit", new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 + 12),
            12, faint, centered: true);

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
        TouchpadEdge? hover = pad.Contains(point) ? HitEdge(pad, point) : null;
        if (hover != _hoverEdge)
        {
            _hoverEdge = hover;
            Cursor = hover.HasValue ? WpfCursors.Hand : WpfCursors.Arrow;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverEdge = null;
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

        TouchpadEdge? edge = HitEdge(pad, point);
        if (edge is null)
            return;

        SelectedEdge = edge.Value;
        EdgeSelected?.Invoke(edge.Value);
        e.Handled = true;
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
        _signal is { Edge: not null, Phase: GesturePhase.Claimed or GesturePhase.Active } && _signal.Edge == edge;

    private bool IsCandidateEdge(TouchpadEdge edge) =>
        _signal is { Edge: not null, Phase: GesturePhase.Candidate } && _signal.Edge == edge;

    private void DrawEdgeBand(DrawingContext dc, Rect pad, TouchpadEdge edge, Brush accent, Brush muted, Brush faint)
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        bool active = IsActiveEdge(edge);
        bool candidate = IsCandidateEdge(edge);
        bool selected = edge == _selectedEdge;
        bool hovered = edge == _hoverEdge;
        bool enabled = binding.Action != GestureActionKind.Disabled;
        Rect zone = EdgeBandRect(pad, edge);

        // Selection is deliberately neutral; live interaction is the only state that
        // gets a strong accent treatment. This prevents a selected edge from looking
        // like it is already changing hardware.
        Brush fillSource = active || candidate ? accent : muted;
        double opacity = active ? 0.24 : candidate ? 0.14 : selected ? 0.12 : hovered ? 0.10 : enabled ? 0.065 : 0.025;
        dc.DrawRectangle(TransparentClone(fillSource, opacity), null, zone);

        Brush lineSource = active || candidate || selected ? accent : enabled || hovered ? muted : faint;
        Pen threshold = new(
            TransparentClone(lineSource, active ? 1.0 : candidate ? 0.72 : selected ? 0.50 : hovered ? 0.55 : enabled ? 0.28 : 0.14),
            active ? 2.2 : candidate ? 1.8 : selected ? 1.4 : 1.0);
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
        double releasedOpacity = ReleasedFeedbackOpacity();
        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
        {
            TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
            bool active = IsActiveEdge(edge);
            bool candidate = IsCandidateEdge(edge);
            bool selected = edge == _selectedEdge;
            bool hovered = edge == _hoverEdge;
            bool enabled = binding.Action != GestureActionKind.Disabled;
            Brush labelBrush = active || candidate ? accent : selected ? ResourceBrush("Tc.Text", muted) : hovered ? ResourceBrush("Tc.Text", muted) : enabled ? muted : faint;
            Rect band = EdgeBandRect(pad, edge);

            WpfPoint point = edge switch
            {
                TouchpadEdge.Top => new(pad.Left + pad.Width / 2, band.Bottom + 13),
                TouchpadEdge.Bottom => new(pad.Left + pad.Width / 2, band.Top - 13),
                TouchpadEdge.Left => new(band.Right + 25, pad.Top + pad.Height / 2),
                _ => new(band.Left - 25, pad.Top + pad.Height / 2)
            };

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
        switch (binding.Action)
        {
            case GestureActionKind.Volume:
            case GestureActionKind.Brightness:
                DrawContinuousGlyph(dc, edge, point, binding, brush, accent, active);
                return;
            case GestureActionKind.MediaSeek:
                DrawScrubGlyph(dc, point, brush, edge is TouchpadEdge.Left or TouchpadEdge.Right);
                return;
            case GestureActionKind.PreviousNextTrack:
                DrawTrackGlyph(dc, edge, point, binding, brush, accent, active, candidate);
                return;
            case GestureActionKind.PlayPause:
                DrawPlayPauseGlyph(dc, point, brush);
                return;
            case GestureActionKind.OpenThinkControl:
                DrawCompactGlyph(dc, point, brush);
                return;
            default:
                DrawDisabledGlyph(dc, point, brush);
                return;
        }
    }

    private void DrawContinuousGlyph(
        DrawingContext dc,
        TouchpadEdge edge,
        WpfPoint point,
        TouchpadEdgeBinding binding,
        Brush brush,
        Brush accent,
        bool active)
    {
        string iconKey = binding.Action == GestureActionKind.Volume ? "Tc.Icon.Audio" : "Tc.Icon.Brightness";
        DrawMaterialIcon(dc, iconKey, new Rect(point.X - 8, point.Y - 8, 16, 16), brush);

        double effectiveDelta = edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? -(_signal?.DeltaMm ?? 0)
            : (_signal?.DeltaMm ?? 0);
        bool plusActive = active && effectiveDelta > 0.01;
        bool minusActive = active && effectiveDelta < -0.01;

        if (edge is TouchpadEdge.Left or TouchpadEdge.Right)
        {
            string upper = binding.Inverted ? "−" : "+";
            string lower = binding.Inverted ? "+" : "−";
            bool upperActive = upper == "+" ? plusActive : minusActive;
            bool lowerActive = lower == "+" ? plusActive : minusActive;
            DrawLabel(dc, upper, new WpfPoint(point.X, point.Y - 31), upperActive ? 14 : 12, upperActive ? accent : brush, centered: true);
            DrawLabel(dc, lower, new WpfPoint(point.X, point.Y + 31), lowerActive ? 14 : 12, lowerActive ? accent : brush, centered: true);
        }
        else
        {
            string left = binding.Inverted ? "+" : "−";
            string right = binding.Inverted ? "−" : "+";
            bool leftActive = left == "+" ? plusActive : minusActive;
            bool rightActive = right == "+" ? plusActive : minusActive;
            DrawLabel(dc, left, new WpfPoint(point.X - 34, point.Y), leftActive ? 14 : 12, leftActive ? accent : brush, centered: true);
            DrawLabel(dc, right, new WpfPoint(point.X + 34, point.Y), rightActive ? 14 : 12, rightActive ? accent : brush, centered: true);
        }
    }

    private void DrawTrackGlyph(
        DrawingContext dc,
        TouchpadEdge edge,
        WpfPoint point,
        TouchpadEdgeBinding binding,
        Brush brush,
        Brush accent,
        bool active,
        bool candidate)
    {
        bool vertical = edge is TouchpadEdge.Left or TouchpadEdge.Right;
        bool firstIsNext = vertical ? !binding.Inverted : binding.Inverted;
        string firstKey = firstIsNext ? "Tc.Icon.SkipNext" : "Tc.Icon.SkipPrevious";
        string lastKey = firstIsNext ? "Tc.Icon.SkipPrevious" : "Tc.Icon.SkipNext";

        double controlDelta = edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? -(_signal?.DeltaMm ?? 0)
            : (_signal?.DeltaMm ?? 0);
        bool nextActive = active && controlDelta > 0.01;
        bool previousActive = active && controlDelta < -0.01;

        Brush firstBrush = firstIsNext ? (nextActive ? accent : brush) : (previousActive ? accent : brush);
        Brush lastBrush = firstIsNext ? (previousActive ? accent : brush) : (nextActive ? accent : brush);
        const double icon = 15;
        const double spread = 27;

        if (vertical)
        {
            DrawMaterialIcon(dc, firstKey, new Rect(point.X - icon / 2, point.Y - spread - icon / 2, icon, icon), firstBrush);
            DrawMaterialIcon(dc, lastKey, new Rect(point.X - icon / 2, point.Y + spread - icon / 2, icon, icon), lastBrush);
        }
        else
        {
            DrawMaterialIcon(dc, firstKey, new Rect(point.X - spread - icon / 2, point.Y - icon / 2, icon, icon), firstBrush);
            DrawMaterialIcon(dc, lastKey, new Rect(point.X + spread - icon / 2, point.Y - icon / 2, icon, icon), lastBrush);
        }

        if (_configuration.TrackCenterPlayPauseEnabled)
            DrawPlayPauseGlyph(dc, point, candidate ? accent : brush);
        else
            DrawSmallDot(dc, point, brush);
    }

    private static void DrawPlayPauseGlyph(DrawingContext dc, WpfPoint point, Brush brush)
    {
        var play = new StreamGeometry();
        using (StreamGeometryContext context = play.Open())
        {
            context.BeginFigure(new WpfPoint(point.X - 7, point.Y - 7), true, true);
            context.LineTo(new WpfPoint(point.X + 1, point.Y), true, false);
            context.LineTo(new WpfPoint(point.X - 7, point.Y + 7), true, false);
        }
        play.Freeze();
        dc.DrawGeometry(brush, null, play);
        dc.DrawRoundedRectangle(brush, null, new Rect(point.X + 4, point.Y - 7, 2.8, 14), 1, 1);
        dc.DrawRoundedRectangle(brush, null, new Rect(point.X + 9, point.Y - 7, 2.8, 14), 1, 1);
    }

    private static void DrawScrubGlyph(DrawingContext dc, WpfPoint point, Brush brush, bool vertical)
    {
        var pen = new Pen(brush, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (vertical)
        {
            dc.DrawLine(pen, new WpfPoint(point.X, point.Y - 12), new WpfPoint(point.X, point.Y + 12));
            dc.DrawEllipse(brush, null, point, 3.6, 3.6);
            dc.DrawLine(pen, new WpfPoint(point.X - 4, point.Y - 8), new WpfPoint(point.X, point.Y - 12));
            dc.DrawLine(pen, new WpfPoint(point.X + 4, point.Y - 8), new WpfPoint(point.X, point.Y - 12));
            dc.DrawLine(pen, new WpfPoint(point.X - 4, point.Y + 8), new WpfPoint(point.X, point.Y + 12));
            dc.DrawLine(pen, new WpfPoint(point.X + 4, point.Y + 8), new WpfPoint(point.X, point.Y + 12));
        }
        else
        {
            dc.DrawLine(pen, new WpfPoint(point.X - 12, point.Y), new WpfPoint(point.X + 12, point.Y));
            dc.DrawEllipse(brush, null, point, 3.6, 3.6);
            dc.DrawLine(pen, new WpfPoint(point.X - 8, point.Y - 4), new WpfPoint(point.X - 12, point.Y));
            dc.DrawLine(pen, new WpfPoint(point.X - 8, point.Y + 4), new WpfPoint(point.X - 12, point.Y));
            dc.DrawLine(pen, new WpfPoint(point.X + 8, point.Y - 4), new WpfPoint(point.X + 12, point.Y));
            dc.DrawLine(pen, new WpfPoint(point.X + 8, point.Y + 4), new WpfPoint(point.X + 12, point.Y));
        }
    }

    private static void DrawCompactGlyph(DrawingContext dc, WpfPoint point, Brush brush)
    {
        var pen = new Pen(brush, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        WpfPoint trStart = new(point.X + 10, point.Y - 10);
        WpfPoint trEnd = new(point.X + 2, point.Y - 2);
        dc.DrawLine(pen, trStart, trEnd);
        dc.DrawLine(pen, trEnd, new WpfPoint(point.X + 2, point.Y - 7));
        dc.DrawLine(pen, trEnd, new WpfPoint(point.X + 7, point.Y - 2));
        WpfPoint blStart = new(point.X - 10, point.Y + 10);
        WpfPoint blEnd = new(point.X - 2, point.Y + 2);
        dc.DrawLine(pen, blStart, blEnd);
        dc.DrawLine(pen, blEnd, new WpfPoint(point.X - 7, point.Y + 2));
        dc.DrawLine(pen, blEnd, new WpfPoint(point.X - 2, point.Y + 7));
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
            live ? 12.5 : 12,
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
            _ => anchor.Y - height / 2
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

    private readonly record struct TrailPoint(int ContactId, int X, int Y, DateTimeOffset Timestamp, bool StartsSegment);
}
