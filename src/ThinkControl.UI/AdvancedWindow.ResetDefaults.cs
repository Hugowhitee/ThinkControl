using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Controls;
using ThinkControl.UI.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfGrid = System.Windows.Controls.Grid;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string ResetDefaultsConfiguredKey = "ThinkControl.Advanced.ResetDefaultsConfigured";
    private const string PageResetButtonTag = "ThinkControl.Page.ResetDefaults";
    private const string GlobalResetCardTag = "ThinkControl.Settings.GlobalResetCard";

    private void ConfigureResetDefaults()
    {
        if (Resources.Contains(ResetDefaultsConfiguredKey))
        {
            AddGlobalResetCard();
            return;
        }

        Resources[ResetDefaultsConfiguredKey] = true;

        AddPageReset(
            PageDisplay,
            "Restore Auto refresh. Brightness and adaptive brightness stay unchanged.",
            async () =>
            {
                _app.ResetDisplayDefaults();
                await _app.RefreshStatusAsync();
                SyncControls();
            });

        AddPageReset(
            PageKeyboard,
            "Restore Auto keyboard mode, High resting level and 1.0× effect speed.",
            async () =>
            {
                await _app.ResetKeyboardDefaultsAsync();
                await _app.RefreshStatusAsync();
                SyncControls();
            });

        AddGlobalResetCard();
    }

    private void AddPageReset(
        WpfScrollViewer page,
        string tooltip,
        Func<Task> reset)
    {
        if (page.Content is not WpfStackPanel stack)
            return;

        AdvancedPageHeader? header = stack.Children
            .OfType<AdvancedPageHeader>()
            .FirstOrDefault();
        if (header is null)
            return;

        WpfStackPanel rail = header.EnsureActionStack();
        if (rail.Children.OfType<WpfButton>().Any(button => Equals(button.Tag, PageResetButtonTag)))
            return;

        WpfButton button = CreatePageResetButton(tooltip);
        button.Tag = PageResetButtonTag;

        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await reset(); }
            finally { button.IsEnabled = true; }
        };
        header.AddAction(button, PageHeaderActionRole.Defaults);
    }

    private WpfButton CreatePageResetButton(string tooltip)
    {
        var button = new WpfButton
        {
            Content = "Defaults",
            ToolTip = "Reset this page · " + tooltip,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = TryFindResource("TcPageHeaderAction") as Style
        };
        button.SetResourceReference(WpfButton.ForegroundProperty, "Tc.TextMuted");
        return button;
    }

    private void AddGlobalResetCard()
    {
        WpfStackPanel stack = SettingsAdvancedBody;
        if (stack.Children.OfType<Border>().Any(border => Equals(border.Tag, GlobalResetCardTag)))
        {
            return;
        }

        var copy = new WpfStackPanel { Margin = new Thickness(0, 0, 24, 0) };
        copy.Children.Add(new WpfTextBlock
        {
            Text = "Reset ThinkControl",
            FontWeight = FontWeights.SemiBold,
            FontSize = TypographyScale.ControlLabel
        });
        var detail = new WpfTextBlock
        {
            Text = "Restore ThinkControl settings and reset Modes to Focus, Battery saver and Performance. Battery history and diagnostics consent are kept.",
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        detail.SetResourceReference(WpfTextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);

        var reset = new WpfButton
        {
            Content = "Reset all ThinkControl",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(13, 7, 13, 7),
            Style = TryFindResource("TcButton") as Style,
            ToolTip = "Restore all ThinkControl-owned settings"
        };
        reset.SetResourceReference(WpfButton.ForegroundProperty, "Tc.Accent");
        reset.Click += async (_, _) =>
        {
            reset.IsEnabled = false;
            try { await ResetAllDefaultsAsync(); }
            finally { reset.IsEnabled = true; }
        };

        var grid = new WpfGrid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(copy);
        WpfGrid.SetColumn(reset, 1);
        grid.Children.Add(reset);

        var card = new Border
        {
            Tag = GlobalResetCardTag,
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 18, 0, 0),
            BorderThickness = new Thickness(1),
            Child = grid
        };
        card.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");
        stack.Children.Add(card);
    }

    private async Task ResetAllDefaultsAsync()
    {
        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            "Reset ThinkControl settings?\n\n" +
            "Modes will return to Focus, Battery saver and Performance. Battery history, diagnostics consent and Windows brightness settings are kept.",
            "ThinkControl · Reset all",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (answer != System.Windows.MessageBoxResult.Yes)
            return;

        await _app.ResetAllDefaultsAsync();
        StartupSwitch.IsChecked = StartupService.IsEnabled();
        ThemeSystem.IsChecked = true;
        SyncControls();

        TouchpadPanelControl.Initialize(_app);
        DiagnosticsPanelControl?.Refresh();
    }
}
