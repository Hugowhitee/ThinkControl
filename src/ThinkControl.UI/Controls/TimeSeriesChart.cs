using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ThinkControl.UI.Controls;

public sealed record TimeSeriesPoint(DateTimeOffset At, double Value, string? Label = null);

/// <summary>
/// Lightweight reusable telemetry chart. Time lives on the x axis, values on the
/// y axis, live sessions can stay right-aligned, and the nearest measured sample
/// is exposed only while the pointer is over the plot.
/// </summary>
public sealed class TimeSeriesChart : FrameworkElement
{
    private const double LeftAxis = 50;
    private const double RightPadding = 12;
    private const double TopPadding = 12;
    private const double BottomAxis = 30;

    private Point? _hover;
    private INotifyCollectionChanged? _observableValues;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<TimeSeriesPoint>), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(
        nameof(ValueFormat), typeof(string), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata("0.#", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IncludeZeroProperty = DependencyProperty.Register(
        nameof(IncludeZero), typeof(bool), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LiveWindowMinutesProperty = DependencyProperty.Register(
        nameof(LiveWindowMinutes), typeof(double), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GapThresholdMinutesProperty = DependencyProperty.Register(
        nameof(GapThresholdMinutes), typeof(double), typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<TimeSeriesPoint>? Values { get => (IEnumerable<TimeSeriesPoint>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string ValueFormat { get => (string)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public bool IncludeZero { get => (bool)GetValue(IncludeZeroProperty); set => SetValue(IncludeZeroProperty, value); }
    public bool IsLive { get => (bool)GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }
    public double LiveWindowMinutes { get => (double)GetValue(LiveWindowMinutesProperty); set => SetValue(LiveWindowMinutesProperty, value); }
    public double GapThresholdMinutes { get => (double)GetValue(GapThresholdMinutesProperty); set => SetValue(GapThresholdMinutesProperty, value); }

    public TimeSeriesChart()
    {
        MinHeight = 110;
        Cursor = Cursors.Cross;
        SnapsToDevicePixels = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _hover = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Rect plot = new(LeftAxis, TopPadding, Math.Max(1, ActualWidth - LeftAxis - RightPadding), Math.Max(1, ActualHeight - TopPadding - BottomAxis));
        Brush gridBrush = ResourceBrush("Tc.Border", Brushes.DimGray);
        Brush axisBrush = ResourceBrush("Tc.TextFaint", Brushes.Gray);
        Brush textBrush = ResourceBrush("Tc.TextMuted", Brushes.Gray);
        Brush accent = ResourceBrush("Tc.Accent", Brushes.Red);
        Brush surface = ResourceBrush("Tc.SurfaceAlt", Brushes.Black);
        Brush foreground = ResourceBrush("Tc.Text", Brushes.White);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        DrawGrid(dc, plot, gridBrush);

        TimeSeriesPoint[] all = (Values ?? Array.Empty<TimeSeriesPoint>()).Where(point => double.IsFinite(point.Value)).OrderBy(point => point.At).ToArray();
        if (all.Length == 0)
        {
            DrawText(dc, "Waiting for telemetry", new Point(plot.Left + 8, plot.Top + plot.Height / 2), 10.5, axisBrush);
            return;
        }

        (DateTimeOffset xMin, DateTimeOffset xMax) = ResolveTimeRange(all);
        TimeSeriesPoint[] visible = all.Where(point => point.At >= xMin && point.At <= xMax).ToArray();
        if (visible.Length == 0) visible = [all[^1]];
        (double yMin, double yMax) = ResolveValueRange(visible);
        DrawAxes(dc, plot, xMin, xMax, yMin, yMax, textBrush);
        DrawSeries(dc, plot, visible, xMin, xMax, yMin, yMax, accent);

        if (_hover is Point hover && plot.Contains(hover))
            DrawHover(dc, plot, visible, xMin, xMax, yMin, yMax, hover, gridBrush, accent, surface, foreground);
    }

    private void DrawSeries(DrawingContext dc, Rect plot, IReadOnlyList<TimeSeriesPoint> visible, DateTimeOffset xMin, DateTimeOffset xMax, double yMin, double yMax, Brush accent)
    {
        if (visible.Count == 1)
        {
            Point p = Map(visible[0], plot, xMin, xMax, yMin, yMax);
            dc.DrawEllipse(accent, null, p, 2.6, 2.6);
            return;
        }

        TimeSpan gap = GapThresholdMinutes > 0 && double.IsFinite(GapThresholdMinutes)
            ? TimeSpan.FromMinutes(Math.Clamp(GapThresholdMinutes, 0.1, 24 * 60))
            : TimeSpan.MaxValue;
        var pen = new Pen(accent, 1.6);
        int segmentStart = 0;
        for (int i = 1; i <= visible.Count; i++)
        {
            bool end = i == visible.Count;
            bool breakHere = !end && visible[i].At - visible[i - 1].At > gap;
            if (!end && !breakHere) continue;

            int segmentEnd = i - 1;
            if (segmentEnd == segmentStart)
            {
                Point p = Map(visible[segmentStart], plot, xMin, xMax, yMin, yMax);
                dc.DrawEllipse(accent, null, p, 2.3, 2.3);
            }
            else
            {
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(Map(visible[segmentStart], plot, xMin, xMax, yMin, yMax), false, false);
                    for (int j = segmentStart + 1; j <= segmentEnd; j++)
                        context.LineTo(Map(visible[j], plot, xMin, xMax, yMin, yMax), true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }
            segmentStart = i;
        }
    }

    private (DateTimeOffset Min, DateTimeOffset Max) ResolveTimeRange(IReadOnlyList<TimeSeriesPoint> points)
    {
        DateTimeOffset first = points[0].At;
        DateTimeOffset last = points[^1].At;
        if (IsLive)
        {
            DateTimeOffset end = DateTimeOffset.UtcNow > last ? DateTimeOffset.UtcNow : last;
            double minutes = Math.Clamp(double.IsFinite(LiveWindowMinutes) ? LiveWindowMinutes : 60d, 2d, 24d * 60d);
            return (end - TimeSpan.FromMinutes(minutes), end);
        }
        if (last <= first) return (first - TimeSpan.FromMinutes(1), first + TimeSpan.FromMinutes(1));
        TimeSpan span = last - first;
        TimeSpan pad = TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(5).Ticks, span.Ticks / 30));
        return (first - pad, last + pad);
    }

    private (double Min, double Max) ResolveValueRange(IReadOnlyList<TimeSeriesPoint> points)
    {
        double min = points.Min(point => point.Value);
        double max = points.Max(point => point.Value);
        if (IncludeZero) min = Math.Min(0, min);
        if (Math.Abs(max - min) < 0.001)
        {
            double expansion = Math.Max(1, Math.Abs(max) * 0.1);
            min -= expansion; max += expansion;
        }
        else
        {
            double pad = (max - min) * 0.10;
            min -= pad; max += pad;
            if (IncludeZero && min < 0 && points.All(point => point.Value >= 0)) min = 0;
        }
        return (min, max);
    }

    private static void DrawGrid(DrawingContext dc, Rect plot, Brush brush)
    {
        var pen = new Pen(brush, 0.7);
        for (int i = 0; i <= 3; i++)
        {
            double y = plot.Top + plot.Height * i / 3d;
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
        for (int i = 0; i <= 4; i++)
        {
            double x = plot.Left + plot.Width * i / 4d;
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
    }

    private void DrawAxes(DrawingContext dc, Rect plot, DateTimeOffset xMin, DateTimeOffset xMax, double yMin, double yMax, Brush brush)
    {
        for (int i = 0; i <= 2; i++)
        {
            double fraction = i / 2d;
            double value = yMax - (yMax - yMin) * fraction;
            double y = plot.Top + plot.Height * fraction;
            DrawText(dc, FormatValue(value), new Point(plot.Left - 7, y), 9, brush, rightAligned: true, verticallyCentered: true);
        }
        TimeSpan span = xMax - xMin;
        for (int i = 0; i <= 2; i++)
        {
            double fraction = i / 2d;
            DateTimeOffset at = xMin + TimeSpan.FromTicks((long)(span.Ticks * fraction));
            double x = plot.Left + plot.Width * fraction;
            DrawText(dc, FormatTime(at.ToLocalTime(), span), new Point(x, plot.Bottom + 8), 9, brush, centered: true);
        }
    }

    private void DrawHover(DrawingContext dc, Rect plot, IReadOnlyList<TimeSeriesPoint> points, DateTimeOffset xMin, DateTimeOffset xMax, double yMin, double yMax, Point hover, Brush gridBrush, Brush accent, Brush surface, Brush foreground)
    {
        if (points.Count == 0) return;
        TimeSeriesPoint nearest = points.OrderBy(point => Math.Abs(Map(point, plot, xMin, xMax, yMin, yMax).X - hover.X)).First();
        Point p = Map(nearest, plot, xMin, xMax, yMin, yMax);
        var guide = new Pen(gridBrush, 1) { DashStyle = DashStyles.Dot };
        dc.DrawLine(guide, new Point(p.X, plot.Top), new Point(p.X, plot.Bottom));
        dc.DrawLine(guide, new Point(plot.Left, p.Y), new Point(plot.Right, p.Y));
        dc.DrawEllipse(surface, new Pen(accent, 1.5), p, 4.5, 4.5);
        dc.DrawEllipse(accent, null, p, 2.2, 2.2);
        TimeSpan span = xMax - xMin;
        string time = FormatHoverTime(nearest.At.ToLocalTime(), span);
        string value = FormatValue(nearest.Value);
        string text = string.IsNullOrWhiteSpace(nearest.Label) ? $"{time}  ·  {value}" : $"{nearest.Label}  ·  {time}  ·  {value}";
        FormattedText ft = CreateFormattedText(text, 10, foreground);
        double boxWidth = ft.Width + 16;
        double boxHeight = ft.Height + 10;
        double left = p.X + 10;
        if (left + boxWidth > plot.Right) left = p.X - boxWidth - 10;
        left = Math.Clamp(left, plot.Left, Math.Max(plot.Left, plot.Right - boxWidth));
        double top = Math.Clamp(p.Y - boxHeight - 10, plot.Top, Math.Max(plot.Top, plot.Bottom - boxHeight));
        dc.DrawRoundedRectangle(surface, new Pen(gridBrush, 1), new Rect(left, top, boxWidth, boxHeight), 4, 4);
        dc.DrawText(ft, new Point(left + 8, top + 5));

        // The tooltip already owns the exact hovered time/value. Drawing a second
        // red label over both axes made digits collide with the normal tick labels.
    }

    private static Point Map(TimeSeriesPoint point, Rect plot, DateTimeOffset xMin, DateTimeOffset xMax, double yMin, double yMax)
    {
        double totalMs = Math.Max(1, (xMax - xMin).TotalMilliseconds);
        double x = plot.Left + Math.Clamp((point.At - xMin).TotalMilliseconds / totalMs, 0, 1) * plot.Width;
        double yFraction = Math.Clamp((point.Value - yMin) / Math.Max(0.0001, yMax - yMin), 0, 1);
        return new Point(x, plot.Bottom - yFraction * plot.Height);
    }

    private string FormatValue(double value)
    {
        string format = string.IsNullOrWhiteSpace(ValueFormat) ? "0.#" : ValueFormat;
        string formatted;
        try { formatted = value.ToString(format, CultureInfo.CurrentCulture); }
        catch (FormatException) { formatted = value.ToString("0.#", CultureInfo.CurrentCulture); }
        return string.IsNullOrWhiteSpace(Unit) ? formatted : $"{formatted} {Unit}";
    }

    private static string FormatTime(DateTimeOffset local, TimeSpan span) => span >= TimeSpan.FromDays(2) ? local.ToString("d MMM", CultureInfo.CurrentCulture) : span >= TimeSpan.FromHours(20) ? local.ToString("ddd HH:mm", CultureInfo.CurrentCulture) : local.ToString("HH:mm", CultureInfo.CurrentCulture);
    private static string FormatHoverTime(DateTimeOffset local, TimeSpan span) => span >= TimeSpan.FromDays(1) ? local.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) : local.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    private void DrawText(DrawingContext dc, string text, Point origin, double size, Brush brush, bool centered = false, bool rightAligned = false, bool verticallyCentered = false)
    {
        FormattedText ft = CreateFormattedText(text, size, brush);
        double x = centered ? origin.X - ft.Width / 2 : rightAligned ? origin.X - ft.Width : origin.X;
        double y = verticallyCentered ? origin.Y - ft.Height / 2 : origin.Y;
        dc.DrawText(ft, new Point(x, y));
    }

    private FormattedText CreateFormattedText(string text, double size, Brush brush) => new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private Brush ResourceBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TimeSeriesChart)d;
        if (chart._observableValues is not null) chart._observableValues.CollectionChanged -= chart.Values_CollectionChanged;
        chart._observableValues = e.NewValue as INotifyCollectionChanged;
        if (chart._observableValues is not null) chart._observableValues.CollectionChanged += chart.Values_CollectionChanged;
        chart.InvalidateVisual();
    }

    private void Values_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
}
