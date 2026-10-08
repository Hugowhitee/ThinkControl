using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
        bool AccentValue = false,
        string? Unit = null,
        string? ValueFormat = null);

    private static readonly CompactMetricDefinition[] CompactMetricDefinitions =
    [
        new("Battery", "BATTERY", "BatteryPercentText", "BatteryEtaText", "Battery"),
        new("CPU", "CPU", "CpuTemperatureC", "CPU sensor", "Diagnostics", Unit: "°C", ValueFormat: "0"),
        new("Fans", "FAN SPEED", "FanRpm", "FanCountText", "Fans", Unit: "RPM", ValueFormat: "N0"),
        new("Power", "POWER", "BatteryPowerText", "BatteryAveragePowerText", "Battery"),
        new("Sensors", "SENSORS", "SensorCountText", "Hardware telemetry", "Diagnostics"),
        new("Display", "DISPLAY", "CurrentRefreshText", "Refresh rate", "Display"),
        new("Keyboard", "KEYBOARD", "KeyboardStatus", "Keyboard light", "Keyboard"),
        new("Performance", "PERFORMANCE", "SelectedPowerModeDisplay", "Power mode", "Performance")
    ];

    private readonly CompactMetricLayoutService _compactMetricLayout = new();
    private string[] _compactMetricSlots = ["Battery", "CPU", "Fans"];
    private bool _compactMetricsReady;
    private string[] _compactControlSlots = ["Performance", "Fans", "Display", "Mode"];

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
            var gauge = new BatteryGauge { Width = 34, Height = 18, MotionEnabled = false, Margin = new Thickness(0, 5, 6, 0), VerticalAlignment = VerticalAlignment.Center };
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

    internal void OpenCustomization() => OpenLayoutEditor(Window.GetWindow(this));

    private static string FriendlyMetricName(CompactMetricDefinition definition) => definition.Label switch
    {
        "CPU" => "CPU",
        _ => definition.Label[0] + definition.Label[1..].ToLowerInvariant()
    };

    private static CompactMetricDefinition DefinitionFor(string id) =>
        CompactMetricDefinitions.FirstOrDefault(definition => definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? CompactMetricDefinitions[0];
}
