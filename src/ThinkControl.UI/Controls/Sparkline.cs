using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;

namespace ThinkControl.UI.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    private INotifyCollectionChanged? _collection;

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Sparkline)d;
        if (control._collection is not null)
            control._collection.CollectionChanged -= control.OnCollectionChanged;

        control._collection = e.NewValue as INotifyCollectionChanged;
        if (control._collection is not null)
            control._collection.CollectionChanged += control.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 1 || height <= 1)
            return;

        MediaBrush border = TryBrush("Tc.Border", Brushes.DimGray);
        MediaBrush muted = TryBrush("Tc.TextFaint", Brushes.Gray);
        MediaBrush accent = TryBrush("Tc.Accent", Brushes.Red);

        var gridPen = new Pen(border, 0.6);
        gridPen.Freeze();
        for (int i = 1; i < 4; i++)
        {
            double y = height * i / 4d;
            dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }
        for (int i = 1; i < 8; i++)
        {
            double x = width * i / 8d;
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, height));
        }

        List<double> values = Values?.Cast<object>()
            .Select(value => Convert.ToDouble(value))
            .Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
            .ToList() ?? [];

        if (values.Count < 2)
        {
            var text = new FormattedText(
                "Waiting for telemetry",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                muted,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(8, Math.Max(4, (height - text.Height) / 2)));
            return;
        }

        const double minTemp = 20;
        const double maxTemp = 100;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (int i = 0; i < values.Count; i++)
            {
                double x = values.Count == 1 ? 0 : i * width / (values.Count - 1d);
                double normalized = Math.Clamp((values[i] - minTemp) / (maxTemp - minTemp), 0, 1);
                double y = height - normalized * height;
                if (i == 0)
                    ctx.BeginFigure(new Point(x, y), false, false);
                else
                    ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geometry.Freeze();

        var linePen = new Pen(accent, 1.6) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, linePen, geometry);
    }

    private static MediaBrush TryBrush(string key, MediaBrush fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as MediaBrush ?? fallback;
}
