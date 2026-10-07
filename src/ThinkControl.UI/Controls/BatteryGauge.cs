using System.Windows;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Minimal battery indicator that uses the real percentage. While charging the fill
/// becomes green and shows a subtle moving diagonal flow. Rendering is hooked only
/// while visible and charging/discharging, with a brief fade on pause. No idle callback remains.
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

    public static readonly DependencyProperty IsDischargingProperty = DependencyProperty.Register(
        nameof(IsDischarging), typeof(bool), typeof(BatteryGauge),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnChargingChanged));

    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(
        nameof(MotionEnabled), typeof(bool), typeof(BatteryGauge), new FrameworkPropertyMetadata(true, OnChargingChanged));
    public bool MotionEnabled { get => (bool)GetValue(MotionEnabledProperty); set => SetValue(MotionEnabledProperty, value); }

    public static readonly DependencyProperty MotionPreferenceProperty = DependencyProperty.Register(
        nameof(MotionPreference), typeof(string), typeof(BatteryGauge), new FrameworkPropertyMetadata("System", OnChargingChanged));
    public string MotionPreference { get => (string)GetValue(MotionPreferenceProperty); set => SetValue(MotionPreferenceProperty, value); }
    private bool MotionAllowed => MotionEnabled && (MotionPreference == "On" || MotionPreference == "System" && SystemParameters.ClientAreaAnimation);

    private bool _renderHooked;
    private TimeSpan _lastRenderingTime;
    private double _stripePhase;
    private double _flowOpacity;
    private double _flowDirection = 1;
    internal double MotionPhase => _stripePhase;
    internal bool MotionActive => _renderHooked;

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

    public bool IsDischarging
    {
        get => (bool)GetValue(IsDischargingProperty);
        set => SetValue(IsDischargingProperty, value);
    }

    private static void OnChargingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gauge = (BatteryGauge)d;
        gauge.UpdateRenderingHook();
        gauge.InvalidateVisual();
    }

    private void UpdateRenderingHook()
    {
        bool shouldAnimate = MotionAllowed && IsLoaded && IsVisible &&
            (IsCharging || IsDischarging || _flowOpacity > 0.001);
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
        if (!MotionAllowed || !IsVisible) _flowOpacity = 0;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!MotionAllowed || !IsLoaded || !IsVisible)
        {
            UpdateRenderingHook();
            InvalidateVisual();
            return;
        }
        if (e is not RenderingEventArgs args)
            return;

        if (_lastRenderingTime == TimeSpan.Zero)
        {
            _lastRenderingTime = args.RenderingTime;
            return;
        }

        double seconds = Math.Clamp((args.RenderingTime - _lastRenderingTime).TotalSeconds, 0, 0.1);
        _lastRenderingTime = args.RenderingTime;
        double targetOpacity = IsCharging ? 1 : IsDischarging ? 0.85 : 0;
        _flowOpacity += (targetOpacity - _flowOpacity) * Math.Min(1, seconds * 7);
        _flowDirection += ((IsDischarging && !IsCharging ? -1 : 1) - _flowDirection) * Math.Min(1, seconds * 5);
        _stripePhase = (_stripePhase + seconds * 26d * _flowDirection + 16d) % 16d;
        InvalidateVisual();
        if (targetOpacity == 0 && _flowOpacity < 0.001) UpdateRenderingHook();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Every host uses the same silhouette, including wider layout slots.
        const double aspectRatio = 2.5;
        double width = Math.Min(ActualWidth, ActualHeight * aspectRatio);
        double height = width / aspectRatio;
        if (width < 20 || height < 8)
            return;

        WpfBrush borderBrush = WpfApplication.Current?.TryFindResource("Tc.BorderStrong") as WpfBrush ?? WpfBrushes.Gray;
        WpfBrush surfaceBrush = WpfApplication.Current?.TryFindResource("Tc.Surface") as WpfBrush ?? WpfBrushes.Transparent;
        var borderPen = new WpfPen(borderBrush, Math.Clamp(height * 0.04, 0.8, 1.4));
        borderPen.Freeze();

        double terminalWidth = Math.Max(2, width * 0.045);
        double bodyWidth = width - terminalWidth - 2;
        double radius = Math.Min(7, height * 0.16);
        var body = new WpfRect(0.7, 0.7, Math.Max(1, bodyWidth - 1.4), Math.Max(1, height - 1.4));
        dc.DrawRoundedRectangle(surfaceBrush, borderPen, body, radius, radius);

        double terminalHeight = height * 0.36;
        var terminal = new WpfRect(bodyWidth + 1, (height - terminalHeight) / 2, terminalWidth, terminalHeight);
        dc.DrawRoundedRectangle(borderBrush, null, terminal, 2, 2);

        int percent = Math.Clamp(Percent, 0, 100);
        double innerPadding = Math.Min(4, height * 0.12);
        double innerWidth = Math.Max(0, body.Width - innerPadding * 2);
        double innerHeight = Math.Max(0, body.Height - innerPadding * 2);
        double fillWidth = innerWidth * percent / 100d;
        if (fillWidth <= 0.5 || innerHeight <= 0.5)
            return;

        WpfColor fillColor = Lerp(InterpolateBatteryColor(percent),
            SemanticColor("Tc.Success", WpfColor.FromRgb(62, 212, 134)), IsCharging ? _flowOpacity : 0);
        var fillBrush = new SolidColorBrush(fillColor);
        fillBrush.Freeze();
        var fill = new WpfRect(
            body.X + innerPadding,
            body.Y + innerPadding,
            fillWidth,
            innerHeight);
        double fillRadius = Math.Min(4, radius);
        dc.DrawRoundedRectangle(fillBrush, null, fill, fillRadius, fillRadius);

        if (_flowOpacity > 0.001)
            DrawChargeFlow(dc, fill, fillRadius);
    }

    private void DrawChargeFlow(DrawingContext dc, WpfRect fill, double radius)
    {
        var clip = new RectangleGeometry(fill, radius, radius);
        dc.PushClip(clip);

        var stripeBrush = new SolidColorBrush(WpfColor.FromArgb((byte)Math.Round(115 * _flowOpacity), 12, 35, 30));
        stripeBrush.Freeze();
        var stripePen = new WpfPen(stripeBrush, Math.Clamp(fill.Height * 0.17, 1.2, 5.5))
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat
        };
        stripePen.Freeze();

        const double spacing = 16;
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

    private static WpfColor InterpolateBatteryColor(int percent)
    {
        WpfColor red = SemanticColor("Tc.Error", WpfColor.FromRgb(255, 100, 92));
        WpfColor amber = SemanticColor("Tc.Warning", WpfColor.FromRgb(255, 181, 69));
        WpfColor green = SemanticColor("Tc.Success", WpfColor.FromRgb(62, 212, 134));

        // Charge level is a status cue, not an estimate of battery health.
        // Keep normal levels green instead of blending every reading into olive.
        if (percent <= 15)
            return red;
        if (percent < 30)
            return Lerp(red, amber, (percent - 15) / 15d);
        if (percent < 50)
            return Lerp(amber, green, (percent - 30) / 20d);
        return green;
    }

    private static WpfColor SemanticColor(string resource, WpfColor fallback) =>
        (WpfApplication.Current?.TryFindResource(resource) as SolidColorBrush)?.Color ?? fallback;

    private static WpfColor Lerp(WpfColor from, WpfColor to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        byte r = (byte)Math.Round(from.R + (to.R - from.R) * amount);
        byte g = (byte)Math.Round(from.G + (to.G - from.G) * amount);
        byte b = (byte)Math.Round(from.B + (to.B - from.B) * amount);
        return WpfColor.FromRgb(r, g, b);
    }
}
