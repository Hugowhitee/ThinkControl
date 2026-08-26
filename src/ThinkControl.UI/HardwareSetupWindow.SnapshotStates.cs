using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class HardwareSetupWindow
{
    /// <summary>Applies a deterministic state to the production repair flow without service, driver, network or UAC work.</summary>
    internal void PrepareForSnapshot(HardwareSetupStatus status)
    {
        PresentStatus(status, lenovoDevice: true);
        ResultText.Text = IsPawnIoRepairRecommended(status)
            ? "Repair hardware access downloads the verified component, repairs it once, then checks sensors and fan control."
            : "Everything expected for this device is ready. You can close this window.";
    }
}
