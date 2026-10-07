using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using ThinkControl.UI.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string InteractionPolishKey = "ThinkControl.Advanced.Interactions";
    private int _pageEntryGeneration;
    private ScrollViewer? _pendingPageEntry;
    private bool _pendingPageAnimation;

    private void ConfigureInteractionPolish()
    {
        if (Resources.Contains(InteractionPolishKey))
            return;
        Resources[InteractionPolishKey] = true;

        AttachPageInteraction(NavHome, PageHome);
        AttachPageInteraction(NavModes, PageModes);
        AttachPageInteraction(NavAutomation, PageAutomation);
        AttachPageInteraction(NavPerformance, PagePerformance);
        AttachPageInteraction(NavFans, PageFans);
        AttachPageInteraction(NavBattery, PageBattery);
        AttachPageInteraction(NavDisplay, PageDisplay);
        AttachPageInteraction(NavAudio, PageAudio);
        AttachPageInteraction(NavKeyboard, PageKeyboard);
        AttachPageInteraction(NavTouchpad, PageTouchpad);
        AttachPageInteraction(NavSystem, PageSystem);
        AttachPageInteraction(NavUpdates, PageUpdates);
        AttachPageInteraction(NavSettings, PageSettings);

        FixSwitchRow(DisplayAdaptiveSwitch);
        ConfigureUpdateControls();
    }

    private void AttachPageInteraction(RadioButton nav, ScrollViewer page)
    {
        // Checked routes through ShowPage. Click also covers reselecting the active destination.
        nav.Click += (_, _) =>
        {
            if (nav.Tag is string destination) Navigate(destination);
        };
    }

    private void ResetPageForNavigation(ScrollViewer page, bool animate)
    {
        if (!ReferenceEquals(_pendingPageEntry, page)) _pendingPageAnimation = false;
        _pendingPageEntry = page;
        _pendingPageAnimation |= animate;
        int generation = ++_pageEntryGeneration;
        page.Dispatcher.BeginInvoke(() =>
        {
            if (generation != _pageEntryGeneration || page.Visibility != Visibility.Visible) return;
            bool animateEntry = _pendingPageAnimation;
            _pendingPageEntry = null;
            _pendingPageAnimation = false;
            ResetTransientPageUi(page);
            page.ScrollToTop();
            if (animateEntry) AnimatePageEntry(page);
        });
    }

    private static void AnimatePageEntry(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (!SystemParameters.ClientAreaAnimation)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(135)) { EasingFunction = ease });
    }

    private static void ResetTransientPageUi(DependencyObject root)
    {
        foreach (Controls.BatteryTelemetryPanel battery in FindVisualChildren<Controls.BatteryTelemetryPanel>(root))
            battery.ResetNavigationView();
        foreach (Controls.ModesPanel panel in FindVisualChildren<Controls.ModesPanel>(root))
            panel.ResetNavigationView();
        foreach (ComboBox combo in FindVisualChildren<ComboBox>(root))
            combo.IsDropDownOpen = false;
        foreach (Expander expander in FindVisualChildren<Expander>(root))
            expander.IsExpanded = false;
    }

    private static void FixSwitchRow(WpfCheckBox toggle)
    {
        toggle.VerticalAlignment = VerticalAlignment.Center;
        toggle.Margin = new Thickness(0, 4, 0, 4);
        if (toggle.Parent is Grid row)
        {
            row.MinHeight = Math.Max(row.MinHeight, 32);
            row.ClipToBounds = false;
        }
    }

    private void ConfigureUpdateControls()
    {
        AutomaticUpdatesSwitch.IsChecked = _app.UserSettings.Current.AutomaticUpdates;
        _lastUpdate = _app.LatestUpdateResult;
        _app.UpdateAvailabilityChanged += App_UpdateAvailabilityChanged;
        Closed += (_, _) => _app.UpdateAvailabilityChanged -= App_UpdateAvailabilityChanged;
        SyncPublishedUpdateResult();
    }
    private void AutomaticUpdates_Click(object sender, RoutedEventArgs e) =>
        _app.UserSettings.Update(settings => settings with { AutomaticUpdates = AutomaticUpdatesSwitch.IsChecked == true });
    private void App_UpdateAvailabilityChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(SyncPublishedUpdateResult);

    private void SyncPublishedUpdateResult()
    {
        _lastUpdate = _app.LatestUpdateResult ?? _lastUpdate;
        bool ready = IsReleaseReady(_lastUpdate);
        OpenReleaseButton.IsEnabled = ready && !IsUpdateCheckInProgress() && !IsUpdateInstallInProgress();
        OpenReleaseButton.Content = ready && !string.IsNullOrWhiteSpace(_lastUpdate?.Version)
            ? $"Install {_lastUpdate.Version}"
            : "Install update";
        RefreshUpdateAvailabilityVisual();
    }

    private async void CheckUpdatesAndPrepare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton || IsUpdateCheckInProgress())
            return;

        OpenReleaseButton.IsEnabled = false;
        _app.State.UpdateStatus = "Checking for updates…";
        SetUpdateCheckingVisual(true);
        try
        {
            _lastUpdate = await _app.UpdateService.CheckAsync();
            _app.PublishUpdateCheckResult(_lastUpdate);
            SyncPublishedUpdateResult();
        }
        finally
        {
            RecordUpdateCheckCompleted(DateTimeOffset.UtcNow);
            SetUpdateCheckingVisual(false);
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        WpfButton button = OpenReleaseButton;
        bool updaterStarted = false;
        button.IsEnabled = false;
        try
        {
            _lastUpdate ??= _app.LatestUpdateResult;
            if (_lastUpdate is null || !_lastUpdate.Available)
            {
                _app.State.UpdateStatus = "Checking for updates…";
                SetUpdateCheckingVisual(true);
                _lastUpdate = await _app.UpdateService.CheckAsync();
                _app.PublishUpdateCheckResult(_lastUpdate);
                RecordUpdateCheckCompleted(DateTimeOffset.UtcNow);
                SetUpdateCheckingVisual(false);
            }

            if (_lastUpdate is null || !_lastUpdate.Available)
            {
                _app.State.UpdateStatus = _lastUpdate?.Status ?? "No newer release is available";
                return;
            }

            if (!IsReleaseReady(_lastUpdate))
            {
                _app.State.UpdateStatus = "The release is still publishing its verified update files. Check again shortly.";
                return;
            }

            var progress = new Progress<string>(status =>
            {
                _app.State.UpdateStatus = status;
                button.Content = status.StartsWith("Verifying", StringComparison.OrdinalIgnoreCase)
                    ? "Verifying…"
                    : status.StartsWith("Ready to install", StringComparison.OrdinalIgnoreCase)
                        ? "Approve in Windows…"
                        : "Downloading…";
            });

            _app.State.UpdateStatus = $"Downloading {_lastUpdate.Version ?? "update"}…";
            button.Content = "Downloading…";
            UpdateInstallResult result = await _app.UpdateService.DownloadAndLaunchAsync(_lastUpdate, progress);
            _app.State.UpdateStatus = result.Status;
            if (result.Success)
            {
                updaterStarted = true;
                button.Content = "Installer started…";
                button.IsEnabled = false;
                return;
            }
        }
        finally
        {
            SetUpdateCheckingVisual(false);
            if (!updaterStarted)
                SyncPublishedUpdateResult();
        }
    }

    private static bool IsReleaseReady(UpdateCheckResult? update) =>
        update is { Available: true } &&
        !string.IsNullOrWhiteSpace(update.InstallerUrl) &&
        !string.IsNullOrWhiteSpace(update.PayloadUrl) &&
        !string.IsNullOrWhiteSpace(update.ChecksumUrl);
}
