using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    private sealed record CornerLaunchOption(GestureActionKind Action, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly CornerLaunchOption[] CornerLaunchOptions =
    [
        new(GestureActionKind.Disabled, "Off"),
        new(GestureActionKind.OpenThinkControl, "Compact"),
        new(GestureActionKind.OpenAdvanced, "Advanced")
    ];

    private Button? _topLeftLaunchZone;
    private Button? _topRightLaunchZone;
    private ComboBox? _topLeftLaunchCombo;
    private ComboBox? _topRightLaunchCombo;
    private bool _cornerLaunchUiConfigured;
    private bool _cornerHostSubscribed;
    private TouchpadCorner? _selectedLaunchCorner;

    private void ConfigureCornerLaunchUi()
    {
        if (_cornerLaunchUiConfigured)
            return;
        _cornerLaunchUiConfigured = true;

        // Launching a ThinkControl surface is intentionally no longer an edge
        // action. It has its own spatial affordance so the four precision edge bars
        // remain reserved for continuous/media controls.
        ActionCombo.ItemsSource = ActionCombo.Items.Cast<ActionOption>()
            .Where(option => option.Action is not GestureActionKind.OpenThinkControl and not GestureActionKind.OpenAdvanced)
            .ToArray();

        if (Visualizer.Parent is Grid visualizerHost)
        {
            _topLeftLaunchZone = BuildCornerZone(TouchpadCorner.TopLeft);
            _topRightLaunchZone = BuildCornerZone(TouchpadCorner.TopRight);
            visualizerHost.Children.Add(_topLeftLaunchZone);
            visualizerHost.Children.Add(_topRightLaunchZone);
        }

        var card = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Corner launches",
            FontWeight = FontWeights.SemiBold
        });
        var description = new TextBlock
        {
            Text = "Optional quick launch zones. Start inside a top corner and swipe diagonally inward; taps, normal scrolling and along-edge movement do nothing.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 11),
            FontSize = TypographyScale.Caption
        };
        description.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(description);

        _topLeftLaunchCombo = BuildCornerSelector(stack, "Top-left", TouchpadCorner.TopLeft);
        _topRightLaunchCombo = BuildCornerSelector(stack, "Top-right", TouchpadCorner.TopRight, topMargin: 8);
        card.Child = stack;

        int insertIndex = Math.Min(1, SettingsStack.Children.Count);
        SettingsStack.Children.Insert(insertIndex, card);

        Loaded += (_, _) =>
        {
            AttachCornerHost();
            SyncCornerLaunchControls();
        };
        Unloaded += (_, _) => DetachCornerHost();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                AttachCornerHost();
                SyncCornerLaunchControls();
            }
        };
    }

    private Button BuildCornerZone(TouchpadCorner corner)
    {
        var zone = new Button
        {
            Width = 52,
            Height = 40,
            Style = TryFindResource("TcButton") as Style,
            Padding = new Thickness(0),
            Focusable = false,
            ToolTip = corner == TouchpadCorner.TopLeft ? "Configure top-left launch" : "Configure top-right launch",
            HorizontalAlignment = corner == TouchpadCorner.TopLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = corner == TouchpadCorner.TopLeft
                ? new Thickness(22, 16, 0, 0)
                : new Thickness(0, 16, 22, 0),
            Tag = corner,
            Panel = { ZIndex = 7 }
        };
        zone.Click += CornerZone_Click;
        zone.Content = BuildLaunchGlyph(zone, GestureActionKind.Disabled);
        return zone;
    }

    private static FrameworkElement BuildLaunchGlyph(Control owner, GestureActionKind action)
    {
        Geometry geometry = action switch
        {
            GestureActionKind.OpenThinkControl => Geometry.Parse("M2,3 L14,3 L14,12 L2,12 Z M5,9 L11,9"),
            GestureActionKind.OpenAdvanced => Geometry.Parse("M1.5,2.5 L14.5,2.5 L14.5,13 L1.5,13 Z M5,2.5 L5,13 M7.5,5 L12,5 M7.5,8 L12,8"),
            _ => Geometry.Parse("M3,3 L8,8 L13,3 M8,8 L8,13")
        };
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Width = 17,
            Height = 17,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.45,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding(nameof(Control.Foreground)) { Source = owner });
        return path;
    }

    private ComboBox BuildCornerSelector(StackPanel parent, string labelText, TouchpadCorner corner, double topMargin = 0)
    {
        var row = new Grid { Margin = new Thickness(0, topMargin, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(154) });
        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = TypographyScale.Secondary
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        row.Children.Add(label);

        var combo = new ComboBox
        {
            ItemsSource = CornerLaunchOptions,
            Style = TryFindResource("TcComboBox") as Style,
            Tag = corner
        };
        combo.SelectionChanged += CornerLaunchCombo_SelectionChanged;
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        parent.Children.Add(row);
        return combo;
    }

    private void CornerZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TouchpadCorner corner })
            return;
        _selectedLaunchCorner = corner;
        RefreshCornerZoneVisuals(null);

        ComboBox? combo = corner == TouchpadCorner.TopLeft ? _topLeftLaunchCombo : _topRightLaunchCombo;
        if (combo is not null)
        {
            combo.Focus();
            combo.IsDropDownOpen = true;
        }
        e.Handled = true;
    }

    private void CornerLaunchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _host is null || sender is not ComboBox { Tag: TouchpadCorner corner, SelectedItem: CornerLaunchOption option })
            return;

        TouchpadCornerLaunchBindings launches = _configuration.CornerLaunches ?? new TouchpadCornerLaunchBindings();
        launches = corner switch
        {
            TouchpadCorner.TopLeft => launches with { TopLeft = option.Action },
            TouchpadCorner.TopRight => launches with { TopRight = option.Action },
            _ => launches
        };
        _configuration = (_configuration with { CornerLaunches = launches }).Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        _selectedLaunchCorner = corner;
        RefreshCornerZoneVisuals(null);
    }

    private void SyncCornerLaunchControls()
    {
        if (_host is not null)
            _configuration = _host.Configuration.Sanitize();

        _syncing = true;
        try
        {
            if (_topLeftLaunchCombo is not null)
                _topLeftLaunchCombo.SelectedItem = CornerLaunchOptions.First(option => option.Action == _configuration.LaunchFor(TouchpadCorner.TopLeft));
            if (_topRightLaunchCombo is not null)
                _topRightLaunchCombo.SelectedItem = CornerLaunchOptions.First(option => option.Action == _configuration.LaunchFor(TouchpadCorner.TopRight));
        }
        finally
        {
            _syncing = false;
        }
        RefreshCornerZoneVisuals(null);
    }

    private void AttachCornerHost()
    {
        if (_cornerHostSubscribed || _host is null)
            return;
        _host.GestureChanged += CornerHost_GestureChanged;
        _cornerHostSubscribed = true;
    }

    private void DetachCornerHost()
    {
        if (!_cornerHostSubscribed || _host is null)
            return;
        _host.GestureChanged -= CornerHost_GestureChanged;
        _cornerHostSubscribed = false;
    }

    private void CornerHost_GestureChanged(GestureSignal signal)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible)
                return;
            RefreshCornerZoneVisuals(signal);
            if (signal.Corner is TouchpadCorner corner)
            {
                string cornerName = corner == TouchpadCorner.TopLeft ? "Top-left" : "Top-right";
                string action = signal.Action == GestureActionKind.OpenAdvanced ? "Advanced" : "Compact";
                GestureStatusText.Text = signal.Phase switch
                {
                    GesturePhase.Candidate => $"{cornerName} launch · swipe diagonally inward for {action}",
                    GesturePhase.Claimed or GesturePhase.Active => $"{cornerName} launch · opening {action}",
                    GesturePhase.Cancelled => $"{cornerName} launch rejected · {signal.Reason}",
                    GesturePhase.Released => $"{cornerName} launch complete · {action}",
                    _ => GestureStatusText.Text
                };
            }
        }));
    }

    private void RefreshCornerZoneVisuals(GestureSignal? signal)
    {
        ApplyCornerZoneVisual(_topLeftLaunchZone, TouchpadCorner.TopLeft, signal);
        ApplyCornerZoneVisual(_topRightLaunchZone, TouchpadCorner.TopRight, signal);
    }

    private void ApplyCornerZoneVisual(Button? zone, TouchpadCorner corner, GestureSignal? signal)
    {
        if (zone is null)
            return;

        GestureActionKind action = _configuration.LaunchFor(corner);
        bool enabled = action != GestureActionKind.Disabled;
        bool selected = _selectedLaunchCorner == corner;
        bool live = signal?.Corner == corner && signal.Phase is GesturePhase.Candidate or GesturePhase.Claimed or GesturePhase.Active;

        zone.Opacity = enabled ? 0.92 : 0.58;
        zone.SetResourceReference(Control.BackgroundProperty, enabled || selected ? "Tc.SurfaceHover" : "Tc.Surface");
        zone.SetResourceReference(Control.BorderBrushProperty, live ? "Tc.Accent" : selected ? "Tc.TextMuted" : "Tc.BorderStrong");
        zone.SetResourceReference(Control.ForegroundProperty, live ? "Tc.Accent" : enabled ? "Tc.TextMuted" : "Tc.TextFaint");
        zone.Content = BuildLaunchGlyph(zone, action);
    }
}