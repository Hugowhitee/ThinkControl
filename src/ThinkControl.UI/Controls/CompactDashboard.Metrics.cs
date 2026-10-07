using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private const string CompactMetricDragFormat = "ThinkControl.CompactMetric";

    private sealed record CompactMetricDefinition(
        string Id,
        string Label,
        string ValuePath,
        string DetailPathOrText,
        string Page,
        bool AccentValue = false,
        string? Unit = null,
        string? ValueFormat = null);

    private static readonly CompactMetricDefinition[] CompactMetricDefinitions =
    [
        new("Battery", "BATTERY", "BatteryPercentText", "BatteryEtaText", "Battery"),
        new("CPU", "CPU", "CpuTemperatureC", "CPU sensor", "System", Unit: "°C", ValueFormat: "0"),
        new("Fans", "FAN SPEED", "FanRpm", "FanCountText", "Fans", Unit: "RPM", ValueFormat: "N0"),
        new("Power", "POWER", "BatteryPowerText", "BatteryAveragePowerText", "Battery"),
        new("Sensors", "SENSORS", "SensorCountText", "Hardware telemetry", "System"),
        new("Display", "DISPLAY", "CurrentRefreshText", "Refresh rate", "Display"),
        new("Keyboard", "KEYBOARD", "KeyboardStatus", "Keyboard light", "Keyboard"),
        new("Performance", "PERFORMANCE", "SelectedPowerModeDisplay", "Power mode", "Performance")
    ];

    private readonly CompactMetricLayoutService _compactMetricLayout = new();
    private string[] _compactMetricSlots = ["Battery", "CPU", "Fans"];
    private bool _compactMetricsReady;
    private string[] _compactControlSlots = ["Performance", "Fans", "Display", "Keyboard"];

    private void EnsureCompactMetrics()
    {
        if (_compactMetricsReady)
            return;
        _compactMetricsReady = true;
        _compactMetricSlots = _compactMetricLayout.Load();
        _compactControlSlots = _compactMetricLayout.LoadControls();
        RefreshCompactControls();
        RefreshCompactMetrics();
    }

    private void RefreshCompactMetrics()
    {
        if (!_compactMetricsReady)
            return;

        Button[] slots = [CompactMetricSlot0, CompactMetricSlot1, CompactMetricSlot2];
        for (int i = 0; i < slots.Length; i++)
        {
            CompactMetricDefinition definition = DefinitionFor(_compactMetricSlots[i]);
            slots[i].Content = BuildCompactMetricContent(definition);
            slots[i].ToolTip = null;
        }
    }

    private FrameworkElement BuildCompactMetricContent(CompactMetricDefinition definition)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        TextBlock label = new()
        {
            Text = definition.Label,
            FontSize = TypographyScale.Micro,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        stack.Children.Add(label);

        TextBlock value = new()
        {
            FontSize = TypographyScale.CompactValue,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 5, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var valueBinding = new Binding(definition.ValuePath) { TargetNullValue = "—" };
        if (definition.ValueFormat is string format) valueBinding.StringFormat = "{0:" + format + "}";
        value.SetBinding(TextBlock.TextProperty, valueBinding);
        if (definition.AccentValue)
            value.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Accent");
        var valueRow = new Grid { Height = 34, HorizontalAlignment = HorizontalAlignment.Left };
        valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        valueRow.ColumnDefinitions.Add(new ColumnDefinition());
        valueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (definition.Id == "Battery")
        {
            var gauge = new BatteryGauge { Width = 30, Height = 16, Margin = new Thickness(0, 5, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            gauge.SetBinding(BatteryGauge.PercentProperty, new Binding("BatteryPercent"));
            gauge.SetBinding(BatteryGauge.IsChargingProperty, new Binding("BatteryCharging"));
            gauge.SetBinding(BatteryGauge.IsDischargingProperty, new Binding("BatteryDischarging"));
            valueRow.Children.Add(gauge);
        }
        Grid.SetColumn(value, 1);
        value.VerticalAlignment = VerticalAlignment.Bottom;
        value.Margin = new Thickness(0, 0, 0, 0);
        valueRow.Children.Add(value);
        if (definition.Unit is string unit)
        {
            valueRow.ColumnDefinitions[1].Width = GridLength.Auto;
            value.MaxWidth = 72;
            var unitText = new TextBlock { Text = unit, FontSize = TypographyScale.Micro,
                Margin = new Thickness(6, 0, 0, 4), VerticalAlignment = VerticalAlignment.Bottom };
            unitText.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            Grid.SetColumn(unitText, 2);
            valueRow.Children.Add(unitText);
        }
        stack.Children.Add(valueRow);

        TextBlock detail = new()
        {
            FontSize = TypographyScale.Caption,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (definition.DetailPathOrText.Contains(' '))
        {
            detail.Text = definition.DetailPathOrText;
        }
        else
        {
            var binding = new Binding(definition.DetailPathOrText);
            if (definition.DetailPathOrText == "BatteryEtaText")
                binding.Converter = ReadableTypography.BatteryTimeConverter;
            detail.SetBinding(TextBlock.TextProperty, binding);
        }
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(detail);
        return stack;
    }

    private void CompactMetricSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string raw } || !int.TryParse(raw, out int index) || index is < 0 or > 2)
            return;

        CompactMetricDefinition definition = DefinitionFor(_compactMetricSlots[index]);
        SwitchToAdvanced(definition.Page);
    }

    internal void PrepareMetricEditorForSnapshot()
    {
        EnsureCompactMetrics();
        BuildCompactMetricEditor();
        CompactMetricEditorOverlay.Visibility = Visibility.Visible;
    }

    private void CompactMetricsEdit_Click(object sender, RoutedEventArgs e)
    {
        OpenCustomization();
        e.Handled = true;
    }
    internal void OpenCustomization()
    {
        OpenLayoutEditor(Window.GetWindow(this));
    }

    private void BuildCompactMetricEditor()
    {
        CompactMetricEditSlots.Children.Clear();
        CompactMetricPickerItems.Children.Clear();

        for (int index = 0; index < _compactMetricSlots.Length; index++)
        {
            CompactMetricDefinition definition = DefinitionFor(_compactMetricSlots[index]);
            var slot = new Button
            {
                Tag = index.ToString(),
                Content = FriendlyMetricName(definition),
                Style = TryFindResource("TcButton") as Style,
                Padding = new Thickness(8, 7, 8, 7),
                Margin = new Thickness(3),
                AllowDrop = true,
                Cursor = Cursors.SizeAll
            };
            slot.PreviewMouseMove += CompactMetricDragSource_MouseMove;
            slot.DragOver += CompactMetricSlot_DragOver;
            slot.Drop += CompactMetricSlot_Drop;
            CompactMetricEditSlots.Children.Add(slot);
        }

        HashSet<string> selected = _compactMetricSlots.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (CompactMetricDefinition definition in CompactMetricDefinitions.Where(item => !selected.Contains(item.Id)))
        {
            var option = new Button
            {
                Tag = definition.Id,
                Content = FriendlyMetricName(definition),
                Style = TryFindResource("TcInlineButton") as Style,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(3),
                Cursor = Cursors.SizeAll,
                ToolTip = null
            };
            option.PreviewMouseMove += CompactMetricDragSource_MouseMove;
            CompactMetricPickerItems.Children.Add(option);
        }
    }

    private void CompactMetricDragSource_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not Button source)
            return;

        string? id = source.Tag switch
        {
            string raw when int.TryParse(raw, out int index) && index is >= 0 and <= 2 => _compactMetricSlots[index],
            string raw => DefinitionFor(raw).Id,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(id))
            return;

        var data = new System.Windows.DataObject(CompactMetricDragFormat, id);
        System.Windows.DragDrop.DoDragDrop(source, data, System.Windows.DragDropEffects.Move);
    }

    private static void CompactMetricSlot_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(CompactMetricDragFormat)
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void CompactMetricSlot_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not Button { Tag: string rawTarget } ||
            !int.TryParse(rawTarget, out int targetIndex) || targetIndex is < 0 or > 2 ||
            e.Data.GetData(CompactMetricDragFormat) is not string rawId)
        {
            return;
        }

        string id = DefinitionFor(rawId).Id;
        int sourceIndex = Array.FindIndex(
            _compactMetricSlots,
            current => current.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (sourceIndex == targetIndex)
            return;

        if (sourceIndex >= 0)
        {
            (_compactMetricSlots[sourceIndex], _compactMetricSlots[targetIndex]) =
                (_compactMetricSlots[targetIndex], _compactMetricSlots[sourceIndex]);
        }
        else
        {
            _compactMetricSlots[targetIndex] = id;
        }

        _compactMetricLayout.Save(_compactMetricSlots);
        RefreshCompactMetrics();
        BuildCompactMetricEditor();
        e.Handled = true;
    }

    private void CompactMetricsReset_Click(object sender, RoutedEventArgs e)
    {
        _compactMetricSlots = ["Battery", "CPU", "Fans"];
        _compactMetricLayout.Save(_compactMetricSlots);
        RefreshCompactMetrics();
        BuildCompactMetricEditor();
        e.Handled = true;
    }

    private void CompactMetricsDone_Click(object sender, RoutedEventArgs e)
    {
        CompactMetricEditorOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private static string FriendlyMetricName(CompactMetricDefinition definition) => definition.Label switch
    {
        "CPU" => "CPU",
        _ => definition.Label[0] + definition.Label[1..].ToLowerInvariant()
    };

    private static CompactMetricDefinition DefinitionFor(string id) =>
        CompactMetricDefinitions.FirstOrDefault(definition => definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? CompactMetricDefinitions[0];
}
