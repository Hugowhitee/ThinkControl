using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class KeyboardEffectsPanel : System.Windows.Controls.UserControl
{
    private bool _syncing;
    private AppState? _state;

    public KeyboardEffectsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += (_, _) => AttachState();
    }

    private App? AppHost => System.Windows.Application.Current as App;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachState();
        SyncControls();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_state is not null)
            _state.PropertyChanged -= State_PropertyChanged;
        _state = null;
    }

    private void AttachState()
    {
        if (ReferenceEquals(_state, DataContext))
            return;

        if (_state is not null)
            _state.PropertyChanged -= State_PropertyChanged;

        _state = DataContext as AppState;
        if (_state is not null)
            _state.PropertyChanged += State_PropertyChanged;

        SyncControls();
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.KeyboardMode)
            or nameof(AppState.KeyboardBaseLevel)
            or nameof(AppState.KeyboardEffectSpeed)
            or nameof(AppState.CanKeyboardBacklight)
            or nameof(AppState.CanKeyboardEffects)
            or nameof(AppState.ExperimentalKeyboardEffectsEnabled))
        {
            Dispatcher.Invoke(SyncControls);
        }
    }

    private void SyncControls()
    {
        if (_state is null || !IsInitialized)
            return;

        _syncing = true;
        try
        {
            EffectBreathing.IsChecked = _state.KeyboardMode == "Breathing";
            EffectReactive.IsChecked = _state.KeyboardMode == "Reactive";
            EffectAudio.IsChecked = _state.KeyboardMode == "Audio";
            BaseLow.IsChecked = _state.KeyboardBaseLevel == "Low";
            BaseHigh.IsChecked = _state.KeyboardBaseLevel == "High";

            bool fallbackAvailable = _state.CanKeyboardBacklight && !_state.CanKeyboardEffects;
            ExperimentalFallbackRow.Visibility = fallbackAvailable ? Visibility.Visible : Visibility.Collapsed;
            ExperimentalEffectsSwitch.IsChecked = _state.ExperimentalKeyboardEffectsEnabled;
            ExperimentalEffectsSwitch.IsEnabled = fallbackAvailable;

            bool usable = _state.KeyboardEffectsUsable;
            EffectChoicesGrid.IsEnabled = usable;
            EffectBaseGrid.IsEnabled = usable;
            EffectSpeed.IsEnabled = usable;
            if (!EffectSpeed.IsMouseCaptureWithin)
                EffectSpeed.Value = _state.KeyboardEffectSpeed;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void ExperimentalEffects_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _state is null || AppHost is null || _state.CanKeyboardEffects || !_state.CanKeyboardBacklight)
            return;

        bool enable = ExperimentalEffectsSwitch.IsChecked == true;
        if (enable)
        {
            MessageBoxResult answer = MessageBox.Show(
                "Experimental keyboard effects reuse ThinkControl's existing Off / Low / High backlight commands at a bounded rate.\n\nThinkControl hides Lenovo's tposd backlight popup only around its own automatic effect writes when that OSD is present. This provider can still smooth or ignore some rapid writes. No new low-level command is enabled.\n\nEnable experimental effects for this ThinkControl session?",
                "ThinkControl · Experimental keyboard effects",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
                enable = false;
        }

        _state.ExperimentalKeyboardEffectsEnabled = enable;
        if (!enable && _state.KeyboardMode is "Breathing" or "Reactive" or "Audio")
            await AppHost.SetKeyboardModeAsync("Static");

        SyncControls();
    }

    private async void Effect_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || AppHost is null || _state?.KeyboardEffectsUsable != true ||
            sender is not FrameworkElement { Tag: string mode })
        {
            return;
        }

        await AppHost.SetKeyboardModeAsync(mode);
        SyncControls();
    }

    private void BaseLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || AppHost is null || _state?.KeyboardEffectsUsable != true ||
            sender is not FrameworkElement { Tag: string level })
        {
            return;
        }

        AppHost.SetKeyboardBaseLevel(level);
        SyncControls();
    }

    private void EffectSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded || AppHost is null || _state?.KeyboardEffectsUsable != true ||
            sender is not Slider slider || !slider.IsMouseCaptureWithin)
        {
            return;
        }

        AppHost.SetKeyboardEffectSpeed(e.NewValue);
    }
}
