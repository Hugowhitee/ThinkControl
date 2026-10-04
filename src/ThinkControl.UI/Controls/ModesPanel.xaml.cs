using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class ModesPanel : UserControl
{
    private sealed record TriggerBinding(int Index, string Field);

    private App? _app;
    private bool _busy;
    private bool _syncingModeSelection;
    private string? _editingId;
    private string? _editingPerformanceMode;
    private string? _editingCoolingProfile;
    private string? _editingRefreshRate;
    private string? _editingAudioSafety;
    private bool? _editingTouchpadGestures;
    private string? _editingKeyboardLight;
    private readonly List<ThinkControlModeTrigger> _editingTriggers = [];
    private bool _editingAutomationEnabled;
    private bool _editingMatchAllTriggers;
    private int _editingAutomationPriority;

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
            FontSize = TypographyScale.Caption,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Visibility = Visibility.Collapsed
        };
        _modifiedLabel.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        _reapplyButton = HeaderButton("Reapply", Reapply_Click);
        _reapplyButton.Visibility = Visibility.Collapsed;

        _cancelButton = HeaderButton("Cancel", Cancel_Click);
        _cancelButton.Visibility = Visibility.Collapsed;

        _saveButton = HeaderButton("Save & apply", Save_Click);
        _saveButton.Margin = new Thickness(10, 0, 0, 0);
        _saveButton.Visibility = Visibility.Collapsed;

        StackPanel rail = Header.EnsureActionStack();
        rail.Children.Add(_modifiedLabel);
        rail.Children.Add(_reapplyButton);
        rail.Children.Add(_cancelButton);
        rail.Children.Add(_saveButton);
    }

    private Button HeaderButton(string content, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = content,
            Style = TryFindResource("TcButton") as Style,
            Padding = new Thickness(9, 4, 9, 4),
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
        ModeRows.Children.Clear();

        foreach (ThinkControlModeDefinition mode in modes.Where(mode =>
                     mode.Id != ThinkControlModeCatalog.NormalId))
            ModeRows.Children.Add(CreateModeRow(mode));

        _syncingModeSelection = true;
        try
        {
            ModeSelector.ItemsSource = modes;
            ModeSelector.SelectedItem = modes.FirstOrDefault(mode =>
                mode.Id.Equals(_app.Modes.VisibleModeId, StringComparison.OrdinalIgnoreCase));
            ModeSelector.IsEnabled = !_busy && !_app.Modes.IsTransitioning;
        }
        finally
        {
            _syncingModeSelection = false;
        }

        int customCount = modes.Count(mode =>
            mode.Id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase));
        EmptyModesText.Visibility = customCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        NewModeButton.IsEnabled = customCount < ThinkControlModeCatalog.MaxCustomModes;
        UpdateHeaderState();
    }

    private Border CreateModeRow(ThinkControlModeDefinition mode)
    {
        bool editable = mode.Id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase);
        bool active = _app is not null &&
                      _app.Modes.ActiveModeId.Equals(mode.Id, StringComparison.OrdinalIgnoreCase);
        var row = new Grid { MinHeight = 50 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        copy.Children.Add(new TextBlock
        {
            Text = mode.Name,
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = TypographyScale.ControlLabel
        });
        TextBlock summary = MutedText(ThinkControlModeCatalog.Summary(mode));
        summary.Margin = new Thickness(0, 3, 0, 0);
        copy.Children.Add(summary);
        string automation = ThinkControlModeCatalog.AutomationSummary(mode);
        if (automation.Length > 0)
        {
            TextBlock note = MutedText(automation);
            note.Margin = new Thickness(0, 2, 0, 0);
            copy.Children.Add(note);
        }
        row.Children.Add(copy);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        if (active)
            actions.Children.Add(StateText(_app?.Modes.IsModified == true ? "Modified" : "Active"));
        if (editable)
        {
            Button edit = InlineButton("Edit", Edit_Click, mode.Id);
            edit.Margin = new Thickness(8, 0, 0, 0);
            edit.IsEnabled = _app?.Modes.IsTransitioning != true;
            actions.Children.Add(edit);
        }
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);

        var shell = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 8, 14, 8), Child = row };
        shell.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        if (active)
            shell.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");
        return shell;
    }

    private TextBlock StateText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypographyScale.Caption,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 4, 0)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Accent");
        return block;
    }

    private TextBlock MutedText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        return block;
    }

    private Button InlineButton(string content, RoutedEventHandler handler, object? tag = null)
    {
        var button = new Button
        {
            Content = content,
            Tag = tag,
            Style = TryFindResource("TcInlineButton") as Style,
            Padding = new Thickness(7, 4, 7, 4)
        };
        button.Click += handler;
        return button;
    }

    private async void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingModeSelection || _busy || _app is null ||
            ModeSelector.SelectedItem is not ThinkControlModeDefinition mode ||
            (mode.Id.Equals(_app.Modes.ActiveModeId, StringComparison.OrdinalIgnoreCase) &&
             !_app.Modes.IsModified))
            return;

        _busy = true;
        ListStatusText.Visibility = Visibility.Collapsed;
        ModeSelector.IsEnabled = false;
        try
        {
            if (!await _app.Modes.ActivateAsync(mode.Id))
                ShowListStatus(_app.Modes.LastTransitionError ?? "The mode could not be applied.");
        }
        finally
        {
            _busy = false;
            RefreshList();
        }
    }

    private void NewMode_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = NewModeButton,
            Placement = PlacementMode.Bottom
        };
        AddTemplateItem(menu, "Blank mode", "blank");
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "From template", IsEnabled = false });
        AddTemplateItem(menu, "Focus", "focus");
        AddTemplateItem(menu, "Battery saver", "battery");
        AddTemplateItem(menu, "Performance", "performance");
        NewModeButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AddTemplateItem(ContextMenu menu, string label, string id)
    {
        var item = new MenuItem { Header = label, Tag = id };
        item.Click += NewTemplate_Click;
        menu.Items.Add(item);
    }

    private void NewTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string template })
            BeginNewTemplate(template);
    }

    private void BeginNewTemplate(string template)
    {
        string id = "custom:" + Guid.NewGuid().ToString("N");
        BeginEdit(ThinkControlModeCatalog.CreateStarterTemplate(template, id));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
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
        _editingPerformanceMode = mode.PerformanceMode;
        _editingCoolingProfile = mode.CoolingProfile;
        _editingRefreshRate = mode.RefreshRate;
        _editingAudioSafety = mode.AudioSafety;
        _editingTouchpadGestures = mode.TouchpadGesturesEnabled;
        _editingKeyboardLight = mode.KeyboardLight;
        _editingTriggers.Clear();
        _editingTriggers.AddRange(mode.Triggers ?? []);
        _editingAutomationEnabled = mode.AutomationEnabled;
        _editingMatchAllTriggers = mode.MatchAllTriggers;
        _editingAutomationPriority = mode.AutomationPriority;
        TriggerMatchCombo.SelectedIndex = _editingMatchAllTriggers ? 1 : 0;
        TriggerPriorityCombo.SelectedIndex = _editingAutomationPriority + 1;

        ModeNameTextBox.Text = mode.Name;
        AutomationSwitch.IsChecked = _editingAutomationEnabled;
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

        BuildEditorSettings();
        BuildEditorTriggers();
        UpdateAutomationState();
        ModeNameTextBox.Focus();
    }

    private void BuildEditorSettings()
    {
        EditorSettings.Children.Clear();

        if (_editingPerformanceMode is not null)
            EditorSettings.Children.Add(CreateSettingRow(
                ThinkControlModeFacet.PerformanceMode,
                "Performance",
                ["Efficiency", "Balanced", "Performance"],
                _editingPerformanceMode));

        if (_editingCoolingProfile is not null)
            EditorSettings.Children.Add(CreateSettingRow(
                ThinkControlModeFacet.CoolingProfile,
                "Cooling",
                BuildCoolingValues(),
                _editingCoolingProfile));

        if (_editingRefreshRate is not null)
            EditorSettings.Children.Add(CreateSettingRow(
                ThinkControlModeFacet.RefreshRate,
                "Refresh rate",
                BuildRefreshValues(_editingRefreshRate),
                _editingRefreshRate));

        if (_editingAudioSafety is not null)
            EditorSettings.Children.Add(CreateSettingRow(
                ThinkControlModeFacet.AudioSafety,
                "Audio",
                ["Normal", "Gesture lock", "Silent"],
                _editingAudioSafety switch
                {
                    "GestureLock" => "Gesture lock",
                    "Silent" => "Silent",
                    _ => "Normal"
                }));

        if (_editingTouchpadGestures.HasValue)
            EditorSettings.Children.Add(CreateSettingRow(
                ThinkControlModeFacet.TouchpadGestures,
                "Touchpad gestures",
                ["On", "Off"],
                _editingTouchpadGestures.Value ? "On" : "Off"));

        if (_editingKeyboardLight is not null)
            EditorSettings.Children.Add(CreateSettingRow(
                ThinkControlModeFacet.KeyboardLight,
                "Keyboard light",
                ["Off", "Low", "High", "Auto"],
                _editingKeyboardLight));

        AddSettingButton.IsEnabled = EditorSettings.Children.Count < 6;
    }

    private IReadOnlyList<string> BuildCoolingValues()
    {
        var values = new List<string> { "Lenovo Auto", "Quiet", "Balanced", "Max cooling" };
        if (_app is not null &&
            !string.Equals(_app.State.FanControlKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal))
        {
            values.AddRange(_app.FanProfiles.GetProfiles()
                .Where(profile => !_app.FanProfiles.IsBuiltIn(profile.Id))
                .Select(profile => profile.Name));
        }
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<string> BuildRefreshValues(string? selected = null)
    {
        var values = new List<string> { "Auto" };
        if (_app is null)
            values.AddRange(["60 Hz", "Max"]);
        else
        {
            IReadOnlyList<int> supported = _app.DisplayService.GetSupportedRefreshRates();
            if (supported.Contains(60))
                values.Add("60 Hz");
            if (supported.Count > 0 || _app.State.MaxRefreshHz > 0)
                values.Add("Max");
        }

        if (!string.IsNullOrWhiteSpace(selected) &&
            !values.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(selected);
        }

        return values;
    }

    private Border CreateSettingRow(
        ThinkControlModeFacet facet,
        string label,
        IReadOnlyList<string> values,
        string selected)
    {
        var grid = new Grid { MinHeight = 48 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        grid.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        });

        var combo = new ComboBox
        {
            Tag = facet,
            ItemsSource = values,
            SelectedItem = selected,
            Style = TryFindResource("TcComboBox") as Style,
            MinHeight = 38,
            VerticalAlignment = VerticalAlignment.Center
        };
        combo.SelectionChanged += SettingValue_SelectionChanged;
        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);

        var remove = InlineButton("×", RemoveSetting_Click, facet);
        remove.ToolTip = $"Remove {label}";
        remove.Margin = new Thickness(6, 0, 0, 0);
        remove.HorizontalAlignment = HorizontalAlignment.Right;
        remove.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        return SeparatorRow(grid);
    }

    private void AddSetting_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = AddSettingButton,
            Placement = PlacementMode.Bottom
        };

        AddSettingMenuItem(menu, ThinkControlModeFacet.PerformanceMode, "Performance", _editingPerformanceMode is null);
        AddSettingMenuItem(menu, ThinkControlModeFacet.CoolingProfile, "Cooling", _editingCoolingProfile is null);
        AddSettingMenuItem(menu, ThinkControlModeFacet.RefreshRate, "Refresh rate", _editingRefreshRate is null);
        AddSettingMenuItem(menu, ThinkControlModeFacet.AudioSafety, "Audio", _editingAudioSafety is null);
        AddSettingMenuItem(menu, ThinkControlModeFacet.TouchpadGestures, "Touchpad gestures", !_editingTouchpadGestures.HasValue);
        AddSettingMenuItem(menu, ThinkControlModeFacet.KeyboardLight, "Keyboard light", _editingKeyboardLight is null);

        AddSettingButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AddSettingMenuItem(
        ContextMenu menu,
        ThinkControlModeFacet facet,
        string label,
        bool visible)
    {
        if (!visible)
            return;
        var item = new MenuItem { Header = label, Tag = facet };
        item.Click += AddSettingMenuItem_Click;
        menu.Items.Add(item);
    }

    private void AddSettingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ThinkControlModeFacet facet })
            return;

        switch (facet)
        {
            case ThinkControlModeFacet.PerformanceMode:
                _editingPerformanceMode = "Balanced";
                break;
            case ThinkControlModeFacet.CoolingProfile:
                _editingCoolingProfile = "Balanced";
                break;
            case ThinkControlModeFacet.RefreshRate:
                _editingRefreshRate = "Auto";
                break;
            case ThinkControlModeFacet.AudioSafety:
                _editingAudioSafety = "Normal";
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _editingTouchpadGestures = false;
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _editingKeyboardLight = "Auto";
                break;
        }
        BuildEditorSettings();
    }

    private void RemoveSetting_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ThinkControlModeFacet facet })
            return;

        switch (facet)
        {
            case ThinkControlModeFacet.PerformanceMode:
                _editingPerformanceMode = null;
                break;
            case ThinkControlModeFacet.CoolingProfile:
                _editingCoolingProfile = null;
                break;
            case ThinkControlModeFacet.RefreshRate:
                _editingRefreshRate = null;
                break;
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
        BuildEditorSettings();
    }

    private void SettingValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { Tag: ThinkControlModeFacet facet, SelectedItem: string selected })
            return;

        switch (facet)
        {
            case ThinkControlModeFacet.PerformanceMode:
                _editingPerformanceMode = selected;
                break;
            case ThinkControlModeFacet.CoolingProfile:
                _editingCoolingProfile = selected;
                break;
            case ThinkControlModeFacet.RefreshRate:
                _editingRefreshRate = selected;
                break;
            case ThinkControlModeFacet.AudioSafety:
                _editingAudioSafety = selected switch
                {
                    "Gesture lock" => "GestureLock",
                    "Silent" => "Silent",
                    _ => "Normal"
                };
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _editingTouchpadGestures = selected == "On";
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _editingKeyboardLight = selected;
                break;
        }
    }

    private void AutomationSwitch_Click(object sender, RoutedEventArgs e)
    {
        _editingAutomationEnabled = AutomationSwitch.IsChecked == true;
        UpdateAutomationState();
    }

    private void TriggerMatch_Changed(object sender, SelectionChangedEventArgs e) =>
        _editingMatchAllTriggers = TriggerMatchCombo.SelectedIndex == 1;

    private void TriggerPriority_Changed(object sender, SelectionChangedEventArgs e) =>
        _editingAutomationPriority = TriggerPriorityCombo.SelectedIndex - 1;

    private void UpdateAutomationState()
    {
        AutomationSwitch.IsChecked = _editingAutomationEnabled;
        AutomationBody.IsEnabled = _editingAutomationEnabled;
        AutomationBody.Opacity = _editingAutomationEnabled ? 1.0 : 0.5;
    }

    private void AddTrigger_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = AddTriggerButton,
            Placement = PlacementMode.Bottom
        };
        AddTriggerMenuItem(menu, "Wi-Fi network", "Wifi");
        AddTriggerMenuItem(menu, "App running", "Process");
        AddTriggerMenuItem(menu, "Power source", "Power");
        AddTriggerMenuItem(menu, "Battery level", "BatteryBelow");
        AddTriggerMenuItem(menu, "Schedule", "Schedule");
        AddTriggerButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AddTriggerMenuItem(ContextMenu menu, string label, string type)
    {
        var item = new MenuItem { Header = label, Tag = type };
        item.Click += AddTriggerMenuItem_Click;
        menu.Items.Add(item);
    }

    private void AddTriggerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string type } ||
            _editingTriggers.Count >= ThinkControlModeCatalog.MaxTriggersPerMode)
        {
            return;
        }

        ThinkControlModeTrigger trigger = type switch
        {
            "Wifi" => new(
                "Wifi",
                ModeTriggerEnvironment.GetConnectedWifiSsid() ?? string.Empty),
            "Process" => new("Process", string.Empty),
            "Power" => new("Power", "Battery"),
            "BatteryBelow" => new("BatteryBelow", Number: 25),
            "Schedule" => new(
                "Schedule",
                StartTime: "09:00",
                EndTime: "17:00",
                DaysMask: 0b0111110),
            _ => new(type)
        };
        _editingTriggers.Add(trigger);
        _editingAutomationEnabled = true;
        BuildEditorTriggers();
        UpdateAutomationState();
    }

    private void BuildEditorTriggers()
    {
        EditorTriggers.Children.Clear();
        for (int index = 0; index < _editingTriggers.Count; index++)
            EditorTriggers.Children.Add(CreateTriggerRow(_editingTriggers[index], index));
        AddTriggerButton.IsEnabled = _editingTriggers.Count < ThinkControlModeCatalog.MaxTriggersPerMode;
    }

    private Border CreateTriggerRow(ThinkControlModeTrigger trigger, int index)
    {
        var grid = new Grid { MinHeight = 48 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = TriggerLabel(trigger.Type),
            VerticalAlignment = VerticalAlignment.Center
        });

        FrameworkElement editor = trigger.Type switch
        {
            "Wifi" => WifiTriggerCombo(index, trigger.Value),
            "Process" => TriggerTextBox(index, "Value", trigger.Value, "App or process"),
            "Power" => TriggerCombo(index, "Value", ["Battery", "AC"], trigger.Value),
            "BatteryBelow" => TriggerCombo(
                index,
                "Number",
                ["15%", "20%", "25%", "30%", "40%", "50%"],
                $"{trigger.Number}%"),
            "Schedule" => ScheduleEditor(trigger, index),
            _ => TriggerTextBox(index, "Value", trigger.Value, string.Empty)
        };
        editor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        var remove = InlineButton("×", RemoveTrigger_Click, index);
        remove.ToolTip = "Remove trigger";
        remove.Margin = new Thickness(6, 0, 0, 0);
        remove.HorizontalAlignment = HorizontalAlignment.Right;
        remove.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        return SeparatorRow(grid);
    }

    private static string TriggerLabel(string type) => type switch
    {
        "Wifi" => "Wi-Fi network",
        "Process" => "App running",
        "Power" => "Power source",
        "BatteryBelow" => "Battery at or below",
        "Schedule" => "Schedule",
        _ => type
    };

    private TextBox TriggerTextBox(int index, string field, string value, string tooltip)
    {
        var box = new TextBox
        {
            Text = value,
            Tag = new TriggerBinding(index, field),
            Style = TryFindResource("ModeTextBox") as Style,
            MaxLength = field == "Value" ? 96 : 32,
            ToolTip = tooltip
        };
        box.LostFocus += TriggerText_LostFocus;
        return box;
    }

    private ComboBox WifiTriggerCombo(int index, string value)
    {
        // Offer the connected network and a few locally saved profiles; typing
        // remains available for networks not currently known to Windows.
        var combo = new ComboBox
        {
            Tag = new TriggerBinding(index, "Value"),
            ItemsSource = ModeTriggerEnvironment.SuggestedWifiNetworks(),
            Text = value,
            IsEditable = true,
            IsTextSearchEnabled = false,
            MaxDropDownHeight = 230,
            MinHeight = 38,
            Style = TryFindResource("TcComboBox") as Style,
            ToolTip = "Current and saved Windows networks; type any network name."
        };
        combo.SelectionChanged += TriggerCombo_SelectionChanged;
        combo.LostKeyboardFocus += (_, _) =>
        {
            if (index >= 0 && index < _editingTriggers.Count)
                _editingTriggers[index] = _editingTriggers[index] with { Value = combo.Text.Trim() };
        };
        return combo;
    }

    private ComboBox TriggerCombo(
        int index,
        string field,
        IReadOnlyList<string> values,
        string selected)
    {
        var combo = new ComboBox
        {
            Tag = new TriggerBinding(index, field),
            ItemsSource = values,
            SelectedItem = selected,
            Style = TryFindResource("TcComboBox") as Style,
            MinHeight = 38
        };
        combo.SelectionChanged += TriggerCombo_SelectionChanged;
        return combo;
    }

    private StackPanel ScheduleEditor(ThinkControlModeTrigger trigger, int index)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        IReadOnlyList<string> times = TimeOptions();

        ComboBox start = TriggerCombo(index, "StartTime", times, trigger.StartTime);
        start.Width = 92;
        panel.Children.Add(start);

        panel.Children.Add(new TextBlock
        {
            Text = "to",
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        ComboBox end = TriggerCombo(index, "EndTime", times, trigger.EndTime);
        end.Width = 92;
        panel.Children.Add(end);

        ComboBox days = TriggerCombo(
            index,
            "DaysMask",
            ["Every day", "Weekdays", "Weekend"],
            DaysDisplay(trigger.DaysMask));
        days.Width = 112;
        days.Margin = new Thickness(8, 0, 0, 0);
        panel.Children.Add(days);
        return panel;
    }

    private static IReadOnlyList<string> TimeOptions()
    {
        var values = new List<string>(48);
        for (int hour = 0; hour < 24; hour++)
        {
            values.Add($"{hour:00}:00");
            values.Add($"{hour:00}:30");
        }
        return values;
    }

    private static string DaysDisplay(int mask) => (mask & 0x7F) switch
    {
        0b0111110 => "Weekdays",
        0b1000001 => "Weekend",
        _ => "Every day"
    };

    private void TriggerText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { Tag: TriggerBinding binding } box ||
            binding.Index < 0 || binding.Index >= _editingTriggers.Count)
        {
            return;
        }

        ThinkControlModeTrigger current = _editingTriggers[binding.Index];
        if (binding.Field == "Value")
            _editingTriggers[binding.Index] = current with { Value = box.Text.Trim() };
    }

    private void TriggerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { Tag: TriggerBinding binding, SelectedItem: string selected } ||
            binding.Index < 0 || binding.Index >= _editingTriggers.Count)
        {
            return;
        }

        ThinkControlModeTrigger current = _editingTriggers[binding.Index];
        _editingTriggers[binding.Index] = binding.Field switch
        {
            "Value" => current with { Value = selected },
            "Number" => current with
            {
                Number = int.TryParse(selected.TrimEnd('%'), out int value) ? value : current.Number
            },
            "StartTime" => current with { StartTime = selected },
            "EndTime" => current with { EndTime = selected },
            "DaysMask" => current with
            {
                DaysMask = selected switch
                {
                    "Weekdays" => 0b0111110,
                    "Weekend" => 0b1000001,
                    _ => 0x7F
                }
            },
            _ => current
        };
    }

    private void RemoveTrigger_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int index } ||
            index < 0 || index >= _editingTriggers.Count)
        {
            return;
        }

        _editingTriggers.RemoveAt(index);
        if (_editingTriggers.Count == 0)
        {
            _editingAutomationEnabled = false;
            AutomationSwitch.IsChecked = false;
        }
        BuildEditorTriggers();
        UpdateAutomationState();
    }

    private static Border SeparatorRow(UIElement child) =>
        new()
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 3, 0, 3),
            Child = child
        };

    private async void Reapply_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _app is null)
            return;
        _busy = true;
        try
        {
            if (!await _app.Modes.ReapplyAsync())
                ShowListStatus(_app.Modes.LastTransitionError ?? "Could not reapply the mode.");
        }
        finally
        {
            _busy = false;
            RefreshList();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => EndEdit();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || string.IsNullOrWhiteSpace(_editingId))
            return;

        string name = ModeNameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowEditorStatus("Enter a name.");
            return;
        }

        if (_editingAutomationEnabled)
        {
            ThinkControlModeTrigger[] valid = ThinkControlModeCatalog.SanitizeTriggers(_editingTriggers);
            if (valid.Length != _editingTriggers.Count || valid.Length == 0)
            {
                ShowEditorStatus("Finish or remove incomplete triggers.");
                return;
            }
        }

        var mode = new ThinkControlModeDefinition(
            _editingId,
            name,
            _editingAudioSafety,
            _editingTouchpadGestures,
            _editingKeyboardLight,
            _editingPerformanceMode,
            _editingCoolingProfile,
            _editingRefreshRate,
            _editingTriggers.ToArray(),
            _editingAutomationEnabled,
            _editingMatchAllTriggers,
            _editingAutomationPriority);

        if (!ThinkControlModeCatalog.Facets(mode).Any())
        {
            ShowEditorStatus("Add at least one setting.");
            return;
        }

        if (!_app.Modes.SaveCustomMode(mode))
        {
            ShowEditorStatus("Use a unique name and valid settings.");
            return;
        }

        _saveButton.IsEnabled = false;
        try
        {
            if (!await _app.Modes.ActivateAsync(mode.Id))
            {
                ShowEditorStatus(_app.Modes.LastTransitionError ?? "Saved, but the mode could not be applied.");
                return;
            }
            EndEdit();
        }
        finally
        {
            _saveButton.IsEnabled = true;
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || string.IsNullOrWhiteSpace(_editingId))
            return;

        if (!_app.Modes.DeleteCustomMode(_editingId))
        {
            ShowEditorStatus("Deactivate this mode before deleting it.");
            return;
        }

        EndEdit();
    }

    private void EndEdit()
    {
        _editingId = null;
        EditorView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
        _saveButton.Visibility = Visibility.Collapsed;
        _cancelButton.Visibility = Visibility.Collapsed;
        EditorStatusText.Visibility = Visibility.Collapsed;
        RefreshList();
    }

    private void UpdateHeaderState()
    {
        if (_app is null || EditorView.Visibility == Visibility.Visible)
            return;

        bool transitioning = _app.Modes.IsTransitioning;
        bool modified = _app.Modes.IsModified && !transitioning;
        bool automatic = _app.Modes.ActiveModeAutomatic &&
                         _app.Modes.ActiveModeId != ThinkControlModeCatalog.NormalId &&
                         !transitioning &&
                         !modified;

        _modifiedLabel.Text = transitioning
            ? $"Applying {_app.Modes.VisibleModeName}…"
            : modified
                ? "Modified"
                : "Automatic";
        _modifiedLabel.Visibility = transitioning || modified || automatic
            ? Visibility.Visible
            : Visibility.Collapsed;
        _reapplyButton.Visibility = modified ? Visibility.Visible : Visibility.Collapsed;
        _saveButton.Visibility = Visibility.Collapsed;
        _cancelButton.Visibility = Visibility.Collapsed;
    }

    private void ShowListStatus(string message)
    {
        ListStatusText.Text = message;
        ListStatusText.Visibility = Visibility.Visible;
    }

    private void ShowEditorStatus(string message)
    {
        EditorStatusText.Text = message;
        EditorStatusText.Visibility = Visibility.Visible;
    }

    internal void PrepareListForSnapshot()
    {
        ListView.Visibility = Visibility.Visible;
        EditorView.Visibility = Visibility.Collapsed;
        ModeRows.Children.Clear();

        ThinkControlModeDefinition[] fixtures =
        [
            ThinkControlModeCatalog.NoMode,
            .. ThinkControlModeCatalog.StarterModes,
            new(
                "custom:study-snapshot",
                "Study",
                AudioSafety: "Silent",
                KeyboardLight: "Low",
                PerformanceMode: "Efficiency",
                CoolingProfile: "Quiet",
                RefreshRate: "60 Hz",
                Triggers: [new ThinkControlModeTrigger("Wifi", "Campus")],
                AutomationEnabled: true),
            new(
                "custom:solidworks-snapshot",
                "SolidWorks",
                PerformanceMode: "Performance",
                CoolingProfile: "Balanced",
                RefreshRate: "Max",
                Triggers: [new ThinkControlModeTrigger("Process", "SLDWORKS")],
                AutomationEnabled: true),
        ];

        _syncingModeSelection = true;
        try
        {
            ModeSelector.ItemsSource = fixtures;
            ModeSelector.SelectedItem = fixtures[0];
        }
        finally
        {
            _syncingModeSelection = false;
        }

        foreach (ThinkControlModeDefinition mode in fixtures.Where(mode =>
                     mode.Id != ThinkControlModeCatalog.NormalId))
            ModeRows.Children.Add(CreateModeRow(mode));

        EmptyModesText.Visibility = Visibility.Collapsed;
        UpdateHeaderState();
    }

    internal void PrepareEditorForSnapshot()
    {
        BeginEdit(new ThinkControlModeDefinition(
            "custom:snapshot",
            "Study",
            AudioSafety: "Silent",
            TouchpadGesturesEnabled: false,
            KeyboardLight: "Low",
            PerformanceMode: "Efficiency",
            CoolingProfile: "Quiet",
            RefreshRate: "60 Hz",
            Triggers:
            [
                new ThinkControlModeTrigger("Wifi", "Campus"),
                new ThinkControlModeTrigger("Power", "Battery")
            ],
            AutomationEnabled: true));
    }
}
