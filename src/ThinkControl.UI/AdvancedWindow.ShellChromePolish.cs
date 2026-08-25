using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string ShellChromePolishKey = "ThinkControl.Advanced.ShellChromePolish";

    /// <summary>
    /// Keep the native Windows caption for Snap Layouts/system-menu reliability, but
    /// add a thin in-app command/drag strip directly underneath it. The strip makes
    /// the window hierarchy visually obvious and gives Compact view a clear home
    /// near the native caption controls without creating duplicate min/max/close UI.
    /// </summary>
    private void ConfigureShellChromePolish()
    {
        if (Resources.Contains(ShellChromePolishKey))
            return;

        if (Content is not Border { Child: Grid root } || root.RowDefinitions.Count < 2)
            return;

        Resources[ShellChromePolishKey] = true;

        // ConfigureNativeWindow collapses the legacy custom caption because Windows
        // owns the real title bar. Reuse that content row as a small app command strip.
        root.RowDefinitions[0].Height = new GridLength(36);
        foreach (UIElement oldCaption in root.Children
                     .OfType<UIElement>()
                     .Where(element => Grid.GetRow(element) == 0)
                     .ToArray())
        {
            root.Children.Remove(oldCaption);
        }

        // Remove the old tiny sidebar "Advanced ↙" control. Notifications will take
        // this high-signal sidebar position; view switching now belongs in the strip.
        if (NavHome.Parent is StackPanel navStack)
        {
            Grid? oldDockRow = navStack.Children.OfType<Grid>().FirstOrDefault(grid =>
                grid.Children.OfType<TextBlock>().Any(text =>
                    string.Equals(text.Text, "Advanced", StringComparison.OrdinalIgnoreCase)));
            if (oldDockRow is not null)
                navStack.Children.Remove(oldDockRow);
        }

        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var viewLabel = new TextBlock
        {
            Text = "Advanced view",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        viewLabel.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        left.Children.Add(viewLabel);

        var divider = new Border
        {
            Width = 1,
            Height = 15,
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        divider.SetResourceReference(Border.BackgroundProperty, "Tc.BorderStrong");
        left.Children.Add(divider);

        var device = new TextBlock
        {
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 260,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        device.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        device.SetBinding(TextBlock.TextProperty, new Binding("DeviceName"));
        left.Children.Add(device);

        var compactIcon = new Viewbox
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 7, 0)
        };
        var compactPath = new Path
        {
            Data = Geometry.Parse("M2,2 H14 V14 H2 Z M5,5 H11 V11 H5 Z"),
            StrokeThickness = 1.35,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent
        };
        compactPath.SetResourceReference(Shape.StrokeProperty, "Tc.TextMuted");
        compactIcon.Child = compactPath;

        var compactContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        compactContent.Children.Add(compactIcon);
        compactContent.Children.Add(new TextBlock
        {
            Text = "Compact view",
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center
        });

        var compactButton = new Button
        {
            Content = compactContent,
            Style = TryFindResource("TcButton") as Style,
            Height = 28,
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Switch to the compact tray view"
        };
        compactButton.Click += (_, _) => _app.ReturnToCompact();

        var rightDivider = new Border
        {
            Width = 1,
            Height = 18,
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        rightDivider.SetResourceReference(Border.BackgroundProperty, "Tc.Border");

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        right.Children.Add(rightDivider);
        right.Children.Add(compactButton);

        var layout = new Grid { Margin = new Thickness(14, 0, 8, 0) };
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.Children.Add(left);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);

        // A small centered grip plus the bottom separator makes the drag region read
        // as a deliberate strip instead of the page bleeding into the caption.
        var grip = new Border
        {
            Width = 34,
            Height = 2,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 0, 0),
            IsHitTestVisible = false,
            Opacity = 0.8
        };
        grip.SetResourceReference(Border.BackgroundProperty, "Tc.BorderStrong");
        layout.Children.Add(grip);

        var strip = new Border
        {
            Child = layout,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.Arrow
        };
        strip.SetResourceReference(Border.BackgroundProperty, "Tc.Window");
        strip.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        strip.PreviewMouseLeftButtonDown += ShellStrip_MouseLeftButtonDown;

        Grid.SetRow(strip, 0);
        root.Children.Add(strip);
    }

    private void ShellStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && HasButtonAncestor(source))
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch
        {
            // DragMove can throw if Windows ends the mouse capture between the
            // button event and the native move loop. That is harmless.
        }
    }

    private static bool HasButtonAncestor(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is Button)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
