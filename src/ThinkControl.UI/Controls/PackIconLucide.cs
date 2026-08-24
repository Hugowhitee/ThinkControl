using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Small dependency-free icon renderer. The historical PackIconLucide type name is
/// retained for XAML compatibility. All icons use the control Foreground so the
/// navigation style can brighten them automatically when selected or hovered.
/// </summary>
public sealed class PackIconLucide : System.Windows.Controls.Control
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(string),
        typeof(PackIconLucide),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double width = Math.Max(0, ActualWidth);
        double height = Math.Max(0, ActualHeight);
        if (width <= 0 || height <= 0)
            return;

        Brush brush = Foreground ?? Brushes.Gray;
        bool emphasized = FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        double stroke = Math.Max(1.2, Math.Min(width, height) / (emphasized ? 8.9d : 10.5d));
        var pen = new Pen(brush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        if (Kind == "Touchpad")
        {
            // A touchpad is optically wider and flatter than a generic rounded
            // rectangle. Keep the silhouette broad even inside the nav's 15×15 box.
            double shellWidth = width * 0.94;
            double shellHeight = height * 0.67;
            double left = (width - shellWidth) / 2d;
            double top = (height - shellHeight) / 2d;
            Rect shell = new(left, top, shellWidth, shellHeight);
            drawingContext.DrawRoundedRectangle(null, pen, shell, 2.0, 2.0);

            double seamY = shell.Bottom - shell.Height * 0.23;
            drawingContext.DrawLine(
                pen,
                new Point(shell.Left + shell.Width * 0.12, seamY),
                new Point(shell.Right - shell.Width * 0.12, seamY));
            return;
        }

        if (Kind == "Audio")
        {
            double sx = width / 16d;
            double sy = height / 16d;
            drawingContext.PushTransform(new ScaleTransform(sx, sy));
            double audioStroke = emphasized ? 1.7 : 1.45;
            Geometry speaker = Geometry.Parse("M2,6 L5,6 L9,3 L9,13 L5,10 L2,10 Z");
            drawingContext.DrawGeometry(null, new Pen(brush, audioStroke) { LineJoin = PenLineJoin.Round }, speaker);
            drawingContext.DrawGeometry(null, new Pen(brush, audioStroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Geometry.Parse("M11,6 C12.5,7 12.5,9 11,10 M13,4 C16,6 16,10 13,12"));
            drawingContext.Pop();
            return;
        }

        if (Kind == "Sensors")
        {
            // Use a compact gauge/pulse motif with the same rounded stroke language
            // as the other navigation glyphs instead of the old thin ECG zig-zag.
            double sx = width / 16d;
            double sy = height / 16d;
            drawingContext.PushTransform(new ScaleTransform(sx, sy));
            var sensorPen = new Pen(brush, emphasized ? 1.75 : 1.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            drawingContext.DrawEllipse(null, sensorPen, new Point(8, 8), 6.1, 6.1);
            drawingContext.DrawGeometry(null, sensorPen, Geometry.Parse("M3.4,9 L5.8,9 L7.2,5.5 L9.2,11.1 L10.5,8 L12.7,8"));
            drawingContext.Pop();
            return;
        }

        string? resourceKey = Kind switch
        {
            "House" => "Tc.Icon.Home",
            "Gauge" => "Tc.Icon.Performance",
            "Fan" => "Tc.Icon.Fan",
            "Monitor" => "Tc.Icon.Display",
            "Keyboard" => "Tc.Icon.Keyboard",
            "Battery" => "Tc.Icon.Battery",
            "Laptop" => "Tc.Icon.System",
            "RefreshCw" => "Tc.Icon.Updates",
            "Settings" => "Tc.Icon.Settings",
            _ => null
        };
        Geometry? source = resourceKey is null ? null : TryFindResource(resourceKey) as Geometry;
        if (source is null)
            return;

        const double sourceSize = 960d;
        double scale = Math.Min(width / sourceSize, height / sourceSize);
        double contentWidth = sourceSize * scale;
        double contentHeight = sourceSize * scale;
        double left = (width - contentWidth) / 2d;
        double top = (height - contentHeight) / 2d;

        Geometry geometry = source.CloneCurrentValue();
        geometry.Transform = new MatrixTransform(
            scale, 0,
            0, scale,
            left, top + contentHeight);

        drawingContext.DrawGeometry(brush, null, geometry);
    }
}