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

/// <summary>Measured battery level over the charge/resume window. The band
/// distinguishes the preservation window by a quiet tint, with a neutral excluded
/// zone above the limit. Charging phase comes from the existing battery gauge.</summary>
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

    public static readonly DependencyProperty IsPluggedInProperty = DependencyProperty.Register(
        nameof(IsPluggedIn), typeof(bool), typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsPluggedIn { get => (bool)GetValue(IsPluggedInProperty); set => SetValue(IsPluggedInProperty, value); }

    internal int? ActiveBoundaryPercent => ProtectionEnabled == true && IsPluggedIn &&
        StartPercent is int start && StopPercent is int stop
            ? IsCharging ? stop : CurrentPercent >= start ? start : null
            : null;

    public static readonly DependencyProperty FlowPhaseProperty = DependencyProperty.Register(
        nameof(FlowPhase), typeof(double), typeof(BatteryProtectionGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double FlowPhase { get => (double)GetValue(FlowPhaseProperty); set => SetValue(FlowPhaseProperty, value); }

    public BatteryProtectionGauge()
    {
        MouseMove += (_, e) =>
        {
            int percent = (int)Math.Round(Math.Clamp(e.GetPosition(this).X / Math.Max(1, ActualWidth), 0, 1) * 100);
            string zone = ProtectionEnabled != true ? "Preservation off"
                : percent >= StopPercent ? "Above charge limit"
                : percent >= StartPercent ? "Preservation window" : "Below resume threshold";
            string thresholds = ProtectionEnabled == true && StartPercent is int resume && StopPercent is int stop
                ? $" Resume below {resume}%; stop at {stop}%." : string.Empty;
            string target = ActiveBoundaryPercent is int boundary
                ? IsCharging ? $" Charging toward {boundary}%." : $" Holding; resumes below {boundary}%."
                : string.Empty;
            ToolTip = $"{percent}% · {zone}. Current charge: {CurrentPercent}%.{thresholds}{target}";
        };
    }

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

        const double left = 0;
        double right = width;
        double trackWidth = Math.Max(1, right - left);
        const double trackTop = 18;
        const double trackHeight = 18;
        var track = new WpfRect(left, trackTop, trackWidth, trackHeight);
        var clip = new RectangleGeometry(track, 5, 5);

        int current = Math.Clamp(CurrentPercent, 0, 100);
        int? start = ProtectionEnabled == true && StartPercent is int rawStart
            ? Math.Clamp(rawStart, 0, 100)
            : null;
        int? stop = ProtectionEnabled == true && StopPercent is int rawStop
            ? Math.Clamp(rawStop, start ?? 0, 100)
            : null;

        WpfBrush fill = ProtectionEnabled == true ? success : muted;
        dc.DrawRoundedRectangle(surface, null, track, 5, 5);
        double currentX = PercentX(current);
        dc.PushClip(clip);
        if (start is int resume && stop is int limit)
        {
            double resumeX = PercentX(resume);
            double limitX = PercentX(limit);
            dc.DrawRectangle(WithOpacity(faint, 0.10), null,
                new WpfRect(left, trackTop, resumeX, trackHeight));
            dc.DrawRectangle(WithOpacity(success, 0.24), null,
                new WpfRect(resumeX, trackTop, limitX - resumeX, trackHeight));
            dc.DrawRectangle(WithOpacity(muted, 0.28), null,
                new WpfRect(limitX, trackTop, right - limitX, trackHeight));
        }
        // Measured charge fills the track. Capacity beyond the configured limit
        // remains neutral, even when the battery was previously charged higher.
        double railEnd = PercentX(stop is int cap ? Math.Min(current, cap) : current);
        dc.DrawRectangle(fill, null,
            new WpfRect(left, trackTop, railEnd, trackHeight));
        if (currentX > railEnd)
            dc.DrawRectangle(WithOpacity(muted, 0.55), null,
                new WpfRect(railEnd, trackTop, currentX - railEnd, trackHeight));
        if (IsCharging && IsPluggedIn && railEnd > 0)
        {
            // One soft sweep uses the battery's existing motion owner. It is
            // clipped to real charge and never simulates a rising percentage.
            var glow = new LinearGradientBrush();
            glow.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            glow.GradientStops.Add(new GradientStop(Colors.White, 0.5));
            glow.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            glow.Opacity = 0.25;
            dc.PushClip(new RectangleGeometry(new WpfRect(left, trackTop, railEnd, trackHeight)));
            double sweepX = (FlowPhase % 16) / 16d * (railEnd + 80) - 80;
            dc.DrawRectangle(glow, null, new WpfRect(sweepX, trackTop, 80, trackHeight));
            dc.Pop();
        }
        dc.Pop();

        if (start is int startValue && stop is int stopValue)
        {
            double startX = PercentX(startValue);
            double stopX = PercentX(stopValue);

            WpfBrush startMarker = muted;
            WpfBrush stopMarker = muted;

            DrawThreshold(dc, startX, track, startMarker, 2);
            DrawThreshold(dc, stopX, track, stopMarker, 2);

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawThresholdLabels(
                dc,
                $"Resume {startValue}%",
                startMarker,
                $"Limit {stopValue}%",
                stopMarker,
                track.Bottom + 13,
                pixelsPerDip);
        }
        else if (ProtectionEnabled == false)
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawThresholdLabel(dc, "100%", right, track.Bottom + 5, faint, pixelsPerDip);
        }

        if (ActiveBoundaryPercent is int targetBoundary)
        {
            string status = IsCharging ? $"Charging to {targetBoundary}%" : $"Charge hold · resumes below {targetBoundary}%";
            dc.DrawText(CreateThresholdLabel(status, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new WpfPoint(0, 0));
        }
        dc.DrawRoundedRectangle(null, new WpfPen(border, 1), track, 5, 5);

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
            new WpfPoint(x, track.Top + 2),
            new WpfPoint(x, track.Bottom - 2));
    }

    private void DrawThresholdLabels(
        DrawingContext dc,
        string startLabel,
        WpfBrush startBrush,
        string stopLabel,
        WpfBrush stopBrush,
        double y,
        double pixelsPerDip)
    {
        FormattedText startText = CreateThresholdLabel(startLabel, startBrush, pixelsPerDip);
        FormattedText stopText = CreateThresholdLabel(stopLabel, stopBrush, pixelsPerDip);

        // Both boundary roles share one left-aligned legend. Their small marks
        // remain inside the track; no angled leaders or floating arrows are needed.
        dc.DrawText(startText, new WpfPoint(0, y));
        dc.DrawText(stopText, new WpfPoint(startText.Width + 12, y));
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
            11.5,
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
