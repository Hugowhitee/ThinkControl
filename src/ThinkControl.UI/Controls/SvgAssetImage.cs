using System.Windows;
using System.Windows.Media;

namespace ThinkControl.UI.Controls;

/// <summary>Displays an untouched SVG using its original viewport, including transparent whitespace.</summary>
public sealed class SvgAssetImage : FrameworkElement
{
    public static readonly DependencyProperty AssetProperty = DependencyProperty.Register(nameof(Asset), typeof(string), typeof(SvgAssetImage), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ViewportProperty = DependencyProperty.Register(nameof(Viewport), typeof(Rect), typeof(SvgAssetImage), new FrameworkPropertyMetadata(new Rect(0, 0, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));
    public string Asset { get => (string)GetValue(AssetProperty); set => SetValue(AssetProperty, value); }
    public Rect Viewport { get => (Rect)GetValue(ViewportProperty); set => SetValue(ViewportProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Asset) || ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.DrawRectangle(SvgAssetDrawing.Brush(Asset, Viewport), null, new Rect(0, 0, ActualWidth, ActualHeight));
    }
}
