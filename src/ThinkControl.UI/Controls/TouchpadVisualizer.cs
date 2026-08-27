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
    private const int MaxTrailPoints = 34;

    private readonly List<TrailPoint> _trail = [];
    private readonly DispatcherTimer _trailTimer;
    private TouchpadGestureConfiguration _configuration =
        TouchpadGestureConfiguration.Default with { Enabled = false };
    private TouchpadGeometry _geometry = new(0, 13500, 0, 8000, 135, 80, true);
    private IReadOnlyList<TouchContact> _contacts = Array.Empty<TouchContact>();
    private TouchpadEdge _selectedEdge = TouchpadEdge.Top;
    private TouchpadEdge? _hoverEdge;
    private GestureSignal? _signal;
    private string? _activeValueText;

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
        Unloaded += (_, _) => _trailTimer.Stop();
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
        if (signal is null || signal.Phase is GesturePhase.Cancelled or GesturePhase.Released)
            _activeValueText = null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (TouchContact contact in contacts.Where(static contact => contact.IsDown && contact.Confidence))
        {
            if (_trail.Count == 0 || _trail[^1].ContactId != contact.ContactId ||
                Math.Abs(_trail[^1].X - contact.X) + Math.Abs(_trail[^1].Y - contact.Y) >= 8)
            {
                _trail.Add(new TrailPoint(contact.ContactId, contact.X, contact.Y, now));
            }
        }
        while (_trail.Count > MaxTrailPoints)
            _trail.RemoveAt(0);

        if ((_trail.Count > 0 || contacts.Any(static contact => contact.IsDown)) && !_trailTimer.IsEnabled)
            _trailTimer.Start();
        InvalidateVisual();
    }

    public void SetActiveGestureValue(string? value)
    {
        _activeValueText = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        TouchpadEdgeBinding selectedBinding = _configuration.BindingFor(_selectedEdge);
        DrawLabel(dc, $"{EdgeName(_selectedEdge).ToUpperInvariant()} · {ActionLabel(selectedBinding.Action).ToUpperInvariant()}",
            new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 - 8),
            11, muted, centered: true);
        string size = _geometry.PhysicalSizeEstimated
            ? $"~{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm"
            : $"{_geometry.EffectiveWidthMm:0} × {_geometry.EffectiveHeightMm:0} mm";
        DrawLabel(dc, $"{size} · click an edge to edit", new WpfPoint(pad.Left + pad.Width / 2, pad.Top + pad.Height / 2 + 11),
            10.5, faint, centered: true);

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

    private void DrawTrail(DrawingContext dc, Rect pad, Brush accent)
    {
        if (_trail.Count == 0)
            return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 1; i < _trail.Count; i++)
        {
            TrailPoint previous = _trail[i - 1];
            TrailPoint current = _trail[i];
            if (previous.ContactId != current.ContactId)
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

    private void DrawEdgeBand(DrawingContext dc, Rect pad, TouchpadEdge edge, Brush accent, Brush muted, Brush faint)
    {
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);
        bool active = IsActiveEdge(edge);
        bool selected = edge == _selectedEdge;
        bool hovered = edge == _hoverEdge;
        bool enabled = binding.Action != GestureActionKind.Disabled;
        Rect zone = EdgeBandRect(pad, edge);

        Brush source = active || selected || hovered ? accent : enabled ? muted : faint;
        double opacity = active ? 0.62 : selected ? 0.48 : hovered ? 0.30 : enabled ? 0.13 : 0.045;
        dc.DrawRectangle(TransparentClone(source, opacity), null, zone);

        Pen threshold = new(
            TransparentClone(source, active ? 1.0 : selected ? 0.92 : hovered ? 0.72 : enabled ? 0.34 : 0.18),
            active ? 2.0 : selected ? 1.5 : hovered ? 1.35 : 1.0);
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
            bool active = IsActiveEdge(edge);
            bool selected = edge == _selectedEdge;
            bool hovered = edge == _hoverEdge;
            bool enabled = binding.Action != GestureActionKind.Disabled;
            Brush labelBrush = active || selected || hovered ? accent : enabled ? muted : faint;
            string label = ActionLabel(binding.Action);
            Rect band = EdgeBandRect(pad, edge);

            WpfPoint point = edge switch
            {
                TouchpadEdge.Top => new(pad.Left + pad.Width / 2, band.Bottom + 12),
                TouchpadEdge.Bottom => new(pad.Left + pad.Width / 2, band.Top - 12),
                TouchpadEdge.Left => new(band.Right + 24, pad.Top + pad.Height / 2),
                _ => new(band.Left - 24, pad.Top + pad.Height / 2)
            };
            if (edge is TouchpadEdge.Left or TouchpadEdge.Right &&
                binding.Action is GestureActionKind.Volume or GestureActionKind.Brightness)
            {
                string iconKey = binding.Action == GestureActionKind.Volume ? "Tc.Icon.Audio" : "Tc.Icon.Brightness";
                DrawMaterialIcon(dc, iconKey, new Rect(point.X - 8, point.Y - 8, 16, 16), labelBrush);

                bool plusActive = active && (_signal?.DeltaMm ?? 0) < -0.01;
                bool minusActive = active && (_signal?.DeltaMm ?? 0) > 0.01;
                string upper = binding.Inverted ? "−" : "+";
                string lower = binding.Inverted ? "+" : "−";
                bool upperActive = upper == "+" ? plusActive : minusActive;
                bool lowerActive = lower == "+" ? plusActive : minusActive;
                DrawLabel(dc, upper, new WpfPoint(point.X, point.Y - 31), upperActive ? 14 : 12, upperActive ? accent : labelBrush, centered: true);
                DrawLabel(dc, lower, new WpfPoint(point.X, point.Y + 31), lowerActive ? 14 : 12, lowerActive ? accent : labelBrush, centered: true);

                if (active && !string.IsNullOrWhiteSpace(_activeValueText))
                    DrawActiveValueBadge(dc, pad, edge, point, _activeValueText!, accent);
            }
            else
            {
                DrawLabel(dc, label, point, 10.5, labelBrush, centered: true);
                if (active && !string.IsNullOrWhiteSpace(_activeValueText))
                    DrawActiveValueBadge(dc, pad, edge, point, _activeValueText!, accent);
            }
        }
    }

    private void DrawActiveValueBadge(DrawingContext dc, Rect pad, TouchpadEdge edge, WpfPoint anchor, string value, Brush accent)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"),
            11.5,
            ResourceBrush("Tc.Text", Brushes.White),
            pixelsPerDip);
        double width = text.Width + 14;
        double height = text.Height + 8;
        double x = edge switch
        {
            TouchpadEdge.Left => Math.Min(pad.Right - width - 8, anchor.X + 18),
            TouchpadEdge.Right => Math.Max(pad.Left + 8, anchor.X - width - 18),
            _ => anchor.X - width / 2
        };
        double y = edge switch
        {
            TouchpadEdge.Top => Math.Min(pad.Bottom - height - 8, anchor.Y + 18),
            TouchpadEdge.Bottom => Math.Max(pad.Top + 8, anchor.Y - height - 18),
            _ => anchor.Y - height / 2
        };
        var badge = new Rect(x, y, width, height);
        dc.DrawRoundedRectangle(TransparentClone(accent, 0.22), new Pen(TransparentClone(accent, 0.9), 1), badge, 5, 5);
        dc.DrawText(text, new WpfPoint(badge.Left + 7, badge.Top + 4));
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
            new Typeface("Segoe UI"),
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

    private static string ActionLabel(GestureActionKind action) => action switch
    {
        GestureActionKind.Volume => "Volume",
        GestureActionKind.Brightness => "Brightness",
        GestureActionKind.MediaSeek => "Media seek",
        GestureActionKind.PreviousNextTrack => "Tracks",
        GestureActionKind.PlayPause => "Play / pause",
        GestureActionKind.Mute => "Mute",
        GestureActionKind.TaskView => "Task view",
        GestureActionKind.ShowDesktop => "Desktop",
        GestureActionKind.KeyboardBacklight => "Keyboard light",
        GestureActionKind.PerformanceMode => "Performance",
        GestureActionKind.CustomShortcut => "Shortcut",
        _ => "Off"
    };

    private readonly record struct TrailPoint(int ContactId, int X, int Y, DateTimeOffset Timestamp);
}
