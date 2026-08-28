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

    private void ConfigureInteractionPolish()
    {
        if (Resources.Contains(InteractionPolishKey))
            return;
        Resources[InteractionPolishKey] = true;

        AttachPageInteraction(NavHome, PageHome);
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
        FixSwitchRow(HomeAdaptiveSwitch);
        ConfigureUpdateControls();
    }

    private void AttachPageInteraction(RadioButton nav, ScrollViewer page)
    {
        nav.Checked += (_, _) => page.Dispatcher.BeginInvoke(() =>
        {
            ResetTransientPageUi(page);
            page.ScrollToTop();
            AnimatePageEntry(page);
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
        // Page-local disclosure state is transient UI, not a user setting. Returning
        // to a page starts clean while actual switches/sliders/settings stay exactly
        // as the user left them.
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
        if (PageUpdates.Content is not StackPanel stack)
            return;

        TextBlock? description = stack.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();
        if (description is not null)
        {
            description.Text = "ThinkControl checks GitHub Releases automatically. Installing is one click: the setup and payload are downloaded and SHA-256 verified before Windows asks for administrator permission.";
        }

        WpfButton? checkButton = FindVisualChildren<WpfButton>(PageUpdates)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Check for updates", StringComparison.Ordinal));
        if (checkButton is not null)
        {
            checkButton.Click -= CheckUpdates_Click;
            checkButton.Click += CheckUpdatesAndPrepare_Click;
        }

        OpenReleaseButton.Click -= OpenRelease_Click;
        OpenReleaseButton.Click += InstallUpdate_Click;
        OpenReleaseButton.Content = "Install update";
        OpenReleaseButton.ToolTip = "Download, verify and install the newest ThinkControl release";
        OpenReleaseButton.IsEnabled = false;

        _lastUpdate = _app.LatestUpdateResult;
        _app.UpdateAvailabilityChanged += App_UpdateAvailabilityChanged;
        Closed += (_, _) => _app.UpdateAvailabilityChanged -= App_UpdateAvailabilityChanged;
        SyncPublishedUpdateResult();

        var autoSwitch = new WpfCheckBox
        {
            Style = TryFindResource("TcSwitch") as Style,
            IsChecked = _app.UserSettings.Current.AutomaticUpdates,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        autoSwitch.Click += (_, _) =>
            _app.UserSettings.Update(settings => settings with { AutomaticUpdates = autoSwitch.IsChecked == true });

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = "Check for updates at startup", FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = "Check once shortly after ThinkControl starts. A newer release appears as an Update / Dismiss prompt and remains available in Notifications and Updates. ThinkControl never installs or opens an administrator prompt on its own.",
            FontSize = TypographyScale.Caption,
            Margin = new Thickness(0, 4, 80, 0),
            TextWrapping = TextWrapping.Wrap
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);

        var grid = new Grid();
        grid.Children.Add(copy);
        grid.Children.Add(autoSwitch);

        var card = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Child = grid
        };
        stack.Children.Add(card);
    }

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
            DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
            UpdateCheckHistoryService.Record(checkedAt);
            RefreshLastChecked(checkedAt);
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
                DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
                UpdateCheckHistoryService.Record(checkedAt);
                RefreshLastChecked(checkedAt);
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
