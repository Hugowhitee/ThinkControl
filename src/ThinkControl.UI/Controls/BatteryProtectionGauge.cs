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
/// Compact preservation gauge. Unlike the normal battery gauge, this surface is
/// about the active charge window: one current-level fill, two quiet threshold
/// markers, and state-dependent color. It intentionally avoids permanent
/// green/amber/red zones and decorative charge/pause icons.
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

    public static readonly DependencyProperty IsChargingProperty = DependencyProperty.Register(
        nameof(IsCharging),
        typeof(bool),
        typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public bool IsCharging
    {
        get => (bool)GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 120 || height < 42)
            return;

        WpfBrush surface = ResourceBrush("Tc.SurfaceAlt", WpfBrushes.Transparent);
        WpfBrush border = ResourceBrush("Tc.BorderStrong", WpfBrushes.Gray);
        WpfBrush faint = ResourceBrush("Tc.TextFaint", WpfBrushes.Gray);
        WpfBrush muted = ResourceBrush("Tc.TextMuted", WpfBrushes.Gray);
        WpfBrush accent = ResourceBrush("Tc.Accent", WpfBrushes.DodgerBlue);
        WpfBrush warning = ResourceBrush("Tc.Warning", WpfBrushes.Goldenrod);

        const double left = 8;
        double right = width - 8;
        double trackWidth = Math.Max(1, right - left);
        const double trackTop = 10;
        const double trackHeight = 17;
        var track = new WpfRect(left, trackTop, trackWidth, trackHeight);
        var clip = new RectangleGeometry(track, 5, 5);

        int current = Math.Clamp(CurrentPercent, 0, 100);
        int? start = ProtectionEnabled == true && StartPercent is int rawStart
            ? Math.Clamp(rawStart, 0, 100)
            : null;
        int? stop = ProtectionEnabled == true && StopPercent is int rawStop
            ? Math.Clamp(rawStop, start ?? 0, 100)
            : null;

        WpfBrush fill = ResolveFillBrush(current, start, stop, accent, warning, muted);

        dc.DrawRoundedRectangle(surface, null, track, 5, 5);

        double currentX = PercentX(current);
        if (current > 0)
        {
            dc.PushClip(clip);
            dc.DrawRectangle(
                WithOpacity(fill, ProtectionEnabled == true ? 0.76 : 0.42),
                null,
                new WpfRect(left, trackTop, Math.Max(0, currentX - left), trackHeight));
            dc.Pop();
        }

        if (start is int startValue && stop is int stopValue)
        {
            double startX = PercentX(startValue);
            double stopX = PercentX(stopValue);

            WpfBrush startMarker = IsCharging && current <= startValue ? accent : faint;
            WpfBrush stopMarker = !IsCharging && current >= stopValue ? warning : faint;

            DrawThreshold(dc, startX, track, startMarker, 1.15);
            DrawThreshold(dc, stopX, track, stopMarker, 1.35);

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawThresholdLabel(dc, $"{startValue}%", startX, track.Bottom + 5, startMarker, pixelsPerDip);
            DrawThresholdLabel(dc, $"{stopValue}%", stopX, track.Bottom + 5, stopMarker, pixelsPerDip);
        }
        else if (ProtectionEnabled == false)
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawThresholdLabel(dc, "100%", right, track.Bottom + 5, faint, pixelsPerDip);
        }

        dc.DrawRoundedRectangle(null, new WpfPen(border, 1), track, 5, 5);

        // The current marker sits exactly on the end of the fill. A small surface
        // ring keeps it readable without adding another full-height ruler line.
        double currentY = trackTop + trackHeight / 2d;
        dc.DrawEllipse(
            fill,
            new WpfPen(surface, 2),
            new WpfPoint(currentX, currentY),
            4.1,
            4.1);

        double PercentX(int percent) => left + trackWidth * percent / 100d;
    }

    private WpfBrush ResolveFillBrush(
        int current,
        int? start,
        int? stop,
        WpfBrush accent,
        WpfBrush warning,
        WpfBrush muted)
    {
        if (ProtectionEnabled != true)
            return muted;

        if (stop is int stopValue && current >= stopValue && !IsCharging)
            return warning;

        if (IsCharging)
            return accent;

        // Inside the hold window the same accent remains, just quieter. Color now
        // communicates state rather than permanently painting three unrelated zones.
        return WithOpacity(accent, start is int startValue && current < startValue ? 0.88 : 0.68);
    }

    private static void DrawThreshold(
        DrawingContext dc,
        double x,
        WpfRect track,
        WpfBrush brush,
        double thickness)
    {
        var pen = new WpfPen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        dc.DrawLine(
            pen,
            new WpfPoint(x, track.Top - 2),
            new WpfPoint(x, track.Bottom + 2));
    }

    private void DrawThresholdLabel(
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
            10.2,
            brush,
            pixelsPerDip);

        double x = Math.Clamp(
            centerX - formatted.Width / 2d,
            2,
            Math.Max(2, ActualWidth - formatted.Width - 2));

        dc.DrawText(formatted, new WpfPoint(x, y));
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
