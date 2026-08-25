using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ThinkControl.Core.Cooling;
using ThinkControl.UI.Services;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private bool _quickControlsAdded;
    private bool _syncingQuickControls;
    private WpfButton? _quickPerformance;
    private WpfButton? _quickFan;
    private WpfButton? _quickRefresh;
    private WpfButton? _quickKeyboard;
    private Popup? _quickPopup;

    private void EnsureQuickControls()
    {
        if (_quickControlsAdded || Content is not Border { Child: Grid root })
            return;

        StackPanel? links = root.Children.OfType<StackPanel>().FirstOrDefault(panel => Grid.GetRow(panel) == 4);
        if (links is null)
            return;

        _quickPerformance = CreateQuickButton();
        _quickPerformance.Click += (_, _) => OpenQuickMenu(
            _quickPerformance,
            ["Efficiency", "Balanced", "Performance"],
            QuickPerformanceSelected);

        _quickFan = CreateQuickButton();
        _quickFan.Click += (_, _) => OpenQuickMenu(
            _quickFan,
            ["Auto", "Quiet", "Balanced", "Max cooling", "Custom"],
            QuickFanSelected);

        _quickRefresh = CreateQuickButton();
        _quickRefresh.Click += (_, _) => OpenQuickMenu(
            _quickRefresh,
            BuildRefreshOptions(),
            QuickRefreshSelected);

        _quickKeyboard = CreateQuickButton();
        _quickKeyboard.Click += (_, _) => OpenQuickMenu(
            _quickKeyboard,
            ["Off", "Low", "High", "Auto"],
            QuickKeyboardSelected);

        var grid = new Grid();
        for (int i = 0; i < 4; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(CreateQuickColumn("Performance", _quickPerformance, 0));
        grid.Children.Add(CreateQuickColumn("Fan", _quickFan, 1));
        grid.Children.Add(CreateQuickColumn("Refresh", _quickRefresh, 2));
        grid.Children.Add(CreateQuickColumn("Keyboard", _quickKeyboard, 3));

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

    private WpfButton CreateQuickButton() => new()
    {
        MinWidth = 78,
        Height = 28,
        FontSize = 10.5,
        Padding = new Thickness(8, 2, 7, 2),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Style = TryFindResource("TcButton") as Style
    };

    private FrameworkElement CreateQuickColumn(string label, WpfButton button, int column)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(column == 0 ? 0 : 3, 0, column == 3 ? 0 : 3, 0)
        };
        var title = new TextBlock { Text = label, FontSize = 9.5, Margin = new Thickness(1, 0, 0, 4) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        panel.Children.Add(title);
        panel.Children.Add(button);
        Grid.SetColumn(panel, column);
        return panel;
    }

    private void OpenQuickMenu(WpfButton button, IEnumerable<string> options, Action<string> selected)
    {
        if (_syncingQuickControls || _app is null || !button.IsEnabled)
            return;

        _quickPopup?.SetCurrentValue(Popup.IsOpenProperty, false);

        var list = new StackPanel();
        Popup? popup = null;
        foreach (string option in options)
        {
            var item = new WpfButton
            {
                Content = option,
                Style = TryFindResource("TcButton") as Style,
                Height = 32,
                MinWidth = Math.Max(116, button.ActualWidth + 20),
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(10, 4, 10, 4),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            item.Click += (_, _) =>
            {
                if (popup is not null)
                    popup.IsOpen = false;
                selected(option);
            };
            list.Children.Add(item);
        }

        var surface = new Border
        {
            MinWidth = Math.Max(122, button.ActualWidth + 24),
            Padding = new Thickness(5, 5, 5, 3),
            Margin = new Thickness(0, 5, 0, 0),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = list,
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = 0.32
            }
        };
        surface.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");
        surface.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");

        popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            Child = surface
        };
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_quickPopup, popup))
                _quickPopup = null;
        };
        _quickPopup = popup;
        popup.IsOpen = true;
    }

    private IReadOnlyList<string> BuildRefreshOptions()
    {
        if (_app is null)
            return ["Auto"];

        var values = new List<string> { "Auto" };
        IReadOnlyList<int> supported = _app.DisplayService.GetSupportedRefreshRates();
        if (supported.Contains(60))
            values.Add("60 Hz");
        if (supported.Count > 0 || _app.State.MaxRefreshHz > 0)
            values.Add("Max");
        return values;
    }

    private void SyncQuickControls()
    {
        if (!_quickControlsAdded || _app is null || _quickPerformance is null || _quickFan is null || _quickRefresh is null || _quickKeyboard is null)
            return;

        _syncingQuickControls = true;
        try
        {
            // Performance is Windows power behavior; it must not read like a fan
            // profile. The internal historical enum value Quiet is displayed as the
            // actual user concept: Efficiency.
            _quickPerformance.Content = QuickButtonContent(_app.State.SelectedModeDisplay);

            _quickFan.IsEnabled = _app.State.CanFanControl;
            _quickFan.Content = QuickButtonContent(_app.State.CoolingProfileDisplay);

            string refresh = _app.State.RefreshAutoEnabled
                ? "Auto"
                : _app.State.MaxRefreshHz > 0 && _app.State.CurrentRefreshHz == _app.State.MaxRefreshHz
                    ? "Max"
                    : _app.State.CurrentRefreshHz == 60
                        ? "60 Hz"
                        : _app.State.CurrentRefreshHz > 0 ? $"{_app.State.CurrentRefreshHz} Hz" : "Refresh";
            _quickRefresh.Content = QuickButtonContent(refresh);

            _quickKeyboard.IsEnabled = _app.State.CanKeyboardBacklight;
            string keyboard = _app.State.KeyboardMode == "Auto"
                ? "Auto"
                : _app.State.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase)
                    ? "Off"
                    : _app.State.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase)
                        ? "Low"
                        : _app.State.KeyboardStatus.Contains("High", StringComparison.OrdinalIgnoreCase)
                            ? "High"
                            : "Keyboard";
            _quickKeyboard.Content = QuickButtonContent(keyboard);
        }
        finally
        {
            _syncingQuickControls = false;
        }
    }

    private static FrameworkElement QuickButtonContent(string value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock
        {
            Text = value,
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var chevron = new TextBlock
        {
            Text = "⌄",
            FontSize = 10,
            Margin = new Thickness(6, -1, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        grid.Children.Add(text);
        Grid.SetColumn(chevron, 1);
        grid.Children.Add(chevron);
        return grid;
    }

    private void QuickPerformanceSelected(string raw)
    {
        if (_app is null)
            return;

        ThinkControlPowerMode mode = raw switch
        {
            "Efficiency" => ThinkControlPowerMode.Quiet,
            "Performance" => ThinkControlPowerMode.Performance,
            _ => ThinkControlPowerMode.Balanced
        };
        _app.SetPowerMode(mode);
        SyncQuickControls();
    }

    private async void QuickFanSelected(string raw)
    {
        if (_app is null || _quickFan is null)
            return;

        _quickFan.IsEnabled = false;
        try
        {
            if (raw == "Custom")
            {
                double[] curve = _app.UserSettings.Current.CustomFanThresholds ?? FanCurvePolicy.DefaultCustomThresholds.ToArray();
                await _app.SetCustomCoolingCurveAsync(curve);
            }
            else
            {
                await _app.SetCoolingProfileAsync(raw);
            }
        }
        finally
        {
            SyncQuickControls();
        }
    }

    private void QuickRefreshSelected(string raw)
    {
        if (_app is null)
            return;

        if (raw == "Auto")
        {
            _app.EnableRefreshAuto();
        }
        else if (raw == "Max")
        {
            int max = _app.State.MaxRefreshHz;
            if (max <= 0)
                max = _app.DisplayService.GetSupportedRefreshRates().DefaultIfEmpty(0).Max();
            if (max > 0)
                _app.SetRefresh(max);
        }
        else if (raw == "60 Hz")
        {
            _app.SetRefresh(60);
        }

        SyncQuickControls();
    }

    private async void QuickKeyboardSelected(string raw)
    {
        if (_app is null || _quickKeyboard is null)
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
