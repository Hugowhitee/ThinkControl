using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Audio;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string AppPreferencesConfiguredKey = "ThinkControl.Advanced.AppPreferencesConfigured";
    private const string AudioSafetyCardTag = "ThinkControl.Settings.AudioSafety";
    private const string DefaultOpeningViewCardTag = "ThinkControl.Settings.DefaultOpeningView";
    private const string GitHubCardTag = "ThinkControl.Settings.GitHub";

    private RadioButton? _audioSafetyNormal;
    private RadioButton? _audioSafetyMediaLock;
    private RadioButton? _audioSafetySilent;
    private TextBlock? _audioSafetyStatus;
    private RadioButton? _openingCompact;
    private RadioButton? _openingAdvanced;
    private ComboBox? _batteryRetention;

    private void ConfigureAppPreferencesUi()
    {
        if (Resources.Contains(AppPreferencesConfiguredKey))
        {
            RefreshOpeningViewSelection();
            RefreshAudioSafetySelection();
            return;
        }

        if (PageSettings.Content is not StackPanel stack)
            return;

        Border? startupCard = stack.Children
            .OfType<Border>()
            .FirstOrDefault(border => FindVisualChildren<TextBlock>(border)
                .Any(text => string.Equals(text.Text, "Start with Windows", StringComparison.Ordinal)));

        Border audioSafetyCard = CreateAudioSafetyCard();
        int audioSafetyIndex = startupCard is null
            ? Math.Min(3, stack.Children.Count)
            : stack.Children.IndexOf(startupCard);
        stack.Children.Insert(audioSafetyIndex, audioSafetyCard);

        Border openingCard = CreateOpeningViewCard();
        int openingIndex = startupCard is null
            ? Math.Min(audioSafetyIndex + 2, stack.Children.Count)
            : stack.Children.IndexOf(startupCard) + 1;
        stack.Children.Insert(openingIndex, openingCard);
        stack.Children.Insert(Math.Min(openingIndex + 1, stack.Children.Count), CreateBatteryRetentionCard());

        Border githubCard = CreateGitHubCard();
        Border? resetCard = stack.Children
            .OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, GlobalResetCardTag));
        int githubIndex = resetCard is null ? stack.Children.Count : stack.Children.IndexOf(resetCard);
        stack.Children.Insert(githubIndex, githubCard);

        NavSettings.Checked += (_, _) =>
        {
            RefreshOpeningViewSelection();
            RefreshAudioSafetySelection();
        };
        _app.AudioSafety.ModeChanged += AudioSafety_ModeChanged;
        Closed += (_, _) => _app.AudioSafety.ModeChanged -= AudioSafety_ModeChanged;

        Resources[AppPreferencesConfiguredKey] = true;
        RefreshOpeningViewSelection();
        RefreshBatteryRetentionSelection();
        RefreshAudioSafetySelection();
    }

    private void AudioSafety_ModeChanged(AudioSafetyMode mode) =>
        Dispatcher.BeginInvoke(new Action(RefreshAudioSafetySelection));

    private Border CreateAudioSafetyCard()
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Audio safety",
            FontWeight = FontWeights.SemiBold
        });

        var detail = new TextBlock
        {
            Text = "Media lock prevents ThinkControl Touchpad volume, track and seek actions without muting Windows audio. Silent adds an output mute and blocks ThinkControl output changes. Microphone input is never changed. This mode lasts for the current ThinkControl session only.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        content.Children.Add(detail);

        var choices = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        choices.ColumnDefinitions.Add(new ColumnDefinition());
        choices.ColumnDefinitions.Add(new ColumnDefinition());
        choices.ColumnDefinitions.Add(new ColumnDefinition());

        _audioSafetyNormal = CreateAudioSafetyChoice("Normal", AudioSafetyMode.Normal, new Thickness(0, 0, 4, 0));
        _audioSafetyMediaLock = CreateAudioSafetyChoice("Media lock", AudioSafetyMode.MediaLock, new Thickness(2, 0, 2, 0));
        _audioSafetySilent = CreateAudioSafetyChoice("Silent", AudioSafetyMode.Silent, new Thickness(4, 0, 0, 0));
        choices.Children.Add(_audioSafetyNormal);
        Grid.SetColumn(_audioSafetyMediaLock, 1);
        choices.Children.Add(_audioSafetyMediaLock);
        Grid.SetColumn(_audioSafetySilent, 2);
        choices.Children.Add(_audioSafetySilent);
        content.Children.Add(choices);

        _audioSafetyStatus = new TextBlock
        {
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _audioSafetyStatus.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        content.Children.Add(_audioSafetyStatus);

        return new Border
        {
            Tag = AudioSafetyCardTag,
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Child = content
        };
    }

    private RadioButton CreateAudioSafetyChoice(string label, AudioSafetyMode mode, Thickness margin)
    {
        var button = new RadioButton
        {
            GroupName = "AudioSafetyMode",
            Content = label,
            Tag = mode,
            Style = TryFindResource("TcSegment") as Style,
            Margin = margin
        };
        button.Click += AudioSafety_Click;
        return button;
    }

    private async void AudioSafety_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AudioSafetyMode mode })
            return;

        SetAudioSafetyChoicesEnabled(false);
        try
        {
            AudioSafetyTransitionResult result = await _app.SetAudioSafetyModeAsync(mode);
            if (_audioSafetyStatus is not null)
                _audioSafetyStatus.Text = result.Detail;
        }
        finally
        {
            SetAudioSafetyChoicesEnabled(true);
            RefreshAudioSafetySelection();
        }
    }

    private void SetAudioSafetyChoicesEnabled(bool enabled)
    {
        if (_audioSafetyNormal is not null) _audioSafetyNormal.IsEnabled = enabled;
        if (_audioSafetyMediaLock is not null) _audioSafetyMediaLock.IsEnabled = enabled;
        if (_audioSafetySilent is not null) _audioSafetySilent.IsEnabled = enabled;
    }

    private void RefreshAudioSafetySelection()
    {
        AudioSafetyMode mode = _app.AudioSafety.Mode;
        if (_audioSafetyNormal is not null) _audioSafetyNormal.IsChecked = mode == AudioSafetyMode.Normal;
        if (_audioSafetyMediaLock is not null) _audioSafetyMediaLock.IsChecked = mode == AudioSafetyMode.MediaLock;
        if (_audioSafetySilent is not null) _audioSafetySilent.IsChecked = mode == AudioSafetyMode.Silent;
        if (_audioSafetyStatus is not null)
        {
            _audioSafetyStatus.Text = mode switch
            {
                AudioSafetyMode.MediaLock => "Media lock active · intentional Windows/app audio remains available.",
                AudioSafetyMode.Silent => "Silent active · current Windows output is muted and ThinkControl media/output actions are locked.",
                _ => "Normal · ThinkControl media and volume controls are available."
            };
        }
    }

    internal void PrepareAudioSafetyForSnapshot(AudioSafetyMode mode)
    {
        if (_audioSafetyNormal is null)
            ConfigureAppPreferencesUi();
        if (_audioSafetyNormal is not null) _audioSafetyNormal.IsChecked = mode == AudioSafetyMode.Normal;
        if (_audioSafetyMediaLock is not null) _audioSafetyMediaLock.IsChecked = mode == AudioSafetyMode.MediaLock;
        if (_audioSafetySilent is not null) _audioSafetySilent.IsChecked = mode == AudioSafetyMode.Silent;
        if (_audioSafetyStatus is not null)
        {
            _audioSafetyStatus.Text = mode == AudioSafetyMode.Silent
                ? "Silent active · current Windows output is muted and ThinkControl media/output actions are locked."
                : mode == AudioSafetyMode.MediaLock
                    ? "Media lock active · intentional Windows/app audio remains available."
                    : "Normal · ThinkControl media and volume controls are available.";
        }
    }

    private Border CreateBatteryRetentionCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        var copy = new StackPanel { Margin = new Thickness(0, 0, 22, 0) };
        copy.Children.Add(new TextBlock { Text = "Battery history detail", FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = "Keep session graphs for this long. Older sessions are compacted into daily summaries; health trends and learned estimates are retained.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);
        grid.Children.Add(copy);

        _batteryRetention = new ComboBox
        {
            Style = TryFindResource("TcComboBox") as Style,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new[] { "7 days", "14 days", "30 days" }
        };
        _batteryRetention.SelectionChanged += BatteryRetention_SelectionChanged;
        Grid.SetColumn(_batteryRetention, 1);
        grid.Children.Add(_batteryRetention);
        return new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Child = grid
        };
    }

    private void BatteryRetention_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_batteryRetention?.SelectedItem is not string text || !int.TryParse(text.Split(' ')[0], out int days) ||
            days == _app.UserSettings.Current.BatteryDetailRetentionDays)
            return;
        _app.UserSettings.Update(settings => settings with { BatteryDetailRetentionDays = days });
        _app.BatteryHistoryService.ConfigureDetailedRetentionDays(days);
    }

    private void RefreshBatteryRetentionSelection()
    {
        if (_batteryRetention is not null)
            _batteryRetention.SelectedItem = $"{_app.UserSettings.Current.BatteryDetailRetentionDays} days";
    }

    private Border CreateOpeningViewCard()
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "App icon opens",
            FontWeight = FontWeights.SemiBold
        });

        var detail = new TextBlock
        {
            Text = "Choose what opens from Start, a desktop shortcut, the taskbar app icon or a second launch. The tray icon always remains the Compact quick view; Start with Windows stays tray-only.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        content.Children.Add(detail);

        var choices = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        choices.ColumnDefinitions.Add(new ColumnDefinition());
        choices.ColumnDefinitions.Add(new ColumnDefinition());

        _openingCompact = new RadioButton
        {
            GroupName = "DefaultOpeningView",
            Content = "Compact",
            Tag = "Compact",
            Style = TryFindResource("TcSegment") as Style,
            Margin = new Thickness(0, 0, 4, 0)
        };
        _openingCompact.Click += OpeningView_Click;
        choices.Children.Add(_openingCompact);

        _openingAdvanced = new RadioButton
        {
            GroupName = "DefaultOpeningView",
            Content = "Full",
            Tag = "Advanced",
            Style = TryFindResource("TcSegment") as Style,
            Margin = new Thickness(4, 0, 0, 0)
        };
        _openingAdvanced.Click += OpeningView_Click;
        Grid.SetColumn(_openingAdvanced, 1);
        choices.Children.Add(_openingAdvanced);

        content.Children.Add(choices);

        return new Border
        {
            Tag = DefaultOpeningViewCardTag,
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Child = content
        };
    }

    private Border CreateGitHubCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
        copy.Children.Add(new TextBlock
        {
            Text = "ThinkControl on GitHub",
            FontWeight = FontWeights.SemiBold
        });
        var detail = new TextBlock
        {
            Text = "Source code, releases, changelog and issue tracker.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);
        grid.Children.Add(copy);

        var button = new Button
        {
            Content = "Open GitHub  ↗",
            Tag = "https://github.com/Hugowhitee/ThinkControl",
            ToolTip = "Open the ThinkControl repository on GitHub",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Style = TryFindResource("TcButton") as Style,
            Padding = new Thickness(13, 7, 13, 7)
        };
        button.Click += OpenUrl_Click;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        return new Border
        {
            Tag = GitHubCardTag,
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 18, 0, 0),
            Child = grid
        };
    }

    private void OpeningView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string view })
            return;

        _app.UserSettings.Update(settings => settings with { DefaultOpeningView = view });
        RefreshOpeningViewSelection();
    }

    private void RefreshOpeningViewSelection()
    {
        string view = _app.UserSettings.Current.DefaultOpeningView;
        if (_openingCompact is not null)
            _openingCompact.IsChecked = !string.Equals(view, "Advanced", StringComparison.OrdinalIgnoreCase);
        if (_openingAdvanced is not null)
            _openingAdvanced.IsChecked = string.Equals(view, "Advanced", StringComparison.OrdinalIgnoreCase);
    }
}
