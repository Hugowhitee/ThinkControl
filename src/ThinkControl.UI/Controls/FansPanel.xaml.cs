using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class FansPanel : UserControl
{
    private readonly ObservableCollection<CalibrationRow> _calibrationRows = [];
    private App? _app;
    private bool _resetAdded;
    private bool _statusSubscribed;
    private string _currentProfileId = "Lenovo Auto";
    private Popup? _profilePopup;

    public FansPanel()
    {
        InitializeComponent();
        CalibrationResults.ItemsSource = _calibrationRows;
        Loaded += (_, _) => SyncStatusSubscription();
        Unloaded += (_, _) =>
        {
            CloseProfilePopup();
            UnsubscribeStatus();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
                CloseProfilePopup();
            SyncStatusSubscription();
        };
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

        SyncProfileButton(app.State.CoolingProfile, app.UserSettings.Current.CoolingProfile);
        SyncStatusSubscription();
        if (IsVisible)
            _ = app.HardwareClient.GetStatusAsync();
    }

    internal void PrepareForSnapshot(AppState state)
    {
        EnsureResetButton();
        DataContext = state;
        SyncProfileButton(state.CoolingProfile, state.CoolingProfile);
        ProfileMenuButton.IsEnabled = state.CanFanControl;
        CoolingDetailText.Text = state.CanFanControl
            ? $"{DisplayProfile(state.CoolingProfile)} · {state.ControlTemperatureText} control temperature"
            : DescribeUnavailable(state.MachineType, state.HardwareAccess, state.CanSensorTelemetry || state.CanFanTelemetry);
        AppliedLevelText.Text = state.CanFanControl ? "Auto" : "Unavailable";
        CharacterizeButton.IsEnabled = state.CanFanControl;
        StopCharacterizationButton.Visibility = Visibility.Collapsed;
        CharacterizationProgress.Visibility = Visibility.Collapsed;
        CharacterizationStatusText.Text = state.CanFanControl
            ? "Ready to calibrate the verified X9 fan states"
            : "Calibration requires a verified writable fan provider";
        MarkAudibleButton.Visibility = Visibility.Collapsed;
        ManualPercentSlider.IsEnabled = state.CanFanControl;
        _calibrationRows.Clear();
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
        SyncProfileButton(profileName, profileId);
        ProfileMenuButton.IsEnabled = canControl;

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

    private void SyncProfileButton(string? profileName, string? profileId)
    {
        _currentProfileId = string.IsNullOrWhiteSpace(profileId) ? "Lenovo Auto" : profileId;
        string name = DisplayProfile(profileName);
        ProfileMenuButton.Content = name + "  ⌄";
    }

    private static string DisplayProfile(string? raw) => raw?.Trim() switch
    {
        null or "" or "Lenovo Auto" or "Auto" => "Auto",
        "Silent" => "Quiet",
        "Normal" => "Balanced",
        "Cool" => "Max cooling",
        string value => value
    };

    private void ProfileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button || !button.IsEnabled)
            return;

        if (_profilePopup?.IsOpen == true)
        {
            CloseProfilePopup();
            return;
        }

        var list = new StackPanel();
        var heading = new TextBlock
        {
            Text = "Fan profile",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(9, 5, 9, 7)
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        list.Children.Add(heading);
        list.Children.Add(CreateProfilePickerItem("Auto", "Lenovo firmware", "Lenovo Auto"));

        var separator = new Border { Height = 1, Margin = new Thickness(7, 4, 7, 5) };
        separator.SetResourceReference(Border.BackgroundProperty, "Tc.Border");
        list.Children.Add(separator);

        foreach (FanCurveDefinition profile in _app.FanProfiles.GetProfiles())
        {
            string detail = _app.FanProfiles.IsBuiltIn(profile.Id) ? "Built-in curve" : "Custom curve";
            list.Children.Add(CreateProfilePickerItem(profile.Name, detail, profile.Id));
        }

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 330,
            Content = list
        };
        var surface = new Border
        {
            MinWidth = Math.Max(220, button.ActualWidth + 55),
            Padding = new Thickness(5),
            Margin = new Thickness(0, 5, 0, 0),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = scroller,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.28 }
        };
        surface.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");
        surface.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");

        var popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            Child = surface
        };
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_profilePopup, popup))
                _profilePopup = null;
        };
        _profilePopup = popup;
        popup.IsOpen = true;
    }

    private Button CreateProfilePickerItem(string title, string detail, string id)
    {
        bool selected = string.Equals(_currentProfileId, id, StringComparison.OrdinalIgnoreCase) ||
                        id == "Lenovo Auto" && string.Equals(_currentProfileId, "Auto", StringComparison.OrdinalIgnoreCase);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var marker = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = selected ? Visibility.Visible : Visibility.Hidden
        };
        marker.SetResourceReference(Border.BackgroundProperty, "Tc.Accent");
        grid.Children.Add(marker);

        var copy = new StackPanel { Margin = new Thickness(5, 0, 8, 0) };
        copy.Children.Add(new TextBlock { Text = title, FontSize = 11 });
        var sub = new TextBlock { Text = detail, FontSize = 9.2, Margin = new Thickness(0, 1, 0, 0) };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        copy.Children.Add(sub);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        var item = new Button
        {
            Content = grid,
            Style = TryFindResource("TcButton") as Style,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(5, 6, 5, 6),
            Margin = new Thickness(0, 1, 0, 1),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Tag = id
        };
        if (selected)
            item.SetResourceReference(Button.BackgroundProperty, "Tc.SurfaceHover");
        item.Click += async (_, _) => await ApplyProfileFromPickerAsync(id);
        return item;
    }

    private async Task ApplyProfileFromPickerAsync(string id)
    {
        if (_app is null)
            return;

        CloseProfilePopup();
        ProfileMenuButton.IsEnabled = false;
        try
        {
            if (!await _app.SetCoolingProfileAsync(id))
            {
                CoolingDetailText.Text = _app.State.HardwareAccess;
                return;
            }

            // AppState is the immediate UI source of truth; the service readback
            // will confirm hardware state on the next status snapshot.
            SyncProfileButton(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile);
        }
        finally
        {
            ProfileMenuButton.IsEnabled = _app.State.CanFanControl;
        }
    }

    private void CloseProfilePopup()
    {
        if (_profilePopup is not null)
            _profilePopup.IsOpen = false;
        _profilePopup = null;
    }

    private async void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Compatibility handler for old XAML/snapshot revisions. The live fan picker
        // no longer uses stock WPF MenuItem controls.
        if (_app is null || sender is not MenuItem { Tag: string id })
            return;
        await ApplyProfileFromPickerAsync(id);
    }

    private void EditCurves_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        CloseProfilePopup();
        var editor = new FanCurveEditorWindow(_app) { Owner = Window.GetWindow(this) };
        editor.ShowDialog();
        SyncProfileButton(_app.State.CoolingProfile, _app.UserSettings.Current.CoolingProfile);
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

    // Kept as no-op compatibility handlers for stale alpha.16 visual snapshots that
    // may instantiate the hidden migration controls from older XAML revisions.
    private void CustomCurveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }
    private void ApplyCustomCurve_Click(object sender, RoutedEventArgs e) { }
    private void ResetCustomCurve_Click(object sender, RoutedEventArgs e) { }
    private void Profile_Click(object sender, RoutedEventArgs e) { }

    private sealed record CalibrationRow(string LevelText, string RpmText, string StabilityText);
}
