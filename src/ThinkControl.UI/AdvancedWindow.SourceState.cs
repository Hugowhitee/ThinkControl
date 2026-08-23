using System.Windows;
using System.Windows.Media;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    // WPF Window has no IsSourceInitialized property. Keep the existing chrome
    // guard readable while deriving it from the actual PresentationSource.
    private bool IsSourceInitialized => PresentationSource.FromVisual(this) is not null;
}
