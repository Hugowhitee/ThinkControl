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
    private static readonly Geometry TouchpadGeometry = Geometry.Parse(
        "M216-744H744V-672H216ZM216-672H288V-288H216ZM672-672H744V-288H672ZM216-288H744V-216H216ZM396-336H564V-300H396Z");

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

        Geometry? source;
        if (Kind == "Touchpad")
        {
            source = TouchpadGeometry;
        }
        else
        {
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
            source = resourceKey is null ? null : TryFindResource(resourceKey) as Geometry;
        }

        if (source is null)
            return;

        double width = Math.Max(0, ActualWidth);
        double height = Math.Max(0, ActualHeight);
        if (width <= 0 || height <= 0)
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
