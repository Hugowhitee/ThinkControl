using System.Windows;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel
{
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == IsVisibleProperty && e.NewValue is false)
            ResetTransientAudioInteractionState();
    }

    private void ResetTransientAudioInteractionState()
    {
        // IsVisible can change while InitializeComponent is still constructing the
        // control, before the polling timer has been assigned by our ctor.
        if (_volumeRefreshTimer is null)
            return;

        _volumeRefreshTimer.Stop();
        _volumeDragging = false;
        _microphoneDragging = false;
    }
}
