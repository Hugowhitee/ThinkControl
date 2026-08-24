using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using ThinkControl.UI.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string InteractionPolishKey = "ThinkControl.Advanced.InteractionPolish";

    private void ConfigureInteractionPolish()
    {
        if (Resources.Contains(InteractionPolishKey))
            return;
        Resources[InteractionPolishKey] = true;

        AttachScrollReset(NavHome, PageHome);
        AttachScrollReset(NavPerformance, PagePerformance);
        AttachScrollReset(NavFans, PageFans);
        AttachScrollReset(NavDisplay, PageDisplay);
        AttachScrollReset(NavKeyboard, PageKeyboard);
        AttachScrollReset(NavBattery, PageBattery);
        AttachScrollReset(NavSystem, PageSystem);
        AttachScrollReset(NavUpdates, PageUpdates);
        AttachScrollReset(NavSettings, PageSettings);

        AttachDynamicScrollReset("ThinkControl.Dynamic.NavTouchpad", "ThinkControl.Dynamic.PageTouchpad");
        AttachDynamicScrollReset("ThinkControl.Dynamic.NavSensors", "ThinkControl.Dynamic.PageSensors");
        AttachDynamicScrollReset("ThinkControl.Dynamic.NavAudio", "ThinkControl.Dynamic.PageAudio");

        if (Resources["ThinkControl.Dynamic.NavSensors"] is RadioButton sensorsNav)
        {
            foreach (Path path in FindVisualChildren<Path>(sensorsNav))
            {
                path.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground))
                {
                    Source = sensorsNav,
                    Mode = BindingMode.OneWay
                });
            }
        }
        ConfigureDynamicNavIconWeight();

        FixSwitchRow(DisplayAdaptiveSwitch);
        FixSwitchRow(HomeAdaptiveSwitch);
        ConfigureUpdateControls();
    }

    private static void AttachScrollReset(RadioButton nav, ScrollViewer page)
    {
        nav.Checked += (_, _) => page.Dispatcher.BeginInvoke(() => page.ScrollToTop());
    }

    private void AttachDynamicScrollReset(string navKey, string pageKey)
    {
        if (Resources[navKey] is not RadioButton nav || Resources[pageKey] is not ScrollViewer page)
            return;
        AttachScrollReset(nav, page);
    }

    private static void FixSwitchRow(WpfCheckBox toggle)
    {
        toggle.VerticalAlignment = VerticalAlignment.Center;
        toggle.Margin = new Thickness(0, 4, 0, 4);
        if (toggle.Parent is Grid row)
        {
            row.MinHeight = Math.Max(row.MinHeight, 32);
            row.ClipToBounds = false;
        }
    }

    private void ConfigureUpdateControls()
    {
        if (PageUpdates.Content is not StackPanel stack)
            return;

        TextBlock? description = stack.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();
        if (description is not null)
        {
            description.Text = "ThinkControl checks GitHub Releases automatically, verifies SHA-256 and installs updates in place. No permanent updater service is required.";
        }

        WpfButton? checkButton = FindVisualChildren<WpfButton>(PageUpdates)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Check for updates", StringComparison.Ordinal));
        if (checkButton is not null)
        {
            checkButton.Click -= CheckUpdates_Click;
            checkButton.Click += CheckUpdatesAndPrepare_Click;
        }

        OpenReleaseButton.Click -= OpenRelease_Click;
        OpenReleaseButton.Click += InstallUpdate_Click;
        OpenReleaseButton.Content = "Install update";
        OpenReleaseButton.ToolTip = "Download, verify and install the newest ThinkControl release";
        OpenReleaseButton.IsEnabled = false;

        var autoSwitch = new WpfCheckBox
        {
            Style = TryFindResource("TcSwitch") as Style,
            IsChecked = _app.UserSettings.Current.AutomaticUpdates,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        autoSwitch.Click += (_, _) =>
            _app.UserSettings.Update(settings => settings with { AutomaticUpdates = autoSwitch.IsChecked == true });

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = "Automatic updates", FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock
        {
            Text = "Check in the background and install verified releases automatically. Windows may show the normal administrator prompt when setup starts.",
            FontSize = 10.5,
            Margin = new Thickness(0, 4, 80, 0),
            TextWrapping = TextWrapping.Wrap
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);

        var grid = new Grid();
        grid.Children.Add(copy);
        grid.Children.Add(autoSwitch);

        var card = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Child = grid
        };
        stack.Children.Add(card);
    }

    private async void CheckUpdatesAndPrepare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button)
            return;

        button.IsEnabled = false;
        OpenReleaseButton.IsEnabled = false;
        _app.State.UpdateStatus = "Checking…";
        try
        {
            _lastUpdate = await _app.UpdateService.CheckAsync();
            _app.State.UpdateStatus = _lastUpdate.Status;
            bool ready = _lastUpdate.Available &&
                !string.IsNullOrWhiteSpace(_lastUpdate.InstallerUrl) &&
                !string.IsNullOrWhiteSpace(_lastUpdate.ChecksumUrl);
            OpenReleaseButton.IsEnabled = ready;
            OpenReleaseButton.Content = ready ? $"Install {_lastUpdate.Version}" : "Install update";
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        WpfButton button = OpenReleaseButton;
        button.IsEnabled = false;
        try
        {
            if (_lastUpdate is null || !_lastUpdate.Available)
                _lastUpdate = await _app.UpdateService.CheckAsync();

            if (_lastUpdate is null || !_lastUpdate.Available)
            {
                _app.State.UpdateStatus = _lastUpdate?.Status ?? "No newer release is available";
                return;
            }

            _app.State.UpdateStatus = $"Downloading {_lastUpdate.Version ?? "update"}…";
            button.Content = "Verifying…";
            UpdateInstallResult result = await _app.UpdateService.DownloadAndLaunchAsync(_lastUpdate);
            _app.State.UpdateStatus = result.Status;
            if (result.Success)
            {
                button.Content = "Installing…";
                _app.ExitApplication();
                return;
            }
        }
        finally
        {
            button.Content = _lastUpdate?.Available == true && !string.IsNullOrWhiteSpace(_lastUpdate.Version)
                ? $"Install {_lastUpdate.Version}"
                : "Install update";
            button.IsEnabled = _lastUpdate?.Available == true &&
                !string.IsNullOrWhiteSpace(_lastUpdate.InstallerUrl) &&
                !string.IsNullOrWhiteSpace(_lastUpdate.ChecksumUrl);
        }
    }
}
