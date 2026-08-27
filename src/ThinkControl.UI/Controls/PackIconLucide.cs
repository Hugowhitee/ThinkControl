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
        double stroke = Math.Max(1.2, Math.Min(width, height) / 10.5d);
        var pen = new Pen(brush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        if (Kind == "Touchpad")
        {
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

        // Compact/full view is deliberately represented by inward/outward diagonal
        // arrows. ViewSidebar remains only as a compatibility alias so older XAML
        // can never render the misleading sidebar-layout glyph again.
        if (Kind is "CompactView" or "FullView" or "ViewSidebar")
        {
            bool compact = Kind is "CompactView" or "ViewSidebar";
            DrawViewModeGlyph(drawingContext, pen, width, height, compact);
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
            "OpenInFull" => "Tc.Icon.OpenInFull",
            "Close" => "Tc.Icon.Close",
            "Check" => "Tc.Icon.Check",
            "Error" => "Tc.Icon.Error",
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

    private static void DrawViewModeGlyph(DrawingContext dc, Pen pen, double width, double height, bool compact)
    {
        double size = Math.Min(width, height);
        double left = (width - size) / 2d;
        double top = (height - size) / 2d;

        Point P(double x, double y) => new(left + size * x, top + size * y);

        if (compact)
        {
            // Two diagonal arrows pointing toward the centre: "make this window compact".
            Point trStart = P(0.80, 0.20);
            Point trEnd = P(0.55, 0.45);
            dc.DrawLine(pen, trStart, trEnd);
            dc.DrawLine(pen, trEnd, P(0.55, 0.29));
            dc.DrawLine(pen, trEnd, P(0.71, 0.45));

            Point blStart = P(0.20, 0.80);
            Point blEnd = P(0.45, 0.55);
            dc.DrawLine(pen, blStart, blEnd);
            dc.DrawLine(pen, blEnd, P(0.29, 0.55));
            dc.DrawLine(pen, blEnd, P(0.45, 0.71));
        }
        else
        {
            // Same visual language in reverse: outward arrows mean "expand to full view".
            Point trStart = P(0.55, 0.45);
            Point trEnd = P(0.80, 0.20);
            dc.DrawLine(pen, trStart, trEnd);
            dc.DrawLine(pen, trEnd, P(0.64, 0.20));
            dc.DrawLine(pen, trEnd, P(0.80, 0.36));

            Point blStart = P(0.45, 0.55);
            Point blEnd = P(0.20, 0.80);
            dc.DrawLine(pen, blStart, blEnd);
            dc.DrawLine(pen, blEnd, P(0.20, 0.64));
            dc.DrawLine(pen, blEnd, P(0.36, 0.80));
        }
    }
}
