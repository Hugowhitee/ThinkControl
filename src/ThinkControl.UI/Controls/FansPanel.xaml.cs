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

    private bool UsesFirmwarePolicy =>
        string.Equals(_fanControlKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal);

    private bool HasDirectFanWriter =>
        string.Equals(_fanControlKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal) ||
        string.Equals(_fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);

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
            _fanControlKind = ResolveFanControlKind(null, app.State.CanFanControl);

        // The selector represents applied runtime state, not merely the preference
        // stored for the next restore attempt. Showing saved Quiet while telemetry is
        // still Auto made startup look successful even when Lenovo had not applied it.
        SyncProfileSelector(app.State.CoolingProfile, RuntimeProfileIdForDisplay(app.State.CoolingProfile));
        ApplyProviderCopy(app.State.CanFanControl, _fanControlKind);
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
            _fanControlKind = ResolveFanControlKind(null, state.CanFanControl);
        SyncProfileSelector(state.CoolingProfile, state.CoolingProfile);
        ApplyProviderCopy(state.CanFanControl, _fanControlKind);
        CoolingDetailText.Text = state.CanFanControl
            ? UsesFirmwarePolicy
                ? $"{DisplayProfile(state.CoolingProfile)} · Lenovo firmware cooling policy"
                : $"{DisplayProfile(state.CoolingProfile)} · {state.ControlTemperatureText} control temperature"
            : DescribeUnavailable(state.HardwareAccess, state.CanSensorTelemetry || state.CanFanTelemetry);
        AppliedLevelText.Text = state.CanFanControl
            ? UsesFirmwarePolicy ? "Lenovo firmware" : state.FanStateText
            : "Unavailable";

        // Snapshot fixtures do not have a live service capability object. Model the
        // current discrete-provider fixture as calibration-capable without teaching
        // the production UI anything about a particular OEM or machine type.
        bool canCalibrate = state.CanFanControl && state.CanFanTelemetry &&
                            string.Equals(_fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
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
        _fanControlKind = ResolveFanControlKind(response?.Capabilities?.FanControlKind, canControl);

        string profileName = telemetry?.CoolingProfile ?? "Lenovo Auto";
        string profileId = telemetry?.CoolingProfileId ?? (profileName.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ? "Lenovo Auto" : profileName);
        SyncProfileSelector(profileName, profileId);
        ApplyProviderCopy(canControl, _fanControlKind);

        CoolingDetailText.Text = telemetry?.CoolingStatus ?? (canControl
            ? UsesFirmwarePolicy
                ? "Choose Auto, Quiet, Balanced or Max cooling. Lenovo firmware owns the fan loop."
                : "Choose a fan profile or open the curve editor."
            : DescribeUnavailable(telemetry?.HardwareAccess ?? _app?.State.HardwareAccess, hasTelemetry));

        if (UsesFirmwarePolicy && !profileName.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) &&
            !profileName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            AppliedLevelText.Text = "Lenovo firmware";
        }
        else if (telemetry?.CoolingAppliedPercent is int percent)
        {
            if (_fanControlKind == FanControlKinds.OemTargetRpm && telemetry.CoolingAppliedLevel is null)
                AppliedLevelText.Text = $"{percent}% OEM target";
            else
                AppliedLevelText.Text = telemetry.CoolingAppliedLevel is int step
                    ? $"{percent}% · State {step}"
                    : $"{percent}%";
        }
        else if (telemetry?.CoolingAppliedLevel is int legacyLevel)
        {
            AppliedLevelText.Text = $"State {legacyLevel}";
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
        bool running = calibration.Running;
        bool ready = calibration.Ready;
        bool attention = calibration.Required || running;
        bool showCalibrationTask = calibration.Relevant && attention;
        CalibrationCard.Visibility = showCalibrationTask ? Visibility.Visible : Visibility.Collapsed;

        if (!calibration.Relevant)
        {
            bool directWriter = canControl && HasDirectFanWriter;
            ProfileComboBox.IsEnabled = canControl;
            EditCurvesButton.IsEnabled = directWriter;
            ProfileCard.Opacity = canControl ? 1 : 0.42;
            ManualControlExpander.IsEnabled = directWriter;
            ManualControlExpander.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;
            ManualControlExpander.Opacity = 1;
            ManualPercentSlider.IsEnabled = directWriter;
            ManualPercentApplyButton.IsEnabled = directWriter;
            RawEcStepsExpander.IsEnabled = directWriter;
            return;
        }

        CalibrationCard.SetResourceReference(Border.BorderBrushProperty, "Tc.Accent");
        CalibrationCard.BorderThickness = new Thickness(1);

        CharacterizationTitleText.Text = running
            ? "Fan calibration in progress"
            : "Fan calibration required";
        CalibrationDescriptionText.Text = running
            ? "ThinkControl temporarily owns the active provider's calibration states while each state settles and real tachometer samples are measured. Other fan controls are locked until calibration finishes or is stopped; firmware Auto is restored automatically."
            : "The active fan provider requires a measured output mapping before it can safely translate percentage profiles or temporary percentage tests. Firmware Auto remains the safe default until calibration completes.";
        CharacterizationStatusText.Text = calibration.Status;

        CharacterizeButton.Content = "Calibrate now";
        CharacterizeButton.IsEnabled = canControl && !running;
        CharacterizeButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        StopCharacterizationButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        StopCharacterizationButton.IsEnabled = running;
        CharacterizationProgress.Maximum = Math.Max(1, calibration.TotalLevels);
        CharacterizationProgress.Value = Math.Clamp(calibration.CompletedLevels, 0, calibration.TotalLevels);
        CharacterizationProgress.Visibility = running || calibration.CompletedLevels > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool semanticControlsEnabled = canControl && ready;
        ProfileComboBox.IsEnabled = semanticControlsEnabled;
        EditCurvesButton.IsEnabled = semanticControlsEnabled;
        ProfileCard.Opacity = semanticControlsEnabled ? 1 : 0.42;
        ManualControlExpander.IsEnabled = semanticControlsEnabled;
        ManualControlExpander.Visibility = semanticControlsEnabled ? Visibility.Visible : Visibility.Collapsed;
        ManualControlExpander.Opacity = 1;
        ManualPercentSlider.IsEnabled = semanticControlsEnabled;
        ManualPercentApplyButton.IsEnabled = semanticControlsEnabled;
        RawEcStepsExpander.IsEnabled = semanticControlsEnabled;

        if (!semanticControlsEnabled)
        {
            CoolingDetailText.Text = running
                ? "Calibration currently owns fan output. Profile controls return after the run finishes or is stopped."
                : "Firmware Auto remains active until the provider's calibration requirement is satisfied.";
        }
    }

    private void BuildCalibrationRows(FanCharacterizationSnapshot? characterization)
    {
        _calibrationRows.Clear();
        if (characterization is null)
            return;

        FanLevelCalibrationSnapshot? maximum = characterization.Levels.MaxBy(static level => level.Level);
        double? maximumRpm = maximum?.Fans.Count > 0 ? maximum.Fans.Average(fan => fan.MedianRpm) : null;

        foreach (FanLevelCalibrationSnapshot point in characterization.Levels.OrderBy(level => level.Level))
        {
            string label = $"Output state {point.Level}";
            string rpm;
            if (point.Fans.Count == 0)
            {
                rpm = "No tachometer sample";
            }
            else
            {
                double average = point.Fans.Average(fan => fan.MedianRpm);
                string values = string.Join(" · ", point.Fans.Select(fan => $"{fan.Label} {fan.MedianRpm:N0} RPM"));
                if (maximumRpm is > 0 && maximum is not null)
                {
                    int relative = point.Level == maximum.Level ? 100 : (int)Math.Round(Math.Clamp(average / maximumRpm.Value * 100.0, 0, 99));
                    rpm = $"{values} · ~{relative}% of calibrated maximum state";
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
        if (UsesFirmwarePolicy)
        {
            ActiveCurvePreview.Visibility = Visibility.Collapsed;
            _activeCurveGraph.SetLiveState(null, null, null);
            return;
        }

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
        if (IsManualProfile(manualState) && !UsesFirmwarePolicy)
            desired.Add(new FanProfileChoice(manualState!.Trim(), manualState.Trim(), Selectable: false));
        desired.Add(new FanProfileChoice("Lenovo Auto", "Auto"));
        IEnumerable<FanCurveDefinition> profiles = _app.FanProfiles.GetProfiles();
        if (UsesFirmwarePolicy)
            profiles = profiles.Where(profile => _app.FanProfiles.IsBuiltIn(profile.Id));
        desired.AddRange(profiles.Select(profile => new FanProfileChoice(profile.Id, profile.Name)));

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

    private string RuntimeProfileIdForDisplay(string? profileName)
    {
        if (IsManualProfile(profileName))
            return profileName!.Trim();

        string runtime = profileName?.Trim() ?? string.Empty;
        if (runtime.Length == 0 || runtime.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
            runtime.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return "Lenovo Auto";
        }
        if (runtime.Equals("Quiet", StringComparison.OrdinalIgnoreCase) || runtime.Equals("Silent", StringComparison.OrdinalIgnoreCase))
            return FanCurveDefaults.QuietId;
        if (runtime.Equals("Balanced", StringComparison.OrdinalIgnoreCase) || runtime.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            return FanCurveDefaults.BalancedId;
        if (runtime.Equals("Max cooling", StringComparison.OrdinalIgnoreCase) || runtime.Equals("Cool", StringComparison.OrdinalIgnoreCase))
            return FanCurveDefaults.MaxCoolingId;

        // A custom profile has a display name in AppState. Reuse the persisted id only
        // when that id resolves to the same runtime name; never let an unrelated saved
        // preference paint the selector as applied.
        if (_app?.FanProfiles.Find(_app.UserSettings.Current.CoolingProfile) is FanCurveDefinition persisted &&
            string.Equals(persisted.Name, runtime, StringComparison.OrdinalIgnoreCase))
        {
            return persisted.Id;
        }

        return runtime;
    }

    private static string DisplayProfile(string? raw) => raw?.Trim() switch
    {
        null or "" or "Lenovo Auto" or "Auto" => "Auto",
        "Silent" => "Quiet",
        "Normal" => "Balanced",
        "Cool" or "Max cooling" => "Max cooling",
        string value => value
    };

    private void ProfileComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required)
            return;
        SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
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
                SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
                return;
            }

            SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
        }
        finally
        {
            ProfileComboBox.IsEnabled = _app.State.CanFanControl && !_app.FanCalibrationState.Required;
        }
    }

    private void EditCurves_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required || !HasDirectFanWriter)
            return;
        ProfileComboBox.IsDropDownOpen = false;
        var editor = new FanCurveEditorWindow(_app) { Owner = Window.GetWindow(this) };
        editor.ShowDialog();
        SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
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
        if (_app is null || sender is not Button button || _app.FanCalibrationState.Required || !HasDirectFanWriter)
            return;
        int percent = (int)Math.Round(ManualPercentSlider.Value);
        button.IsEnabled = false;
        try
        {
            if (!await _app.SetManualFanPercentAsync(percent))
                CoolingDetailText.Text = _app.State.HardwareAccess;
        }
        finally { button.IsEnabled = HasDirectFanWriter && !_app.FanCalibrationState.Required; }
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

    private void ApplyProviderCopy(bool canControl, string fanControlKind)
    {
        bool firmwarePolicy = canControl && string.Equals(fanControlKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal);
        bool oemTargetRpm = canControl && string.Equals(fanControlKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal);
        bool discreteEcWriter = canControl && string.Equals(fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        bool directWriter = oemTargetRpm || discreteEcWriter;

        FansIntroText.Text = firmwarePolicy
            ? "Quiet, Balanced and Max cooling use Lenovo's verified firmware thermal-policy path on this X9. Windows power preference remains a separate OS setting; while a cooling profile is selected, its Lenovo policy override takes precedence until Auto is restored."
            : "Fan behavior is independent from Windows performance mode. ThinkControl uses only capabilities exposed by the active verified fan provider; firmware Auto remains the fail-safe whenever no writable provider is active.";

        FanMappingDetailText.Text = !canControl
            ? "Firmware Auto keeps fan ownership. Native telemetry can still be shown when available, but profiles and temporary tests stay unavailable until a supported cooling backend is active."
            : firmwarePolicy
                ? "Built-in profiles stay functional through Lenovo firmware: Quiet → Quiet policy, Balanced → Balanced policy, and Max cooling → Performance cooling policy. The alpha.38 fixed target-RPM writer remains read-only because its physical fan behavior failed validation; custom curves and manual percentages stay unavailable until a direct writer passes that gate."
                : oemTargetRpm
                    ? "Built-in and custom curves send continuous 0–100% targets through the active provider's target-RPM contract. Each fan is mapped independently across the minimum and maximum RPM range reported by that provider."
                    : discreteEcWriter
                        ? "This provider exposes discrete output states. Percentage profiles use its measured calibration rather than pretending those states are a continuous PWM scale."
                        : "Profiles use the active verified cooling backend. Direct percentage controls appear only when the provider exposes a physically accepted direct writer.";
        FanProviderDetailText.ToolTip = null;

        // Raw EC diagnostics exist only for a provider that explicitly advertises
        // the discrete-EC semantic contract. They are never a generic laptop option.
        RawEcStepsExpander.Visibility = discreteEcWriter ? Visibility.Visible : Visibility.Collapsed;
        ManualControlExpander.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;
        ManualControlDescriptionText.Text = firmwarePolicy
            ? "Manual percentage tests are intentionally unavailable on the firmware-policy backend. Use the built-in profiles above; they keep Lenovo's own smooth fan loop in control."
            : oemTargetRpm
                ? "Temporary 30-second test. 0% requests the provider-reported minimum running target and 100% its reported maximum target RPM; the previous profile is restored automatically. Firmware Auto remains a separate ownership state."
                : discreteEcWriter
                    ? "Temporary 30-second test. The percentage target maps onto the provider's calibrated discrete states; raw EC diagnostics remain available below for this provider only. The previous profile is restored automatically."
                    : "Temporary tests use only the active provider's verified output range and restore the previous profile automatically. Provider-specific raw diagnostics appear only when that exact semantic contract is exposed.";
        ManualControlExpander.IsEnabled = directWriter;
        EditCurvesButton.IsEnabled = directWriter && !_app?.FanCalibrationState.Required == true;
    }

    private static string ResolveFanControlKind(string? explicitKind, bool canControl)
    {
        if (!canControl)
            return FanControlKinds.None;
        if (string.Equals(explicitKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal) ||
            string.Equals(explicitKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal) ||
            string.Equals(explicitKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal))
        {
            return explicitKind!;
        }
        return FanControlKinds.None;
    }

    private static string DescribeUnavailable(string? hardwareAccess, bool telemetryReady)
    {
        string detail = string.IsNullOrWhiteSpace(hardwareAccess) ? "provider status unavailable" : hardwareAccess;
        return telemetryReady
            ? $"Read-only telemetry is active. No verified cooling-control backend is active, so firmware keeps cooling ownership. {detail}"
            : $"Cooling stays firmware-managed until a compatible cooling provider is detected. {detail}";
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
        CharacterizationStatusText.Text = "Stopping calibration and returning fan ownership to firmware Auto…";
        if (!await _app.StopFanCharacterizationAsync())
        {
            CharacterizationStatusText.Text = _app.State.HardwareAccess;
            StopCharacterizationButton.IsEnabled = true;
        }
    }

    private async void ManualLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required || !HasDirectFanWriter ||
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
