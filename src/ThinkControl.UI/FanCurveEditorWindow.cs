using System.Globalization;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.Core.Cooling;
using ThinkControl.UI.Services;
using ListBox = System.Windows.Controls.ListBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace ThinkControl.UI;

internal sealed class FanCurveEditorWindow : Window
{
    private readonly App _app;
    private readonly ListBox _builtInProfiles = new();
    private readonly ListBox _customProfiles = new();
    private readonly TextBlock _customHeading = new();
    private readonly TextBlock _customEmpty = new();
    private readonly FanCurveGraph _graph = new();
    private readonly TextBox _temperature = new();
    private readonly TextBox _output = new();
    private readonly TextBlock _pointValue = new();
    private readonly TextBlock _liveValue = new();
    private readonly TextBlock _status = new();
    private readonly Button _rename = new();
    private readonly Button _delete = new();
    private readonly Button _reset = new();
    private readonly Button _addPoint = new();
    private readonly Button _removePoint = new();
    private readonly Button _apply = new();
    private bool _syncing;
    private bool _syncingProfileSelection;
    private FanCurveDefinition? _editing;

    internal FanCurveEditorWindow(App app)
    {
        _app = app;
        Title = "ThinkControl · Fan curves";
        Width = 900;
        Height = 620;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = Application.Current.TryFindResource("Tc.Background") as Brush ?? SystemColors.WindowBrush;
        Foreground = Application.Current.TryFindResource("Tc.Text") as Brush ?? SystemColors.WindowTextBrush;

        Content = BuildLayout();
        _graph.ShowLiveLabel = false;
        _builtInProfiles.SelectionChanged += Profiles_SelectionChanged;
        _customProfiles.SelectionChanged += Profiles_SelectionChanged;
        _graph.SelectionChanged += Graph_SelectionChanged;
        _graph.CurveChanged += Graph_CurveChanged;
        ConfigureNumberField(_temperature, allowDecimal: true);
        ConfigureNumberField(_output, allowDecimal: false);
        Loaded += (_, _) =>
        {
            ReloadProfiles(_app.UserSettings.Current.CoolingProfile);
            _app.State.PropertyChanged += State_PropertyChanged;
            RefreshLiveMarker();
        };
        Unloaded += (_, _) => _app.State.PropertyChanged -= State_PropertyChanged;
    }

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = "Fan curves", FontSize = 24, FontWeight = FontWeights.SemiBold });
        var subtitle = new TextBlock
        {
            Text = "Tune temperature against a real 0–100% target. ThinkControl maps the graph to measured X9 EC states after calibration; 100% uses the physically verified maximum, EC step 7.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        heading.Children.Add(subtitle);
        root.Children.Add(heading);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var profileCard = Section();
        var profileStack = new StackPanel();
        profileStack.Children.Add(new TextBlock { Text = "Built-in", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) });
        ConfigureProfileList(_builtInProfiles, maxHeight: 116);
        profileStack.Children.Add(_builtInProfiles);

        _customHeading.Text = $"Custom · 0/{FanProfileCatalog.MaxCustomProfiles}";
        _customHeading.FontWeight = FontWeights.SemiBold;
        _customHeading.Margin = new Thickness(0, 13, 0, 7);
        profileStack.Children.Add(_customHeading);
        _customEmpty.Text = "No custom curves yet";
        _customEmpty.FontSize = 10;
        _customEmpty.Margin = new Thickness(5, 6, 0, 7);
        _customEmpty.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        profileStack.Children.Add(_customEmpty);
        ConfigureProfileList(_customProfiles, maxHeight: 132);
        profileStack.Children.Add(_customProfiles);

        var row1 = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row1.ColumnDefinitions.Add(new ColumnDefinition());
        row1.ColumnDefinitions.Add(new ColumnDefinition());
        Button add = SmallButton("Add");
        add.ToolTip = "Clone the selected curve into a new custom profile";
        add.Click += Add_Click;
        _rename.Content = "Rename";
        ConfigureSmallButton(_rename);
        _rename.Margin = new Thickness(6, 0, 0, 0);
        _rename.Click += Rename_Click;
        row1.Children.Add(add);
        Grid.SetColumn(_rename, 1);
        row1.Children.Add(_rename);
        profileStack.Children.Add(row1);

        var row2 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        row2.ColumnDefinitions.Add(new ColumnDefinition());
        row2.ColumnDefinitions.Add(new ColumnDefinition());
        _reset.Content = "Reset";
        ConfigureSmallButton(_reset);
        _reset.Click += Reset_Click;
        _delete.Content = "Delete";
        ConfigureSmallButton(_delete);
        _delete.Margin = new Thickness(6, 0, 0, 0);
        _delete.Click += Delete_Click;
        row2.Children.Add(_reset);
        Grid.SetColumn(_delete, 1);
        row2.Children.Add(_delete);
        profileStack.Children.Add(row2);
        profileCard.Child = profileStack;
        body.Children.Add(profileCard);

        var editorCard = Section();
        Grid.SetColumn(editorCard, 2);
        var editor = new Grid();
        editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editor.RowDefinitions.Add(new RowDefinition());
        editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var editorHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        editorHeader.ColumnDefinitions.Add(new ColumnDefinition());
        editorHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editorHeader.Children.Add(new TextBlock { Text = "Temperature / fan target", FontWeight = FontWeights.SemiBold });
        var safety = new TextBlock { Text = "94 °C → Lenovo firmware", FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        safety.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(safety, 1);
        editorHeader.Children.Add(safety);
        editor.Children.Add(editorHeader);

        _graph.MinHeight = 300;
        _graph.Focusable = true;
        Grid.SetRow(_graph, 1);
        editor.Children.Add(_graph);

        var pointTools = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        pointTools.ColumnDefinitions.Add(new ColumnDefinition());
        pointTools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _liveValue.FontSize = 10.5;
        _liveValue.FontWeight = FontWeights.SemiBold;
        _liveValue.VerticalAlignment = VerticalAlignment.Center;
        _liveValue.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        pointTools.Children.Add(_liveValue);
        var toolButtons = new StackPanel { Orientation = Orientation.Horizontal };
        _addPoint.Content = "+ Point";
        ConfigureSmallButton(_addPoint);
        _addPoint.Click += AddPoint_Click;
        _removePoint.Content = "Remove";
        ConfigureSmallButton(_removePoint);
        _removePoint.Margin = new Thickness(6, 0, 0, 0);
        _removePoint.Click += RemovePoint_Click;
        Button smooth = SmallButton("Smooth");
        smooth.Margin = new Thickness(6, 0, 0, 0);
        smooth.ToolTip = "Even out abrupt changes while keeping temperatures and safety endpoints";
        smooth.Click += Smooth_Click;
        toolButtons.Children.Add(_addPoint);
        toolButtons.Children.Add(_removePoint);
        toolButtons.Children.Add(smooth);
        Grid.SetColumn(toolButtons, 1);
        pointTools.Children.Add(toolButtons);
        Grid.SetRow(pointTools, 2);
        editor.Children.Add(pointTools);

        var precision = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        precision.ColumnDefinitions.Add(new ColumnDefinition());
        precision.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        precision.ColumnDefinitions.Add(new ColumnDefinition());
        precision.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        precision.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        precision.Children.Add(PrecisionPanel("Temperature", _temperature, "°C"));
        var outputPanel = PrecisionPanel("Fan target", _output, "%");
        Grid.SetColumn(outputPanel, 2);
        precision.Children.Add(outputPanel);
        _pointValue.FontSize = 10;
        _pointValue.Margin = new Thickness(0, 7, 0, 0);
        _pointValue.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetRow(_pointValue, 1);
        Grid.SetColumnSpan(_pointValue, 3);
        precision.Children.Add(_pointValue);
        Grid.SetRow(precision, 3);
        editor.Children.Add(precision);
        editorCard.Child = editor;
        body.Children.Add(editorCard);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.FontSize = 10.5;
        _status.TextWrapping = TextWrapping.Wrap;
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        footer.Children.Add(_status);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        Button close = SmallButton("Close");
        close.Padding = new Thickness(14, 7, 14, 7);
        close.Click += (_, _) => Close();
        _apply.Content = "Save and apply";
        ConfigureSmallButton(_apply);
        _apply.Padding = new Thickness(14, 7, 14, 7);
        _apply.Margin = new Thickness(8, 0, 0, 0);
        _apply.Click += Apply_Click;
        actions.Children.Add(close);
        actions.Children.Add(_apply);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private Border Section()
    {
        var border = new Border { Padding = new Thickness(14), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        border.SetResourceReference(Border.BackgroundProperty, "Tc.Surface");
        border.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");
        return border;
    }

    private StackPanel PrecisionPanel(string title, TextBox input, string suffix)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontSize = 10, Margin = new Thickness(0, 0, 0, 4) });
        var field = new Grid();
        field.ColumnDefinitions.Add(new ColumnDefinition());
        field.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        input.Height = 32;
        input.Padding = new Thickness(9, 4, 9, 4);
        input.VerticalContentAlignment = VerticalAlignment.Center;
        input.SetResourceReference(Control.BackgroundProperty, "Tc.SurfaceAlt");
        input.SetResourceReference(Control.ForegroundProperty, "Tc.Text");
        input.SetResourceReference(Control.BorderBrushProperty, "Tc.BorderStrong");
        field.Children.Add(input);
        var unit = new TextBlock { Text = suffix, FontSize = 10.5, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        unit.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(unit, 1);
        field.Children.Add(unit);
        panel.Children.Add(field);
        return panel;
    }

    private static void ConfigureProfileList(ListBox list, double maxHeight)
    {
        list.MaxHeight = maxHeight;
        list.BorderThickness = new Thickness(0);
        list.Background = Brushes.Transparent;
        list.DisplayMemberPath = nameof(FanCurveDefinition.Name);
    }

    private void ConfigureNumberField(TextBox input, bool allowDecimal)
    {
        input.PreviewTextInput += (_, e) =>
        {
            string candidate = input.Text.Remove(input.SelectionStart, input.SelectionLength)
                .Insert(input.SelectionStart, e.Text);
            e.Handled = !IsValidNumericInput(candidate, allowDecimal);
        };
        System.Windows.DataObject.AddPastingHandler(input, (_, e) =>
        {
            string pasted = e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) as string ?? string.Empty;
            string candidate = input.Text.Remove(input.SelectionStart, input.SelectionLength)
                .Insert(input.SelectionStart, pasted);
            if (!IsValidNumericInput(candidate, allowDecimal))
                e.CancelCommand();
        });
        input.LostKeyboardFocus += PrecisionField_LostKeyboardFocus;
        input.KeyDown += PrecisionField_KeyDown;
    }

    private static bool IsValidNumericInput(string candidate, bool allowDecimal) =>
        allowDecimal
            ? Regex.IsMatch(candidate, @"^\d{0,2}([\.,]\d?)?$")
            : Regex.IsMatch(candidate, @"^\d{0,3}$");

    private Button SmallButton(string text)
    {
        var button = new Button { Content = text };
        ConfigureSmallButton(button);
        return button;
    }

    private void ConfigureSmallButton(Button button)
    {
        button.Padding = new Thickness(9, 5, 9, 5);
        button.FontSize = 10;
        button.SetResourceReference(Button.StyleProperty, "TcButton");
    }

    private void ReloadProfiles(string? selectId)
    {
        SetProfileLists(_app.FanProfiles.GetProfiles(), selectId);
    }

    private void SetProfileLists(IReadOnlyList<FanCurveDefinition> profiles, string? selectId)
    {
        FanCurveDefinition[] builtIns = profiles.Where(profile => _app.FanProfiles.IsBuiltIn(profile.Id)).ToArray();
        FanCurveDefinition[] customs = profiles.Where(profile => !_app.FanProfiles.IsBuiltIn(profile.Id)).ToArray();
        _syncingProfileSelection = true;
        _builtInProfiles.ItemsSource = builtIns;
        _customProfiles.ItemsSource = customs;
        _customHeading.Text = $"Custom · {customs.Length}/{FanProfileCatalog.MaxCustomProfiles}";
        _customEmpty.Visibility = customs.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _customProfiles.Visibility = customs.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        FanCurveDefinition? selected = profiles.FirstOrDefault(profile => string.Equals(profile.Id, selectId, StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault(profile => profile.Id == FanCurveDefaults.BalancedId)
            ?? profiles.FirstOrDefault();
        _builtInProfiles.SelectedItem = builtIns.FirstOrDefault(profile => string.Equals(profile.Id, selected?.Id, StringComparison.OrdinalIgnoreCase));
        _customProfiles.SelectedItem = customs.FirstOrDefault(profile => string.Equals(profile.Id, selected?.Id, StringComparison.OrdinalIgnoreCase));
        _syncingProfileSelection = false;
        if (selected is not null)
            BeginEditing(selected);
    }

    private void Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingProfileSelection || sender is not ListBox list || list.SelectedItem is not FanCurveDefinition selected)
            return;

        _syncingProfileSelection = true;
        if (ReferenceEquals(list, _builtInProfiles))
            _customProfiles.SelectedItem = null;
        else
            _builtInProfiles.SelectedItem = null;
        _syncingProfileSelection = false;
        BeginEditing(selected);
    }

    private FanCurveDefinition? SelectedProfile =>
        _builtInProfiles.SelectedItem as FanCurveDefinition ?? _customProfiles.SelectedItem as FanCurveDefinition;

    private void BeginEditing(FanCurveDefinition selected)
    {
        _editing = new FanCurveDefinition(selected.Id, selected.Name, selected.Points.Select(point => point with { }).ToArray());
        _graph.SetCurve(_editing.Points);
        _graph.SelectedIndex = 0;
        SyncSelectedPoint();
        RefreshLiveMarker();
        bool builtIn = _app.FanProfiles.IsBuiltIn(selected.Id);
        _rename.IsEnabled = !builtIn;
        _delete.IsEnabled = !builtIn;
        _reset.IsEnabled = builtIn;
        _status.Text = builtIn
            ? "Built-in profile · edits are stored as an override; Reset restores ThinkControl defaults."
            : "Custom profile · edit the graph, rename it, or delete it without changing the built-in profiles.";
    }

    private void Graph_SelectionChanged(object? sender, EventArgs e) => SyncSelectedPoint();

    private void Graph_CurveChanged(object? sender, EventArgs e)
    {
        SyncSelectedPoint();
        RefreshLiveMarker();
        _status.Text = "Unsaved curve changes · hardware is not touched until Save and apply.";
    }

    private void SyncSelectedPoint()
    {
        if (_graph.SelectedPoint is not FanCurvePoint point)
            return;
        _syncing = true;
        try
        {
            _temperature.Text = point.TemperatureC.ToString("0.0", CultureInfo.CurrentCulture);
            _output.Text = point.Percent.ToString(CultureInfo.CurrentCulture);
            _output.IsEnabled = _graph.SelectedIndex != _graph.PointCount - 1;
            _pointValue.Text = $"Point {_graph.SelectedIndex + 1}/{_graph.PointCount} · drag it or type an exact value" +
                               (_graph.SelectedIndex == _graph.PointCount - 1 ? " · final target stays at 100%" : string.Empty);
            _addPoint.IsEnabled = _graph.PointCount < FanCurveGraphPolicy.MaxPointCount;
            _removePoint.IsEnabled = _graph.PointCount > FanCurveGraphPolicy.MinPointCount;
        }
        finally { _syncing = false; }
    }

    private void PrecisionField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        CommitPrecisionFields();
        e.Handled = true;
        _graph.Focus();
    }

    private void PrecisionField_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitPrecisionFields();

    private void CommitPrecisionFields()
    {
        if (_syncing || _graph.SelectedIndex < 0)
            return;
        string rawTemperature = _temperature.Text.Replace(',', '.');
        if (!double.TryParse(rawTemperature, NumberStyles.Number, CultureInfo.InvariantCulture, out double temperature) ||
            !int.TryParse(_output.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int output))
        {
            SyncSelectedPoint();
            return;
        }
        _graph.SetSelectedPoint(Math.Round(temperature, 1), output);
    }

    private void AddPoint_Click(object sender, RoutedEventArgs e)
    {
        if (!_graph.AddPointNearSelection())
            _status.Text = $"A curve can contain at most {FanCurveGraphPolicy.MaxPointCount} points.";
    }

    private void RemovePoint_Click(object sender, RoutedEventArgs e)
    {
        if (!_graph.RemoveSelectedPoint())
            _status.Text = $"Keep at least {FanCurveGraphPolicy.MinPointCount} points so the curve stays predictable.";
    }

    private void Smooth_Click(object sender, RoutedEventArgs e)
    {
        _graph.Smooth();
        _status.Text = "Abrupt target changes were softened; temperatures and safety endpoints were kept.";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string? source = SelectedProfile?.Id;
        FanCurveDefinition? created = _app.FanProfiles.CreateCustom(source, out string? error);
        if (created is null)
        {
            _status.Text = error ?? "Could not create a custom profile.";
            return;
        }
        ReloadProfiles(created.Id);
        _status.Text = $"{created.Name} created from the selected curve.";
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not FanCurveDefinition selected || _app.FanProfiles.IsBuiltIn(selected.Id))
            return;
        string? name = PromptName(selected.Name);
        if (name is null)
            return;
        if (!_app.FanProfiles.Rename(selected.Id, name, out string? error))
        {
            _status.Text = error ?? "Profile could not be renamed.";
            return;
        }
        ReloadProfiles(selected.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not FanCurveDefinition selected || _app.FanProfiles.IsBuiltIn(selected.Id))
            return;
        if (MessageBox.Show(this, $"Delete fan profile ‘{selected.Name}’?", "ThinkControl · Fan curves", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        if (!_app.FanProfiles.Delete(selected.Id, out string? error))
        {
            _status.Text = error ?? "Profile could not be deleted.";
            return;
        }
        ReloadProfiles(FanCurveDefaults.BalancedId);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not FanCurveDefinition selected || !_app.FanProfiles.IsBuiltIn(selected.Id))
            return;
        FanCurveDefinition factory = _app.FanProfiles.ResetBuiltIn(selected.Id);
        ReloadProfiles(factory.Id);
        _status.Text = $"{factory.Name} restored to ThinkControl factory curve. Save and apply to activate it now.";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is null)
            return;
        FanCurveDefinition definition = _editing with { Points = _graph.GetCurve() };
        if (!_app.FanProfiles.SaveCurve(definition, out string? saveError))
        {
            _status.Text = saveError ?? "Curve could not be saved.";
            return;
        }

        _apply.IsEnabled = false;
        try
        {
            bool applied = await _app.ApplyFanCurveAsync(definition, persistSelection: true);
            _status.Text = applied
                ? $"{definition.Name} saved and active. The safety supervisor remains independent of this graph."
                : _app.State.HardwareAccess;
            ReloadProfiles(definition.Id);
        }
        finally { _apply.IsEnabled = true; }
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(_app.State.ControlTemperatureC) or nameof(_app.State.FanRpm))
            RefreshLiveMarker();
    }

    private void RefreshLiveMarker()
    {
        if (_editing is null || _app.State.ControlTemperatureC is not double temperature)
        {
            _graph.SetLiveState(null, null, null);
            _liveValue.Text = "LIVE · waiting for control temperature";
            return;
        }

        FanCurvePoint[] curve = _graph.GetCurve();
        int target;
        try { target = FanCurveGraphPolicy.ResolvePercent(curve, temperature); }
        catch
        {
            _graph.SetLiveState(null, null, null);
            return;
        }

        _graph.SetLiveState(temperature, target, _app.State.FanRpm);
        string rpm = _app.State.FanRpm is int actual ? $" · current {actual:N0} RPM" : string.Empty;
        _liveValue.Text = $"LIVE · {temperature:0.0} °C · {target}% target{rpm}";
    }

    internal void PrepareForSnapshot()
    {
        var sampleCustom = new FanCurveDefinition(
            "custom:visual-qa",
            "Travel",
            [new(40, 0), new(54, 12), new(65, 36), new(76, 58), new(85, 78), new(92, 100)]);
        SetProfileLists([.. FanCurveDefaults.BuiltIns, sampleCustom], sampleCustom.Id);
        RefreshLiveMarker();
    }

    private string? PromptName(string current)
    {
        var dialog = new Window
        {
            Title = "Rename fan profile",
            Width = 360,
            Height = 150,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = Background,
            Foreground = Foreground
        };
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var input = new TextBox { Text = current, MaxLength = 32, VerticalContentAlignment = VerticalAlignment.Center };
        input.SelectAll();
        root.Children.Add(input);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        Button cancel = SmallButton("Cancel");
        Button save = SmallButton("Save");
        save.Margin = new Thickness(7, 0, 0, 0);
        cancel.Click += (_, _) => dialog.DialogResult = false;
        save.Click += (_, _) => dialog.DialogResult = true;
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        Grid.SetRow(actions, 1);
        root.Children.Add(actions);
        dialog.Content = root;
        dialog.Loaded += (_, _) => input.Focus();
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }
}

