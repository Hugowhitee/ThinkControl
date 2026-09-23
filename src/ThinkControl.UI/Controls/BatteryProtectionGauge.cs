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
/// Compact threshold ruler for Battery Preservation. It reuses ThinkControl theme
/// resources and shows the real start/stop window plus the current battery position.
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
        if (width < 80 || height < 40)
            return;

        WpfBrush surface = ResourceBrush("Tc.SurfaceAlt", WpfBrushes.Transparent);
        WpfBrush border = ResourceBrush("Tc.BorderStrong", WpfBrushes.Gray);
        WpfBrush muted = ResourceBrush("Tc.TextFaint", WpfBrushes.Gray);
        WpfBrush text = ResourceBrush("Tc.Text", WpfBrushes.White);
        WpfBrush accent = ResourceBrush("Tc.Accent", WpfBrushes.Red);

        const double left = 7;
        double right = width - 7;
        double trackWidth = Math.Max(1, right - left);
        const double trackTop = 23;
        const double trackHeight = 12;

        var track = new WpfRect(left, trackTop, trackWidth, trackHeight);
        dc.DrawRoundedRectangle(surface, new WpfPen(border, 1), track, 4, 4);

        if (ProtectionEnabled == true && StartPercent is int rawStart && StopPercent is int rawStop)
        {
            int start = Math.Clamp(rawStart, 0, 100);
            int stop = Math.Clamp(rawStop, start, 100);
            double startX = left + trackWidth * start / 100d;
            double stopX = left + trackWidth * stop / 100d;
            var active = new WpfRect(startX, trackTop, Math.Max(2, stopX - startX), trackHeight);
            dc.DrawRoundedRectangle(accent, null, active, 3, 3);
            DrawLock(dc, (startX + stopX) / 2d, 12, accent);
        }

        var tickPen = new WpfPen(muted, 1);
        for (int step = 0; step <= 10; step++)
        {
            double x = left + trackWidth * step / 10d;
            double tickTop = step is 0 or 5 or 10 ? trackTop - 5 : trackTop - 3;
            double tickBottom = track.Bottom + (step is 0 or 5 or 10 ? 5 : 3);
            dc.DrawLine(tickPen, new WpfPoint(x, tickTop), new WpfPoint(x, tickBottom));
        }

        int current = Math.Clamp(CurrentPercent, 0, 100);
        double currentX = left + trackWidth * current / 100d;
        var markerPen = new WpfPen(text, 1.5);
        dc.DrawLine(markerPen, new WpfPoint(currentX, trackTop - 8), new WpfPoint(currentX, track.Bottom + 8));
        dc.DrawEllipse(text, null, new WpfPoint(currentX, trackTop - 8), 2.4, 2.4);
    }

    private static void DrawLock(DrawingContext dc, double centerX, double centerY, WpfBrush brush)
    {
        var pen = new WpfPen(brush, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var shackle = new StreamGeometry();
        using (StreamGeometryContext geometry = shackle.Open())
        {
            geometry.BeginFigure(new WpfPoint(centerX - 4.5, centerY + 1.5), false, false);
            geometry.BezierTo(
                new WpfPoint(centerX - 4.5, centerY - 4.5),
                new WpfPoint(centerX + 4.5, centerY - 4.5),
                new WpfPoint(centerX + 4.5, centerY + 1.5),
                true,
                false);
        }
        shackle.Freeze();
        dc.DrawGeometry(null, pen, shackle);
        dc.DrawRoundedRectangle(
            brush,
            null,
            new WpfRect(centerX - 6, centerY + 1, 12, 9),
            2,
            2);
    }

    private static WpfBrush ResourceBrush(string key, WpfBrush fallback) =>
        WpfApplication.Current?.TryFindResource(key) as WpfBrush ?? fallback;
}
