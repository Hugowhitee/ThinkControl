using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private Button? _updateCheckButton;
    private ProgressBar? _updateCheckProgress;
    private TextBlock? _updateLastCheckedText;
    private AppState? _updateUiState;
    private bool _updateUiConfigured;
    private bool _manualUpdateCheckBusy;

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
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Check for updates", StringComparison.Ordinal));
        if (_updateCheckButton is null)
            return;

        _updateLastCheckedText = new TextBlock
        {
            FontSize = 10.5,
            Margin = new Thickness(0, 7, 0, 0)
        };
        _updateLastCheckedText.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _updateCheckProgress = new ProgressBar
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

        // Replace the original simple handler with the richer stateful one. The
        // existing XAML stays simple while this keeps manual and automatic update
        // checks visually consistent.
        _updateCheckButton.Click -= CheckUpdates_Click;
        _updateCheckButton.Click += CheckUpdatesPolished_Click;

        if (DataContext is AppState state)
        {
            _updateUiState = state;
            state.PropertyChanged += UpdateUiState_PropertyChanged;
        }

        _updateUiConfigured = true;
        RefreshUpdateUi();
    }

    private async void CheckUpdatesPolished_Click(object sender, RoutedEventArgs e)
    {
        if (_manualUpdateCheckBusy)
            return;

        _manualUpdateCheckBusy = true;
        SetUpdateCheckingVisual(true);
        _lastUpdate = null;
        OpenReleaseButton.IsEnabled = false;
        _app.State.UpdateStatus = "Checking for updates…";

        DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
        try
        {
            UpdateCheckResult result = await _app.UpdateService.CheckAsync();
            checkedAt = DateTimeOffset.UtcNow;
            UpdateCheckHistoryService.Record(checkedAt);
            _lastUpdate = result;
            _app.State.UpdateStatus = result.Status;
            OpenReleaseButton.IsEnabled = !string.IsNullOrWhiteSpace(result.Url);
        }
        catch
        {
            checkedAt = DateTimeOffset.UtcNow;
            UpdateCheckHistoryService.Record(checkedAt);
            _app.State.UpdateStatus = "Could not reach the release channel";
        }
        finally
        {
            _manualUpdateCheckBusy = false;
            SetUpdateCheckingVisual(false);
            RefreshLastChecked(checkedAt);
        }
    }

    private void UpdateUiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppState.UpdateStatus))
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            bool checking = _app.State.UpdateStatus.StartsWith("Checking", StringComparison.OrdinalIgnoreCase);
            if (!_manualUpdateCheckBusy)
                SetUpdateCheckingVisual(checking);
            if (!checking)
                RefreshLastChecked(UpdateCheckHistoryService.Read());
        }));
    }

    private void RefreshUpdateUi()
    {
        bool checking = _manualUpdateCheckBusy ||
                        _app.State.UpdateStatus.StartsWith("Checking", StringComparison.OrdinalIgnoreCase);
        SetUpdateCheckingVisual(checking);
        RefreshLastChecked(UpdateCheckHistoryService.Read());
    }

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

    private void RefreshLastChecked(DateTimeOffset? timestamp)
    {
        if (_updateLastCheckedText is not null)
            _updateLastCheckedText.Text = UpdateCheckHistoryService.Format(timestamp);
    }
}
