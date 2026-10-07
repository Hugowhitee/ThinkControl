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
/// markers, and a healthy preservation color. It intentionally avoids permanent
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
        WpfBrush success = ResourceBrush("Tc.Success", WpfBrushes.ForestGreen);

        const double left = 8;
        double right = width - 8;
        double trackWidth = Math.Max(1, right - left);
        const double trackTop = 10;
        const double trackHeight = 10;
        var track = new WpfRect(left, trackTop, trackWidth, trackHeight);
        var clip = new RectangleGeometry(track, 5, 5);

        int current = Math.Clamp(CurrentPercent, 0, 100);
        int? start = ProtectionEnabled == true && StartPercent is int rawStart
            ? Math.Clamp(rawStart, 0, 100)
            : null;
        int? stop = ProtectionEnabled == true && StopPercent is int rawStop
            ? Math.Clamp(rawStop, start ?? 0, 100)
            : null;

        // Preservation is a healthy operating state, including when charging is
        // paused at the limit. Red/amber here misleadingly suggested a fault.
        WpfBrush fill = ProtectionEnabled == true ? success : muted;

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

            WpfBrush startMarker = IsCharging && current <= startValue ? success : faint;
            WpfBrush stopMarker = muted;

            DrawThreshold(dc, startX, track, startMarker, 1.15);
            DrawThreshold(dc, stopX, track, stopMarker, 1.35);

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawThresholdLabels(
                dc,
                $"{startValue}%",
                startX,
                startMarker,
                $"{stopValue}%",
                stopX,
                stopMarker,
                track.Bottom + 8,
                pixelsPerDip);
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

    private void DrawThresholdLabels(
        DrawingContext dc,
        string startLabel,
        double startCenterX,
        WpfBrush startBrush,
        string stopLabel,
        double stopCenterX,
        WpfBrush stopBrush,
        double y,
        double pixelsPerDip)
    {
        FormattedText startText = CreateThresholdLabel(startLabel, startBrush, pixelsPerDip);
        FormattedText stopText = CreateThresholdLabel(stopLabel, stopBrush, pixelsPerDip);

        const double edge = 2;
        const double minimumGap = 4;
        double startMaxX = Math.Max(edge, ActualWidth - startText.Width - edge);
        double stopMaxX = Math.Max(edge, ActualWidth - stopText.Width - edge);
        double startX = Math.Clamp(startCenterX - startText.Width / 2d, edge, startMaxX);
        double stopX = Math.Clamp(stopCenterX - stopText.Width / 2d, edge, stopMaxX);

        // Five-percent preservation windows put the markers only a few pixels
        // farther apart than the label widths on the compact card. Resolve that
        // collision symmetrically while keeping both labels beside their markers.
        double overlap = startX + startText.Width + minimumGap - stopX;
        if (overlap > 0)
        {
            double shift = overlap / 2d;
            startX = Math.Max(edge, startX - shift);
            stopX = Math.Min(stopMaxX, stopX + shift);

            // If either edge absorbed part of the symmetric shift, move the other
            // label just enough to preserve a readable gap.
            if (startX + startText.Width + minimumGap > stopX)
            {
                double preferredStart = stopX - minimumGap - startText.Width;
                if (preferredStart >= edge)
                    startX = preferredStart;
                else
                    stopX = Math.Min(stopMaxX, edge + startText.Width + minimumGap);
            }
        }

        dc.DrawText(startText, new WpfPoint(startX, y));
        dc.DrawText(stopText, new WpfPoint(stopX, y));
    }

    private void DrawThresholdLabel(
        DrawingContext dc,
        string label,
        double centerX,
        double y,
        WpfBrush brush,
        double pixelsPerDip)
    {
        FormattedText formatted = CreateThresholdLabel(label, brush, pixelsPerDip);
        double x = Math.Clamp(
            centerX - formatted.Width / 2d,
            2,
            Math.Max(2, ActualWidth - formatted.Width - 2));

        dc.DrawText(formatted, new WpfPoint(x, y));
    }

    private static FormattedText CreateThresholdLabel(
        string label,
        WpfBrush brush,
        double pixelsPerDip) =>
        new(
            label,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            ThinkControl.UI.TypographyScale.Typeface,
            10.2,
            brush,
            pixelsPerDip);

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