internal sealed class FanCurveGraph : FrameworkElement
{
    private readonly List<FanCurvePoint> _points = [];
    private int _selectedIndex = -1;
    private bool _dragging;
    private double? _liveTemperatureC;
    private int? _liveTargetPercent;
    private int? _liveRpm;

    internal bool IsReadOnly { get; set; }
    internal bool ShowLiveLabel { get; set; } = true;

    internal event EventHandler? SelectionChanged;
    internal event EventHandler? CurveChanged;

    internal int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = _points.Count == 0 || value < 0 ? -1 : Math.Clamp(value, 0, _points.Count - 1);
            if (_selectedIndex == next) return;
            _selectedIndex = next;
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal FanCurvePoint? SelectedPoint => _selectedIndex >= 0 && _selectedIndex < _points.Count ? _points[_selectedIndex] : null;
    internal int PointCount => _points.Count;

    internal FanCurveGraph()
    {
        SnapsToDevicePixels = true;
        Cursor = Cursors.Cross;
        PreviewKeyDown += OnKeyDown;
    }

    internal void SetCurve(IReadOnlyList<FanCurvePoint> points)
    {
        _points.Clear();
        _points.AddRange(points.Select(point => point with { }));
        _selectedIndex = _points.Count > 0 ? 0 : -1;
        InvalidateVisual();
    }

