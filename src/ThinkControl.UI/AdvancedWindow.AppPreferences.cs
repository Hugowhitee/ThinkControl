using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string AppPreferencesConfiguredKey = "ThinkControl.Advanced.AppPreferencesConfigured";
    private const string DefaultOpeningViewCardTag = "ThinkControl.Settings.DefaultOpeningView";
    private const string GitHubCardTag = "ThinkControl.Settings.GitHub";

    private RadioButton? _openingCompact;
    private RadioButton? _openingAdvanced;
    private ComboBox? _batteryRetention;

    private void ConfigureAppPreferencesUi()
    {
        if (Resources.Contains(AppPreferencesConfiguredKey))
        {
            RefreshOpeningViewSelection();
            return;
        }

        if (PageSettings.Content is not StackPanel stack)
            return;

        Border? startupCard = stack.Children
            .OfType<Border>()
            .FirstOrDefault(border => FindVisualChildren<TextBlock>(border)
                .Any(text => string.Equals(text.Text, "Start with Windows", StringComparison.Ordinal)));

        Border openingCard = CreateOpeningViewCard();
        int openingIndex = startupCard is null
            ? Math.Min(3, stack.Children.Count)
            : stack.Children.IndexOf(startupCard) + 1;
        stack.Children.Insert(openingIndex, openingCard);
        stack.Children.Insert(Math.Min(openingIndex + 1, stack.Children.Count), CreateBatteryRetentionCard());

        StackPanel advanced = SettingsAdvancedBody;
        Border githubCard = CreateGitHubCard();
        Border? resetCard = advanced.Children
            .OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, GlobalResetCardTag));
        int githubIndex = resetCard is null ? advanced.Children.Count : advanced.Children.IndexOf(resetCard);
        advanced.Children.Insert(githubIndex, githubCard);

        NavSettings.Checked += (_, _) => RefreshOpeningViewSelection();

        Resources[AppPreferencesConfiguredKey] = true;
        RefreshOpeningViewSelection();
        RefreshBatteryRetentionSelection();
    }

    private Border CreateBatteryRetentionCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });

        var copy = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        copy.Children.Add(new TextBlock { Text = "Battery history detail", FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = "Older samples become daily summaries; health trends remain.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
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
        return new Border { Style = TryFindResource("TcSettingsRow") as Style, Child = grid };
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
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        var copy = new StackPanel { Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock { Text = "App icon opens", FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = "The tray icon always opens Compact.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);
        grid.Children.Add(copy);

        var choices = new Grid { VerticalAlignment = VerticalAlignment.Center };
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
            Content = "Advanced",
            Tag = "Advanced",
            Style = TryFindResource("TcSegment") as Style,
            Margin = new Thickness(4, 0, 0, 0)
        };
        _openingAdvanced.Click += OpeningView_Click;
        Grid.SetColumn(_openingAdvanced, 1);
        choices.Children.Add(_openingAdvanced);
        Grid.SetColumn(choices, 1);
        grid.Children.Add(choices);

        return new Border { Tag = DefaultOpeningViewCardTag, Style = TryFindResource("TcSettingsRow") as Style, Child = grid };
    }

    private Border CreateGitHubCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = "Source & releases",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var button = new Button
        {
            Content = "GitHub ↗",
            Tag = "https://github.com/Hugowhitee/ThinkControl",
            ToolTip = "Source, releases, changelog and issues",
            Style = TryFindResource("TcExternalSettingsLink") as Style
        };
        button.Click += OpenUrl_Click;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);
        return new Border { Tag = GitHubCardTag, Style = TryFindResource("TcSettingsRow") as Style, Child = grid };
    }

    private void OpeningView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string view })
            return;

        _app.UserSettings.Update(settings => settings with { DefaultOpeningView = view });
        RefreshOpeningViewSelection();
    }

    internal void PrepareOpeningViewForSnapshot(string view)
    {
        if (_openingCompact is null || _openingAdvanced is null)
            ConfigureAppPreferencesUi();

        bool advanced = string.Equals(view, "Advanced", StringComparison.OrdinalIgnoreCase);
        if (_openingCompact is not null)
            _openingCompact.IsChecked = !advanced;
        if (_openingAdvanced is not null)
            _openingAdvanced.IsChecked = advanced;
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
