using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Controls;

public partial class FansPanel : UserControl
{
    private readonly ObservableCollection<CalibrationRow> _calibrationRows = [];
    private App? _app;
    private bool _syncing;

    public FansPanel()
    {
        InitializeComponent();
        CalibrationResults.ItemsSource = _calibrationRows;
    }

    internal void Initialize(App app)
    {
        if (ReferenceEquals(_app, app))
            return;
        if (_app is not null)
            _app.HardwareClient.StatusObserved -= HardwareClient_StatusObserved;
        _app = app;
        DataContext = app.State;
        app.HardwareClient.StatusObserved += HardwareClient_StatusObserved;
        _ = app.RefreshStatusAsync();
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
        _syncing = true;
        try
        {
            string profile = telemetry?.CoolingProfile ?? "Lenovo Auto";
            AutoProfile.IsChecked = profile.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase);
            SilentProfile.IsChecked = profile.Equals("Silent", StringComparison.OrdinalIgnoreCase);
            NormalProfile.IsChecked = profile.Equals("Normal", StringComparison.OrdinalIgnoreCase);
            CoolProfile.IsChecked = profile.Equals("Cool", StringComparison.OrdinalIgnoreCase);

            SilentProfile.IsEnabled = NormalProfile.IsEnabled = CoolProfile.IsEnabled = canControl;
            ProfileStatusText.Text = telemetry?.CoolingSafetyOverride == true ? "Safety · Lenovo firmware" : profile;
            CoolingDetailText.Text = telemetry?.CoolingStatus ?? (canControl
                ? "Choose a cooling profile."
                : "Custom cooling is unavailable until the verified hardware provider is ready.");
            AppliedLevelText.Text = telemetry?.CoolingAppliedLevel is int level ? $"Level {level}" : "Auto";

            FanCharacterizationSnapshot? characterization = telemetry?.FanCharacterization;
            bool running = characterization?.Running == true;
            CharacterizeButton.IsEnabled = canControl && !running;
            StopCharacterizationButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            CharacterizationProgress.Visibility = running || (characterization?.Levels.Count ?? 0) > 0
                ? Visibility.Visible : Visibility.Collapsed;
            CharacterizationProgress.Value = characterization?.CompletedLevels ?? 0;
            CharacterizationStatusText.Text = characterization?.Status ?? "Not characterized yet";
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