    internal FanCurvePoint[] GetCurve() => _points.Select(point => point with { }).ToArray();

    internal void SetLiveState(double? temperatureC, int? targetPercent, int? rpm)
    {
        _liveTemperatureC = temperatureC;
        _liveTargetPercent = targetPercent;
        _liveRpm = rpm;
        InvalidateVisual();
    }

    internal bool AddPointNearSelection()
    {
        if (_points.Count >= FanCurveGraphPolicy.MaxPointCount || _points.Count < 2)
            return false;
        int lower = Enumerable.Range(0, _points.Count - 1)
            .OrderByDescending(index => _points[index + 1].TemperatureC - _points[index].TemperatureC)
            .First();
        FanCurvePoint a = _points[lower];
        FanCurvePoint b = _points[lower + 1];
        return AddPoint((a.TemperatureC + b.TemperatureC) / 2.0, (a.Percent + b.Percent) / 2.0);
    }

    internal bool RemoveSelectedPoint()
    {
        if (_points.Count <= FanCurveGraphPolicy.MinPointCount || _selectedIndex < 0)
            return false;
        int removed = _selectedIndex;
        _points.RemoveAt(removed);
        _points[^1] = _points[^1] with { Percent = 100 };
        _selectedIndex = Math.Clamp(removed, 0, _points.Count - 1);
        NotifyCurveChanged();
        return true;
    }

