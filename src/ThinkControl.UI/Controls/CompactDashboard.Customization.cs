using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private Window? _layoutEditor;
    internal Window? LayoutEditorForSnapshot => _layoutEditor;
    private const string LayoutDragFormat = "ThinkControl.CompactLayout";

    private void RefreshCompactControls()
    {
        ComboBox[] controls = [CompactPerformanceCombo, CompactFanCombo, CompactRefreshCombo, CompactKeyboardCombo];
        string[] ids = ["Performance", "Fans", "Display", "Keyboard"];
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i].Parent is not FrameworkElement stack || stack.Parent is not Border card) continue;
            int slot = Array.IndexOf(_compactControlSlots, ids[i]);
            Grid.SetRow(card, slot / 2);
            Grid.SetColumn(card, slot % 2);
            card.Margin = new Thickness(slot % 2 == 0 ? 0 : 6, slot < 2 ? 0 : 6, slot % 2 == 0 ? 6 : 0, slot < 2 ? 6 : 0);
        }
    }

    internal void OpenLayoutEditor(Window? owner)
    {
        if (_layoutEditor is not null) { _layoutEditor.Activate(); return; }
        EnsureCompactMetrics();
        var surface = new Border { Padding = new Thickness(24), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        surface.SetResourceReference(Border.BackgroundProperty, "Tc.Window");
        surface.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        var window = new Window { Title = "ThinkControl: Compact layout", Width = 920, Height = 590,
            MinWidth = 780, MinHeight = 500, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResize,
            Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = surface,
            DataContext = DataContext, ShowInTaskbar = false, UseLayoutRounding = true };
        window.SetResourceReference(Window.FontFamilyProperty, "Tc.Font");
        window.SetResourceReference(Window.ForegroundProperty, "Tc.Text");
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0) });
        _layoutEditor = window;
        window.Closed += (_, _) => _layoutEditor = null;
        void Rebuild()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var done = new Button { Content = "Done", Style = TryFindResource("TcButton") as Style };
            done.Click += (_, _) => window.Close(); DockPanel.SetDock(done, Dock.Right); heading.Children.Add(done);
            var reset = new Button { Content = "Reset layout", Style = TryFindResource("TcInlineButton") as Style, Margin = new Thickness(0, 0, 12, 0) };
            reset.Click += (_, _) => { _compactMetricSlots = ["Battery", "CPU", "Fans"]; _compactControlSlots = ["Performance", "Fans", "Display", "Keyboard"]; Save(); Rebuild(); };
            DockPanel.SetDock(reset, Dock.Right); heading.Children.Add(reset);
            var title = new TextBlock { Text = "Compact layout", FontSize = TypographyScale.PageTitle, FontWeight = FontWeights.SemiBold };
            title.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) window.DragMove(); };
            heading.Children.Add(title); root.Children.Add(heading);
            var hint = new TextBlock { Text = "Drag onto a preview tile to swap. Changes save automatically.", FontSize = TypographyScale.Caption, Margin = new Thickness(0, 0, 0, 24) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted"); Grid.SetRow(hint, 1); root.Children.Add(hint);
            var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); columns.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(columns, 2); root.Children.Add(columns);
            for (int family = 0; family < 2; family++)
            {
                bool metrics = family == 0; string[] slots = metrics ? _compactMetricSlots : _compactControlSlots;
                var panel = new StackPanel(); Grid.SetColumn(panel, family * 2); columns.Children.Add(panel);
                panel.Children.Add(new TextBlock { Text = metrics ? "Status" : "Quick controls", FontSize = TypographyScale.SectionTitle, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
                var preview = new System.Windows.Controls.Primitives.UniformGrid { Columns = metrics ? 3 : 2, Height = metrics ? 90 : 192 };
                panel.Children.Add(preview);
                for (int i = 0; i < slots.Length; i++)
                {
                    int target = i;
                    object content = metrics ? BuildCompactMetricContent(DefinitionFor(slots[i])) : BuildControlPreview(slots[i]);
                    preview.Children.Add(Tile(slots[i], content, metrics, id => { int source = Array.IndexOf(slots, id); if (source == target) return;
                        if (source >= 0) (slots[source], slots[target]) = (slots[target], slots[source]); else if (metrics && CompactMetricDefinitions.Any(d => d.Id == id)) slots[target] = id; else return;
                        Save(); Rebuild(); }));
                }
                panel.Children.Add(new TextBlock { Text = "Available", FontSize = TypographyScale.Caption, Margin = new Thickness(0, 20, 0, 8) });
                if (metrics)
                {
                    var available = new WrapPanel(); panel.Children.Add(available);
                    foreach (var definition in CompactMetricDefinitions.Where(d => !slots.Contains(d.Id))) available.Children.Add(Tile(definition.Id, FriendlyMetricName(definition), true, null));
                }
                else panel.Children.Add(new TextBlock { Text = "All controls in use", FontSize = TypographyScale.Caption });
            }
            surface.Child = root;
        }
        void Save() { _compactMetricLayout.Save(_compactMetricSlots); _compactMetricLayout.SaveControls(_compactControlSlots); RefreshCompactMetrics(); RefreshCompactControls(); }
        Rebuild(); window.Show();
    }

    private Button Tile(string id, object content, bool metric, Action<string>? drop)
    {
        var tile = new Button { Content = content, Style = TryFindResource("TcButton") as Style, Margin = new Thickness(3), Padding = new Thickness(8), Cursor = Cursors.SizeAll,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, AllowDrop = drop is not null };
        System.Windows.Point? origin = null;
        tile.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(tile);
        tile.PreviewMouseLeftButtonUp += (_, _) => origin = null;
        tile.LostMouseCapture += (_, _) => origin = null;
        tile.PreviewMouseMove += (_, e) => { if (origin is not System.Windows.Point start || e.LeftButton != MouseButtonState.Pressed) return;
            var at = e.GetPosition(tile); if (Math.Abs(at.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(at.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null; System.Windows.DragDrop.DoDragDrop(tile, new System.Windows.DataObject(LayoutDragFormat, (metric ? "M:" : "C:") + id), System.Windows.DragDropEffects.Move); };
        tile.DragOver += (_, e) => { e.Effects = e.Data.GetData(LayoutDragFormat) is string raw && raw.StartsWith(metric ? "M:" : "C:", StringComparison.Ordinal) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None; e.Handled = true; };
        tile.Drop += (_, e) => { if (e.Data.GetData(LayoutDragFormat) is string raw && raw.StartsWith(metric ? "M:" : "C:", StringComparison.Ordinal)) drop?.Invoke(raw[2..]); e.Handled = true; };
        return tile;
    }
    private static string ControlName(string id) => id switch { "Performance" => "Power profile", "Fans" => "Cooling", "Display" => "Refresh rate", _ => "Keyboard light" };
    private FrameworkElement BuildControlPreview(string id)
    {
        var source = id switch { "Performance" => CompactPerformanceCombo, "Fans" => CompactFanCombo, "Display" => CompactRefreshCombo, _ => CompactKeyboardCombo };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = ControlName(id), FontSize = TypographyScale.ControlLabel, FontWeight = FontWeights.SemiBold });
        var combo = new ComboBox { Style = TryFindResource("CompactSelect") as Style, Margin = new Thickness(0, 10, 0, 0), IsHitTestVisible = false, Focusable = false };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding("ItemsSource") { Source = source });
        combo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new System.Windows.Data.Binding("SelectedItem") { Source = source, Mode = System.Windows.Data.BindingMode.OneWay });
        combo.SetBinding(IsEnabledProperty, new System.Windows.Data.Binding("IsEnabled") { Source = source });
        panel.Children.Add(combo);
        return panel;
    }
}
