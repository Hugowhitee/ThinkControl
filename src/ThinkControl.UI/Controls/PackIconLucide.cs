using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Small dependency-free icon renderer. The historical PackIconLucide type name is
/// retained for XAML compatibility. All icons use the control Foreground so the
/// navigation style can brighten them automatically when selected or hovered.
/// Selection changes color only; icon geometry/weight stays constant across pages.
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
        // Do not derive stroke weight from inherited FontWeight. TcNav makes the
        // selected label SemiBold; Audio/Sensors/Touchpad are stroked custom glyphs,
        // so using FontWeight here made only those icons become visibly bolder.
        double stroke = Math.Max(1.2, Math.Min(width, height) / 10.5d);
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
            double touchLeft = (width - shellWidth) / 2d;
            double touchTop = (height - shellHeight) / 2d;
            Rect shell = new(touchLeft, touchTop, shellWidth, shellHeight);
            drawingContext.DrawRoundedRectangle(null, pen, shell, 2.0, 2.0);

            double seamY = shell.Bottom - shell.Height * 0.23;
            drawingContext.DrawLine(
                pen,
                new Point(shell.Left + shell.Width * 0.12, seamY),
                new Point(shell.Right - shell.Width * 0.12, seamY));
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
            "Reset" => "Tc.Icon.Reset",
            "Audio" => "Tc.Icon.Audio",
            "Sensors" => "Tc.Icon.Sensors",
            "Cpu" => "Tc.Icon.Cpu",
            "Brightness" => "Tc.Icon.Brightness",
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
