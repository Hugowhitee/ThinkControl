using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string ResetDefaultsConfiguredKey = "ThinkControl.Advanced.ResetDefaultsConfigured";

    private void ConfigureResetDefaults()
    {
        if (Resources.Contains(ResetDefaultsConfiguredKey))
            return;

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

        AddPageReset(
            PageSettings,
            "Reset all",
            "Restore all ThinkControl preferences and supported hardware controls to their defaults.",
            ResetAllDefaultsAsync);
    }

    private void AddPageReset(
        ScrollViewer page,
        string label,
        string tooltip,
        Func<Task> reset)
    {
        if (page.Content is not StackPanel stack ||
            stack.Children.Count == 0 ||
            stack.Children[0] is not TextBlock title)
        {
            return;
        }

        stack.Children.RemoveAt(0);

        var header = new Grid();
        header.Children.Add(title);

        var button = new Button
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
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await reset(); }
            finally { button.IsEnabled = true; }
        };
        header.Children.Add(button);
        stack.Children.Insert(0, header);
    }

    private async Task ResetAllDefaultsAsync()
    {
        MessageBoxResult answer = MessageBox.Show(
            "Reset all ThinkControl preferences to their defaults?\n\n" +
            "This restores Balanced performance, Lenovo Auto fans, Auto refresh, keyboard defaults, touchpad gesture defaults, Windows haptic defaults, System theme and disables Start with Windows. " +
            "Display brightness, adaptive brightness, diagnostics consent and battery history are kept because they are not portable ThinkControl defaults.",
            "ThinkControl · Reset all",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        await _app.ResetAllDefaultsAsync();
        StartupSwitch.IsChecked = StartupService.IsEnabled();
        ThemeSystem.IsChecked = true;
        SyncControls();

        foreach (TouchpadPanel panel in FindVisualChildren<TouchpadPanel>(this))
            panel.RefreshFromSettings();

        DiagnosticsPanelControl?.Refresh();
    }
}
