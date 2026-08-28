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
        _volumeApplyTimer.Stop();
        _microphoneApplyTimer.Stop();
        _volumeRefreshTimer.Stop();
        _volumeDragging = false;
        _microphoneDragging = false;
    }
}
