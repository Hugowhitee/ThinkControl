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
        bool AccentValue = false);

    private static readonly CompactMetricDefinition[] CompactMetricDefinitions =
    [
        new("Battery", "BATTERY", "BatteryPercentText", "BatteryEtaText", "Battery"),
        new("CPU", "CPU", "CpuTemperatureText", "Live temperature", "System"),
        new("Fans", "FANS", "CoolingProfileDisplay", "FanRpmText", "Fans"),
        new("Power", "POWER", "BatteryPowerText", "BatteryAveragePowerText", "Battery"),
        new("Sensors", "SENSORS", "SensorCountText", "Hardware telemetry", "System"),
        new("Display", "DISPLAY", "CurrentRefreshText", "Refresh rate", "Display"),
        new("Keyboard", "KEYBOARD", "KeyboardStatus", "Keyboard light", "Keyboard"),
        new("Performance", "PERFORMANCE", "SelectedModeDisplay", "Power mode", "Performance")
    ];

    private readonly CompactMetricLayoutService _compactMetricLayout = new();
    private string[] _compactMetricSlots = ["Battery", "CPU", "Fans"];
    private bool _compactMetricsReady;

    private void EnsureCompactMetrics()
    {
        if (_compactMetricsReady)
            return;
        _compactMetricsReady = true;
        _compactMetricSlots = _compactMetricLayout.Load();
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
            FontSize = TypographyScale.Caption,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        stack.Children.Add(label);

        TextBlock value = new()
        {
            FontSize = TypographyScale.Value,
            FontWeight = FontWeights.Light,
            Margin = new Thickness(0, 5, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        value.SetBinding(TextBlock.TextProperty, new Binding(definition.ValuePath));
        if (definition.AccentValue)
            value.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Accent");
        stack.Children.Add(value);

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
        BuildCompactMetricEditor();
        CompactMetricEditorOverlay.Visibility = Visibility.Visible;
        e.Handled = true;
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

        var data = new DataObject(CompactMetricDragFormat, id);
        DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
    }

    private static void CompactMetricSlot_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(CompactMetricDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
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
