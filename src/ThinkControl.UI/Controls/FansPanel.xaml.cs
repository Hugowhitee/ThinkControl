using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class FansPanel : UserControl
{
    private readonly ObservableCollection<CalibrationRow> _calibrationRows = [];
    private readonly ObservableCollection<FanProfileChoice> _profileChoices = [];
    private readonly FanCurveGraph _activeCurveGraph = new() { IsReadOnly = true, ShowLiveLabel = false };
    private App? _app;
    private bool _resetAdded;
    private bool _statusSubscribed;
    private bool _syncingProfileSelection;
    private string _currentProfileId = "Lenovo Auto";

    public FansPanel()
    {
        InitializeComponent();
        ActiveCurvePreviewHost.Content = _activeCurveGraph;
        CalibrationResults.ItemsSource = _calibrationRows;
        ProfileComboBox.ItemsSource = _profileChoices;
        Loaded += (_, _) => SyncStatusSubscription();
        Unloaded += (_, _) => UnsubscribeStatus();
        IsVisibleChanged += (_, _) => SyncStatusSubscription();
    }

    internal void Initialize(App app)
    {
        EnsureResetButton();
        if (!ReferenceEquals(_app, app))
        {
            UnsubscribeStatus();
            _app = app;
            DataContext = app.State;
        }

        SyncProfileSelector(app.State.CoolingProfile, app.UserSettings.Current.CoolingProfile);
        SyncStatusSubscription();
        if (IsVisible)
            _ = app.HardwareClient.GetStatusAsync();
    }

    internal void PrepareForSnapshot(AppState state)
    {
        EnsureResetButton();
        DataContext = state;
        SyncProfileSelector(state.CoolingProfile, state.CoolingProfile);
        ProfileComboBox.IsEnabled = state.CanFanControl;
        CoolingDetailText.Text = state.CanFanControl
            ? $"{DisplayProfile(state.CoolingProfile)} · {state.ControlTemperatureText} control temperature"
            : DescribeUnavailable(state.MachineType, state.HardwareAccess, state.CanSensorTelemetry || state.CanFanTelemetry);
        AppliedLevelText.Text = state.CanFanControl ? state.FanStateText : "Unavailable";
        CharacterizeButton.IsEnabled = state.CanFanControl;
        StopCharacterizationButton.Visibility = Visibility.Collapsed;
        CharacterizationProgress.Visibility = Visibility.Collapsed;
        CharacterizationStatusText.Text = state.CanFanControl
            ? "Ready to calibrate the verified X9 fan states"
            : "Calibration requires a verified writable fan provider";
        MarkAudibleButton.Visibility = Visibility.Collapsed;
        ManualPercentSlider.IsEnabled = state.CanFanControl;
        _calibrationRows.Clear();
        UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, state.ControlTemperatureC, state.FanRpm);
    }

    private void SyncStatusSubscription()
    {
        bool shouldSubscribe = _app is not null && IsLoaded && IsVisible;
        if (shouldSubscribe == _statusSubscribed)
            return;

        if (shouldSubscribe)
        {
            _app!.HardwareClient.StatusObserved += HardwareClient_StatusObserved;
            _statusSubscribed = true;
            _ = _app.HardwareClient.GetStatusAsync();
        }
        else
        {
            UnsubscribeStatus();
        }
    }

    private void UnsubscribeStatus()
    {
        if (!_statusSubscribed || _app is null)
            return;
        _app.HardwareClient.StatusObserved -= HardwareClient_StatusObserved;
        _statusSubscribed = false;
    }

    private void HardwareClient_StatusObserved(object? sender, ServiceResponse? response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => HardwareClient_StatusObserved(sender, response));
            return;
        }
        if (IsVisible)
            ApplyStatus(response);
    }

    private void ApplyStatus(ServiceResponse? response)
    {
        TelemetrySnapshot? telemetry = response?.Success == true ? response.Telemetry : null;
        bool canControl = response?.Capabilities?.FanControl == true;
        bool hasTelemetry = response?.Capabilities?.FanTelemetry == true || response?.Capabilities?.SensorTelemetry == true;

        string profileName = telemetry?.CoolingProfile ?? "Lenovo Auto";
        string profileId = telemetry?.CoolingProfileId ?? (profileName.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ? "Lenovo Auto" : profileName);
        SyncProfileSelector(profileName, profileId);
        ProfileComboBox.IsEnabled = canControl;

        CoolingDetailText.Text = telemetry?.CoolingStatus ?? (canControl
            ? "Choose a fan profile or open the curve editor."
            : DescribeUnavailable(_app?.State.MachineType, telemetry?.HardwareAccess ?? _app?.State.HardwareAccess, hasTelemetry));

        if (telemetry?.CoolingAppliedPercent is int percent)
        {
            AppliedLevelText.Text = telemetry.CoolingAppliedLevel == 8
                ? "100% · Full speed"
                : telemetry.CoolingAppliedLevel is int step
                    ? $"{percent}% · Step {step}"
                    : $"{percent}%";
        }
        else if (telemetry?.CoolingAppliedLevel is int legacyLevel)
        {
            AppliedLevelText.Text = legacyLevel == 8 ? "Full speed" : $"Step {legacyLevel}";
        }
        else
        {
            AppliedLevelText.Text = "Auto";
        }

        FanCharacterizationSnapshot? characterization = telemetry?.FanCharacterization;
        bool running = characterization?.Running == true;
        CharacterizeButton.IsEnabled = canControl && !running;
        StopCharacterizationButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        CharacterizationProgress.Maximum = Math.Max(1, characterization?.TotalLevels ?? 8);
        CharacterizationProgress.Visibility = running || (characterization?.Levels.Count ?? 0) > 0
            ? Visibility.Visible : Visibility.Collapsed;
        CharacterizationProgress.Value = characterization?.CompletedLevels ?? 0;
        CharacterizationStatusText.Text = characterization?.Status ?? (canControl
            ? "Not calibrated yet"
            : "Calibration requires a verified writable fan provider");
        MarkAudibleButton.Visibility = running && characterization?.CurrentLevel is >= 1 and <= 8
            ? Visibility.Visible : Visibility.Collapsed;
        if (characterization?.CurrentLevel is int current)
            MarkAudibleButton.Content = current == 8 ? "Full speed is clearly audible" : $"Clearly audible at step {current}";

        ManualPercentSlider.IsEnabled = canControl;
        BuildCalibrationRows(characterization);
        UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, telemetry?.ControlTemperatureC, telemetry?.FanRpm);
    }

    private void BuildCalibrationRows(FanCharacterizationSnapshot? characterization)
    {
        _calibrationRows.Clear();
        if (characterization is null)
            return;

        FanLevelCalibrationSnapshot? full = characterization.Levels.FirstOrDefault(level => level.Level == 8);
        double? fullRpm = full?.Fans.Count > 0 ? full.Fans.Average(fan => fan.MedianRpm) : null;

        foreach (FanLevelCalibrationSnapshot point in characterization.Levels.OrderBy(level => level.Level))
        {
            string label = point.Level == 8 ? "Full speed" : $"EC step {point.Level}";
            string rpm;
            if (point.Fans.Count == 0)
            {
                rpm = "No tachometer sample";
            }
            else
            {
                double average = point.Fans.Average(fan => fan.MedianRpm);
                string values = string.Join(" · ", point.Fans.Select(fan => $"{fan.Label} {fan.MedianRpm:N0} RPM"));
                if (fullRpm is > 0)
                {
                    int relative = point.Level == 8 ? 100 : (int)Math.Round(Math.Clamp(average / fullRpm.Value * 100.0, 0, 99));
                    rpm = $"{values} · ~{relative}% of full speed";
                }
                else
                {
                    rpm = values;
                }
            }

            _calibrationRows.Add(new CalibrationRow(label, rpm, point.Stable ? "Stable" : "Variable"));
        }

        if (characterization.AudibleFromLevel is int audible)
            CharacterizationStatusText.Text += audible == 8 ? " · full speed marked audible" : $" · clearly audible from step {audible}";
    }

    private void SyncProfileSelector(string? profileName, string? profileId)
    {
        _currentProfileId = string.IsNullOrWhiteSpace(profileId) ? "Lenovo Auto" : profileId;
        RebuildProfileChoices();

        _syncingProfileSelection = true;
        try
        {
            FanProfileChoice? selected = _profileChoices.FirstOrDefault(choice => ProfileIdsEqual(choice.Id, _currentProfileId));
            if (selected is null)
            {
                string display = DisplayProfile(profileName);
                selected = _profileChoices.FirstOrDefault(choice => string.Equals(choice.Name, display, StringComparison.OrdinalIgnoreCase));
            }
            ProfileComboBox.SelectedItem = selected ?? _profileChoices.FirstOrDefault();
            UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, _app?.State.ControlTemperatureC, _app?.State.FanRpm);
        }
        finally
        {
            _syncingProfileSelection = false;
        }
    }

    private void UpdateActiveCurvePreview(FanProfileChoice? choice, double? temperatureC, int? rpm)
    {
        FanCurveDefinition? curve = choice is null ? null : _app?.FanProfiles.Find(choice.Id);
        if (curve is null)
        {
            ActiveCurvePreview.Visibility = Visibility.Collapsed;
            _activeCurveGraph.SetLiveState(null, null, null);
            return;
        }

        _activeCurveGraph.SetCurve(curve.Points);
        _activeCurveGraph.SelectedIndex = -1;
        int? target = temperatureC is double temperature
            ? FanCurveGraphPolicy.ResolvePercent(curve.Points, temperature)
            : null;
        _activeCurveGraph.SetLiveState(temperatureC, target, rpm);
        LiveCurveStatus.Text = temperatureC is double live && target is int percent
            ? $"{live:0.0} °C → {percent}% target" + (rpm is int actual ? $" · {actual:N0} RPM now" : string.Empty)
            : "Waiting for control temperature";
        ActiveCurvePreview.Visibility = Visibility.Visible;
    }

    private void RebuildProfileChoices()
    {
        if (_app is null)
            return;

        FanProfileChoice[] desired =
        [
            new("Lenovo Auto", "Auto"),
            .. _app.FanProfiles.GetProfiles().Select(profile => new FanProfileChoice(profile.Id, profile.Name))
        ];

        if (_profileChoices.SequenceEqual(desired))
            return;

        _profileChoices.Clear();
        foreach (FanProfileChoice choice in desired)
            _profileChoices.Add(choice);
    }

    private static bool ProfileIdsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(left, "Lenovo Auto", StringComparison.OrdinalIgnoreCase) && string.Equals(right, "Auto", StringComparison.OrdinalIgnoreCase)) ||
        (string.Equals(right, "Lenovo Auto", StringComparison.OrdinalIgnoreCase) && string.Equals(left, "Auto", StringComparison.OrdinalIgnoreCase));

    private static string DisplayProfile(string? raw) => raw?.Trim() switch
    {
        null or "" or "Lenovo Auto" or "Auto" => "Auto",
        "Silent" => "Quiet",
        "Normal" => "Balanced",
        "Cool" => "Max cooling",
        string value => value
    };

    private void ProfileComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (_app is null)
            return;
        SyncProfileSelector(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile);
        ProfileComboBox.IsDropDownOpen = true;
    }

    private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingProfileSelection || _app is null || ProfileComboBox.SelectedItem is not FanProfileChoice choice)
            return;
        if (ProfileIdsEqual(choice.Id, _currentProfileId))
            return;

        ProfileComboBox.IsEnabled = false;
        try
        {
            if (!await _app.SetCoolingProfileAsync(choice.Id))
            {
                CoolingDetailText.Text = _app.State.HardwareAccess;
                SyncProfileSelector(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile);
                return;
            }

            SyncProfileSelector(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile);
        }
        finally
        {
            ProfileComboBox.IsEnabled = _app.State.CanFanControl;
        }
    }

    private void EditCurves_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        ProfileComboBox.IsDropDownOpen = false;
        var editor = new FanCurveEditorWindow(_app) { Owner = Window.GetWindow(this) };
        editor.ShowDialog();
        SyncProfileSelector(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile);
        if (IsVisible)
            _ = _app.HardwareClient.GetStatusAsync();
    }

    private void ManualPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ManualPercentValue is not null)
            ManualPercentValue.Text = $"{Math.Round(e.NewValue):0}%";
    }

    private async void ManualPercentApply_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button)
            return;
        int percent = (int)Math.Round(ManualPercentSlider.Value);
        button.IsEnabled = false;
        try
        {
            if (!await _app.SetManualFanPercentAsync(percent))
                CoolingDetailText.Text = _app.State.HardwareAccess;
        }
        finally { button.IsEnabled = true; }
    }

    private void EnsureResetButton()
    {
        if (_resetAdded || Content is not StackPanel stack || stack.Children.Count == 0 || stack.Children[0] is not TextBlock title)
            return;

        stack.Children.RemoveAt(0);
        var header = new Grid();
        header.Children.Add(title);
        var reset = new Button
        {
            Content = "Defaults",
            Style = TryFindResource("TcButton") as Style,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 10.5,
            ToolTip = "Return fan ownership to Lenovo Auto"
        };
        reset.Click += Reset_Click;
        header.Children.Add(reset);
        stack.Children.Insert(0, header);
        _resetAdded = true;
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button)
            return;
        button.IsEnabled = false;
        try { _ = await _app.ResetFanDefaultsAsync(); }
        finally { button.IsEnabled = true; }
    }

    private static string DescribeUnavailable(string? machineType, string? hardwareAccess, bool telemetryReady)
    {
        string detail = string.IsNullOrWhiteSpace(hardwareAccess) ? "provider status unavailable" : hardwareAccess;
        bool x9 = string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase);

        if (x9)
        {
            return telemetryReady
                ? $"Read-only telemetry is active. Direct fan writes stay firmware-managed until the verified X9 low-level provider passes. {detail}"
                : $"Firmware currently owns cooling. ThinkControl retries the verified X9 provider with bounded backoff. {detail}";
        }

        return telemetryReady
            ? $"Read-only telemetry is active. No verified writable fan provider is active for this model, so firmware keeps cooling ownership. {detail}"
            : $"Cooling stays firmware-managed until a compatible fan provider is detected for this model. {detail}";
    }

    private async void Characterize_Click(object sender, RoutedEventArgs e)
    {
        if (_app is not null)
            await _app.StartFanCharacterizationAsync();
    }

    private async void StopCharacterization_Click(object sender, RoutedEventArgs e)
    {
        if (_app is not null)
            await _app.StopFanCharacterizationAsync();
    }

    private async void MarkAudible_Click(object sender, RoutedEventArgs e)
    {
        if (_app is not null)
            await _app.MarkCurrentFanLevelAudibleAsync();
    }

    private async void ManualLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not FrameworkElement { Tag: string raw } || !int.TryParse(raw, out int level))
            return;
        ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(level);
        if (response?.Success != true)
        {
            _app.State.HardwareAccess = response?.Error ?? "Manual fan control unavailable";
            _ = _app.HardwareClient.GetStatusAsync();
        }
    }

    private sealed record CalibrationRow(string LevelText, string RpmText, string StabilityText);
    private sealed record FanProfileChoice(string Id, string Name);
}
