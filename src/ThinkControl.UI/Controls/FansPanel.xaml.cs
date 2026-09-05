using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;
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
    private bool _snapshotMode;
    private bool _syncingProfileSelection;
    private string _currentProfileId = "Lenovo Auto";
    private string _fanControlKind = FanControlKinds.None;

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

        _fanControlKind = app.State.FanControlKind;
        if (_fanControlKind == FanControlKinds.None)
            _fanControlKind = ResolveFanControlKind(null, app.State.HardwareAccess, app.State.CanFanControl);
        SyncProfileSelector(app.State.CoolingProfile, CurrentProfileIdForDisplay(app.State.CoolingProfile, app.UserSettings.Current.CoolingProfile));
        ApplyProviderCopy(app.State, app.State.CanFanControl, _fanControlKind);
        ApplyCalibrationUi(app.FanCalibrationState, app.State.CanFanControl);
        SyncStatusSubscription();
    }

    internal void PrepareForSnapshot(AppState state)
    {
        _snapshotMode = true;
        UnsubscribeStatus();
        EnsureResetButton();
        DataContext = state;
        _fanControlKind = state.FanControlKind;
        if (_fanControlKind == FanControlKinds.None)
            _fanControlKind = ResolveFanControlKind(null, state.HardwareAccess, state.CanFanControl);
        SyncProfileSelector(state.CoolingProfile, state.CoolingProfile);
        ApplyProviderCopy(state, state.CanFanControl, _fanControlKind);
        CoolingDetailText.Text = state.CanFanControl
            ? $"{DisplayProfile(state.CoolingProfile)} · {state.ControlTemperatureText} control temperature"
            : DescribeUnavailable(state.MachineType, state.HardwareAccess, state.CanSensorTelemetry || state.CanFanTelemetry);
        AppliedLevelText.Text = state.CanFanControl ? state.FanStateText : "Unavailable";

        bool canCalibrate = IsVerifiedX9DiscreteEc(state, state.CanFanControl, _fanControlKind) && state.CanFanTelemetry;
        FanCalibrationUiState calibration = canCalibrate
            ? new FanCalibrationUiState(
                Relevant: true,
                Running: false,
                Ready: false,
                CompletedLevels: 0,
                TotalLevels: 7,
                Status: "Calibration required before percentage fan profiles and manual targets are enabled.")
            : FanCalibrationUiState.None;
        ApplyCalibrationUi(calibration, state.CanFanControl);
        _calibrationRows.Clear();
        UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, state.ControlTemperatureC, state.FanRpm);
    }

    private void SyncStatusSubscription()
    {
        bool shouldSubscribe = !_snapshotMode && _app is not null && IsLoaded && IsVisible;
        if (shouldSubscribe == _statusSubscribed)
            return;

        if (shouldSubscribe)
        {
            _app!.HardwareClient.StatusObserved += HardwareClient_StatusObserved;
            _statusSubscribed = true;
            // One immediate refresh is enough when entering the page. Ongoing status
            // cadence is owned centrally by App.RuntimeRefresh so the Fans page cannot
            // create a second hardware/IPC polling loop.
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
        bool canFanTelemetry = response?.Capabilities?.FanTelemetry == true;
        bool hasTelemetry = canFanTelemetry || response?.Capabilities?.SensorTelemetry == true;
        _fanControlKind = ResolveFanControlKind(
            response?.Capabilities?.FanControlKind,
            telemetry?.HardwareAccess ?? _app?.State.HardwareAccess,
            canControl);

        string profileName = telemetry?.CoolingProfile ?? "Lenovo Auto";
        string profileId = telemetry?.CoolingProfileId ?? (profileName.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ? "Lenovo Auto" : profileName);
        SyncProfileSelector(profileName, profileId);
        if (_app is not null)
            ApplyProviderCopy(_app.State, canControl, _fanControlKind);

        CoolingDetailText.Text = telemetry?.CoolingStatus ?? (canControl
            ? "Choose a fan profile or open the curve editor."
            : DescribeUnavailable(_app?.State.MachineType, telemetry?.HardwareAccess ?? _app?.State.HardwareAccess, hasTelemetry));

        if (telemetry?.CoolingAppliedPercent is int percent)
        {
            if (_fanControlKind == FanControlKinds.OemTargetRpm && telemetry.CoolingAppliedLevel is null)
            {
                AppliedLevelText.Text = $"{percent}% OEM target";
            }
            else
            {
                AppliedLevelText.Text = telemetry.CoolingAppliedLevel is int step
                    ? $"{percent}% · Step {Math.Min(step, 7)}"
                    : $"{percent}%";
            }
        }
        else if (telemetry?.CoolingAppliedLevel is int legacyLevel)
        {
            AppliedLevelText.Text = $"Step {Math.Min(legacyLevel, 7)}";
        }
        else
        {
            AppliedLevelText.Text = "Auto";
        }

        FanCharacterizationSnapshot? characterization = telemetry?.FanCharacterization;
        FanCalibrationUiState calibration = _app?.FanCalibrationState ?? FanCalibrationUiState.None;
        ApplyCalibrationUi(calibration, canControl);
        BuildCalibrationRows(characterization);
        UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, telemetry?.ControlTemperatureC, telemetry?.FanRpm);
    }

    private void ApplyCalibrationUi(FanCalibrationUiState calibration, bool canControl)
    {
        CalibrationCard.Visibility = calibration.Relevant ? Visibility.Visible : Visibility.Collapsed;
        if (!calibration.Relevant)
        {
            ProfileComboBox.IsEnabled = canControl;
            EditCurvesButton.IsEnabled = canControl;
            ProfileCard.Opacity = 1;
            ManualControlExpander.IsEnabled = canControl;
            ManualControlExpander.Opacity = 1;
            ManualPercentSlider.IsEnabled = canControl;
            ManualPercentApplyButton.IsEnabled = canControl;
            RawEcStepsExpander.IsEnabled = canControl;
            return;
        }

        bool locked = calibration.Required;
        bool running = calibration.Running;
        bool ready = calibration.Ready;
        bool attention = locked || running;
        CalibrationCard.SetResourceReference(Border.BorderBrushProperty, attention ? "Tc.Accent" : "Tc.Border");
        CalibrationCard.BorderThickness = new Thickness(1);

        CharacterizationTitleText.Text = running
            ? "Fan calibration in progress"
            : ready
                ? "Fan calibration complete"
                : "Fan calibration required";
        CalibrationDescriptionText.Text = running
            ? "ThinkControl temporarily owns the verified EC fan states while each level settles and real tachometer samples are measured. Other fan controls are locked until calibration finishes or is stopped; Lenovo Auto is restored automatically."
            : ready
                ? "All seven verified X9 EC states have a measured tachometer mapping. Percentage profiles and manual targets can now use the calibrated discrete range."
                : "ThinkControl must measure all seven verified X9 EC fan states before percentage profiles and manual targets are enabled. Lenovo Auto remains the safe default until calibration starts.";
        CharacterizationStatusText.Text = calibration.Status;

        CharacterizeButton.Content = ready ? "Recalibrate" : "Calibrate now";
        CharacterizeButton.IsEnabled = canControl && !running;
        CharacterizeButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        StopCharacterizationButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        StopCharacterizationButton.IsEnabled = running;
        CharacterizationProgress.Maximum = Math.Max(1, calibration.TotalLevels);
        CharacterizationProgress.Value = Math.Clamp(calibration.CompletedLevels, 0, calibration.TotalLevels);
        CharacterizationProgress.Visibility = running || calibration.CompletedLevels > 0 || ready
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool semanticControlsEnabled = canControl && ready;
        ProfileComboBox.IsEnabled = semanticControlsEnabled;
        EditCurvesButton.IsEnabled = semanticControlsEnabled;
        ProfileCard.Opacity = semanticControlsEnabled ? 1 : 0.42;
        ManualControlExpander.IsEnabled = semanticControlsEnabled;
        ManualControlExpander.Opacity = semanticControlsEnabled ? 1 : 0.42;
        ManualPercentSlider.IsEnabled = semanticControlsEnabled;
        ManualPercentApplyButton.IsEnabled = semanticControlsEnabled;
        RawEcStepsExpander.IsEnabled = semanticControlsEnabled;

        if (!semanticControlsEnabled)
        {
            CoolingDetailText.Text = running
                ? "Calibration currently owns fan output. Profile controls return after the run finishes or is stopped."
                : "Lenovo Auto remains active until fan calibration completes.";
        }
    }

    private void BuildCalibrationRows(FanCharacterizationSnapshot? characterization)
    {
        _calibrationRows.Clear();
        if (characterization is null)
            return;

        FanLevelCalibrationSnapshot? maximum = characterization.Levels.FirstOrDefault(level => level.Level == 7);
        double? maximumRpm = maximum?.Fans.Count > 0 ? maximum.Fans.Average(fan => fan.MedianRpm) : null;

        foreach (FanLevelCalibrationSnapshot point in characterization.Levels.OrderBy(level => level.Level))
        {
            string label = $"EC step {point.Level}";
            string rpm;
            if (point.Fans.Count == 0)
            {
                rpm = "No tachometer sample";
            }
            else
            {
                double average = point.Fans.Average(fan => fan.MedianRpm);
                string values = string.Join(" · ", point.Fans.Select(fan => $"{fan.Label} {fan.MedianRpm:N0} RPM"));
                if (maximumRpm is > 0)
                {
                    int relative = point.Level == 7 ? 100 : (int)Math.Round(Math.Clamp(average / maximumRpm.Value * 100.0, 0, 99));
                    rpm = $"{values} · ~{relative}% of calibrated step 7";
                }
                else
                {
                    rpm = values;
                }
            }

            _calibrationRows.Add(new CalibrationRow(label, rpm, point.Stable ? "Stable" : "Variable"));
        }
    }

    private void SyncProfileSelector(string? profileName, string? profileId)
    {
        bool manual = IsManualProfile(profileName);
        _currentProfileId = manual
            ? profileName!.Trim()
            : string.IsNullOrWhiteSpace(profileId) ? "Lenovo Auto" : profileId;
        RebuildProfileChoices(manual ? _currentProfileId : null);

        _syncingProfileSelection = true;
        try
        {
            FanProfileChoice? selected = _profileChoices.FirstOrDefault(choice => ProfileIdsEqual(choice.Id, _currentProfileId));
            if (selected is null && !manual)
            {
                string display = DisplayProfile(profileName);
                selected = _profileChoices.FirstOrDefault(choice => string.Equals(choice.Name, display, StringComparison.OrdinalIgnoreCase));
            }

            ProfileComboBox.SelectedItem = selected;
            UpdateActiveCurvePreview(selected, _app?.State.ControlTemperatureC, _app?.State.FanRpm);
        }
        finally
        {
            _syncingProfileSelection = false;
        }
    }

    private void UpdateActiveCurvePreview(FanProfileChoice? choice, double? temperatureC, int? rpm)
    {
        FanCurveDefinition? curve = choice is null || !choice.Selectable ? null : _app?.FanProfiles.Find(choice.Id);
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

    private void RebuildProfileChoices(string? manualState)
    {
        if (_app is null)
            return;

        var desired = new List<FanProfileChoice>();
        if (IsManualProfile(manualState))
            desired.Add(new FanProfileChoice(manualState!.Trim(), manualState.Trim(), Selectable: false));
        desired.Add(new FanProfileChoice("Lenovo Auto", "Auto"));
        desired.AddRange(_app.FanProfiles.GetProfiles().Select(profile => new FanProfileChoice(profile.Id, profile.Name)));

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

    private static bool IsManualProfile(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("Manual ", StringComparison.OrdinalIgnoreCase);

    private static string CurrentProfileIdForDisplay(string? profileName, string? persistedProfileId) =>
        IsManualProfile(profileName) ? profileName!.Trim() : persistedProfileId ?? "Lenovo Auto";

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
        if (_app is null || _app.FanCalibrationState.Required)
            return;
        SyncProfileSelector(
            _app.State.CoolingProfile,
            CurrentProfileIdForDisplay(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile));
        ProfileComboBox.IsDropDownOpen = true;
    }

    private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingProfileSelection || _app is null || ProfileComboBox.SelectedItem is not FanProfileChoice choice)
            return;
        if (!choice.Selectable || ProfileIdsEqual(choice.Id, _currentProfileId))
            return;

        ProfileComboBox.IsEnabled = false;
        try
        {
            if (!await _app.SetCoolingProfileAsync(choice.Id))
            {
                CoolingDetailText.Text = _app.State.HardwareAccess;
                SyncProfileSelector(
                    _app.State.CoolingProfile,
                    CurrentProfileIdForDisplay(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile));
                return;
            }

            SyncProfileSelector(
                _app.State.CoolingProfile,
                CurrentProfileIdForDisplay(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile));
        }
        finally
        {
            ProfileComboBox.IsEnabled = _app.State.CanFanControl && !_app.FanCalibrationState.Required;
        }
    }

    private void EditCurves_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required)
            return;
        ProfileComboBox.IsDropDownOpen = false;
        var editor = new FanCurveEditorWindow(_app) { Owner = Window.GetWindow(this) };
        editor.ShowDialog();
        SyncProfileSelector(
            _app.State.CoolingProfile,
            CurrentProfileIdForDisplay(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile));
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
        if (_app is null || sender is not Button button || _app.FanCalibrationState.Required)
            return;
        int percent = (int)Math.Round(ManualPercentSlider.Value);
        button.IsEnabled = false;
        try
        {
            if (!await _app.SetManualFanPercentAsync(percent))
                CoolingDetailText.Text = _app.State.HardwareAccess;
        }
        finally { button.IsEnabled = !_app.FanCalibrationState.Required; }
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
            FontSize = TypographyScale.Caption,
            ToolTip = null
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

    private void ApplyProviderCopy(AppState state, bool canControl, string fanControlKind)
    {
        bool x9Model = DeviceCapabilityExpectations.IsVerifiedX9(state.MachineType);
        bool oemTargetRpm = canControl && string.Equals(fanControlKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal);
        bool x9EcWriter = x9Model && canControl && string.Equals(fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        bool x9Calibration = x9EcWriter && state.CanFanTelemetry;

        FansIntroText.Text = x9Model
            ? "Fan behavior is independent from Windows performance mode. ThinkControl keeps Lenovo Auto as the fail-safe and uses the highest-capability verified X9 fan provider available."
            : "Fan behavior is independent from Windows performance mode. ThinkControl uses only fan telemetry and control states exposed by the active provider; firmware stays in charge when no writable provider is verified.";

        FanMappingDetailText.Text = oemTargetRpm
            ? "Built-in and custom curves send continuous 0–100% targets through Lenovo's capability-reported target-RPM interface. Each fan is mapped independently across its OEM-provided minimum and maximum RPM range."
            : x9EcWriter
                ? "The verified X9 EC fallback has seven discrete output states. ThinkControl enables percentage-based profiles only after those states have a complete real tachometer calibration."
                : "Profiles and curves use the active provider's verified output range. ThinkControl does not assume EC steps or PWM when the provider does not expose them.";
        FanProviderDetailText.ToolTip = null;

        CalibrationCard.Visibility = x9Calibration ? Visibility.Visible : Visibility.Collapsed;
        RawEcStepsExpander.Visibility = x9EcWriter ? Visibility.Visible : Visibility.Collapsed;
        ManualControlDescriptionText.Text = oemTargetRpm
            ? "0% requests the Lenovo-reported minimum running target for each fan. 100% requests each fan's Lenovo-reported maximum target RPM. Auto is a separate firmware-owned mode; returning to Auto releases the OEM targets instead of pretending 100% and Auto are the same state."
            : x9EcWriter
                ? "0% means the lowest verified running EC state, not fan-off. 100% requests the highest verified standard X9 EC step (step 7) in the calibrated fallback range. The unverified 0x40 full-speed/disengaged family remains blocked."
                : "The manual target uses only the active provider's verified output range. ThinkControl does not expose raw EC steps unless the active provider explicitly supports the verified X9 EC contract.";
        ManualControlExpander.IsEnabled = canControl;
    }

    private static bool IsVerifiedX9DiscreteEc(AppState state, bool canControl, string fanControlKind) =>
        canControl &&
        DeviceCapabilityExpectations.IsVerifiedX9(state.MachineType) &&
        string.Equals(fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);

    private static string ResolveFanControlKind(string? explicitKind, string? hardwareAccess, bool canControl)
    {
        if (!canControl)
            return FanControlKinds.None;
        if (string.Equals(explicitKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal) ||
            string.Equals(explicitKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal))
        {
            return explicitKind!;
        }

        string access = hardwareAccess ?? string.Empty;
        if (access.Contains("OEM target-RPM", StringComparison.OrdinalIgnoreCase) ||
            access.Contains("Other Mode", StringComparison.OrdinalIgnoreCase))
            return FanControlKinds.OemTargetRpm;
        if (access.Contains("discrete EC", StringComparison.OrdinalIgnoreCase) ||
            access.Contains("verified X9 EC", StringComparison.OrdinalIgnoreCase))
            return FanControlKinds.DiscreteEc;
        return FanControlKinds.None;
    }

    private static string DescribeUnavailable(string? machineType, string? hardwareAccess, bool telemetryReady)
    {
        string detail = string.IsNullOrWhiteSpace(hardwareAccess) ? "provider status unavailable" : hardwareAccess;
        bool x9 = string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase);

        if (x9)
        {
            return telemetryReady
                ? $"Read-only telemetry is active. Direct fan writes stay firmware-managed until a verified X9 provider passes. {detail}"
                : $"Firmware currently owns cooling. ThinkControl retries verified X9 providers with bounded backoff. {detail}";
        }

        return telemetryReady
            ? $"Read-only telemetry is active. No verified writable fan provider is active for this model, so firmware keeps cooling ownership. {detail}"
            : $"Cooling stays firmware-managed until a compatible fan provider is detected for this model. {detail}";
    }

    private async void Characterize_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        CharacterizeButton.IsEnabled = false;
        CharacterizationTitleText.Text = "Starting fan calibration…";
        if (!await _app.StartFanCharacterizationAsync())
        {
            CharacterizationStatusText.Text = _app.State.HardwareAccess;
            CharacterizeButton.IsEnabled = true;
        }
    }

    private async void StopCharacterization_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        StopCharacterizationButton.IsEnabled = false;
        CharacterizationStatusText.Text = "Stopping calibration and returning fan ownership to Lenovo Auto…";
        if (!await _app.StopFanCharacterizationAsync())
        {
            CharacterizationStatusText.Text = _app.State.HardwareAccess;
            StopCharacterizationButton.IsEnabled = true;
        }
    }

    private async void ManualLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required ||
            sender is not FrameworkElement { Tag: string raw } || !int.TryParse(raw, out int level))
        {
            return;
        }
        ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(level);
        if (response?.Success != true)
        {
            _app.State.HardwareAccess = response?.Error ?? "Manual fan control unavailable";
            _ = _app.HardwareClient.GetStatusAsync();
        }
    }

    private sealed record CalibrationRow(string LevelText, string RpmText, string StabilityText);
    private sealed record FanProfileChoice(string Id, string Name, bool Selectable = true);
}
