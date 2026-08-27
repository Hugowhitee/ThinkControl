using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
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
    private int _editingCompactMetricSlot = -1;
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
            TcToolTip.Apply(slots[i], $"Change {definition.Label.ToLowerInvariant()} metric");
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
        if (sender is not Button { Tag: string raw } slot || !int.TryParse(raw, out int index) || index is < 0 or > 2)
            return;

        _editingCompactMetricSlot = index;
        CompactMetricPickerItems.Children.Clear();
        HashSet<string> inUse = _compactMetricSlots.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (CompactMetricDefinition definition in CompactMetricDefinitions.Where(definition => !inUse.Contains(definition.Id)))
        {
            var option = new Button
            {
                Tag = definition.Id,
                Content = definition.Label[0] + definition.Label[1..].ToLowerInvariant(),
                Style = (Style)FindResource("TcButton"),
                Padding = new Thickness(9, 7, 9, 7),
                Margin = new Thickness(3)
            };
            option.Click += CompactMetricOption_Click;
            CompactMetricPickerItems.Children.Add(option);
        }

        CompactMetricPicker.PlacementTarget = slot;
        CompactMetricPicker.HorizontalOffset = -20;
        CompactMetricPicker.VerticalOffset = 5;
        CompactMetricPicker.IsOpen = true;
    }

    private void CompactMetricOption_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCompactMetricSlot is < 0 or > 2 || sender is not Button { Tag: string id })
            return;

        _compactMetricSlots[_editingCompactMetricSlot] = DefinitionFor(id).Id;
        _compactMetricLayout.Save(_compactMetricSlots);
        CompactMetricPicker.IsOpen = false;
        _editingCompactMetricSlot = -1;
        RefreshCompactMetrics();
    }

    private static CompactMetricDefinition DefinitionFor(string id) =>
        CompactMetricDefinitions.FirstOrDefault(definition => definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? CompactMetricDefinitions[0];
}
