using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI.Controls;

public partial class AdvancedPageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(AdvancedPageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(AdvancedPageHeader),
        new PropertyMetadata(string.Empty, OnSubtitleChanged));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(AdvancedPageHeader), new PropertyMetadata(null));

    public AdvancedPageHeader()
    {
        InitializeComponent();
        UpdateSubtitleVisibility();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public StackPanel EnsureActionStack()
    {
        if (Actions is StackPanel stack)
            return stack;

        var rail = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Actions is UIElement existing)
            rail.Children.Add(existing);
        Actions = rail;
        return rail;
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AdvancedPageHeader)d).UpdateSubtitleVisibility();

    private void UpdateSubtitleVisibility()
    {
        if (SubtitleText is null)
            return;

        SubtitleText.Visibility = string.IsNullOrWhiteSpace(Subtitle)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
