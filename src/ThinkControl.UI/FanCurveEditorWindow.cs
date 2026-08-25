using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.Core.Cooling;

namespace ThinkControl.UI;

internal sealed class FanCurveEditorWindow : Window
{
    private readonly App _app;
    private readonly ListBox _profiles = new();
    private readonly FanCurveGraph _graph = new();
    private readonly Slider _temperature = new() { Minimum = FanCurveGraphPolicy.MinTemperatureC, Maximum = FanCurveGraphPolicy.MaxTemperatureC, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly Slider _output = new() { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly TextBlock _pointValue = new();
    private readonly TextBlock _status = new();
    private readonly Button _rename = new();
    private readonly Button _delete = new();
    private readonly Button _reset = new();
    private readonly Button _apply = new();
    private bool _syncing;
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
        _profiles.SelectionChanged += Profiles_SelectionChanged;
        _graph.SelectionChanged += Graph_SelectionChanged;
        _graph.CurveChanged += Graph_CurveChanged;
        _temperature.ValueChanged += Precision_ValueChanged;
        _output.ValueChanged += Precision_ValueChanged;

        Loaded += (_, _) => ReloadProfiles(_app.UserSettings.Current.CoolingProfile);
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
            Text = "Tune temperature against a real 0–100% target. ThinkControl maps the graph to measured X9 EC states after characterization; 100% uses the separately verified full-speed state.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        heading.Children.Add(subtitle);
        root.Children.Add(heading);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var profileCard = Section();
        var profileStack = new StackPanel();
        profileStack.Children.Add(new TextBlock { Text = "Profiles", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        _profiles.MinHeight = 260;
        _profiles.BorderThickness = new Thickness(0);
        _profiles.Background = Brushes.Transparent;
        profileStack.Children.Add(_profiles);

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
        _reset.Content = "Factory reset";
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

        var editorHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        editorHeader.ColumnDefinitions.Add(new ColumnDefinition());
        editorHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editorHeader.Children.Add(new TextBlock { Text = "Temperature / fan target", FontWeight = FontWeights.SemiBold });
        var safety = new TextBlock { Text = "94 °C → Lenovo firmware", FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        safety.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(safety, 1);
        editorHeader.Children.Add(safety);
        editor.Children.Add(editorHeader);

        _graph.MinHeight = 310;
        _graph.Focusable = true;
        Grid.SetRow(_graph, 1);
        editor.Children.Add(_graph);

        var precision = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        precision.ColumnDefinitions.Add(new ColumnDefinition());
        precision.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        precision.ColumnDefinitions.Add(new ColumnDefinition());

        var tempPanel = PrecisionPanel("Temperature", _temperature);
        var outputPanel = PrecisionPanel("Fan target", _output);
        Grid.SetColumn(outputPanel, 2);
        precision.Children.Add(tempPanel);
        precision.Children.Add(outputPanel);
        precision.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        precision.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _pointValue.FontSize = 10;
        _pointValue.Margin = new Thickness(0, 7, 0, 0);
        _pointValue.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetRow(_pointValue, 1);
        Grid.SetColumnSpan(_pointValue, 3);
        precision.Children.Add(_pointValue);
        Grid.SetRow(precision, 2);
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

    private StackPanel PrecisionPanel(string title, Slider slider)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontSize = 10, Margin = new Thickness(0, 0, 0, 4) });
        slider.SetResourceReference(Slider.StyleProperty, "TcSlider");
        panel.Children.Add(slider);
        return panel;
    }

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
        FanCurveDefinition[] profiles = _app.FanProfiles.GetProfiles().ToArray();
        _profiles.ItemsSource = profiles;
        _profiles.DisplayMemberPath = nameof(FanCurveDefinition.Name);
        FanCurveDefinition? selected = profiles.FirstOrDefault(profile => string.Equals(profile.Id, selectId, StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault(profile => profile.Id == FanCurveDefaults.BalancedId)
            ?? profiles.FirstOrDefault();
        _profiles.SelectedItem = selected;
    }

    private void Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_profiles.SelectedItem is not FanCurveDefinition selected)
            return;
        _editing = new FanCurveDefinition(selected.Id, selected.Name, selected.Points.Select(point => point with { }).ToArray());
        _graph.SetCurve(_editing.Points);
        _graph.SelectedIndex = 0;
        SyncSelectedPoint();
        bool builtIn = _app.FanProfiles.IsBuiltIn(selected.Id);
        _rename.IsEnabled = !builtIn;
        _delete.IsEnabled = !builtIn;
        _reset.IsEnabled = builtIn;
        _status.Text = builtIn
            ? "Built-in profile · edits are stored as an override; Factory reset restores ThinkControl defaults."
            : "Custom profile · edit the graph, rename it, or delete it without changing the built-in profiles.";
    }

    private void Graph_SelectionChanged(object? sender, EventArgs e) => SyncSelectedPoint();

    private void Graph_CurveChanged(object? sender, EventArgs e)
    {
        SyncSelectedPoint();
        _status.Text = "Unsaved curve changes · hardware is not touched until Save and apply.";
    }

    private void SyncSelectedPoint()
    {
        if (_graph.SelectedPoint is not FanCurvePoint point)
            return;
        _syncing = true;
        try
        {
            _temperature.Value = point.TemperatureC;
            _output.Value = point.Percent;
            _output.IsEnabled = _graph.SelectedIndex != FanCurveGraphPolicy.PointCount - 1;
            _pointValue.Text = $"Point {_graph.SelectedIndex + 1}/8 · {point.TemperatureC:0} °C · {point.Percent}%" +
                               (_graph.SelectedIndex == 7 ? " · final point is locked to 100%" : string.Empty);
        }
        finally { _syncing = false; }
    }

    private void Precision_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || _graph.SelectedIndex < 0)
            return;
        _graph.SetSelectedPoint(_temperature.Value, (int)Math.Round(_output.Value));
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string? source = (_profiles.SelectedItem as FanCurveDefinition)?.Id;
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
        if (_profiles.SelectedItem is not FanCurveDefinition selected || _app.FanProfiles.IsBuiltIn(selected.Id))
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
        if (_profiles.SelectedItem is not FanCurveDefinition selected || _app.FanProfiles.IsBuiltIn(selected.Id))
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
        if (_profiles.SelectedItem is not FanCurveDefinition selected || !_app.FanProfiles.IsBuiltIn(selected.Id))
            return;
        FanCurveDefinition factory = _app.FanProfiles.ResetBuiltIn(selected.Id);
        ReloadProfiles(factory.Id);
        _status.Text = $"{factory.Name} restored to ThinkControl factory curve. Save and apply to activate it now.";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is null)
            return;
        FanCurvePoint[] points = _graph.GetCurve();
        FanCurveDefinition definition = _editing with { Points = points };
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

    internal event EventHandler? SelectionChanged;
    internal event EventHandler? CurveChanged;

    internal int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = _points.Count == 0 ? -1 : Math.Clamp(value, 0, _points.Count - 1);
            if (_selectedIndex == next) return;
            _selectedIndex = next;
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal FanCurvePoint? SelectedPoint => _selectedIndex >= 0 && _selectedIndex < _points.Count ? _points[_selectedIndex] : null;

    internal FanCurveGraph()
    {
        SnapsToDevicePixels = true;
        Cursor = Cursors.Cross;
        KeyDown += OnKeyDown;
    }

    internal void SetCurve(IReadOnlyList<FanCurvePoint> points)
    {
        _points.Clear();
        _points.AddRange(points.Select(point => point with { }));
        _selectedIndex = _points.Count > 0 ? 0 : -1;
        InvalidateVisual();
    }

    internal FanCurvePoint[] GetCurve() => _points.Select(point => point with { }).ToArray();

    internal void SetSelectedPoint(double temperature, int percent)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _points.Count)
            return;
        MovePoint(_selectedIndex, temperature, percent);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = Math.Max(1, ActualWidth);
        double h = Math.Max(1, ActualHeight);
        Rect plot = new(46, 14, Math.Max(40, w - 62), Math.Max(40, h - 48));

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

        DrawText(dc, "Temperature", muted, 9, new Point(plot.Right - 63, plot.Bottom + 7));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (_points.Count == 0) return;
        Rect plot = PlotRect();
        Point mouse = e.GetPosition(this);
        int nearest = Enumerable.Range(0, _points.Count)
            .OrderBy(i => (ToPoint(plot, _points[i]) - mouse).LengthSquared)
            .First();
        if ((ToPoint(plot, _points[nearest]) - mouse).Length > 18)
            return;
        SelectedIndex = nearest;
        _dragging = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || _selectedIndex < 0 || e.LeftButton != MouseButtonState.Pressed)
            return;
        Rect plot = PlotRect();
        Point p = e.GetPosition(this);
        double temperature = FanCurveGraphPolicy.MinTemperatureC +
            Math.Clamp((p.X - plot.Left) / plot.Width, 0, 1) * (FanCurveGraphPolicy.MaxTemperatureC - FanCurveGraphPolicy.MinTemperatureC);
        int percent = (int)Math.Round((1 - Math.Clamp((p.Y - plot.Top) / plot.Height, 0, 1)) * 100);
        MovePoint(_selectedIndex, Math.Round(temperature), percent);
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
        if (_selectedIndex < 0 || _points.Count == 0) return;
        int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1;
        FanCurvePoint point = _points[_selectedIndex];
        switch (e.Key)
        {
            case Key.Tab:
                SelectedIndex = Math.Clamp(_selectedIndex + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1), 0, _points.Count - 1);
                e.Handled = true;
                return;
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
        double nextT = Math.Clamp(Math.Round(temperature), minT, maxT);
        int nextP = index == _points.Count - 1 ? 100 : Math.Clamp(percent, minP, maxP);
        FanCurvePoint next = new(nextT, nextP);
        if (_points[index] == next) return;
        _points[index] = next;
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
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, origin);
    }
}
