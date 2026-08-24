using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public sealed record TelemetryDetailMetric(string Label, string Value, string? Detail = null);

public sealed record TelemetryDetailModel(
    string Title,
    string Subtitle,
    string ChartTitle,
    IReadOnlyList<TimeSeriesPoint> Timeline,
    string Unit,
    string ValueFormat,
    IReadOnlyList<TelemetryDetailMetric> Metrics,
    string Footer = "Local telemetry · nothing is uploaded");

public partial class TelemetryDetailWindow : Window
{
    public TelemetryDetailWindow(TelemetryDetailModel model)
    {
        InitializeComponent();
        Title = $"ThinkControl · {model.Title}";
        TitleText.Text = model.Title;
        SubtitleText.Text = model.Subtitle;
        ChartTitleText.Text = model.ChartTitle;
        DetailChart.Values = model.Timeline;
        DetailChart.Unit = model.Unit;
        DetailChart.ValueFormat = model.ValueFormat;
        FooterText.Text = model.Footer;

        MetricsGrid.Columns = Math.Clamp(model.Metrics.Count, 1, 4);
        foreach (TelemetryDetailMetric metric in model.Metrics.Take(8))
            MetricsGrid.Children.Add(CreateMetric(metric));
    }

    private Border CreateMetric(TelemetryDetailMetric metric)
    {
        var stack = new StackPanel();
        var label = new TextBlock
        {
            Text = metric.Label.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(label);
        stack.Children.Add(new TextBlock
        {
            Text = metric.Value,
            FontSize = 17,
            Margin = new Thickness(0, 5, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(metric.Detail))
        {
            var detail = new TextBlock
            {
                Text = metric.Detail,
                FontSize = 9.5,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
            stack.Children.Add(detail);
        }

        return new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 0, 8, 0),
            Child = stack
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
