using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Semantic Battery Preservation range: green = charging may resume below the
/// start threshold, amber = hysteresis/hold band, red = charging is stopped at
/// the upper threshold. Only meaningful thresholds and the live battery position
/// are marked; there is deliberately no generic ruler/tick noise.
/// </summary>
public sealed class BatteryProtectionGauge : FrameworkElement
{
    public static readonly DependencyProperty ProtectionEnabledProperty = DependencyProperty.Register(
        nameof(ProtectionEnabled),
        typeof(bool?),
        typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartPercentProperty = DependencyProperty.Register(
        nameof(StartPercent),
        typeof(int?),
        typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StopPercentProperty = DependencyProperty.Register(
        nameof(StopPercent),
        typeof(int?),
        typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentPercentProperty = DependencyProperty.Register(
        nameof(CurrentPercent),
        typeof(int),
        typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool? ProtectionEnabled
    {
        get => (bool?)GetValue(ProtectionEnabledProperty);
        set => SetValue(ProtectionEnabledProperty, value);
    }

    public int? StartPercent
    {
        get => (int?)GetValue(StartPercentProperty);
        set => SetValue(StartPercentProperty, value);
    }

    public int? StopPercent
    {
        get => (int?)GetValue(StopPercentProperty);
        set => SetValue(StopPercentProperty, value);
    }

    public int CurrentPercent
    {
        get => (int)GetValue(CurrentPercentProperty);
        set => SetValue(CurrentPercentProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 120 || height < 52)
            return;

        WpfBrush surface = ResourceBrush("Tc.SurfaceAlt", WpfBrushes.Transparent);
        WpfBrush border = ResourceBrush("Tc.BorderStrong", WpfBrushes.Gray);
        WpfBrush faint = ResourceBrush("Tc.TextFaint", WpfBrushes.Gray);
        WpfBrush text = ResourceBrush("Tc.Text", WpfBrushes.White);
        WpfBrush success = ResourceBrush("Tc.Success", WpfBrushes.Green);
        WpfBrush warning = ResourceBrush("Tc.Warning", WpfBrushes.Goldenrod);
        WpfBrush accent = ResourceBrush("Tc.Accent", WpfBrushes.Red);

        const double left = 8;
        double right = width - 8;
        double trackWidth = Math.Max(1, right - left);
        const double trackTop = 23;
        const double trackHeight = 14;
        var track = new WpfRect(left, trackTop, trackWidth, trackHeight);
        var clip = new RectangleGeometry(track, 4, 4);

        dc.DrawRoundedRectangle(surface, null, track, 4, 4);

        int? start = null;
        int? stop = null;
        double startX = left;
        double stopX = right;

        if (ProtectionEnabled == true && StartPercent is int rawStart && StopPercent is int rawStop)
        {
            start = Math.Clamp(rawStart, 0, 100);
            stop = Math.Clamp(rawStop, start.Value, 100);
            startX = PercentX(start.Value);
            stopX = PercentX(stop.Value);

            dc.PushClip(clip);
            dc.DrawRectangle(
                WithOpacity(success, 0.34),
                null,
                new WpfRect(left, trackTop, Math.Max(0, startX - left), trackHeight));
            dc.DrawRectangle(
                WithOpacity(warning, 0.27),
                null,
                new WpfRect(startX, trackTop, Math.Max(0, stopX - startX), trackHeight));
            dc.DrawRectangle(
                WithOpacity(accent, 0.18),
                null,
                new WpfRect(stopX, trackTop, Math.Max(0, right - stopX), trackHeight));
            dc.Pop();

            DrawThreshold(dc, startX, track, success);
            DrawThreshold(dc, stopX, track, accent);
            DrawLightning(dc, startX, 11, success);
            DrawPause(dc, stopX, 11, accent);

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawPercentLabel(dc, $"{start.Value}%", startX, track.Bottom + 5, success, pixelsPerDip);
            DrawPercentLabel(dc, $"{stop.Value}%", stopX, track.Bottom + 5, accent, pixelsPerDip);
        }

        dc.DrawRoundedRectangle(null, new WpfPen(border, 1), track, 4, 4);

        // Current battery position is the only non-threshold marker. A bright ring
        // stays legible over all three semantic zones without introducing another
        // range color.
        int current = Math.Clamp(CurrentPercent, 0, 100);
        double currentX = PercentX(current);
        double currentY = trackTop + trackHeight / 2d;
        dc.DrawLine(
            new WpfPen(text, 1.35),
            new WpfPoint(currentX, trackTop - 3),
            new WpfPoint(currentX, track.Bottom + 3));
        dc.DrawEllipse(surface, new WpfPen(text, 1.6), new WpfPoint(currentX, currentY), 3.5, 3.5);

        if (ProtectionEnabled != true)
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawCenteredText(dc, "Full charge", width / 2d, track.Bottom + 5, faint, pixelsPerDip);
        }

        double PercentX(int percent) => left + trackWidth * percent / 100d;
    }

    private static void DrawThreshold(DrawingContext dc, double x, WpfRect track, WpfBrush brush)
    {
        var pen = new WpfPen(brush, 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(pen, new WpfPoint(x, track.Top - 2), new WpfPoint(x, track.Bottom + 2));
    }

    private static void DrawLightning(DrawingContext dc, double centerX, double centerY, WpfBrush brush)
    {
        var bolt = new StreamGeometry();
        using (StreamGeometryContext geometry = bolt.Open())
        {
            geometry.BeginFigure(new WpfPoint(centerX + 1, centerY - 6), true, true);
            geometry.LineTo(new WpfPoint(centerX - 4, centerY + 1), true, false);
            geometry.LineTo(new WpfPoint(centerX, centerY + 1), true, false);
            geometry.LineTo(new WpfPoint(centerX - 1, centerY + 7), true, false);
            geometry.LineTo(new WpfPoint(centerX + 5, centerY - 1), true, false);
            geometry.LineTo(new WpfPoint(centerX + 1, centerY - 1), true, false);
        }
        bolt.Freeze();
        dc.DrawGeometry(brush, null, bolt);
    }

    private static void DrawPause(DrawingContext dc, double centerX, double centerY, WpfBrush brush)
    {
        dc.DrawRoundedRectangle(
            brush,
            null,
            new WpfRect(centerX - 4.5, centerY - 5.5, 3, 11),
            1,
            1);
        dc.DrawRoundedRectangle(
            brush,
            null,
            new WpfRect(centerX + 1.5, centerY - 5.5, 3, 11),
            1,
            1);
    }

    private void DrawPercentLabel(
        DrawingContext dc,
        string label,
        double centerX,
        double y,
        WpfBrush brush,
        double pixelsPerDip)
    {
        var formatted = new FormattedText(
            label,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Text"),
            10.5,
            brush,
            pixelsPerDip);

        double x = Math.Clamp(centerX - formatted.Width / 2d, 2, Math.Max(2, ActualWidth - formatted.Width - 2));
        dc.DrawText(formatted, new WpfPoint(x, y));
    }

    private void DrawCenteredText(
        DrawingContext dc,
        string label,
        double centerX,
        double y,
        WpfBrush brush,
        double pixelsPerDip)
    {
        var formatted = new FormattedText(
            label,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Text"),
            10.5,
            brush,
            pixelsPerDip);
        dc.DrawText(formatted, new WpfPoint(centerX - formatted.Width / 2d, y));
    }

    private static WpfBrush WithOpacity(WpfBrush source, double opacity)
    {
        WpfBrush brush = source.CloneCurrentValue();
        brush.Opacity = Math.Clamp(opacity, 0, 1);
        if (brush.CanFreeze)
            brush.Freeze();
        return brush;
    }

    private static WpfBrush ResourceBrush(string key, WpfBrush fallback) =>
        WpfApplication.Current?.TryFindResource(key) as WpfBrush ?? fallback;
}
