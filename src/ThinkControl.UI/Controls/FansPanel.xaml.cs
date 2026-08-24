using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class FansPanel : UserControl
{
    private readonly ObservableCollection<CalibrationRow> _calibrationRows = [];
    private App? _app;
    private bool _syncing;
    private bool _resetAdded;

    public FansPanel()
    {
        InitializeComponent();
        CalibrationResults.ItemsSource = _calibrationRows;
    }

    internal void Initialize(App app)
    {
        EnsureResetButton();
        if (ReferenceEquals(_app, app))
        {
            _ = app.RefreshStatusAsync();
            return;
        }
        if (_app is not null)
            _app.HardwareClient.StatusObserved -= HardwareClient_StatusObserved;
        _app = app;
        DataContext = app.State;
        app.HardwareClient.StatusObserved += HardwareClient_StatusObserved;
        _ = app.RefreshStatusAsync();
    }

    internal void PrepareForSnapshot(AppState state)
    {
        EnsureResetButton();
        DataContext = state;
        string profile = NormalizeProfile(state.FanStateText);

        _syncing = true;
        try
        {
            SetProfileChecks(profile);
            SilentProfile.IsEnabled = NormalProfile.IsEnabled = CoolProfile.IsEnabled = state.CanFanControl;
            ProfileStatusText.Text = DisplayProfile(profile);
            CoolingDetailText.Text = state.CanFanControl
                ? $"{DisplayProfile(profile)} cooling · {state.ControlTemperatureText} control temperature"
                : DescribeUnavailable(state.MachineType, state.HardwareAccess, state.CanSensorTelemetry || state.CanFanTelemetry);

            string? levelPart = state.FanStateText.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(part => part.StartsWith("level ", StringComparison.OrdinalIgnoreCase));
            AppliedLevelText.Text = levelPart is null
                ? "Auto"
                : char.ToUpperInvariant(levelPart[0]) + levelPart[1..];

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

    private void HardwareClient_StatusObserved(object? sender, ServiceResponse? response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ApplyStatus(response));
            return;
        }
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

            SilentProfile.IsEnabled = NormalProfile.IsEnabled = CoolProfile.IsEnabled = canControl;
            ProfileStatusText.Text = telemetry?.CoolingSafetyOverride == true ? "Safety · firmware" : DisplayProfile(profile);
            CoolingDetailText.Text = telemetry?.CoolingStatus ?? (canControl
                ? "Choose a cooling profile."
                : DescribeUnavailable(_app?.State.MachineType, telemetry?.HardwareAccess ?? _app?.State.HardwareAccess, hasTelemetry));
            AppliedLevelText.Text = telemetry?.CoolingAppliedLevel is int level ? $"Level {level}" : "Auto";

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
                MarkAudibleButton.Content = $"Clearly audible at level {current}";

            _calibrationRows.Clear();
            if (characterization is not null)
            {
                foreach (FanLevelCalibrationSnapshot point in characterization.Levels.OrderBy(level => level.Level))
                {
                    string rpm = point.Fans.Count == 0
                        ? "No tachometer sample"
                        : string.Join(" · ", point.Fans.Select(fan => $"{fan.Label} {fan.MedianRpm:N0} RPM"));
                    _calibrationRows.Add(new CalibrationRow(
                        $"Level {point.Level}",
                        rpm,
                        point.Stable ? "Stable" : "Variable"));
                }
                if (characterization.AudibleFromLevel is int audible)
                    CharacterizationStatusText.Text += $" · clearly audible from level {audible}";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SetProfileChecks(string profile)
    {
        AutoProfile.IsChecked = profile == "Lenovo Auto";
        SilentProfile.IsChecked = profile == "Silent";
        NormalProfile.IsChecked = profile == "Normal";
        CoolProfile.IsChecked = profile == "Cool";
    }

    private static string NormalizeProfile(string? raw)
    {
        string profile = raw?.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "Lenovo Auto";
        return profile is "Silent" or "Normal" or "Cool" ? profile : "Lenovo Auto";
    }

    private static string DisplayProfile(string profile) =>
        profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : profile;

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
            ToolTip = "Return cooling ownership to the provider/firmware default"
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
        try
        {
            _ = await _app.ResetFanDefaultsAsync();
            await _app.RefreshStatusAsync();
        }
        finally
        {
            button.IsEnabled = true;
        }
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
        if (!await _app.SetCoolingProfileAsync(profile))
            await _app.RefreshStatusAsync();
    }

    private async void Characterize_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        await _app.StartFanCharacterizationAsync();
        await _app.RefreshStatusAsync();
    }

    private async void StopCharacterization_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        await _app.StopFanCharacterizationAsync();
        await _app.RefreshStatusAsync();
    }

    private async void MarkAudible_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        await _app.MarkCurrentFanLevelAudibleAsync();
        await _app.RefreshStatusAsync();
    }

    private async void ManualLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not FrameworkElement { Tag: string raw } || !int.TryParse(raw, out int level))
            return;
        ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(level);
        if (response?.Success != true)
            _app.State.HardwareAccess = response?.Error ?? "Manual fan control unavailable";
        await _app.RefreshStatusAsync();
    }

    private sealed record CalibrationRow(string LevelText, string RpmText, string StabilityText);
}
