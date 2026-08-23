using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class KeyboardEffectsPanel : UserControl
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
            or nameof(AppState.CanKeyboardBacklight))
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
            EffectAuto.IsChecked = _state.KeyboardMode == "Auto";
            EffectBreathing.IsChecked = _state.KeyboardMode == "Breathing";
            EffectReactive.IsChecked = _state.KeyboardMode == "Reactive";
            EffectAudio.IsChecked = _state.KeyboardMode == "Audio";
            BaseLow.IsChecked = _state.KeyboardBaseLevel == "Low";
            BaseHigh.IsChecked = _state.KeyboardBaseLevel == "High";
            if (!EffectSpeed.IsMouseCaptureWithin)
                EffectSpeed.Value = _state.KeyboardEffectSpeed;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void Effect_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || AppHost is null || sender is not FrameworkElement { Tag: string mode })
            return;

        await AppHost.SetKeyboardModeAsync(mode);
        SyncControls();
    }

    private void BaseLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || AppHost is null || sender is not FrameworkElement { Tag: string level })
            return;

        AppHost.SetKeyboardBaseLevel(level);
        SyncControls();
    }

    private void EffectSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded || AppHost is null || sender is not Slider slider || !slider.IsMouseCaptureWithin)
            return;

        AppHost.SetKeyboardEffectSpeed(e.NewValue);
    }
}
