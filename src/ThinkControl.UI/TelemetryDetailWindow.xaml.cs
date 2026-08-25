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
    string Footer = "Local telemetry · nothing is uploaded",
    IReadOnlyList<TimeSeriesPoint>? SecondaryTimeline = null,
    string? SecondaryChartTitle = null,
    string SecondaryUnit = "%",
    string SecondaryValueFormat = "0",
    bool PreferSecondaryTimeline = false,
    string PrimaryToggleLabel = "Power",
    string SecondaryToggleLabel = "Battery");

public partial class TelemetryDetailWindow : Window
{
    private readonly TelemetryDetailModel _model;
    private bool _showingSecondary;

    public TelemetryDetailWindow(TelemetryDetailModel model)
    {
        _model = model;
        InitializeComponent();
        Title = $"ThinkControl · {model.Title}";
        TitleText.Text = model.Title;
        SubtitleText.Text = model.Subtitle;
        FooterText.Text = model.Footer;

        bool hasSecondary = model.SecondaryTimeline is { Count: > 0 };
        ChartModePanel.Visibility = hasSecondary ? Visibility.Visible : Visibility.Collapsed;
        PrimaryChartButton.Content = model.PrimaryToggleLabel;
        SecondaryChartButton.Content = model.SecondaryToggleLabel;

        MetricsGrid.Columns = model.Metrics.Count switch
        {
            <= 1 => 1,
            <= 4 => model.Metrics.Count,
            _ => 3
        };
        foreach (TelemetryDetailMetric metric in model.Metrics.Take(8))
            MetricsGrid.Children.Add(CreateMetric(metric));

        ApplyChart(hasSecondary && model.PreferSecondaryTimeline);
    }

    private void ApplyChart(bool secondary)
    {
        bool hasSecondary = _model.SecondaryTimeline is { Count: > 0 };
        _showingSecondary = secondary && hasSecondary;

        if (_showingSecondary)
        {
            ChartTitleText.Text = string.IsNullOrWhiteSpace(_model.SecondaryChartTitle)
                ? "Battery level"
                : _model.SecondaryChartTitle;
            DetailChart.Values = _model.SecondaryTimeline!;
            DetailChart.Unit = _model.SecondaryUnit;
            DetailChart.ValueFormat = _model.SecondaryValueFormat;
            DetailChart.IncludeZero = false;
        }
        else
        {
            ChartTitleText.Text = _model.ChartTitle;
            DetailChart.Values = _model.Timeline;
            DetailChart.Unit = _model.Unit;
            DetailChart.ValueFormat = _model.ValueFormat;
            DetailChart.IncludeZero = true;
        }

        if (hasSecondary)
        {
            PrimaryChartButton.Opacity = _showingSecondary ? 0.62 : 1.0;
            SecondaryChartButton.Opacity = _showingSecondary ? 1.0 : 0.62;
        }
    }

    private Border CreateMetric(TelemetryDetailMetric metric)
    {
        var stack = new StackPanel();
        var label = new TextBlock
        {
            Text = metric.Label.ToUpperInvariant(),
            FontSize = 8.8,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(label);
        stack.Children.Add(new TextBlock
        {
            Text = metric.Value,
            FontSize = 16,
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(metric.Detail))
        {
            var detail = new TextBlock
            {
                Text = metric.Detail,
                FontSize = 9.2,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
            stack.Children.Add(detail);
        }

        var border = new Border
        {
            Padding = new Thickness(9, 7, 9, 9),
            Margin = new Thickness(0, 0, 8, 8),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = stack
        };
        border.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        return border;
    }

    private void PrimaryChart_Click(object sender, RoutedEventArgs e) => ApplyChart(false);

    private void SecondaryChart_Click(object sender, RoutedEventArgs e) => ApplyChart(true);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
