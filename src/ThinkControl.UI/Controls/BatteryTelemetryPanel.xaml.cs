using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel : UserControl
{
    private AppState? _subscribedState;
    private bool _historyRefreshQueued;
    private bool _batteryUsageCardAdded;
    private bool _showAllSessions;

    public BatteryTelemetryPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyBatteryGaugePolish();
            EnsureBatteryUsageCard();
            AttachState();
            RefreshHistoryUi();
        };
        Unloaded += (_, _) => DetachState();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
                QueueHistoryRefresh();
        };
    }

    private void AttachState()
    {
        if (DataContext is not AppState state || ReferenceEquals(_subscribedState, state))
            return;
        DetachState();
        _subscribedState = state;
        state.PropertyChanged += State_PropertyChanged;
    }

    private void DetachState()
    {
        if (_subscribedState is not null)
            _subscribedState.PropertyChanged -= State_PropertyChanged;
        _subscribedState = null;
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "BatteryChargePercentTimeline" or
            "BatteryChargePowerTimeline" or
            "BatteryHealthTrendTimeline" or
            "BatteryCurrentSessionText" or
            "BatteryHealthTrendText")
        {
            QueueHistoryRefresh();
        }
    }

    private void QueueHistoryRefresh()
    {
        if (!IsVisible || _historyRefreshQueued)
            return;
        _historyRefreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _historyRefreshQueued = false;
            if (IsVisible)
                RefreshHistoryUi();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ApplyBatteryGaugePolish()
    {
        BatteryGauge? gauge = FindVisualChild<BatteryGauge>(this);
        if (gauge is not null)
        {
            gauge.Width = 198;
            gauge.Height = 58;
        }

        // Never imply that ThinkControl measured continuously while Windows was
        // asleep, hibernated or the app was not scheduled. A real sampling gap is
        // rendered as a gap in the line rather than a fake straight connection.
        // Stored battery sessions are intentionally sampled sparsely. They are one
        // continuous session, so a two-minute live-sensor gap threshold reduced the
        // history to disconnected dots instead of a useful charge/discharge curve.
        ChargePercentChart.GapThresholdMinutes = 0;
        DischargePercentChart.GapThresholdMinutes = 0;
        DischargeChart.GapThresholdMinutes = 0;
    }

    private void EnsureBatteryUsageCard()
    {
        if (_batteryUsageCardAdded || Content is not StackPanel root)
            return;

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = "App battery usage", FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = "See Windows' own per-app battery estimates without running another profiler in ThinkControl. This keeps background overhead near zero and uses the same system energy accounting Windows Settings relies on.",
            FontSize = 10.5,
            Margin = new Thickness(0, 5, 190, 0),
            TextWrapping = TextWrapping.Wrap
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);

        var button = new Button
        {
            Content = "Open app battery usage",
            Style = TryFindResource("TcButton") as Style,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 6, 12, 6)
        };
        button.Click += OpenBatteryUsage_Click;

        var grid = new Grid();
        grid.Children.Add(copy);
        grid.Children.Add(button);
        var card = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Child = grid
        };

        // Keep Recent sessions as the final full-width history section, with the
        // local-history footer underneath it.
        int insert = Math.Max(0, root.Children.Count - 2);
        root.Children.Insert(insert, card);
        _batteryUsageCardAdded = true;
    }

    private void OpenBatteryUsage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:batterysaver-usagedetails") { UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:batterysaver") { UseShellExecute = true }); }
            catch { }
        }
    }

    private void RefreshHistoryUi()
    {
        if (WpfApplication.Current is not App app)
            return;

        IReadOnlyList<BatterySessionDetail> sessions = app.BatteryHistoryService.GetRecentSessionDetails(10);
        IReadOnlyList<TimeSeriesPoint> chargePercent = app.State.BatteryChargePercentTimeline;
        IReadOnlyList<TimeSeriesPoint> dischargePower = app.BatteryHistoryService.GetLatestDischargeTimeline();
        IReadOnlyList<TimeSeriesPoint> dischargePercent = app.BatteryHistoryService.GetLatestDischargePercentTimeline();

        ChargePercentChart.Values = chargePercent;
        DischargeChart.Values = dischargePower;
        DischargePercentChart.Values = dischargePercent;
        DischargeSummaryText.Text = app.BatteryHistoryService.GetLatestDischargeSummary();

        RecentSessionItems.Children.Clear();
        if (sessions.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No completed battery sessions yet. ThinkControl starts learning automatically while you use and charge the laptop.",
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(1, 5, 1, 2)
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            RecentSessionItems.Children.Add(empty);
            return;
        }

        int visibleCount = _showAllSessions ? sessions.Count : Math.Min(3, sessions.Count);
        foreach (BatterySessionDetail session in sessions.Take(visibleCount))
            RecentSessionItems.Children.Add(CreateSessionRow(session));
        ToggleSessionsButton.Visibility = sessions.Count > 3 ? Visibility.Visible : Visibility.Collapsed;
        ToggleSessionsButton.Content = _showAllSessions ? "Show less" : $"Show all {sessions.Count}";
    }

    internal void PrepareForSnapshot(AppState state)
    {
        ApplyBatteryGaugePolish();
        EnsureBatteryUsageCard();

        TimeSeriesPoint[] chargePower = state.BatteryChargePowerTimeline.ToArray();
        ChargePercentChart.Values = state.BatteryChargePercentTimeline.Count > 0
            ? state.BatteryChargePercentTimeline
            : BuildPercentTimeline(chargePower);

        DateTimeOffset end = chargePower.Length > 0 ? chargePower[^1].At : DateTimeOffset.UtcNow;
        TimeSeriesPoint[] dischargePower = Enumerable.Range(0, 46)
            .Select(index =>
            {
                double watts = 6.5 + Math.Sin(index / 4.2) * 0.7 + index * 0.018;
                int percent = (int)Math.Round(88d - 25d * index / 45d);
                return new TimeSeriesPoint(end - TimeSpan.FromMinutes((45 - index) * 5), watts, $"{percent}%");
            })
            .ToArray();

        DischargeChart.Values = dischargePower;
        DischargePercentChart.Values = BuildPercentTimeline(dischargePower);
        DischargeSummaryText.Text = "Latest discharge · 88% → 63% · 3h 45m · 6.9 W avg";
    }

    private Button CreateSessionRow(BatterySessionDetail session)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

        var kind = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        kind.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");
        var kindText = new TextBlock
        {
            Text = session.IsActive ? $"{session.Kind} · live" : session.Kind,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold
        };
        kindText.SetResourceReference(TextBlock.ForegroundProperty, session.Kind == "Charge" ? "Tc.Accent" : "Tc.TextMuted");
        kind.Child = kindText;
        grid.Children.Add(kind);

        var summary = new TextBlock
        {
            Text = session.Summary,
            FontSize = 10.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(summary, 1);
        grid.Children.Add(summary);

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        arrow.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(arrow, 2);
        grid.Children.Add(arrow);

        var row = new Button
        {
            Style = TryFindResource("TcButton") as Style,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(2, 8, 2, 8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = grid,
            ToolTip = "Open session statistics and graphs"
        };
        row.SetResourceReference(Button.BorderBrushProperty, "Tc.Border");
        row.Click += (_, _) => ShowSession(session);
        return row;
    }

    private void ShowSession(BatterySessionDetail session)
    {
        TimeSpan duration = session.Duration < TimeSpan.Zero ? TimeSpan.Zero : session.Duration;
        string durationText = duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes:00}m"
            : $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min";
        int percentageChange = session.EndPercent - session.StartPercent;
        string energy = session.EnergyWh is double wh ? $"{(session.Kind == "Charge" ? "+" : "−")}{wh:0.##} Wh" : "—";
        string average = session.AveragePowerWatts is double avg ? $"{avg:0.##} W" : "—";
        string peak = session.PeakPowerWatts is double max ? $"{max:0.##} W" : "—";
        string rate = session.PercentPerHour is double pp ? $"{pp:0.#}%/h" : "—";

        DateTimeOffset local = session.StartedAt.ToLocalTime();
        string subtitle = $"{session.Kind} · {local.ToString("g", CultureInfo.CurrentCulture)} · {session.StartPercent}% → {session.EndPercent}%";
        TelemetryDetailMetric[] metrics =
        [
            new("Duration", durationText),
            new("Battery", $"{percentageChange:+0;-0;0}%", $"{session.StartPercent}% → {session.EndPercent}%"),
            new("Energy", energy),
            new("Average power", average),
            new("Peak power", peak),
            new(session.Kind == "Charge" ? "Charge rate" : "Drain rate", rate)
        ];

        var model = new TelemetryDetailModel(
            $"{session.Kind} session",
            subtitle,
            session.Kind == "Charge" ? "Charging power" : "Discharge power",
            session.PowerTimeline,
            "W",
            "0.0",
            metrics,
            "Battery history is stored locally in ThinkControl and automatically compacted over time.",
            SecondaryTimeline: session.PercentTimeline,
            SecondaryChartTitle: "Battery level",
            SecondaryUnit: "%",
            SecondaryValueFormat: "0");

        var window = new TelemetryDetailWindow(model) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private static IReadOnlyList<TimeSeriesPoint> BuildPercentTimeline(IEnumerable<TimeSeriesPoint> points)
    {
        var result = new List<TimeSeriesPoint>();
        foreach (TimeSeriesPoint point in points)
        {
            string label = point.Label?.Trim() ?? string.Empty;
            if (label.EndsWith('%')) label = label[..^1].Trim();
            if (!double.TryParse(label, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)) continue;
            if (percent is < 0 or > 100) continue;
            result.Add(new TimeSeriesPoint(point.At, percent));
        }
        return result;
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            T? nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (WpfApplication.Current is not App app)
            return;

        MessageBoxResult answer = MessageBox.Show(
            "Clear ThinkControl's locally stored battery charge and discharge history?\n\nCurrent Windows battery health and cycle-count values are not changed.",
            "ThinkControl · Clear battery history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        BatteryHistoryView view = app.BatteryHistoryService.Clear();
        app.State.ApplyBatteryHistory(view);
        app.BatteryTelemetryService.SetHistoricalChargePower(view.TypicalChargePowerWatts);
        RefreshHistoryUi();
    }

    private void ToggleSessions_Click(object sender, RoutedEventArgs e)
    {
        _showAllSessions = !_showAllSessions;
        RefreshHistoryUi();
    }

    private void OpenVantage_Click(object sender, RoutedEventArgs e)
    {
        if (LenovoSoftwareLauncher.TryOpenVantage())
            return;

        try { Process.Start(new ProcessStartInfo("ms-settings:batterysaver") { UseShellExecute = true }); }
        catch { }
    }
}
