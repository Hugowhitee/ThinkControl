namespace ThinkControl.UI;

public partial class App
{
    public void OpenAudio()
    {
        OpenAdvanced("Home");
        _advancedWindow?.NavigateAudio();
    }
}
