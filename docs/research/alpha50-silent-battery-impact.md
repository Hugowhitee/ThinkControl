# Alpha.50 research — Silent enforcement and Battery Preservation impact

This note records the external evidence behind the alpha.50 behavior. It is not a hardware capability grant.

## Silent: keep mute authoritative even when the keyboard is used

Microsoft's EndpointVolume contract is the right semantic layer for Windows output mute. `IAudioEndpointVolumeCallback::OnNotify` is raised for endpoint volume/mute changes including `SetMute`, `VolumeStepUp` and `VolumeStepDown`. Microsoft also documents that callbacks must remain non-blocking and should not tear down endpoint objects from inside the callback.

Sources:

- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolumecallback
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolumecallback-onnotify
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmute

A default output can also change independently of endpoint-volume notifications. `IMMNotificationClient::OnDefaultDeviceChanged` is the Windows Core Audio notification for that transition:

- https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immnotificationclient-ondefaultdevicechanged

For the physical keyboard path, Windows defines the standard multimedia volume keys as `VK_VOLUME_MUTE` (0xAD), `VK_VOLUME_DOWN` (0xAE) and `VK_VOLUME_UP` (0xAF). A `WH_KEYBOARD_LL` hook may return a non-zero result when it handles an event, which prevents that event from being passed onward. The callback must stay extremely small and the installing thread must own a message loop.

Sources:

- https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc
- https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes

Alpha.50 therefore uses three layers while Silent is active:

1. block only the three Windows volume virtual keys before the shell can unmute the endpoint;
2. keep the CoreAudio endpoint-volume callback as the authority for app/SndVol/unexpected changes;
3. keep a persistent device notification client so a new default render endpoint is picked up immediately, with a short bounded retry burst for the transient period in which a newly selected device is not yet ready.

The enforcement worker uses a pending bit rather than dropping callbacks while one pass is already running. This closes the held-key race where another unmute could otherwise arrive between a re-mute write and worker shutdown. There is still no permanent fast polling timer.

## Battery Preservation: do not invent a cycle-count formula

Published lithium-ion aging work consistently shows that degradation depends on more than the configured upper SOC threshold. Temperature, charge/discharge rate, depth of discharge, mean SOC, chemistry and calendar time all matter. High SOC generally increases aging stress, but there is no chemistry-independent conversion such as “85% cap = N cycles saved” that can be inferred from Lenovo's start/stop percentages alone.

Relevant evidence:

- Maheshwari, Heck & Santarelli, *Electrochimica Acta* (2018): capacity fade and impedance rise strongly depend on temperature, current rate, depth of discharge and mean SOC. https://doi.org/10.1016/j.electacta.2018.04.045
- Keil et al., *Journal of Power Sources* (2014): both calendar and cycle aging vary with voltage/SOC range and cycle depth. https://doi.org/10.1016/j.jpowsour.2013.09.143
- Schmalstieg et al., *Journal of Power Sources* (2014): a holistic aging model needs multiple stress factors rather than one charge-limit percentage. https://doi.org/10.1016/j.jpowsour.2014.02.012
- Wang et al., *Journal of Power Sources* (2018): among equal 20%-DoD windows, the 80–100% SOC range produced more capacity loss than the lower windows. https://doi.org/10.1016/j.jpowsour.2018.07.018
- Review evidence also reports materially faster aging at high storage SOC and emphasizes chemistry dependence. https://www.mdpi.com/2313-0105/10/11/374

So alpha.50 deliberately does **not** print “cycles saved” or a fake life multiplier.

Instead, `BatteryPreservationImpactModel` calculates threshold-derived quantities that are true for named and future custom presets:

- **top-end headroom** = `100 - stopPercent`;
- **recharge window** = `stopPercent - startPercent`;
- **equivalent full-charge throughput omitted per 0→100-sized top-up** = `(100 - stopPercent) / 100`;
- **share of a transparent >70% high-SOC reference band omitted** = `clamp((100 - stopPercent) / 30, 0, 1)`.

The 70% line is a UI reference band, not a chemistry-specific aging knee. The visible sentence always says that exact cycle-life gain varies with chemistry and temperature. If a future provider exposes cell chemistry, voltage mapping and trustworthy pack temperature/history, a richer aging model can replace this proxy without changing the Battery page contract.
