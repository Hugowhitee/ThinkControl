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
    private const string TouchpadResetButtonTag = "ThinkControl.Touchpad.ResetDefaults";
    private const string GlobalResetCardTag = "ThinkControl.Settings.GlobalResetCard";

    private void ConfigureResetDefaults()
    {
        if (Resources.Contains(ResetDefaultsConfiguredKey))
        {
            AddTouchpadReset();
            AddGlobalResetCard();
            return;
        }

        Resources[ResetDefaultsConfiguredKey] = true;

        // PerformancePanel and FansPanel own their own reset actions. Static legacy
        // page headers no longer exist for those pages, so there is one reset owner
        // per feature instead of a second AdvancedWindow wrapper.
        AddPageReset(
            PageDisplay,
            "Restore ThinkControl display behavior to Auto refresh. Brightness and adaptive brightness stay with Windows/OEM policy.",
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

        AddTouchpadReset();
        AddGlobalResetCard();
    }

    private void AddPageReset(
        WpfScrollViewer page,
        string tooltip,
        Func<Task> reset)
    {
        if (page.Content is not WpfStackPanel stack ||
            stack.Children.Count == 0 ||
            stack.Children[0] is not WpfTextBlock title)
        {
            return;
        }

        stack.Children.RemoveAt(0);

        var header = new WpfGrid();
        header.Children.Add(title);

        WpfButton button = CreatePageResetButton(tooltip);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await reset(); }
            finally { button.IsEnabled = true; }
        };
        header.Children.Add(button);
        stack.Children.Insert(0, header);
    }

    private void AddTouchpadReset()
    {
        TouchpadPanel panel = TouchpadPanelControl;
        if (panel.Content is not WpfGrid root)
            return;

        WpfGrid? header = root.Children
            .OfType<WpfGrid>()
            .FirstOrDefault(child => WpfGrid.GetRow(child) == 0);
        WpfStackPanel? actions = header?.Children
            .OfType<WpfStackPanel>()
            .FirstOrDefault(child => WpfGrid.GetColumn(child) == 1);
        if (actions is null || actions.Children.OfType<WpfButton>().Any(button => Equals(button.Tag, TouchpadResetButtonTag)))
            return;

        WpfButton reset = CreatePageResetButton(
            "Restore edge gestures, gesture pop-up and supported Windows haptic settings to ThinkControl defaults.");
        reset.Tag = TouchpadResetButtonTag;
        reset.Margin = new Thickness(0, 0, 12, 0);
        reset.Click += async (_, _) =>
        {
            reset.IsEnabled = false;
            try
            {
                _app.ResetTouchpadDefaults();
                await _app.RefreshStatusAsync();
                panel.Initialize(_app);
            }
            finally
            {
                reset.IsEnabled = true;
            }
        };
        actions.Children.Insert(0, reset);
    }

    private WpfButton CreatePageResetButton(string tooltip)
    {
        var button = new WpfButton
        {
            Content = "Defaults",
            ToolTip = "Reset this page · " + tooltip,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = TypographyScale.Caption,
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = TryFindResource("TcButton") as Style
        };
        button.SetResourceReference(WpfButton.ForegroundProperty, "Tc.TextMuted");
        return button;
    }

    private void AddGlobalResetCard()
    {
        if (PageSettings.Content is not WpfStackPanel stack ||
            stack.Children.OfType<Border>().Any(border => Equals(border.Tag, GlobalResetCardTag)))
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
            Text = "Restore app preferences and supported controls to their defaults. Battery history, diagnostics consent and Windows-owned brightness settings are kept.",
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
            "Reset all ThinkControl preferences to their defaults?\n\n" +
            "This restores Balanced performance, Lenovo Auto fans, Auto refresh, keyboard defaults, touchpad gesture defaults, Windows haptic defaults, Dynamic + Balanced Dolby processing, System theme and disables Start with Windows. " +
            "Display brightness, adaptive brightness, diagnostics consent and battery history are kept because they are not portable ThinkControl defaults.",
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
