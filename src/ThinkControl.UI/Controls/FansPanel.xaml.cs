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
    private App? _app;
    private bool _syncing;
    private bool _resetAdded;
    private bool _statusSubscribed;
    private bool _customCurveLoaded;

    public FansPanel()
    {
        InitializeComponent();
        CalibrationResults.ItemsSource = _calibrationRows;
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
            _customCurveLoaded = false;
        }

        LoadCustomCurveFromSettings();
        SyncStatusSubscription();
        if (IsVisible)
            _ = app.HardwareClient.GetStatusAsync();
    }

    internal void PrepareForSnapshot(AppState state)
    {
        EnsureResetButton();
        DataContext = state;
        string profile = NormalizeProfile(state.CoolingProfile);

        _syncing = true;
        try
        {
            if (!_customCurveLoaded)
                SetCustomCurveValues(FanCurvePolicy.DefaultCustomThresholds);
            SetProfileChecks(profile);
            SetWritableControlsEnabled(state.CanFanControl);
            ProfileStatusText.Text = DisplayProfile(profile);
            CoolingDetailText.Text = state.CanFanControl
                ? $"{DisplayProfile(profile)} · {state.ControlTemperatureText} control temperature"
                : DescribeUnavailable(state.MachineType, state.HardwareAccess, state.CanSensorTelemetry || state.CanFanTelemetry);
            CustomCurveCard.Visibility = profile == "Custom" ? Visibility.Visible : Visibility.Collapsed;

            string? levelPart = state.FanStateText.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(part => part.Contains("level ", StringComparison.OrdinalIgnoreCase));
            AppliedLevelText.Text = levelPart is null
                ? "Auto"
                : levelPart.Replace("level", "step", StringComparison.OrdinalIgnoreCase);

            CharacterizeButton.IsEnabled = state.CanFanControl;
            StopCharacterizationButton.Visibility = Visibility.Collapsed;
            CharacterizationProgress.Visibility = Visibility.Collapsed;
            CharacterizationStatusText.Text = state.CanFanControl
                ? "Ready to characterize this provider"
                : "Characterization requires a verified writable fan provider";
            MarkAudibleButton.Visibility = Visibility.Collapsed;
            _calibrationRows.Clear();
        }
        finally
        {
            _syncing = false;
        }
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

        if (!IsVisible)
            return;
        ApplyStatus(response);
    }

    private void ApplyStatus(ServiceResponse? response)
    {
        TelemetrySnapshot? telemetry = response?.Success == true ? response.Telemetry : null;
        bool canControl = response?.Capabilities?.FanControl == true;
        bool hasTelemetry = response?.Capabilities?.FanTelemetry == true || response?.Capabilities?.SensorTelemetry == true;
        _syncing = true;
        try
        {
            string profile = NormalizeProfile(telemetry?.CoolingProfile);
            SetProfileChecks(profile);
            SetWritableControlsEnabled(canControl);
            CustomCurveCard.Visibility = profile == "Custom" ? Visibility.Visible : Visibility.Collapsed;

            ProfileStatusText.Text = telemetry?.CoolingSafetyOverride == true ? "Safety · firmware" : DisplayProfile(profile);
            CoolingDetailText.Text = telemetry?.CoolingStatus ?? (canControl
                ? "Choose a fan profile."
                : DescribeUnavailable(_app?.State.MachineType, telemetry?.HardwareAccess ?? _app?.State.HardwareAccess, hasTelemetry));
            AppliedLevelText.Text = telemetry?.CoolingAppliedLevel is int level ? $"Step {level}" : "Auto";

            FanCharacterizationSnapshot? characterization = telemetry?.FanCharacterization;
            bool running = characterization?.Running == true;
            CharacterizeButton.IsEnabled = canControl && !running;
            StopCharacterizationButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            CharacterizationProgress.Visibility = running || (characterization?.Levels.Count ?? 0) > 0
                ? Visibility.Visible : Visibility.Collapsed;
            CharacterizationProgress.Value = characterization?.CompletedLevels ?? 0;
            CharacterizationStatusText.Text = characterization?.Status ?? (canControl
                ? "Not characterized yet"
                : "Characterization requires a verified writable fan provider");
            MarkAudibleButton.Visibility = running && characterization?.CurrentLevel is >= 1 and <= 7
                ? Visibility.Visible : Visibility.Collapsed;
            if (characterization?.CurrentLevel is int current)
                MarkAudibleButton.Content = $"Clearly audible at step {current}";

            _calibrationRows.Clear();
            if (characterization is not null)
            {
                foreach (FanLevelCalibrationSnapshot point in characterization.Levels.OrderBy(level => level.Level))
                {
                    string rpm = point.Fans.Count == 0
                        ? "No tachometer sample"
                        : string.Join(" · ", point.Fans.Select(fan => $"{fan.Label} {fan.MedianRpm:N0} RPM"));
                    _calibrationRows.Add(new CalibrationRow(
                        $"Step {point.Level}",
                        rpm,
                        point.Stable ? "Stable" : "Variable"));
                }
                if (characterization.AudibleFromLevel is int audible)
                    CharacterizationStatusText.Text += $" · clearly audible from step {audible}";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SetWritableControlsEnabled(bool enabled)
    {
        AutoProfile.IsEnabled = enabled;
        SilentProfile.IsEnabled = enabled;
        NormalProfile.IsEnabled = enabled;
        CoolProfile.IsEnabled = enabled;
        CustomProfile.IsEnabled = enabled;
        ApplyCustomCurveButton.IsEnabled = enabled;

        if (Content is StackPanel root)
        {
            Expander? manual = root.Children
                .OfType<Expander>()
                .FirstOrDefault(item => string.Equals(item.Header?.ToString(), "Advanced manual control", StringComparison.Ordinal));
            if (manual is not null)
                manual.IsEnabled = enabled;
        }
    }

    private void SetProfileChecks(string profile)
    {
        AutoProfile.IsChecked = profile == "Lenovo Auto";
        SilentProfile.IsChecked = profile == "Quiet";
        NormalProfile.IsChecked = profile == "Balanced";
        CoolProfile.IsChecked = profile == "Max cooling";
        CustomProfile.IsChecked = profile == "Custom";
    }

    private static string NormalizeProfile(string? raw)
    {
        string profile = raw?.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "Lenovo Auto";
        return profile switch
        {
            "Silent" or "Quiet" => "Quiet",
            "Normal" or "Balanced" => "Balanced",
            "Cool" or "Max cooling" => "Max cooling",
            "Custom" => "Custom",
            "Lenovo Auto" or "Auto" => "Lenovo Auto",
            _ when profile.StartsWith("Manual level ", StringComparison.OrdinalIgnoreCase) ||
                   profile.StartsWith("Manual EC level ", StringComparison.OrdinalIgnoreCase) => profile,
            _ => "Lenovo Auto"
        };
    }

    private static string DisplayProfile(string profile) =>
        profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : profile;

    private void LoadCustomCurveFromSettings()
    {
        if (_customCurveLoaded || _app is null)
            return;
        _customCurveLoaded = true;
        SetCustomCurveValues(_app.UserSettings.Current.CustomFanThresholds ?? FanCurvePolicy.DefaultCustomThresholds);
    }

    private void SetCustomCurveValues(IReadOnlyList<double> values)
    {
        IReadOnlyList<double> curve = FanCurvePolicy.TryValidateCustomThresholds(values, out double[] valid, out _)
            ? valid
            : FanCurvePolicy.DefaultCustomThresholds;

        _syncing = true;
        try
        {
            Slider[] sliders = [Custom2Slider, Custom3Slider, Custom4Slider, Custom5Slider, Custom6Slider, Custom7Slider];
            for (int i = 0; i < sliders.Length; i++)
                sliders[i].Value = curve[i];
            UpdateCustomCurveLabels();
        }
        finally
        {
            _syncing = false;
        }
    }

    private double[] CurrentCustomCurve() =>
        [Custom2Slider.Value, Custom3Slider.Value, Custom4Slider.Value, Custom5Slider.Value, Custom6Slider.Value, Custom7Slider.Value];

    private void CustomCurveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized)
            return;
        UpdateCustomCurveLabels();
    }

    private void UpdateCustomCurveLabels()
    {
        if (Custom2Value is null)
            return;
        Custom2Value.Text = $"{Custom2Slider.Value:0} °C";
        Custom3Value.Text = $"{Custom3Slider.Value:0} °C";
        Custom4Value.Text = $"{Custom4Slider.Value:0} °C";
        Custom5Value.Text = $"{Custom5Slider.Value:0} °C";
        Custom6Value.Text = $"{Custom6Slider.Value:0} °C";
        Custom7Value.Text = $"{Custom7Slider.Value:0} °C";
    }

    private async Task ApplyCustomCurveAsync()
    {
        if (_app is null)
            return;

        double[] curve = CurrentCustomCurve();
        if (!FanCurvePolicy.TryValidateCustomThresholds(curve, out _, out string? validation))
        {
            CoolingDetailText.Text = validation ?? "Custom curve is invalid.";
            return;
        }

        ApplyCustomCurveButton.IsEnabled = false;
        try
        {
            bool applied = await _app.SetCustomCoolingCurveAsync(curve);
            if (!applied)
                CoolingDetailText.Text = _app.State.HardwareAccess;
        }
        finally
        {
            ApplyCustomCurveButton.IsEnabled = _app.State.CanFanControl;
        }
    }

    private async void ApplyCustomCurve_Click(object sender, RoutedEventArgs e) =>
        await ApplyCustomCurveAsync();

    private void ResetCustomCurve_Click(object sender, RoutedEventArgs e)
    {
        SetCustomCurveValues(FanCurvePolicy.DefaultCustomThresholds);
        CoolingDetailText.Text = "Balanced starting curve loaded · press Apply to activate it as Custom.";
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
            ToolTip = "Return cooling ownership to Lenovo Auto"
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
                : $"Firmware currently owns cooling. ThinkControl will retry the verified X9 provider with bounded backoff. {detail}";
        }

        return telemetryReady
            ? $"Read-only telemetry is active. No verified writable fan provider is active for this model, so firmware keeps cooling ownership. {detail}"
            : $"Cooling stays firmware-managed until a compatible fan provider is detected for this model. {detail}";
    }

    private async void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app is null || sender is not FrameworkElement { Tag: string profile })
            return;

        if (profile == "Custom")
        {
            CustomCurveCard.Visibility = Visibility.Visible;
            await ApplyCustomCurveAsync();
            return;
        }

        CustomCurveCard.Visibility = Visibility.Collapsed;
        if (!await _app.SetCoolingProfileAsync(profile))
            _ = _app.HardwareClient.GetStatusAsync();
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
}
