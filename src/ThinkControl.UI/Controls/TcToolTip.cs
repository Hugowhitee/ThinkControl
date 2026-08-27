using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfToolTip = System.Windows.Controls.ToolTip;
using WpfToolTipService = System.Windows.Controls.ToolTipService;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Compact hover labels for icon-only controls. These intentionally behave more
/// like small interface labels than Windows help balloons: short delay, one-line
/// copy, restrained chrome and a quick fade.
/// </summary>
public static class TcToolTip
{
    public static void Apply(FrameworkElement owner, string text, PlacementMode placement = PlacementMode.Mouse)
    {
        if (owner.ToolTip is WpfToolTip existing &&
            existing.Tag as string == "ThinkControl.CompactToolTip" &&
            existing.Content is Border { Child: TextBlock copy })
        {
            copy.Text = text;
            existing.Placement = placement;
            existing.PlacementTarget = owner;
            existing.HorizontalOffset = placement == PlacementMode.Right ? 8 : 0;
            return;
        }

        var label = new TextBlock
        {
            Text = text,
            FontSize = TypographyScale.Secondary,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");

        var surface = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5, 8, 5),
            Child = label
        };
        surface.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");
        surface.SetResourceReference(Border.BorderBrushProperty, "Tc.BorderStrong");

        var tip = new WpfToolTip
        {
            Tag = "ThinkControl.CompactToolTip",
            Content = surface,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HasDropShadow = false,
            Placement = placement,
            PlacementTarget = owner,
            HorizontalOffset = placement == PlacementMode.Right ? 8 : 0
        };
        tip.Opened += (_, _) =>
        {
            surface.BeginAnimation(UIElement.OpacityProperty, null);
            surface.Opacity = 0;
            surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(90))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
            surface.Opacity = 1;
        };

        owner.ToolTip = tip;
        WpfToolTipService.SetInitialShowDelay(owner, 360);
        WpfToolTipService.SetBetweenShowDelay(owner, 70);
        WpfToolTipService.SetShowDuration(owner, 4000);
    }
}
