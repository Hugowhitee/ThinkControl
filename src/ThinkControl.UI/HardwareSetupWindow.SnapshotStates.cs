using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class HardwareSetupWindow
{
    /// <summary>Applies a deterministic state to the production repair flow without service, driver, network or UAC work.</summary>
    internal void PrepareForSnapshot(HardwareSetupStatus status, bool terminalFailure = false)
    {
        PresentStatus(status);
        if (terminalFailure)
        {
            ShowFailure("Windows finished the PawnIO installer, but the verified driver is still unavailable for fan control and sensors.");
        }
    }
}
