using System.Windows;
using System.Windows.Media;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Small dependency-free Material Symbols renderer. The class intentionally keeps
/// the historical PackIconLucide type name for XAML compatibility while ThinkControl
/// migrates away from the external icon-pack dependency.
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

        if (Kind == "Touchpad")
        {
            // Draw this icon directly instead of scaling the old filled Material
            // glyph. The outline + click seam matches the weight of the other nav
            // symbols and no longer reads as a generic square.
            Brush brush = Foreground ?? Brushes.Gray;
            var pen = new Pen(brush, Math.Max(1.15, Math.Min(width, height) / 11d))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            double inset = Math.Max(1.2, Math.Min(width, height) * 0.12);
            Rect shell = new(inset, inset * 1.15, Math.Max(1, width - inset * 2), Math.Max(1, height - inset * 2.3));
            drawingContext.DrawRoundedRectangle(null, pen, shell, 2.2, 2.2);
            double seamY = shell.Bottom - shell.Height * 0.24;
            drawingContext.DrawLine(pen, new Point(shell.Left + shell.Width * 0.13, seamY), new Point(shell.Right - shell.Width * 0.13, seamY));
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

        drawingContext.DrawGeometry(Foreground, null, geometry);
    }
}
