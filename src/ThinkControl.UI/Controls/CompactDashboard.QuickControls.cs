using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private bool _quickControlsAdded;
    private bool _syncingQuickControls;
    private ComboBox? _quickPerformance;
    private ComboBox? _quickRefresh;
    private ComboBox? _quickKeyboard;

    private void EnsureQuickControls()
    {
        if (_quickControlsAdded || Content is not Border { Child: Grid root })
            return;

        StackPanel? links = root.Children.OfType<StackPanel>().FirstOrDefault(panel => Grid.GetRow(panel) == 4);
        if (links is null)
            return;

        _quickPerformance = CreateQuickCombo(94);
        _quickPerformance.ItemsSource = new[] { "Quiet", "Balanced", "Performance" };
        _quickPerformance.SelectionChanged += QuickPerformance_SelectionChanged;

        _quickRefresh = CreateQuickCombo(82);
        _quickRefresh.SelectionChanged += QuickRefresh_SelectionChanged;

        _quickKeyboard = CreateQuickCombo(78);
        _quickKeyboard.ItemsSource = new[] { "Off", "Low", "High", "Auto" };
        _quickKeyboard.SelectionChanged += QuickKeyboard_SelectionChanged;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(CreateQuickColumn("Performance", _quickPerformance, 0));
        grid.Children.Add(CreateQuickColumn("Refresh", _quickRefresh, 1));
        grid.Children.Add(CreateQuickColumn("Keyboard", _quickKeyboard, 2));

        var card = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Padding = new Thickness(9, 7, 9, 8),
            Margin = new Thickness(0, 1, 0, 5),
            Child = grid
        };
        links.Children.Insert(0, card);
        _quickControlsAdded = true;
    }

    private ComboBox CreateQuickCombo(double width)
    {
        var combo = new ComboBox
        {
            Width = width,
            Height = 28,
            FontSize = 10.5,
            Padding = new Thickness(6, 1, 4, 1),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        combo.SetResourceReference(Control.BackgroundProperty, "Tc.SurfaceAlt");
        combo.SetResourceReference(Control.ForegroundProperty, "Tc.Text");
        combo.SetResourceReference(Control.BorderBrushProperty, "Tc.BorderStrong");
        return combo;
    }

    private FrameworkElement CreateQuickColumn(string label, ComboBox combo, int column)
    {
        var panel = new StackPanel
        {
            Margin = column switch
            {
                0 => new Thickness(0, 0, 4, 0),
                2 => new Thickness(4, 0, 0, 0),
                _ => new Thickness(2, 0, 2, 0)
            }
        };
        var title = new TextBlock { Text = label, FontSize = 9.5, Margin = new Thickness(1, 0, 0, 4) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        panel.Children.Add(title);
        panel.Children.Add(combo);
        Grid.SetColumn(panel, column);
        return panel;
    }

    private void SyncQuickControls()
    {
        if (!_quickControlsAdded || _app is null || _quickPerformance is null || _quickRefresh is null || _quickKeyboard is null)
            return;

        _syncingQuickControls = true;
        try
        {
            _quickPerformance.SelectedItem = _app.State.SelectedMode;

            _quickRefresh.Items.Clear();
            _quickRefresh.Items.Add("Auto");
            if (_app.DisplayService.GetSupportedRefreshRates().Contains(60))
                _quickRefresh.Items.Add("60 Hz");
            if (_app.State.MaxRefreshHz > 0 && _app.State.MaxRefreshHz != 60)
                _quickRefresh.Items.Add($"{_app.State.MaxRefreshHz} Hz");
            _quickRefresh.SelectedItem = _app.State.RefreshAutoEnabled
                ? "Auto"
                : $"{_app.State.CurrentRefreshHz} Hz";

            _quickKeyboard.IsEnabled = _app.State.CanKeyboardBacklight;
            _quickKeyboard.SelectedItem = _app.State.KeyboardMode == "Auto"
                ? "Auto"
                : _app.State.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase)
                    ? "Off"
                    : _app.State.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase)
                        ? "Low"
                        : "High";
        }
        finally
        {
            _syncingQuickControls = false;
        }
    }

    private void QuickPerformance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || _quickPerformance?.SelectedItem is not string raw ||
            !Enum.TryParse(raw, out ThinkControlPowerMode mode))
        {
            return;
        }

        if (!_app.SetPowerMode(mode))
            SyncQuickControls();
    }

    private void QuickRefresh_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || _quickRefresh?.SelectedItem is not string raw)
            return;

        if (raw == "Auto")
        {
            _app.EnableRefreshAuto();
            return;
        }

        if (int.TryParse(raw.Split(' ')[0], out int hz) && !_app.SetRefresh(hz))
            SyncQuickControls();
    }

    private async void QuickKeyboard_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || _quickKeyboard?.SelectedItem is not string raw)
            return;

        _quickKeyboard.IsEnabled = false;
        try
        {
            if (raw == "Auto")
                await _app.SetKeyboardModeAsync("Auto");
            else
                await _app.SetKeyboardStaticLevelAsync(raw);
        }
        finally
        {
            SyncQuickControls();
        }
    }
}
