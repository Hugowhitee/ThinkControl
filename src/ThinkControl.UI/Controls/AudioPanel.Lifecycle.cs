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
        // control, before the timers have been assigned by our ctor.
        if (_volumeRefreshTimer is null ||
            _volumeAutomationCommitTimer is null ||
            _microphoneAutomationCommitTimer is null)
        {
            return;
        }

        _volumeRefreshTimer.Stop();
        _volumeAutomationCommitTimer.Stop();
        _microphoneAutomationCommitTimer.Stop();
        _volumeDragging = false;
        _microphoneDragging = false;
        _volumeInteractionStartPercent = null;
        _microphoneInteractionStartPercent = null;
    }
}
