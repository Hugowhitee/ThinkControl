using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Minimal battery indicator that uses the real percentage and interpolates the fill
/// from red through amber to green. It deliberately stays icon-like instead of
/// becoming a second dashboard inside the Battery page.
/// </summary>
public sealed class BatteryGauge : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent),
        typeof(int),
        typeof(BatteryGauge),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Percent
    {
        get => (int)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
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
        if (fillWidth > 0.5 && innerHeight > 0.5)
        {
            Color fillColor = InterpolateBatteryColor(percent);
            var fillBrush = new SolidColorBrush(fillColor);
            fillBrush.Freeze();
            var fill = new WpfRect(
                body.X + innerPadding,
                body.Y + innerPadding,
                fillWidth,
                innerHeight);
            dc.DrawRoundedRectangle(fillBrush, null, fill, Math.Min(4, radius), Math.Min(4, radius));
        }
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
