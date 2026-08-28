using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Controls;

public partial class FansPanel
{
    private const int ManualFanTestDurationSeconds = 30;

    private readonly DispatcherTimer _manualFanTestTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private Button? _manualFanApplyButton;
    private Button? _manualFanEndButton;
    private TextBlock? _manualFanTestStatus;
    private string? _manualFanRestoreProfile;
    private DateTimeOffset _manualFanTestEndsAt;
    private bool _manualFanTestActive;
    private bool _manualFanTestEnding;
    private bool _manualFanSafetyConfigured;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        ConfigureManualFanTestSafety();
    }

    private void ConfigureManualFanTestSafety()
    {
        if (_manualFanSafetyConfigured)
            return;
        _manualFanSafetyConfigured = true;

        if (ManualPercentSlider.Parent is Grid row)
        {
            Button? apply = row.Children.OfType<Button>()
                .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Apply", StringComparison.Ordinal));
            if (apply is not null)
            {
                apply.Click -= ManualPercentApply_Click;
                apply.Click += ManualPercentTestApply_Click;
                _manualFanApplyButton = apply;

                row.Children.Remove(apply);
                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                apply.Margin = new Thickness(0);
                actions.Children.Add(apply);

                _manualFanEndButton = new Button
                {
                    Content = "End test",
                    Style = TryFindResource("TcButton") as Style,
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(7, 0, 0, 0),
                    Visibility = Visibility.Collapsed,
                    ToolTip = null
                };
                _manualFanEndButton.Click += ManualFanEndTest_Click;
                actions.Children.Add(_manualFanEndButton);
                Grid.SetColumn(actions, 1);
                row.Children.Add(actions);
            }

            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _manualFanTestStatus = new TextBlock
            {
                Margin = new Thickness(1, 6, 0, 0),
                FontSize = TypographyScale.Caption,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            _manualFanTestStatus.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            Grid.SetRow(_manualFanTestStatus, 1);
            Grid.SetColumnSpan(_manualFanTestStatus, 2);
            row.Children.Add(_manualFanTestStatus);
        }

        foreach (Button button in FindDescendants<Button>(RawEcStepsExpander))
        {
            if (button.Tag is not string raw || !int.TryParse(raw, out int level) || level is < 1 or > 7)
                continue;
            button.Click -= ManualLevel_Click;
            button.Click += ManualLevelTest_Click;
        }

        _manualFanTestTimer.Tick += ManualFanTestTimer_Tick;
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is false && _manualFanTestActive && !_manualFanTestEnding)
                _ = EndManualFanTestAsync("Fan page closed");
        };
    }

    private async void ManualPercentTestApply_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;

        int percent = (int)Math.Round(ManualPercentSlider.Value);
        if (_manualFanApplyButton is not null)
            _manualFanApplyButton.IsEnabled = false;
        try
        {
            await BeginOrRefreshManualFanTestAsync(
                () => _app.SetManualFanPercentAsync(percent),
                $"{percent}% target");
        }
        finally
        {
            if (_manualFanApplyButton is not null)
                _manualFanApplyButton.IsEnabled = _app.State.CanFanControl;
        }
    }

    private async void ManualLevelTest_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button { Tag: string raw } || !int.TryParse(raw, out int level))
            return;

        buttonBusy(sender, true);
        try
        {
            await BeginOrRefreshManualFanTestAsync(async () =>
            {
                ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(level);
                if (response?.Success == true)
                    return true;
                _app.State.HardwareAccess = response?.Error ?? "Manual fan control unavailable";
                return false;
            }, $"EC step {level}");
        }
        finally
        {
            buttonBusy(sender, false);
        }

        static void buttonBusy(object source, bool busy)
        {
            if (source is Button button)
                button.IsEnabled = !busy;
        }
    }

    private async Task BeginOrRefreshManualFanTestAsync(Func<Task<bool>> apply, string testLabel)
    {
        if (_app is null || _manualFanTestEnding)
            return;

        string restoreProfile = _manualFanTestActive
            ? _manualFanRestoreProfile ?? _app.UserSettings.Current.CoolingProfile
            : _app.UserSettings.Current.CoolingProfile;

        bool applied = await apply();
        if (!applied)
        {
            UpdateManualFanTestUi();
            return;
        }

        if (!_manualFanTestActive)
            _manualFanRestoreProfile = restoreProfile;

        _manualFanTestActive = true;
        _manualFanTestEndsAt = DateTimeOffset.UtcNow.AddSeconds(ManualFanTestDurationSeconds);
        _manualFanTestTimer.Start();
        UpdateManualFanTestUi(testLabel);
    }

    private async void ManualFanEndTest_Click(object sender, RoutedEventArgs e) =>
        await EndManualFanTestAsync("Ended by user");

    private async void ManualFanTestTimer_Tick(object? sender, EventArgs e)
    {
        if (!_manualFanTestActive)
        {
            _manualFanTestTimer.Stop();
            return;
        }

        if (DateTimeOffset.UtcNow >= _manualFanTestEndsAt)
        {
            await EndManualFanTestAsync("Time limit reached");
            return;
        }

        UpdateManualFanTestUi();
    }

    private async Task EndManualFanTestAsync(string reason)
    {
        if (_app is null || !_manualFanTestActive || _manualFanTestEnding)
            return;

        _manualFanTestEnding = true;
        _manualFanTestTimer.Stop();
        string restore = _manualFanRestoreProfile ?? _app.UserSettings.Current.CoolingProfile;
        string restoreName = FriendlyProfileName(restore);
        if (_manualFanTestStatus is not null)
        {
            _manualFanTestStatus.Visibility = Visibility.Visible;
            _manualFanTestStatus.Text = $"Restoring {restoreName}…";
        }
        if (_manualFanEndButton is not null)
            _manualFanEndButton.IsEnabled = false;

        bool restored = false;
        try
        {
            restored = await _app.SetCoolingProfileAsync(restore);
            if (!restored)
            {
                ServiceResponse? fallback = await _app.HardwareClient.ReturnFanToAutoAsync();
                if (fallback?.Success == true)
                {
                    _app.State.CoolingProfile = "Lenovo Auto";
                    restored = true;
                    restoreName = "Auto";
                }
            }
        }
        finally
        {
            _manualFanTestActive = false;
            _manualFanTestEnding = false;
            _manualFanRestoreProfile = null;
            if (_manualFanEndButton is not null)
            {
                _manualFanEndButton.IsEnabled = true;
                _manualFanEndButton.Visibility = Visibility.Collapsed;
            }
            if (_manualFanTestStatus is not null)
            {
                _manualFanTestStatus.Visibility = Visibility.Visible;
                _manualFanTestStatus.Text = restored
                    ? $"{reason} · restored {restoreName}"
                    : $"{reason} · restore failed; use the profile selector or Auto";
            }
            ProfileComboBox.IsEnabled = _app.State.CanFanControl;
            _ = _app.HardwareClient.GetStatusAsync();
        }
    }

    private void UpdateManualFanTestUi(string? testLabel = null)
    {
        if (_manualFanEndButton is not null)
            _manualFanEndButton.Visibility = _manualFanTestActive ? Visibility.Visible : Visibility.Collapsed;
        if (_manualFanTestStatus is null)
            return;

        if (!_manualFanTestActive)
        {
            _manualFanTestStatus.Visibility = Visibility.Collapsed;
            return;
        }

        int seconds = Math.Max(0, (int)Math.Ceiling((_manualFanTestEndsAt - DateTimeOffset.UtcNow).TotalSeconds));
        string restoreName = FriendlyProfileName(_manualFanRestoreProfile);
        string prefix = string.IsNullOrWhiteSpace(testLabel) ? "Temporary manual test" : $"Temporary test · {testLabel}";
        _manualFanTestStatus.Visibility = Visibility.Visible;
        _manualFanTestStatus.Text = $"{prefix} · restores {restoreName} in {seconds} s";
        ProfileComboBox.IsEnabled = false;
    }

    private string FriendlyProfileName(string? id)
    {
        if (_app is null || string.IsNullOrWhiteSpace(id))
            return "Auto";
        return _app.FanProfiles.Find(id)?.Name ?? DisplayProfile(id);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (T nested in FindDescendants<T>(child))
                yield return nested;
        }
    }
}
