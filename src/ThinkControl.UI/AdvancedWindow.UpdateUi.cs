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
        // owns only presentation/persisted history so one click can never issue two
        // simultaneous release requests.
        if (DataContext is AppState state)
            state.PropertyChanged += UpdateUiState_PropertyChanged;

        _updateUiConfigured = true;
        RefreshUpdateUi();
    }

    private void UpdateUiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppState.UpdateStatus))
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            bool checking = IsUpdateCheckInProgress();
            SetUpdateCheckingVisual(checking);
            if (!checking)
                RefreshLastChecked(UpdateCheckHistoryService.Read());
        }));
    }

    private void RefreshUpdateUi()
    {
        SetUpdateCheckingVisual(IsUpdateCheckInProgress());
        RefreshLastChecked(UpdateCheckHistoryService.Read());
    }

    private bool IsUpdateCheckInProgress() =>
        _app.State.UpdateStatus.StartsWith("Checking", StringComparison.OrdinalIgnoreCase);

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
