using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.Core.Battery;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel : UserControl
{
    private AppState? _subscribedState;
    private bool _historyRefreshQueued;


    public BatteryTelemetryPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyBatteryGaugePolish();

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

        if (e.PropertyName is nameof(AppState.BatteryProtectionEnabled) or
            nameof(AppState.BatteryProtectionStartPercent) or
            nameof(AppState.BatteryProtectionStopPercent))
        {
            RefreshChargeProtectionWearEstimate();
        }
    }

    private void RefreshChargeProtectionWearEstimate()
    {
        if (_subscribedState is not AppState state)
            return;

        UpdateBatteryAgingGuidance(
            state.BatteryProtectionEnabled, _batteryProtectionAvailable);
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

        if (app.IsVisualQa)
        {
            PrepareForSnapshot(app.State);
            return;
        }

        IReadOnlyList<BatteryDaySummary> availableDays = app.BatteryHistoryService.GetRecentDays(14);
        PresentChargeSession(availableDays.SelectMany(day => day.Sessions)
            .Where(session => session.Kind == "Charge").OrderByDescending(session => session.StartedAt).FirstOrDefault());
        IReadOnlyList<BatteryDaySummary> days = availableDays.Take(_historyVisibleDays).ToArray();
        IReadOnlyList<TimeSeriesPoint> chargePercent = app.State.BatteryChargePercentTimeline;
        IReadOnlyList<TimeSeriesPoint> dischargePower = app.BatteryHistoryService.GetLatestDischargeTimeline();
        IReadOnlyList<TimeSeriesPoint> dischargePercent = app.BatteryHistoryService.GetLatestDischargePercentTimeline();

        ChargePercentChart.Values = chargePercent;
        DischargeChart.Values = dischargePower;
        DischargePercentChart.Values = dischargePercent;
        DischargeSummaryText.Text = app.BatteryHistoryService.GetLatestDischargeSummary();
        UpdateHistoryRangeButton(availableDays.Count);

        RecentSessionItems.Children.Clear();
        if (days.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No completed battery sessions yet. ThinkControl starts learning automatically while you use and charge the laptop.",
                FontSize = TypographyScale.Caption,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(1, 5, 1, 2)
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            RecentSessionItems.Children.Add(empty);
            return;
        }

        foreach (BatteryDaySummary day in days)
            RecentSessionItems.Children.Add(CreateDayRow(day));
    }

    private Expander CreateDayRow(BatteryDaySummary day)
    {
        var header = new Grid { MinHeight = 42 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.Children.Add(new TextBlock
        {
            Text = day.HasActiveSession ? $"{day.Label} (live)" : day.Label,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        string charge = day.ChargedPercent > 0 || day.ChargingTime > TimeSpan.Zero ? $"Charged {day.ChargedPercent}% in {FormatShortDuration(day.ChargingTime)}" : "No charging";
        string usage = day.DischargedPercent > 0 || day.UsageTime > TimeSpan.Zero ? $"Used {day.DischargedPercent}% in {FormatShortDuration(day.UsageTime)}" : "No battery usage";
        var summary = new TextBlock
        {
            Text = $"{charge}\n{usage}",
            Margin = new Thickness(20, 6, 0, 6),
            ToolTip = "Time is divided at local midnight. Percentage changes count on the day they were measured; older sessions without samples count on their end day. Expanded sessions show their full duration.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(summary, 1);
        header.Children.Add(summary);

        var sessions = new StackPanel { Margin = new Thickness(20, 0, 0, 7) };
        Grid.SetIsSharedSizeScope(sessions, true);
        foreach (BatterySessionDetail session in day.Sessions)
            sessions.Children.Add(CreateSessionRow(session));

        return new Expander
        {
            Header = header,
            Content = sessions,
            BorderBrush = (Brush)FindResource("Tc.Border"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(2, 0, 2, 0),
            IsExpanded = day.HasActiveSession
        };
    }

    internal void PrepareForSnapshot(AppState state)
    {
        ApplyBatteryGaugePolish();


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
        DischargeSummaryText.Text = "Latest discharge: 88% to 63% in 3h 45m, average power: 6.9 W";

        BatterySessionDetail charge = new(
            "snapshot-charge", "Charge", end - TimeSpan.FromMinutes(43), end,
            61, 78, 17.8, 20.4, 12.1, 23.7,
            chargePower, ChargePercentChart.Values.ToArray(),
            "61% to 78% in 43 min, average power: 17.8 W");
        BatterySessionDetail discharge = new(
            "snapshot-discharge", "Discharge", end - TimeSpan.FromHours(4), end - TimeSpan.FromMinutes(15),
            88, 63, 6.9, 8.2, 25.0, 6.7,
            dischargePower, DischargePercentChart.Values.ToArray(),
            "88% to 63% in 3h 45m, average power: 6.9 W");
        PresentChargeSession(charge);
        var today = new BatteryDaySummary(
            DateOnly.FromDateTime(DateTime.Today), "Today", 17, 25,
            charge.Duration, discharge.Duration, [charge, discharge], false);
        RecentSessionItems.Children.Clear();
        RecentSessionItems.Children.Add(CreateDayRow(today));
        HistoryRangeButton.Visibility = Visibility.Collapsed;

        int snapshotStart = state.BatteryProtectionStartPercent ?? 75;
        int snapshotStop = state.BatteryProtectionStopPercent ?? 85;
        bool snapshotProtection = state.BatteryProtectionEnabled != false;
        _syncingChargeProtection = true;
        try
        {
            RemoveDynamicChargeProtectionPreset();
            ListBoxItem? selected = snapshotProtection
                ? FindChargeProtectionPreset(snapshotStart, snapshotStop)
                : FindChargeProtectionPreset(75, 85);
            if (snapshotProtection && selected is null)
            {
                selected = new ListBoxItem
                {
                    Content = $"Custom: {snapshotStart}–{snapshotStop}%",
                    Tag = $"custom:{snapshotStart},{snapshotStop}", Visibility = Visibility.Collapsed
                };
                ChargeProtectionComboBox.Items.Insert(0, selected);
            }
            ChargeProtectionComboBox.SelectedItem = selected ?? ChargeProtectionComboBox.Items.OfType<ListBoxItem>().FirstOrDefault();
            ChargeProtectionSwitch.IsChecked = snapshotProtection;
            ChargeProtectionSwitch.IsEnabled = state.BatteryProtectionWritable;
            ChargeProtectionComboBox.IsEnabled = state.BatteryProtectionWritable && snapshotProtection;
        }
        finally
        {
            _syncingChargeProtection = false;
        }
        ChargeProtectionStateText.Text = snapshotProtection
            ? $"{snapshotStop}% limit active"
            : "Off";
        ChargeProtectionImpactText.Text = snapshotProtection
            ? DescribeChargeProtectionImpact(snapshotStart, snapshotStop)
            : "Preservation is off; charging is allowed to 100%.";
        _batteryProtectionAvailable = true;
        _lastChargeProtectionStart = snapshotStart;
        _lastChargeProtectionStop = snapshotStop;
        UpdateBatteryAgingGuidance(snapshotProtection, available: true);
        CustomChargeLimitsButton.IsEnabled = state.BatteryProtectionWritable && snapshotProtection;
        ChargeProtectionProviderText.Text = "Charging limits verified";
        ChargeProtectionFallbackButton.Visibility = Visibility.Collapsed;
    }

    internal void ExpandSnapshotHistory()
    {
        BatteryDetails.IsExpanded = true;
        BatterySessions.IsExpanded = true;
        if (RecentSessionItems.Children.OfType<Expander>().FirstOrDefault() is { } day)
            day.IsExpanded = true;
    }

    private Button CreateSessionRow(BatterySessionDetail session)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "SessionKind" });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

        var kind = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 0, 16, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        kind.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");
        var kindText = new TextBlock
        {
            Text = session.IsActive ? $"{session.Kind} (live)" : session.Kind,
            FontSize = TypographyScale.Caption,
            FontWeight = FontWeights.SemiBold
        };
        kindText.SetResourceReference(TextBlock.ForegroundProperty, session.Kind == "Charge" ? "Tc.Success" : "Tc.TextMuted");
        kind.Child = kindText;
        grid.Children.Add(kind);

        var summary = new TextBlock
        {
            Text = $"{session.StartedAt:HH:mm}–{(session.EndedAt is { } ended ? ended.ToString("HH:mm") : "now")}    {FormatShortDuration(session.Duration)}\n{session.StartPercent}% → {session.EndPercent}%" +
                (session.EnergyWh is { } energy ? $"    {energy:0.#} Wh" : string.Empty),
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(summary, 1);
        grid.Children.Add(summary);

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = TypographyScale.Value,
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

    private static string FormatShortDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}m";
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
        string subtitle = $"{session.Kind}: {session.StartPercent}% to {session.EndPercent}% ({local.ToString("g", CultureInfo.CurrentCulture)})";
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

    private void OpenVantage_Click(object sender, RoutedEventArgs e)
    {
        if (LenovoSoftwareLauncher.TryOpenVantage())
            return;

        try { Process.Start(new ProcessStartInfo("ms-settings:batterysaver") { UseShellExecute = true }); }
        catch { }
    }
}
