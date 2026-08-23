using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Minimal battery indicator that uses the real percentage. While charging the fill
/// becomes green and shows a subtle moving diagonal flow. Rendering is hooked only
/// while the element is visible and charging so the animation has no idle cost.
/// </summary>
public sealed class BatteryGauge : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent),
        typeof(int),
        typeof(BatteryGauge),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsChargingProperty = DependencyProperty.Register(
        nameof(IsCharging),
        typeof(bool),
        typeof(BatteryGauge),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnChargingChanged));

    private bool _renderHooked;
    private TimeSpan _lastRenderingTime;
    private double _stripePhase;

    public BatteryGauge()
    {
        Loaded += (_, _) => UpdateRenderingHook();
        Unloaded += (_, _) => StopRendering();
        IsVisibleChanged += (_, _) => UpdateRenderingHook();
    }

    public int Percent
    {
        get => (int)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public bool IsCharging
    {
        get => (bool)GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    private static void OnChargingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gauge = (BatteryGauge)d;
        gauge.UpdateRenderingHook();
        gauge.InvalidateVisual();
    }

    private void UpdateRenderingHook()
    {
        bool shouldAnimate = IsLoaded && IsVisible && IsCharging;
        if (shouldAnimate && !_renderHooked)
        {
            _lastRenderingTime = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
            _renderHooked = true;
        }
        else if (!shouldAnimate)
        {
            StopRendering();
        }
    }

    private void StopRendering()
    {
        if (!_renderHooked)
            return;
        CompositionTarget.Rendering -= OnRendering;
        _renderHooked = false;
        _lastRenderingTime = TimeSpan.Zero;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
            return;

        if (_lastRenderingTime == TimeSpan.Zero)
        {
            _lastRenderingTime = args.RenderingTime;
            return;
        }

        double seconds = Math.Clamp((args.RenderingTime - _lastRenderingTime).TotalSeconds, 0, 0.1);
        _lastRenderingTime = args.RenderingTime;
        _stripePhase = (_stripePhase + seconds * 26d) % 24d;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 20 || height < 16)
            return;

        Brush borderBrush = Application.Current?.TryFindResource("Tc.BorderStrong") as Brush ?? Brushes.Gray;
        Brush surfaceBrush = Application.Current?.TryFindResource("Tc.Surface") as Brush ?? Brushes.Transparent;
        var borderPen = new Pen(borderBrush, 1.4);
        borderPen.Freeze();

        double terminalWidth = Math.Max(4, width * 0.045);
        double bodyWidth = width - terminalWidth - 2;
        double radius = Math.Min(7, height * 0.16);
        var body = new WpfRect(0.7, 0.7, Math.Max(1, bodyWidth - 1.4), Math.Max(1, height - 1.4));
        dc.DrawRoundedRectangle(surfaceBrush, borderPen, body, radius, radius);

        double terminalHeight = height * 0.36;
        var terminal = new WpfRect(bodyWidth + 1, (height - terminalHeight) / 2, terminalWidth, terminalHeight);
        dc.DrawRoundedRectangle(borderBrush, null, terminal, 2, 2);

        int percent = Math.Clamp(Percent, 0, 100);
        double innerPadding = 4;
        double innerWidth = Math.Max(0, body.Width - innerPadding * 2);
        double innerHeight = Math.Max(0, body.Height - innerPadding * 2);
        double fillWidth = innerWidth * percent / 100d;
        if (fillWidth <= 0.5 || innerHeight <= 0.5)
            return;

        Color fillColor = IsCharging
            ? Color.FromRgb(58, 170, 93)
            : InterpolateBatteryColor(percent);
        var fillBrush = new SolidColorBrush(fillColor);
        fillBrush.Freeze();
        var fill = new WpfRect(
            body.X + innerPadding,
            body.Y + innerPadding,
            fillWidth,
            innerHeight);
        double fillRadius = Math.Min(4, radius);
        dc.DrawRoundedRectangle(fillBrush, null, fill, fillRadius, fillRadius);

        if (IsCharging)
            DrawChargeFlow(dc, fill, fillRadius);
    }

    private void DrawChargeFlow(DrawingContext dc, WpfRect fill, double radius)
    {
        var clip = new RectangleGeometry(fill, radius, radius);
        dc.PushClip(clip);

        var stripeBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        stripeBrush.Freeze();
        var stripePen = new Pen(stripeBrush, 5.5)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat
        };
        stripePen.Freeze();

        const double spacing = 24;
        double travel = fill.Height + 18;
        double startX = fill.Left - travel - spacing + _stripePhase;
        for (double x = startX; x < fill.Right + travel; x += spacing)
        {
            dc.DrawLine(
                stripePen,
                new WpfPoint(x, fill.Bottom + 5),
                new WpfPoint(x + travel, fill.Top - 5));
        }

        dc.Pop();
    }

    private static Color InterpolateBatteryColor(int percent)
    {
        Color red = Color.FromRgb(210, 66, 66);
        Color amber = Color.FromRgb(210, 160, 55);
        Color green = Color.FromRgb(64, 166, 96);

        if (percent <= 50)
            return Lerp(red, amber, percent / 50d);
        return Lerp(amber, green, (percent - 50) / 50d);
    }

    private static Color Lerp(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        byte r = (byte)Math.Round(from.R + (to.R - from.R) * amount);
        byte g = (byte)Math.Round(from.G + (to.G - from.G) * amount);
        byte b = (byte)Math.Round(from.B + (to.B - from.B) * amount);
        return Color.FromRgb(r, g, b);
    }
}
