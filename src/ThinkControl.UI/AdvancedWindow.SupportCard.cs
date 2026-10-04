using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string SupportCardKey = "ThinkControl.Settings.SupportCard";
    private const string BuyMeACoffeeUrl = "https://buymeacoffee.com/hugowhite";

    private void ConfigureSupportCard()
    {
        if (Resources.Contains(SupportCardKey))
            return;

        Resources[SupportCardKey] = true;
        AddSettingsSupportCard(SettingsAdvancedBody);
    }

    private void AddSettingsSupportCard(StackPanel settingsStack)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = "Support ThinkControl",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var link = new Button
        {
            Content = "Buy me a coffee ↗",
            Style = TryFindResource("TcExternalSettingsLink") as Style,
            ToolTip = "Open the voluntary ThinkControl support page"
        };
        link.Click += (_, _) => OpenBuyMeACoffee();
        Grid.SetColumn(link, 1);
        grid.Children.Add(link);

        var row = new Border
        {
            Style = TryFindResource("TcSettingsRow") as Style,
            Child = grid
        };
        Border? reset = settingsStack.Children.OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, GlobalResetCardTag));
        int insertAt = reset is null
            ? settingsStack.Children.Count
            : settingsStack.Children.IndexOf(reset);
        settingsStack.Children.Insert(insertAt, row);
    }

    private static void OpenBuyMeACoffee()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BuyMeACoffeeUrl) { UseShellExecute = true });
        }
        catch { }
    }
}
