using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string AppPreferencesConfiguredKey = "ThinkControl.Advanced.AppPreferencesConfigured";
    private const string DefaultOpeningViewCardTag = "ThinkControl.Settings.DefaultOpeningView";
    private const string GitHubCardTag = "ThinkControl.Settings.GitHub";

    private RadioButton? _openingCompact;
    private RadioButton? _openingAdvanced;

    private void ConfigureAppPreferencesUi()
    {
        if (Resources.Contains(AppPreferencesConfiguredKey))
        {
            RefreshOpeningViewSelection();
            return;
        }

        if (PageSettings.Content is not StackPanel stack)
            return;

        Border openingCard = CreateOpeningViewCard();
        Border? startupCard = stack.Children
            .OfType<Border>()
            .FirstOrDefault(border => FindVisualChildren<TextBlock>(border)
                .Any(text => string.Equals(text.Text, "Start with Windows", StringComparison.Ordinal)));
        int openingIndex = startupCard is null
            ? Math.Min(4, stack.Children.Count)
            : stack.Children.IndexOf(startupCard) + 1;
        stack.Children.Insert(openingIndex, openingCard);

        Border githubCard = CreateGitHubCard();
        Border? resetCard = stack.Children
            .OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, GlobalResetCardTag));
        int githubIndex = resetCard is null ? stack.Children.Count : stack.Children.IndexOf(resetCard);
        stack.Children.Insert(githubIndex, githubCard);

        NavSettings.Checked += (_, _) => RefreshOpeningViewSelection();
        Resources[AppPreferencesConfiguredKey] = true;
        RefreshOpeningViewSelection();
    }

    private Border CreateOpeningViewCard()
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Open ThinkControl as",
            FontWeight = FontWeights.SemiBold
        });

        var detail = new TextBlock
        {
            Text = "Choose which interface opens from Start, a desktop shortcut or a second app launch. Start with Windows stays tray-only.",
            FontSize = 10.5,
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
            Content = "Advanced",
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
            FontSize = 10.5,
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
