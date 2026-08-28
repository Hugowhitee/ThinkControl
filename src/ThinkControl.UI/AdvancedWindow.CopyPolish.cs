using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _copyPolishConfigured;

    private void ConfigureCopyPolish()
    {
        if (_copyPolishConfigured)
            return;
        _copyPolishConfigured = true;

        // Keep the home shortcut vocabulary identical to Performance and compact.
        // The underlying enum/tag intentionally remains Quiet for compatibility.
        if (HomeQuiet?.Content is StackPanel homeEfficiency)
        {
            TextBlock[] labels = homeEfficiency.Children.OfType<TextBlock>().ToArray();
            if (labels.Length > 0)
                labels[0].Text = "Efficiency";
            if (labels.Length > 1)
                labels[1].Text = "Lower power";
        }

        ConfigureVendorSupportShortcut();
    }

    private void ConfigureVendorSupportShortcut()
    {
        string manufacturer = _app.CurrentManufacturer?.Trim() ?? string.Empty;
        SystemVendorCardTitle.Text = "Device & drivers";
        SystemVendorPrimaryButton.Visibility = Visibility.Collapsed;
        SystemVendorPrimaryButton.ToolTip = null;

        (string Label, string Target, string Detail)? vendor = manufacturer.ToUpperInvariant() switch
        {
            string value when value.Contains("LENOVO") => (
                "Lenovo Vantage",
                "ms-windows-store://search/?query=Lenovo%20Vantage",
                "Windows driver updates plus Lenovo Vantage for supported Lenovo-specific settings."),
            string value when value.Contains("ASUS") => (
                "MyASUS",
                "ms-windows-store://search/?query=MyASUS",
                "Windows driver updates plus MyASUS for supported ASUS-specific settings."),
            string value when value.Contains("DELL") => (
                "Dell Support",
                "https://www.dell.com/support/home",
                "Windows driver updates plus Dell's support page for detected Dell hardware."),
            string value when value.Contains("HP") || value.Contains("HEWLETT") => (
                "HP Support",
                "https://support.hp.com/",
                "Windows driver updates plus HP's support page for detected HP hardware."),
            _ => null
        };

        if (vendor is not { } resolved)
        {
            SystemVendorCardDetail.Text = "Windows Update remains the universal driver path. ThinkControl adds no vendor shortcut when one is not confidently identified.";
            return;
        }

        SystemVendorPrimaryButton.Content = resolved.Label;
        SystemVendorPrimaryButton.Tag = resolved.Target;
        SystemVendorPrimaryButton.Visibility = Visibility.Visible;
        SystemVendorCardDetail.Text = resolved.Detail;
    }
}
