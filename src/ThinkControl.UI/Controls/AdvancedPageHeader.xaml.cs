using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI.Controls;

public enum PageHeaderActionRole { Context = 0, External = 1, Defaults = 2 }

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

    // Actions live in one stable order, independent of page initialization.
    public static readonly DependencyProperty ActionRoleProperty =
        DependencyProperty.RegisterAttached(
            "ActionRole", typeof(PageHeaderActionRole), typeof(AdvancedPageHeader),
            new PropertyMetadata(PageHeaderActionRole.Context));

    public static void SetActionRole(DependencyObject element, PageHeaderActionRole role) =>
        element.SetValue(ActionRoleProperty, role);

    public static PageHeaderActionRole GetActionRole(DependencyObject element) =>
        (PageHeaderActionRole)element.GetValue(ActionRoleProperty);

    public void AddAction(Button button, PageHeaderActionRole role)
    {
        StackPanel rail = EnsureActionStack();
        SetActionRole(button, role);
        int insertAt = 0;
        foreach (UIElement child in rail.Children)
        {
            if (GetActionRole(child) > role)
                break;
            insertAt++;
        }
        rail.Children.Insert(insertAt, button);
        for (int i = 0; i < rail.Children.Count; i++)
        {
            if (rail.Children[i] is FrameworkElement element)
                element.Margin = i == 0 ? new Thickness(0) : new Thickness(10, 0, 0, 0);
        }
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
