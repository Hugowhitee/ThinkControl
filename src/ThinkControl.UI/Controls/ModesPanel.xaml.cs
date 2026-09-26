using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ThinkControl.Core.Audio;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class ModesPanel : UserControl
{
    private App? _app;
    private bool _busy;
    private string? _editingId;
    private string? _editingAudioSafety;
    private bool? _editingTouchpadGestures;
    private string? _editingKeyboardLight;
    private TextBlock _modifiedLabel = null!;
    private Button _reapplyButton = null!;
    private Button _cancelButton = null!;
    private Button _saveButton = null!;

    public ModesPanel()
    {
        InitializeComponent();
        BuildHeaderActions();
    }

    private void BuildHeaderActions()
    {
        _modifiedLabel = new TextBlock
        {
            Text = "Modified",
            FontSize = TypographyScale.Caption,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Visibility = Visibility.Collapsed
        };
        _modifiedLabel.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _reapplyButton = HeaderButton("Reapply", Reapply_Click, new Thickness(9, 4, 9, 4));
        _reapplyButton.Visibility = Visibility.Collapsed;
        _cancelButton = HeaderButton("Cancel", Cancel_Click, new Thickness(9, 4, 9, 4));
        _cancelButton.Visibility = Visibility.Collapsed;
        _saveButton = HeaderButton("Save", Save_Click, new Thickness(10, 4, 10, 4));
        _saveButton.Margin = new Thickness(10, 0, 0, 0);
        _saveButton.Visibility = Visibility.Collapsed;

        StackPanel rail = Header.EnsureActionStack();
        rail.Children.Add(_modifiedLabel);
        rail.Children.Add(_reapplyButton);
        rail.Children.Add(_cancelButton);
        rail.Children.Add(_saveButton);
    }

    private Button HeaderButton(string content, RoutedEventHandler handler, Thickness padding)
    {
        var button = new Button
        {
            Content = content,
            Style = TryFindResource("TcButton") as Style,
            Padding = padding,
            FontSize = TypographyScale.Caption
        };
        button.Click += handler;
        return button;
    }

    internal void Initialize(App app)
    {
        if (ReferenceEquals(_app, app))
        {
            RefreshList();
            return;
        }

        if (_app is not null)
            _app.Modes.Changed -= Modes_Changed;

        _app = app;
        _app.Modes.Changed += Modes_Changed;
        RefreshList();
    }

    private void Modes_Changed()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Modes_Changed));
            return;
        }

        UpdateHeaderState();
        if (EditorView.Visibility != Visibility.Visible)
            RefreshList();
    }

    private void RefreshList()
    {
        if (_app is null)
            return;

        IReadOnlyList<ThinkControlModeDefinition> modes = _app.Modes.GetModes();
        BuiltInRows.Children.Clear();
        CustomRows.Children.Clear();

        foreach (ThinkControlModeDefinition mode in modes.Where(mode =>
                     !mode.Id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)))
        {
            BuiltInRows.Children.Add(CreateModeRow(mode, editable: false));
        }

        ThinkControlModeDefinition[] customs = modes
            .Where(mode => mode.Id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (ThinkControlModeDefinition mode in customs)
            CustomRows.Children.Add(CreateModeRow(mode, editable: true));

        EmptyCustomText.Visibility = customs.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        NewModeButton.IsEnabled = customs.Length < ThinkControlModeCatalog.MaxCustomModes;
        UpdateHeaderState();
    }

    private Border CreateModeRow(ThinkControlModeDefinition mode, bool editable)
    {
        var row = new Grid { MinHeight = 58 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        copy.Children.Add(new TextBlock
        {
            Text = mode.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = TypographyScale.ControlLabel
        });
        var summary = new TextBlock
        {
            Text = ThinkControlModeCatalog.Summary(mode),
            FontSize = TypographyScale.Caption,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(summary);
        row.Children.Add(copy);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (editable)
        {
            var edit = new Button
            {
                Content = "Edit",
                Tag = mode.Id,
                Style = TryFindResource("TcInlineButton") as Style,
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 0, 7, 0)
            };
            edit.Click += Edit_Click;
            actions.Children.Add(edit);
        }

        bool active = _app is not null &&
                      _app.Modes.ActiveModeId.Equals(mode.Id, StringComparison.OrdinalIgnoreCase);
        if (active)
        {
            var activeText = new TextBlock
            {
                Text = "Active",
                FontSize = TypographyScale.Caption,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0)
            };
            activeText.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Accent");
            actions.Children.Add(activeText);
        }
        else
        {
            var activate = new Button
            {
                Content = "Activate",
                Tag = mode.Id,
                Style = TryFindResource("TcButton") as Style,
                Padding = new Thickness(9, 4, 9, 4),
                FontSize = TypographyScale.Caption
            };
            activate.Click += Activate_Click;
            actions.Children.Add(activate);
        }

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);

        var shell = new Border
        {
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 10, 14, 10),
            Child = row
        };
        shell.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        return shell;
    }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _app is null || sender is not FrameworkElement { Tag: string id })
            return;

        _busy = true;
        ListStatusText.Visibility = Visibility.Collapsed;
        ListView.IsEnabled = false;
        try
        {
            bool success = await _app.Modes.ActivateAsync(id);
            if (!success)
                ShowStatus("Mode could not be applied.");
        }
        finally
        {
            _busy = false;
            ListView.IsEnabled = true;
            RefreshList();
        }
    }

    private void NewMode_Click(object sender, RoutedEventArgs e)
    {
        BeginEdit(new ThinkControlModeDefinition(
            "custom:" + Guid.NewGuid().ToString("N"),
            string.Empty));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not FrameworkElement { Tag: string id })
            return;

        ThinkControlModeDefinition? mode = _app.Modes.GetModes().FirstOrDefault(item =>
            item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (mode is not null)
            BeginEdit(mode);
    }

    private void BeginEdit(ThinkControlModeDefinition mode)
    {
        _editingId = mode.Id;
        _editingAudioSafety = mode.AudioSafety;
        _editingTouchpadGestures = mode.TouchpadGesturesEnabled;
        _editingKeyboardLight = mode.KeyboardLight;

        ModeNameTextBox.Text = mode.Name;
        EditorStatusText.Visibility = Visibility.Collapsed;
        DeleteButton.Visibility = _app is not null &&
                                  _app.UserSettings.Current.CustomModes?.Any(item =>
                                      item.Id.Equals(mode.Id, StringComparison.OrdinalIgnoreCase)) == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeleteButton.IsEnabled = _app is null ||
                                 !_app.Modes.ActiveModeId.Equals(mode.Id, StringComparison.OrdinalIgnoreCase);

        ListView.Visibility = Visibility.Collapsed;
        EditorView.Visibility = Visibility.Visible;
        _saveButton.Visibility = Visibility.Visible;
        _cancelButton.Visibility = Visibility.Visible;
        _modifiedLabel.Visibility = Visibility.Collapsed;
        _reapplyButton.Visibility = Visibility.Collapsed;
        BuildEditorControls();
        ModeNameTextBox.Focus();
    }

    private void BuildEditorControls()
    {
        EditorControls.Children.Clear();

        if (_editingAudioSafety is not null)
        {
            EditorControls.Children.Add(CreateFacetRow(
                ThinkControlModeFacet.AudioSafety,
                "Audio safety",
                ["Normal", "Gesture lock", "Silent"],
                _editingAudioSafety switch
                {
                    "GestureLock" => "Gesture lock",
                    "Silent" => "Silent",
                    _ => "Normal"
                }));
        }

        if (_editingTouchpadGestures.HasValue)
        {
            EditorControls.Children.Add(CreateFacetRow(
                ThinkControlModeFacet.TouchpadGestures,
                "Touchpad gestures",
                ["On", "Off"],
                _editingTouchpadGestures.Value ? "On" : "Off"));
        }

        if (_editingKeyboardLight is not null)
        {
            EditorControls.Children.Add(CreateFacetRow(
                ThinkControlModeFacet.KeyboardLight,
                "Keyboard light",
                ["Off", "Low", "High", "Auto"],
                _editingKeyboardLight));
        }

        EditorEmptyText.Visibility = EditorControls.Children.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        AddControlButton.IsEnabled = EditorControls.Children.Count < 3;
    }

    private Border CreateFacetRow(
        ThinkControlModeFacet facet,
        string label,
        IReadOnlyList<string> values,
        string selected)
    {
        var grid = new Grid { MinHeight = 46 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(name);

        var combo = new ComboBox
        {
            Tag = facet,
            ItemsSource = values,
            SelectedItem = selected,
            Style = TryFindResource("TcComboBox") as Style,
            MinHeight = 38,
            VerticalAlignment = VerticalAlignment.Center
        };
        combo.SelectionChanged += FacetValue_SelectionChanged;
        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);

        var remove = new Button
        {
            Tag = facet,
            Content = "Remove",
            Style = TryFindResource("TcInlineButton") as Style,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        remove.Click += RemoveFacet_Click;
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        var shell = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 0, 8),
            Child = grid
        };
        shell.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        return shell;
    }

    private void FacetValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { Tag: ThinkControlModeFacet facet, SelectedItem: string value })
            return;

        switch (facet)
        {
            case ThinkControlModeFacet.AudioSafety:
                _editingAudioSafety = value switch
                {
                    "Gesture lock" => "GestureLock",
                    "Silent" => "Silent",
                    _ => "Normal"
                };
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _editingTouchpadGestures = value == "On";
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _editingKeyboardLight = value;
                break;
        }
    }

    private void AddControl_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = AddControlButton,
            Placement = PlacementMode.Bottom
        };

        AddFacetMenuItem(menu, ThinkControlModeFacet.AudioSafety, "Audio safety", _editingAudioSafety is null);
        AddFacetMenuItem(menu, ThinkControlModeFacet.TouchpadGestures, "Touchpad gestures", !_editingTouchpadGestures.HasValue);
        AddFacetMenuItem(menu, ThinkControlModeFacet.KeyboardLight, "Keyboard light", _editingKeyboardLight is null);

        AddControlButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AddFacetMenuItem(
        ContextMenu menu,
        ThinkControlModeFacet facet,
        string label,
        bool available)
    {
        if (!available)
            return;

        var item = new MenuItem { Header = label, Tag = facet };
        item.Click += AddFacet_Click;
        menu.Items.Add(item);
    }

    private void AddFacet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ThinkControlModeFacet facet })
            return;

        switch (facet)
        {
            case ThinkControlModeFacet.AudioSafety:
                _editingAudioSafety = _app?.AudioSafety.Mode switch
                {
                    AudioSafetyMode.MediaLock => "GestureLock",
                    AudioSafetyMode.Silent => "Silent",
                    _ => "Normal"
                };
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _editingTouchpadGestures = _app?.GetEffectiveTouchpadGesturesEnabled() ?? true;
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _editingKeyboardLight = CurrentKeyboardLight();
                break;
        }

        BuildEditorControls();
    }

    private string CurrentKeyboardLight()
    {
        if (_app is null)
            return "Auto";
        if (_app.State.KeyboardMode == "Auto")
            return "Auto";
        if (_app.State.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase))
            return "Off";
        if (_app.State.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase))
            return "Low";
        return "High";
    }

    private void RemoveFacet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ThinkControlModeFacet facet })
            return;

        switch (facet)
        {
            case ThinkControlModeFacet.AudioSafety:
                _editingAudioSafety = null;
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _editingTouchpadGestures = null;
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _editingKeyboardLight = null;
                break;
        }

        BuildEditorControls();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || string.IsNullOrWhiteSpace(_editingId))
            return;

        string name = ModeNameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowEditorStatus("Enter a mode name.");
            return;
        }

        if (_editingAudioSafety is null &&
            !_editingTouchpadGestures.HasValue &&
            _editingKeyboardLight is null)
        {
            ShowEditorStatus("Add at least one control.");
            return;
        }

        bool saved = _app.Modes.SaveCustomMode(new ThinkControlModeDefinition(
            _editingId,
            name,
            _editingAudioSafety,
            _editingTouchpadGestures,
            _editingKeyboardLight));
        if (!saved)
        {
            ShowEditorStatus("That mode could not be saved.");
            return;
        }

        EndEdit();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => EndEdit();

    private void EndEdit()
    {
        _editingId = null;
        _editingAudioSafety = null;
        _editingTouchpadGestures = null;
        _editingKeyboardLight = null;
        EditorView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
        _saveButton.Visibility = Visibility.Collapsed;
        _cancelButton.Visibility = Visibility.Collapsed;
        EditorStatusText.Visibility = Visibility.Collapsed;
        RefreshList();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || string.IsNullOrWhiteSpace(_editingId))
            return;

        if (_app.Modes.ActiveModeId.Equals(_editingId, StringComparison.OrdinalIgnoreCase))
        {
            ShowEditorStatus("Switch modes before deleting this one.");
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            $"Delete '{ModeNameTextBox.Text.Trim()}'?",
            "ThinkControl · Delete mode",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        if (_app.Modes.DeleteCustomMode(_editingId))
            EndEdit();
        else
            ShowEditorStatus("Mode could not be deleted.");
    }

    private async void Reapply_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _app is null)
            return;

        _busy = true;
        ListStatusText.Visibility = Visibility.Collapsed;
        _reapplyButton.IsEnabled = false;
        try
        {
            bool success = await _app.Modes.ReapplyAsync();
            if (!success)
                ShowStatus("Mode could not be reapplied.");
        }
        finally
        {
            _busy = false;
            _reapplyButton.IsEnabled = true;
            RefreshList();
        }
    }

    private void UpdateHeaderState()
    {
        if (_app is null || EditorView.Visibility == Visibility.Visible)
            return;

        bool modified = _app.Modes.IsModified;
        _modifiedLabel.Visibility = modified ? Visibility.Visible : Visibility.Collapsed;
        _reapplyButton.Visibility = modified ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(string message)
    {
        if (EditorView.Visibility == Visibility.Visible)
        {
            EditorStatusText.Text = message;
            EditorStatusText.Visibility = Visibility.Visible;
            return;
        }

        ListStatusText.Text = message;
        ListStatusText.Visibility = Visibility.Visible;
    }

    private void ShowEditorStatus(string message) => ShowStatus(message);

    internal void PrepareEditorForSnapshot()
    {
        BeginEdit(new ThinkControlModeDefinition(
            "custom:snapshot",
            "Exam",
            AudioSafety: "Silent",
            TouchpadGesturesEnabled: false,
            KeyboardLight: "Off"));
    }
}
