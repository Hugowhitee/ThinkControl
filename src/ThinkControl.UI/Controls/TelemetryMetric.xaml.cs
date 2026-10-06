using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI.Controls;

public partial class TelemetryMetric : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TelemetryMetric), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(TelemetryMetric), new PropertyMetadata("—"));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(TelemetryMetric), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueFontSizeProperty = DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(TelemetryMetric), new PropertyMetadata((double)TypographyScale.ValueLarge));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public double ValueFontSize { get => (double)GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }
    public TelemetryMetric() => InitializeComponent();
}
