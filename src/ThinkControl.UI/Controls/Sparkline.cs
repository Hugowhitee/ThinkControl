using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfFlowDirection = System.Windows.FlowDirection;

namespace ThinkControl.UI.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(20d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AutoRangeProperty = DependencyProperty.Register(
        nameof(AutoRange),
        typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IncludeZeroProperty = DependencyProperty.Register(
        nameof(IncludeZero),
        typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private INotifyCollectionChanged? _collection;

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool AutoRange
    {
        get => (bool)GetValue(AutoRangeProperty);
        set => SetValue(AutoRangeProperty, value);
    }

    public bool IncludeZero
    {
        get => (bool)GetValue(IncludeZeroProperty);
        set => SetValue(IncludeZeroProperty, value);
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

        MediaBrush border = TryBrush("Tc.Border", MediaBrushes.DimGray);
        MediaBrush muted = TryBrush("Tc.TextFaint", MediaBrushes.Gray);
        MediaBrush accent = TryBrush("Tc.Accent", MediaBrushes.Red);

        var gridPen = new MediaPen(border, 0.6);
        gridPen.Freeze();
        for (int i = 1; i < 4; i++)
        {
            double y = height * i / 4d;
            dc.DrawLine(gridPen, new WpfPoint(0, y), new WpfPoint(width, y));
        }
        for (int i = 1; i < 8; i++)
        {
            double x = width * i / 8d;
            dc.DrawLine(gridPen, new WpfPoint(x, 0), new WpfPoint(x, height));
        }

        List<double> values = Values?.Cast<object>()
            .Select(Convert.ToDouble)
            .Where(double.IsFinite)
            .ToList() ?? [];

        if (values.Count < 2)
        {
            var text = new FormattedText(
                "Waiting for telemetry",
                System.Globalization.CultureInfo.CurrentUICulture,
                WpfFlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                muted,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new WpfPoint(8, Math.Max(4, (height - text.Height) / 2)));
            return;
        }

        (double minimum, double maximum) = ResolveRange(values);
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (int i = 0; i < values.Count; i++)
            {
                double x = i * width / (values.Count - 1d);
                double normalized = Math.Clamp((values[i] - minimum) / (maximum - minimum), 0, 1);
                double y = height - normalized * height;
                if (i == 0)
                    ctx.BeginFigure(new WpfPoint(x, y), false, false);
                else
                    ctx.LineTo(new WpfPoint(x, y), true, false);
            }
        }
        geometry.Freeze();

        var linePen = new MediaPen(accent, 1.6)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        linePen.Freeze();
        dc.DrawGeometry(null, linePen, geometry);
    }

    private (double Min, double Max) ResolveRange(IReadOnlyList<double> values)
    {
        if (!AutoRange)
        {
            double minimum = Minimum;
            double maximum = Maximum;
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
                return (0, 1);
            return (minimum, maximum);
        }

        double minimumAuto = values.Min();
        double maximumAuto = values.Max();
        if (IncludeZero)
            minimumAuto = Math.Min(0, minimumAuto);

        double span = maximumAuto - minimumAuto;
        if (span < 0.001)
        {
            double pad = Math.Max(1, Math.Abs(maximumAuto) * 0.08);
            if (!IncludeZero)
                minimumAuto -= pad;
            maximumAuto += pad;
        }
        else
        {
            double pad = span * 0.10;
            if (!IncludeZero)
                minimumAuto -= pad;
            maximumAuto += pad;
        }

        if (IncludeZero)
            minimumAuto = 0;

        if (!double.IsFinite(minimumAuto) || !double.IsFinite(maximumAuto) || maximumAuto <= minimumAuto)
            return (0, 1);

        return (minimumAuto, maximumAuto);
    }

    private static MediaBrush TryBrush(string key, MediaBrush fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as MediaBrush ?? fallback;
}
