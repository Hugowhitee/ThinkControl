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
        double stroke = Math.Max(1.2, Math.Min(width, height) / 10.5d);
        var pen = new Pen(brush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        if (Kind == "Touchpad")
        {
            double inset = Math.Max(1.2, Math.Min(width, height) * 0.12);
            Rect shell = new(inset, inset * 1.15, Math.Max(1, width - inset * 2), Math.Max(1, height - inset * 2.3));
            drawingContext.DrawRoundedRectangle(null, pen, shell, 2.2, 2.2);
            double seamY = shell.Bottom - shell.Height * 0.24;
            drawingContext.DrawLine(pen, new Point(shell.Left + shell.Width * 0.13, seamY), new Point(shell.Right - shell.Width * 0.13, seamY));
            return;
        }

        if (Kind == "Audio")
        {
            double sx = width / 16d;
            double sy = height / 16d;
            drawingContext.PushTransform(new ScaleTransform(sx, sy));
            Geometry speaker = Geometry.Parse("M2,6 L5,6 L9,3 L9,13 L5,10 L2,10 Z");
            drawingContext.DrawGeometry(null, new Pen(brush, 1.45) { LineJoin = PenLineJoin.Round }, speaker);
            drawingContext.DrawGeometry(null, new Pen(brush, 1.45) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Geometry.Parse("M11,6 C12.5,7 12.5,9 11,10 M13,4 C16,6 16,10 13,12"));
            drawingContext.Pop();
            return;
        }

        if (Kind == "Sensors")
        {
            double sx = width / 16d;
            double sy = height / 16d;
            drawingContext.PushTransform(new ScaleTransform(sx, sy));
            drawingContext.DrawGeometry(null,
                new Pen(brush, 1.5)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                },
                Geometry.Parse("M1,8 L4,8 L6,3 L9,13 L11,8 L15,8"));
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
