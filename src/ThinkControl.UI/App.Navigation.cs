namespace ThinkControl.UI;

public partial class App
{
    public void OpenAudio()
    {
        OpenAdvancedSafely("Home");
        _advancedWindow?.NavigateAudio();
    }
}