    internal void Smooth()
    {
        FanCurvePoint[] smooth = FanCurveGraphPolicy.Smooth(_points);
        _points.Clear();
        _points.AddRange(smooth);
        NotifyCurveChanged();
    }

    internal void SetSelectedPoint(double temperature, int percent)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _points.Count)
            MovePoint(_selectedIndex, temperature, percent);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Rect plot = PlotRect();
        Brush faint = Application.Current.TryFindResource("Tc.TextFaint") as Brush ?? Brushes.Gray;
        Brush muted = Application.Current.TryFindResource("Tc.TextMuted") as Brush ?? Brushes.Gray;
        Brush accent = Application.Current.TryFindResource("Tc.Accent") as Brush ?? Brushes.DodgerBlue;
        Brush surface = Application.Current.TryFindResource("Tc.SurfaceAlt") as Brush ?? Brushes.Transparent;
        Pen grid = new(Application.Current.TryFindResource("Tc.Border") as Brush ?? Brushes.Gray, 1);
        Pen curvePen = new(accent, 2);

        dc.DrawRoundedRectangle(surface, grid, plot, 5, 5);
        for (int p = 0; p <= 100; p += 20)
        {
            double y = Y(plot, p);
            dc.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, $"{p}%", faint, 9, new Point(4, y - 7));
        }
        for (int t = 40; t <= 90; t += 10)
        {
            double x = X(plot, t);
            dc.DrawLine(grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawText(dc, $"{t}°", faint, 9, new Point(x - 9, plot.Bottom + 7));
        }

        if (_points.Count == 0)
            return;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(ToPoint(plot, _points[0]), false, false);
            for (int i = 1; i < _points.Count; i++)
                context.LineTo(ToPoint(plot, _points[i]), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, curvePen, geometry);

        for (int i = 0; i < _points.Count; i++)
        {
            Point pt = ToPoint(plot, _points[i]);
            double radius = i == _selectedIndex ? 7 : 5;
            dc.DrawEllipse(i == _selectedIndex ? accent : surface, new Pen(accent, 2), pt, radius, radius);
        }

        if (_liveTemperatureC is double liveTemperature && _liveTargetPercent is int liveTarget)
        {
            double clampedTemperature = Math.Clamp(liveTemperature, FanCurveGraphPolicy.MinTemperatureC, FanCurveGraphPolicy.MaxTemperatureC);
            Point live = new(X(plot, clampedTemperature), Y(plot, liveTarget));
            var livePen = new Pen(accent, 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(livePen, new Point(live.X, plot.Top), new Point(live.X, plot.Bottom));
            dc.DrawEllipse(accent, new Pen(surface, 2), live, 5, 5);
            if (ShowLiveLabel)
            {
                string rpm = _liveRpm is int value ? $" · {value:N0} RPM now" : string.Empty;
                string label = $"{liveTemperature:0.0} °C → {liveTarget}%{rpm}";
                var text = CreateText(label, muted, 9);
                double labelX = Math.Clamp(live.X + 8, plot.Left + 5, plot.Right - text.Width - 5);
                double labelY = Math.Clamp(live.Y - text.Height - 7, plot.Top + 4, plot.Bottom - text.Height - 4);
                dc.DrawRoundedRectangle(surface, new Pen(grid.Brush, 1), new Rect(labelX - 4, labelY - 2, text.Width + 8, text.Height + 4), 3, 3);
                dc.DrawText(text, new Point(labelX, labelY));
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (IsReadOnly)
            return;
        Focus();
        if (_points.Count == 0) return;
        Rect plot = PlotRect();
        Point mouse = e.GetPosition(this);
        int nearest = Enumerable.Range(0, _points.Count).OrderBy(i => (ToPoint(plot, _points[i]) - mouse).LengthSquared).First();
        if ((ToPoint(plot, _points[nearest]) - mouse).Length > 18)
        {
            if (e.ClickCount == 2 && plot.Contains(mouse))
            {
                double temperature = FanCurveGraphPolicy.MinTemperatureC + (mouse.X - plot.Left) / plot.Width *
                    (FanCurveGraphPolicy.MaxTemperatureC - FanCurveGraphPolicy.MinTemperatureC);
                double percent = (1 - (mouse.Y - plot.Top) / plot.Height) * 100;
                AddPoint(temperature, percent);
                e.Handled = true;
            }
            return;
        }
        SelectedIndex = nearest;
        _dragging = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || _selectedIndex < 0 || e.LeftButton != MouseButtonState.Pressed) return;
        Rect plot = PlotRect();
        Point p = e.GetPosition(this);
        double temperature = FanCurveGraphPolicy.MinTemperatureC + Math.Clamp((p.X - plot.Left) / plot.Width, 0, 1) *
            (FanCurveGraphPolicy.MaxTemperatureC - FanCurveGraphPolicy.MinTemperatureC);
        int percent = (int)Math.Round((1 - Math.Clamp((p.Y - plot.Top) / plot.Height, 0, 1)) * 100);
        MovePoint(_selectedIndex, Math.Round(temperature, 1), percent);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (IsReadOnly || _selectedIndex < 0 || _points.Count == 0) return;
        int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1;
        FanCurvePoint point = _points[_selectedIndex];
        switch (e.Key)
        {
            case Key.Tab: SelectedIndex = Math.Clamp(_selectedIndex + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1), 0, _points.Count - 1); e.Handled = true; return;
            case Key.Left: MovePoint(_selectedIndex, point.TemperatureC - step, point.Percent); break;
            case Key.Right: MovePoint(_selectedIndex, point.TemperatureC + step, point.Percent); break;
            case Key.Down: MovePoint(_selectedIndex, point.TemperatureC, point.Percent - step); break;
            case Key.Up: MovePoint(_selectedIndex, point.TemperatureC, point.Percent + step); break;
            case Key.Home: SelectedIndex = 0; e.Handled = true; return;
            case Key.End: SelectedIndex = _points.Count - 1; e.Handled = true; return;
            default: return;
        }
        e.Handled = true;
    }

    private void MovePoint(int index, double temperature, int percent)
    {
        double minT = index == 0 ? FanCurveGraphPolicy.MinTemperatureC : _points[index - 1].TemperatureC + FanCurveGraphPolicy.MinimumTemperatureSpacingC;
        double maxT = index == _points.Count - 1 ? FanCurveGraphPolicy.MaxTemperatureC : _points[index + 1].TemperatureC - FanCurveGraphPolicy.MinimumTemperatureSpacingC;
        int minP = index == 0 ? 0 : _points[index - 1].Percent;
        int maxP = index == _points.Count - 1 ? 100 : _points[index + 1].Percent;
        FanCurvePoint next = new(Math.Clamp(Math.Round(temperature, 1), minT, maxT), index == _points.Count - 1 ? 100 : Math.Clamp(percent, minP, maxP));
        if (_points[index] == next) return;
        _points[index] = next;
        NotifyCurveChanged();
    }

    private bool AddPoint(double temperature, double percent)
    {
        if (_points.Count >= FanCurveGraphPolicy.MaxPointCount)
            return false;
        int insert = _points.FindIndex(point => point.TemperatureC > temperature);
        if (insert <= 0 || insert >= _points.Count)
            return false;
        FanCurvePoint lower = _points[insert - 1];
        FanCurvePoint upper = _points[insert];
        double min = lower.TemperatureC + FanCurveGraphPolicy.MinimumTemperatureSpacingC;
        double max = upper.TemperatureC - FanCurveGraphPolicy.MinimumTemperatureSpacingC;
        if (min > max)
            return false;
        double t = Math.Clamp(Math.Round(temperature, 1), min, max);
        int p = Math.Clamp((int)Math.Round(percent), lower.Percent, upper.Percent);
        _points.Insert(insert, new FanCurvePoint(t, p));
        _selectedIndex = insert;
        NotifyCurveChanged();
        return true;
    }

    private void NotifyCurveChanged()
    {
        InvalidateVisual();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        CurveChanged?.Invoke(this, EventArgs.Empty);
    }

    private Rect PlotRect() => new(46, 14, Math.Max(40, ActualWidth - 62), Math.Max(40, ActualHeight - 48));
    private static Point ToPoint(Rect plot, FanCurvePoint point) => new(X(plot, point.TemperatureC), Y(plot, point.Percent));
    private static double X(Rect plot, double temperature) => plot.Left + (temperature - FanCurveGraphPolicy.MinTemperatureC) / (FanCurveGraphPolicy.MaxTemperatureC - FanCurveGraphPolicy.MinTemperatureC) * plot.Width;
    private static double Y(Rect plot, double percent) => plot.Bottom - percent / 100.0 * plot.Height;

    private void DrawText(DrawingContext dc, string text, Brush brush, double size, Point origin)
    {
        dc.DrawText(CreateText(text, brush, size), origin);
    }

    private FormattedText CreateText(string text, Brush brush, double size) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
