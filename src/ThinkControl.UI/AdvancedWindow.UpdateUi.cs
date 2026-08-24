using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using WpfProgressBar = System.Windows.Controls.ProgressBar;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private Button? _updateCheckButton;
    private WpfProgressBar? _updateCheckProgress;
    private TextBlock? _updateLastCheckedText;
    private Border? _updateUpToDateBadge;
    private bool _updateUiConfigured;

    private void ConfigureUpdateUi()
    {
        if (_updateUiConfigured)
        {
            RefreshUpdateUi();
            return;
        }

        if (PageUpdates?.Content is not StackPanel root)
            return;

        Border? section = root.Children.OfType<Border>().FirstOrDefault();
        if (section?.Child is not StackPanel content)
            return;

        StackPanel? actions = content.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Orientation == Orientation.Horizontal);
        if (actions is null)
            return;

        _updateCheckButton = actions.Children
            .OfType<Button>()
            .FirstOrDefault(button =>
                string.Equals(button.Content?.ToString(), "Check for updates", StringComparison.Ordinal) ||
                string.Equals(button.Content?.ToString(), "Checking…", StringComparison.Ordinal));
        if (_updateCheckButton is null)
            return;

        ConfigureVersionStatusBadge(content);

        _updateLastCheckedText = new TextBlock
        {
            FontSize = 10.5,
            Margin = new Thickness(0, 7, 0, 0)
        };
        _updateLastCheckedText.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _updateCheckProgress = new WpfProgressBar
        {
            Height = 2,
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 12, 0, 0),
            BorderThickness = new Thickness(0)
        };

        int actionIndex = content.Children.IndexOf(actions);
        content.Children.Insert(actionIndex, _updateLastCheckedText);
        content.Children.Insert(actionIndex + 1, _updateCheckProgress);

        // ConfigureInteractionPolish owns the one manual check handler. This class
        // owns presentation/persisted history and enforces the install-button state
        // so a stale release URL can never make an unavailable update look installable.
        if (DataContext is AppState state)
            state.PropertyChanged += UpdateUiState_PropertyChanged;

        _updateUiConfigured = true;
        RefreshUpdateUi();
    }

    private void ConfigureVersionStatusBadge(StackPanel content)
    {
        TextBlock? versionText = content.Children
            .OfType<TextBlock>()
            .FirstOrDefault(text => text.FontSize >= 24);
        if (versionText is null)
            return;

        int versionIndex = content.Children.IndexOf(versionText);
        if (versionIndex < 0)
            return;

        Thickness versionMargin = versionText.Margin;
        content.Children.RemoveAt(versionIndex);
        versionText.Margin = new Thickness(0);
        versionText.VerticalAlignment = VerticalAlignment.Center;

        var check = new TextBlock
        {
            Text = "✓",
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        };

        _updateUpToDateBadge = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(8, 1, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            ToolTip = "Up to date",
            Child = check
        };
        _updateUpToDateBadge.SetResourceReference(Border.BackgroundProperty, "Tc.Success");

        var versionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = versionMargin
        };
        versionRow.Children.Add(versionText);
        versionRow.Children.Add(_updateUpToDateBadge);
        content.Children.Insert(versionIndex, versionRow);
    }

    private void UpdateUiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppState.UpdateStatus))
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            bool checking = IsUpdateCheckInProgress();
            SetUpdateCheckingVisual(checking);
            RefreshUpdateAvailabilityVisual();
            if (!checking)
                RefreshLastChecked(UpdateCheckHistoryService.Read());
        }));
    }

    private void RefreshUpdateUi()
    {
        SetUpdateCheckingVisual(IsUpdateCheckInProgress());
        RefreshUpdateAvailabilityVisual();
        RefreshLastChecked(UpdateCheckHistoryService.Read());
    }

    private bool IsUpdateCheckInProgress() =>
        _app.State.UpdateStatus.StartsWith("Checking", StringComparison.OrdinalIgnoreCase);

    private bool IsUpdateInstallInProgress() =>
        _app.State.UpdateStatus.StartsWith("Downloading", StringComparison.OrdinalIgnoreCase) ||
        _app.State.UpdateStatus.StartsWith("Installing", StringComparison.OrdinalIgnoreCase);

    private bool IsUpdateReadyToInstall() =>
        _lastUpdate is { Available: true } update &&
        !string.IsNullOrWhiteSpace(update.InstallerUrl) &&
        !string.IsNullOrWhiteSpace(update.ChecksumUrl);

    private void SetUpdateCheckingVisual(bool checking)
    {
        if (_updateCheckButton is not null)
        {
            _updateCheckButton.IsEnabled = !checking;
            _updateCheckButton.Content = checking ? "Checking…" : "Check for updates";
        }

        if (_updateCheckProgress is not null)
            _updateCheckProgress.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshUpdateAvailabilityVisual()
    {
        bool checking = IsUpdateCheckInProgress();
        bool installing = IsUpdateInstallInProgress();
        OpenReleaseButton.IsEnabled = !checking && !installing && IsUpdateReadyToInstall();

        if (_updateUpToDateBadge is not null)
        {
            bool upToDate = !checking &&
                _app.State.UpdateStatus.StartsWith("Up to date", StringComparison.OrdinalIgnoreCase);
            _updateUpToDateBadge.Visibility = upToDate ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshLastChecked(DateTimeOffset? timestamp)
    {
        if (_updateLastCheckedText is not null)
            _updateLastCheckedText.Text = UpdateCheckHistoryService.Format(timestamp);
    }
}
