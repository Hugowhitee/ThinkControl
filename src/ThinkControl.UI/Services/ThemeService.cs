using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace ThinkControl.UI.Services;

public enum ThemeMode
{
    System,
    Dark,
    Light
}

public static class ThemeService
{
    public static ThemeMode Current { get; private set; } = ThemeMode.System;

    public static bool IsLightEffective =>
        Current == ThemeMode.Light || (Current == ThemeMode.System && SystemPrefersLight());

    public static void Apply(ThemeMode mode)
    {
        Current = mode;
        bool light = IsLightEffective;
        ResourceDictionary resources = System.Windows.Application.Current.Resources;

        string window = light ? "#F5F5F7" : "#101214";
        SetBrush(resources, "Tc.Window", window);
        SetBrush(resources, "Tc.Background", window);
        SetBrush(resources, "Tc.Surface", light ? "#FFFFFF" : "#171A1D");
        SetBrush(resources, "Tc.SurfaceAlt", light ? "#F1F2F4" : "#1D2024");
        SetBrush(resources, "Tc.SurfaceHover", light ? "#E7E9EC" : "#25292E");
        SetBrush(resources, "Tc.Border", light ? "#D5D8DC" : "#34383D");
        SetBrush(resources, "Tc.BorderStrong", light ? "#B9BDC3" : "#474C52");
        SetBrush(resources, "Tc.Text", light ? "#15171A" : "#F2F3F4");
        SetBrush(resources, "Tc.TextMuted", light ? "#555C64" : "#A8ADB4");
        SetBrush(resources, "Tc.TextFaint", light ? "#737A82" : "#858B92");
        SetBrush(resources, "Tc.Accent", "#E32929");
        SetBrush(resources, "Tc.AccentHover", "#F13B3B");
        SetBrush(resources, "Tc.Success", light ? "#168A45" : "#4CCB7A");
        SetBrush(resources, "Tc.Warning", light ? "#A76800" : "#E7A640");

        // Stock WPF controls otherwise fall back to the Windows blue selection
        // brush. A few diagnostic/list surfaces intentionally keep native control
        // behavior, so override only the system selection palette app-wide and let
        // their own templates/layout remain untouched.
        resources[SystemColors.HighlightBrushKey] = resources["Tc.SurfaceHover"];
        resources[SystemColors.HighlightTextBrushKey] = resources["Tc.Text"];
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = resources["Tc.SurfaceAlt"];
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = resources["Tc.Text"];
    }

    private static bool SystemPrefersLight()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(color));
    }
}
