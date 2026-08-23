using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private bool _audioRowAdded;

    private void EnsureAudioRow()
    {
        if (_audioRowAdded || Content is not Border { Child: Grid root })
            return;

        StackPanel? links = root.Children.OfType<StackPanel>().FirstOrDefault(panel => Grid.GetRow(panel) == 4);
        if (links is null)
            return;

        var button = new WpfButton
        {
            Height = 52,
            Style = TryFindResource("CompactLinkRow") as Style,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        button.Click += (_, _) => _app?.OpenAudio();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });

        var icon = new Viewbox { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
        var iconCanvas = new Canvas { Width = 16, Height = 16 };
        var speaker = new System.Windows.Shapes.Path
        {
            Stroke = TryFindResource("Tc.TextMuted") as Brush ?? Brushes.Gray,
            StrokeThickness = 1.35,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse("M2,6 L5,6 L9,3 L9,13 L5,10 L2,10 Z M11,6 C12.5,7 12.5,9 11,10 M13,4 C16,6 16,10 13,12")
        };
        iconCanvas.Children.Add(speaker);
        icon.Child = iconCanvas;
        grid.Children.Add(icon);

        var label = new TextBlock { Text = "Audio", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var value = new TextBlock
        {
            FontSize = 11,
            Foreground = TryFindResource("Tc.TextMuted") as Brush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = _app?.UserSettings.Current.DolbyProfile ?? "Dynamic"
        };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 18,
            Foreground = TryFindResource("Tc.TextMuted") as Brush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrow, 3);
        grid.Children.Add(arrow);

        button.Content = grid;
        int keyboardIndex = -1;
        for (int i = 0; i < links.Children.Count; i++)
        {
            if (links.Children[i] is WpfButton existing && existing.Tag?.ToString() == "Keyboard")
            {
                keyboardIndex = i;
                break;
            }
        }
        links.Children.Insert(keyboardIndex >= 0 ? keyboardIndex : links.Children.Count, button);
        _audioRowAdded = true;
    }
}
