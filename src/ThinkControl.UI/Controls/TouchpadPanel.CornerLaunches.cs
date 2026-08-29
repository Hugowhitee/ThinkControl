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
    private ComboBox? _cornerLaunchCombo;
    private CheckBox? _cornerReverseCloseCheckBox;
    private FrameworkElement? _edgeEditorCard;
    private Border? _cornerEditorCard;
    private TextBlock? _cornerEditorTitle;
    private TextBlock? _cornerEditorDescription;
    private bool _cornerLaunchUiConfigured;

    private void ConfigureCornerLaunchUi()
    {
        if (_cornerLaunchUiConfigured)
            return;
        _cornerLaunchUiConfigured = true;

        // Surface launches are owned by the two deliberate diagonal corner zones,
        // not by the four edge bindings. Runtime recognition remains separate, but
        // editor selection/rendering is one six-zone model in TouchpadVisualizer.
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
            visualizerHost.Children.Add(_gestureZoneOverlay);
        }

        _cornerEditorCard = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Visibility = Visibility.Collapsed
        };
        var stack = new StackPanel();
        _cornerEditorTitle = new TextBlock
        {
            Text = "Selected corner",
            FontWeight = FontWeights.SemiBold
        };
        stack.Children.Add(_cornerEditorTitle);

        _cornerEditorDescription = new TextBlock
        {
            Text = "Choose which ThinkControl surface this deliberate diagonal corner launch opens.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 12),
            FontSize = TypographyScale.Caption
        };
        _cornerEditorDescription.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(_cornerEditorDescription);

        var actionLabel = new TextBlock
        {
            Text = "Action",
            FontSize = TypographyScale.Secondary
        };
        actionLabel.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(actionLabel);

        _cornerLaunchCombo = new ComboBox
        {
            ItemsSource = CornerLaunchOptions,
            Style = TryFindResource("TcComboBox") as Style,
            Margin = new Thickness(0, 5, 0, 0)
        };
        _cornerLaunchCombo.SelectionChanged += CornerLaunchCombo_SelectionChanged;
        stack.Children.Add(_cornerLaunchCombo);

        _cornerReverseCloseCheckBox = new CheckBox
        {
            Content = "Reverse swipe closes ThinkControl",
            Margin = new Thickness(0, 14, 0, 0),
            FontSize = TypographyScale.Secondary
        };
        _cornerReverseCloseCheckBox.Checked += CornerReverseClose_Changed;
        _cornerReverseCloseCheckBox.Unchecked += CornerReverseClose_Changed;
        stack.Children.Add(_cornerReverseCloseCheckBox);

        var reverseDescription = new TextBlock
        {
            Text = "Start at the rounded inner end and swipe back toward the corner to hide Compact or Advanced.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 4, 0, 0),
            FontSize = TypographyScale.Caption
        };
        reverseDescription.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(reverseDescription);

        _cornerEditorCard.Child = stack;

        int insertIndex = Math.Min(1, SettingsStack.Children.Count);
        SettingsStack.Children.Insert(insertIndex, _cornerEditorCard);

        Loaded += (_, _) =>
        {
            SyncCornerLaunchControls();
            ApplySelectedZoneEditor();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                SyncCornerLaunchControls();
                ApplySelectedZoneEditor();
            }
            else
            {
                SetCornerLiveEmphasis(false);
                if (_gestureZoneOverlay is not null)
                    _gestureZoneOverlay.Signal = null;
            }
        };
    }

    private void CornerLaunchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _host is null ||
            sender is not ComboBox { Tag: TouchpadCorner corner, SelectedItem: CornerLaunchOption option })
        {
            return;
        }

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
        SyncCornerLaunchControls();
    }

    private void CornerReverseClose_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing || _host is null ||
            sender is not CheckBox { Tag: TouchpadCorner corner } checkBox)
        {
            return;
        }

        bool enabled = checkBox.IsChecked == true;
        TouchpadCornerLaunchBindings launches = _configuration.CornerLaunches ?? new TouchpadCornerLaunchBindings();
        launches = corner switch
        {
            TouchpadCorner.TopLeft => launches with { TopLeftReverseClose = enabled },
            TouchpadCorner.TopRight => launches with { TopRightReverseClose = enabled },
            _ => launches
        };
        _configuration = (_configuration with { CornerLaunches = launches }).Sanitize();
        _host.UpdateConfiguration(_configuration);
        Visualizer.Configuration = _configuration;
        SyncGestureZoneOverlay();
    }

    private void SyncCornerLaunchControls()
    {
        // _configuration is the panel's current state owner. SyncAll refreshes it
        // from the host before calling here; snapshot fixtures intentionally inject
        // deterministic bindings and must not be overwritten by a second host read.
        if (_cornerLaunchCombo is null)
            return;

        _syncing = true;
        try
        {
            if (_selectedZone.Corner is TouchpadCorner corner)
            {
                TouchpadCornerLaunchBindings launches = _configuration.CornerLaunches ?? new TouchpadCornerLaunchBindings();
                _cornerLaunchCombo.Tag = corner;
                _cornerLaunchCombo.SelectedItem = CornerLaunchOptions.First(option => option.Action == _configuration.LaunchFor(corner));
                if (_cornerReverseCloseCheckBox is not null)
                {
                    _cornerReverseCloseCheckBox.Tag = corner;
                    _cornerReverseCloseCheckBox.IsChecked = launches.ReverseCloseFor(corner);
                    _cornerReverseCloseCheckBox.IsEnabled = _configuration.LaunchFor(corner) != GestureActionKind.Disabled;
                }
            }
            else
            {
                _cornerLaunchCombo.Tag = null;
                _cornerLaunchCombo.SelectedItem = null;
                if (_cornerReverseCloseCheckBox is not null)
                {
                    _cornerReverseCloseCheckBox.Tag = null;
                    _cornerReverseCloseCheckBox.IsChecked = false;
                    _cornerReverseCloseCheckBox.IsEnabled = false;
                }
            }
        }
        finally
        {
            _syncing = false;
        }

        SyncGestureZoneOverlay();
    }

    private void ApplySelectedZoneEditor()
    {
        bool cornerSelected = _selectedZone.Corner is TouchpadCorner;
        if (_edgeEditorCard is not null)
        {
            _edgeEditorCard.Visibility = cornerSelected ? Visibility.Collapsed : Visibility.Visible;
            _edgeEditorCard.Opacity = 1;
            _edgeEditorCard.IsHitTestVisible = true;
        }

        if (_cornerEditorCard is null)
            return;

        _cornerEditorCard.Visibility = cornerSelected ? Visibility.Visible : Visibility.Collapsed;
        _cornerEditorCard.Opacity = 1;
        _cornerEditorCard.IsHitTestVisible = true;

        if (_selectedZone.Corner is not TouchpadCorner corner)
            return;

        if (_cornerEditorTitle is not null)
            _cornerEditorTitle.Text = corner == TouchpadCorner.TopLeft ? "Top-left corner" : "Top-right corner";
        if (_cornerEditorDescription is not null)
        {
            _cornerEditorDescription.Text = corner == TouchpadCorner.TopLeft
                ? "Start in the top-left corner guard and move deliberately along the arrow to launch the selected ThinkControl surface."
                : "Start in the top-right corner guard and move deliberately along the arrow to launch the selected ThinkControl surface.";
        }

        SyncCornerLaunchControls();
    }

    private void UpdateCornerGestureUi(GestureSignal signal)
    {
        bool live = signal.Corner is not null &&
                    signal.Phase is GesturePhase.Candidate or GesturePhase.Claimed or GesturePhase.Active;
        SetCornerLiveEmphasis(live);
        SyncGestureZoneOverlay();

        if (signal.Corner is not TouchpadCorner corner)
            return;

        string cornerName = corner == TouchpadCorner.TopLeft ? "Top-left" : "Top-right";
        bool closing = signal.CornerDirection == CornerGestureDirection.Outward;
        string action = signal.Action == GestureActionKind.OpenAdvanced ? "Advanced" : "Compact";
        GestureStatusText.Text = signal.Phase switch
        {
            GesturePhase.Candidate when closing => $"{cornerName} reverse · continue diagonally toward the corner to close",
            GesturePhase.Candidate => $"{cornerName} launch · continue diagonally inward for {action}",
            GesturePhase.Claimed or GesturePhase.Active when closing => $"{cornerName} reverse · closing ThinkControl",
            GesturePhase.Claimed or GesturePhase.Active => $"{cornerName} launch · opening {action}",
            GesturePhase.Cancelled when closing => $"{cornerName} reverse rejected · {signal.Reason}",
            GesturePhase.Cancelled => $"{cornerName} launch rejected · {signal.Reason}",
            GesturePhase.Released when closing => $"{cornerName} reverse complete · ThinkControl hidden",
            GesturePhase.Released => $"{cornerName} launch complete · {action}",
            _ => GestureStatusText.Text
        };
    }

    private void SetCornerLiveEmphasis(bool live)
    {
        // Runtime corner ownership is visual-only. Candidate/active frames must never
        // collapse or reveal an editor card, because that would remeasure the page
        // while the finger is moving (the alpha.33 regression boundary).
        FrameworkElement? selectedEditor = _selectedZone.Corner is not null
            ? _cornerEditorCard
            : _edgeEditorCard;
        if (selectedEditor is null || selectedEditor.Visibility != Visibility.Visible)
            return;

        selectedEditor.Opacity = live ? 0.38 : 1;
        selectedEditor.IsHitTestVisible = !live;
    }

    private void SyncGestureZoneOverlay()
    {
        if (_gestureZoneOverlay is null)
            return;
        _gestureZoneOverlay.Configuration = _configuration;
        _gestureZoneOverlay.Geometry = _host?.Geometry ?? DefaultGeometry();
        _gestureZoneOverlay.Signal = _signal;
    }

    private void RefreshGestureZoneVisuals(GestureSignal? signal)
    {
        if (_gestureZoneOverlay is not null)
        {
            _gestureZoneOverlay.Configuration = _configuration;
            _gestureZoneOverlay.Geometry = _host?.Geometry ?? DefaultGeometry();
            _gestureZoneOverlay.Signal = signal;
        }
        SetCornerLiveEmphasis(signal?.Corner is not null &&
                              signal.Phase is GesturePhase.Candidate or GesturePhase.Claimed or GesturePhase.Active);
    }
}
