using System.Windows;
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
    private const string TouchpadPageKey = "ThinkControl.Dynamic.PageTouchpad";

    private void ConfigureResetDefaults()
    {
        if (Resources.Contains(ResetDefaultsConfiguredKey))
        {
            // Dynamic feature pages can be attached after the first pass. Re-run only
            // the idempotent dynamic reset hook so Touchpad always gets its button.
            AddTouchpadReset();
            return;
        }

        Resources[ResetDefaultsConfiguredKey] = true;

        AddPageReset(
            PagePerformance,
            "Reset",
            "Restore Balanced performance mode.",
            async () =>
            {
                _ = _app.ResetPerformanceDefaults();
                await _app.RefreshStatusAsync();
                SyncControls();
            });

        AddPageReset(
            PageFans,
            "Reset",
            "Return fan control to Lenovo Auto.",
            async () =>
            {
                _ = await _app.ResetFanDefaultsAsync();
                await _app.RefreshStatusAsync();
                SyncControls();
            });

        AddPageReset(
            PageDisplay,
            "Reset",
            "Restore ThinkControl display behavior to Auto refresh. Brightness and adaptive brightness stay with Windows/OEM policy.",
            async () =>
            {
                _app.ResetDisplayDefaults();
                await _app.RefreshStatusAsync();
                SyncControls();
            });

        AddPageReset(
            PageKeyboard,
            "Reset",
            "Restore Auto keyboard mode, High resting level and 1.0× effect speed.",
            async () =>
            {
                await _app.ResetKeyboardDefaultsAsync();
                await _app.RefreshStatusAsync();
                SyncControls();
            });

        AddTouchpadReset();

        AddPageReset(
            PageSettings,
            "Reset all",
            "Restore all ThinkControl preferences and supported hardware controls to their defaults.",
            ResetAllDefaultsAsync);
    }

    private void AddPageReset(
        WpfScrollViewer page,
        string label,
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

        WpfButton button = CreateResetButton(label, tooltip);
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
        TouchpadPanel? panel = Resources.Contains(TouchpadPageKey) &&
            Resources[TouchpadPageKey] is WpfScrollViewer { Content: TouchpadPanel resourcePanel }
                ? resourcePanel
                : FindVisualChildren<TouchpadPanel>(this).FirstOrDefault();
        if (panel?.Content is not WpfGrid root)
            return;

        WpfGrid? header = root.Children
            .OfType<WpfGrid>()
            .FirstOrDefault(child => WpfGrid.GetRow(child) == 0);
        WpfStackPanel? actions = header?.Children
            .OfType<WpfStackPanel>()
            .FirstOrDefault(child => WpfGrid.GetColumn(child) == 1);
        if (actions is null || actions.Children.OfType<WpfButton>().Any(button => Equals(button.Tag, TouchpadResetButtonTag)))
            return;

        WpfButton reset = CreateResetButton(
            "Reset",
            "Restore edge gestures, gesture pop-up and supported Windows haptic settings to ThinkControl defaults.");
        reset.Tag = TouchpadResetButtonTag;
        reset.Margin = new Thickness(0, 0, 14, 0);
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

    private WpfButton CreateResetButton(string label, string tooltip) => new()
    {
        Content = label,
        ToolTip = tooltip,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        MinWidth = 0,
        MinHeight = 28,
        Padding = new Thickness(10, 4, 10, 4),
        FontSize = 10.5,
        Style = TryFindResource("TcButton") as Style
    };

    private async Task ResetAllDefaultsAsync()
    {
        System.Windows.MessageBoxResult answer = System.Windows.MessageBox.Show(
            "Reset all ThinkControl preferences to their defaults?\n\n" +
            "This restores Balanced performance, Lenovo Auto fans, Auto refresh, keyboard defaults, touchpad gesture defaults, Windows haptic defaults, System theme and disables Start with Windows. " +
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

        foreach (TouchpadPanel panel in FindVisualChildren<TouchpadPanel>(this))
            panel.Initialize(_app);

        DiagnosticsPanelControl?.Refresh();
    }
}
