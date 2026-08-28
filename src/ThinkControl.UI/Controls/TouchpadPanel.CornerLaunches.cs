using System.Windows;
using System.Windows.Controls;
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

    private TouchpadGestureZoneOverlay? _gestureZoneOverlay;
    private ComboBox? _topLeftLaunchCombo;
    private ComboBox? _topRightLaunchCombo;
    private FrameworkElement? _edgeEditorCard;
    private bool _cornerLaunchUiConfigured;
    private bool _cornerHostSubscribed;
    private TouchpadCorner? _selectedLaunchCorner;

    private void ConfigureCornerLaunchUi()
    {
        if (_cornerLaunchUiConfigured)
            return;
        _cornerLaunchUiConfigured = true;

        // Launching a ThinkControl surface is intentionally no longer an edge
        // action. It has its own physical diagonal lane so continuous/media edge
        // gestures and launch gestures cannot pretend to own the same pixels.
        ActionCombo.ItemsSource = ActionCombo.Items.Cast<ActionOption>()
            .Where(option => option.Action != GestureActionKind.OpenThinkControl &&
                             option.Action != GestureActionKind.OpenAdvanced)
            .ToArray();

        _edgeEditorCard = SettingsStack.Children.Count > 0
            ? SettingsStack.Children[0] as FrameworkElement
            : null;

        if (Visualizer.Parent is Grid visualizerHost)
        {
            _gestureZoneOverlay = new TouchpadGestureZoneOverlay
            {
                Configuration = _configuration,
                Geometry = _host?.Geometry ?? DefaultGeometry(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Panel.SetZIndex(_gestureZoneOverlay, 7);
            _gestureZoneOverlay.CornerSelected += CornerZone_Selected;
            visualizerHost.Children.Add(_gestureZoneOverlay);
        }

        // Explicit editor selection may change which editor is shown. Runtime
        // corner candidates never do: live input must not collapse/re-expand a card
        // and feed another layout pass while the finger is moving.
        Visualizer.EdgeSelected += _ =>
        {
            _selectedLaunchCorner = null;
            SetCornerSelectionOwnership(false);
            SyncGestureZoneOverlay();
        };

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
            Text = "Start inside a diagonal corner guide and move deliberately inward. The guide uses the same physical trigger lane as recognition; taps and ordinary edge movement do nothing.",
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

    private void CornerZone_Selected(TouchpadCorner corner)
    {
        _selectedLaunchCorner = corner;
        SetCornerSelectionOwnership(true);
        SyncGestureZoneOverlay();

        ComboBox? combo = corner == TouchpadCorner.TopLeft ? _topLeftLaunchCombo : _topRightLaunchCombo;
        if (combo is not null)
        {
            combo.Focus();
            combo.IsDropDownOpen = true;
        }
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
        SetCornerSelectionOwnership(true);
        SyncGestureZoneOverlay();
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
        SetCornerSelectionOwnership(_selectedLaunchCorner is not null);
        SyncGestureZoneOverlay();
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

            if (_gestureZoneOverlay is not null)
                _gestureZoneOverlay.Signal = signal.Phase is GesturePhase.Released or GesturePhase.Cancelled ? null : signal;

            if (signal.Corner is TouchpadCorner corner)
            {
                bool live = signal.Phase is GesturePhase.Candidate or GesturePhase.Claimed or GesturePhase.Active;
                SetCornerLiveEmphasis(live);

                string cornerName = corner == TouchpadCorner.TopLeft ? "Top-left" : "Top-right";
                string action = signal.Action == GestureActionKind.OpenAdvanced ? "Advanced" : "Compact";
                GestureStatusText.Text = signal.Phase switch
                {
                    GesturePhase.Candidate => $"{cornerName} launch · continue diagonally inward for {action}",
                    GesturePhase.Claimed or GesturePhase.Active => $"{cornerName} launch · opening {action}",
                    GesturePhase.Cancelled => $"{cornerName} launch rejected · {signal.Reason}",
                    GesturePhase.Released => $"{cornerName} launch complete · {action}",
                    _ => GestureStatusText.Text
                };
            }
        }));
    }

    private void SetCornerSelectionOwnership(bool cornerOwns)
    {
        if (_edgeEditorCard is not null)
        {
            _edgeEditorCard.Opacity = 1;
            _edgeEditorCard.IsHitTestVisible = true;
            _edgeEditorCard.Visibility = cornerOwns ? Visibility.Collapsed : Visibility.Visible;
        }
        Visualizer.EdgeSelectionVisible = !cornerOwns;
    }

    private void SetCornerLiveEmphasis(bool live)
    {
        // Runtime ownership is visual-only. Never mutate card visibility here: doing
        // that on Candidate/Cancelled frames causes the whole Touchpad page to
        // remeasure while the user is touching it. Dim and disable the existing edge
        // editor in place so the corner remains the sole active interaction model.
        Visualizer.EdgeSelectionVisible = !live && _selectedLaunchCorner is null;
        if (_edgeEditorCard is not null && _edgeEditorCard.Visibility == Visibility.Visible)
        {
            _edgeEditorCard.Opacity = live ? 0.38 : 1;
            _edgeEditorCard.IsHitTestVisible = !live;
        }
    }

    private void SyncGestureZoneOverlay()
    {
        if (_gestureZoneOverlay is null)
            return;
        _gestureZoneOverlay.Configuration = _configuration;
        _gestureZoneOverlay.Geometry = _host?.Geometry ?? DefaultGeometry();
        _gestureZoneOverlay.SelectedCorner = _selectedLaunchCorner;
        _gestureZoneOverlay.Signal = _signal;
    }

    // Visual QA uses the real overlay instead of a snapshot-only imitation. This
    // helper only controls the live signal shown in that canonical overlay.
    private void RefreshCornerZoneVisuals(GestureSignal? signal)
    {
        if (_gestureZoneOverlay is null)
            return;
        _gestureZoneOverlay.Configuration = _configuration;
        _gestureZoneOverlay.Geometry = _host?.Geometry ?? DefaultGeometry();
        _gestureZoneOverlay.SelectedCorner = _selectedLaunchCorner;
        _gestureZoneOverlay.Signal = signal;
        SetCornerLiveEmphasis(signal?.Corner is not null);
    }
}
