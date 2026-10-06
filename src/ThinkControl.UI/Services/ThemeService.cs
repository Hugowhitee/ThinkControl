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
    // Use assembly-qualified pack URIs. ThemeService is also exercised by the
    // separate snapshot host; plain relative URIs would resolve against that EXE
    // instead of ThinkControl.UI and make visual QA differ from the real app.
    private const string SelectionStylesSource = "/ThinkControl.UI;component/Resources/SelectionStyles.xaml";
    private const string ScrollBarStylesSource = "/ThinkControl.UI;component/Resources/ScrollBarStyles.xaml";

    public static ThemeMode Current { get; private set; } = ThemeMode.System;

    public static bool IsLightEffective =>
        Current == ThemeMode.Light || (Current == ThemeMode.System && SystemPrefersLight());

    public static void Apply(ThemeMode mode)
    {
        WindowCaptionTheme.Register();
        Current = mode;
        bool light = IsLightEffective;
        ResourceDictionary resources = System.Windows.Application.Current.Resources;

        foreach (ResourceDictionary previous in resources.MergedDictionaries
                     .Where(item => item.Source?.OriginalString.Contains("Theme.", StringComparison.Ordinal) == true).ToArray())
            resources.MergedDictionaries.Remove(previous);
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/ThinkControl.UI;component/Resources/Theme.{(light ? "Light" : "Dark")}.xaml", UriKind.Relative)
        });

        // Native WPF selectors can still consult Windows system-selection colors
        // even when a custom container template is not in play. Keep this fallback
        // neutral too, so no system-blue flash can leak through the app.
        resources[SystemColors.HighlightBrushKey] = resources["Tc.SurfaceHover"];
        resources[SystemColors.HighlightTextBrushKey] = resources["Tc.Text"];
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = resources["Tc.SurfaceAlt"];
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = resources["Tc.Text"];

        EnsureSharedStyles(resources, SelectionStylesSource, "SelectionStyles.xaml");
        EnsureSharedStyles(resources, ScrollBarStylesSource, "ScrollBarStyles.xaml");
        foreach (Window window in System.Windows.Application.Current.Windows)
            WindowCaptionTheme.Apply(window);
    }

    private static void EnsureSharedStyles(ResourceDictionary resources, string source, string fileName)
    {
        bool alreadyLoaded = resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) == true);
        if (alreadyLoaded)
            return;

        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(source, UriKind.RelativeOrAbsolute)
        });
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
