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

1. install the Windows volume-key guard before the final mute write and block only volume **key-down/repeat** events;
2. keep the CoreAudio endpoint-volume callback as the authority for app/SndVol/unexpected changes;
3. keep a persistent device notification client so a new default render endpoint is picked up immediately, with a short bounded retry burst for the transient period in which a newly selected device is not yet ready.

The key-up detail matters. Physical testing found an activation-edge case where a volume key pressed just before Silent could begin outside ThinkControl and then have its release swallowed after the hook appeared, making Windows behave as if the old press was still held. The current guard therefore always lets key-up pass through while suppressing later volume key-down/repeat events. Silent installs the guard before its final mute write, so that final mute wins over a pre-hook key-down without trapping the corresponding release.

The enforcement worker uses a pending bit rather than dropping callbacks while one pass is already running. This closes the repeated-unmute race where another change could otherwise arrive between a re-mute write and worker shutdown. There is still no permanent fast polling timer.

## Battery Preservation: comparative wear cycles without pretending they are measured

The reference-device review showed that the earlier "top-end headroom / >70% exposure" sentence was technically cautious but not very understandable. AccuBattery's public methodology is a better presentation model: express the selected charge ceiling as an estimated fraction of one full high-voltage wear cycle, then keep the modeling assumptions visible.

AccuBattery documents two useful ideas:

- charging to a lower maximum percentage reduces wear;
- its wear-cycle estimate maps percentage to an idealized end voltage and then applies the observation that roughly **0.10 V lower end-of-charge voltage doubles cycle life**.

Source:

- https://accubattery.zendesk.com/hc/en-us/articles/210224725-Charging-research-and-methodology

AccuBattery has device-scale discharge-curve data and a phone-oriented voltage model. ThinkControl does **not** have the exact per-cell voltage curve, chemistry or end-of-charge voltage for every laptop pack, so copying its proprietary percentage-to-voltage lookup would create false precision.

Alpha.50 therefore uses the same *comparative* concept with a transparent generic laptop-safe approximation:

- 0% is mapped to an idealized 3.52 V/cell;
- 100% is mapped to 4.35 V/cell;
- the curve rises slowly through the middle and more sharply near full charge using `0.28 × SOC + 0.72 × SOC^4`;
- relative wear is `2^(-10 × (4.35 - Vend))`, with the tiny 0%-SOC baseline removed;
- the result is normalized so **100% = 1.00 wear cycle**.

That produces the current UI comparisons:

- 85% ≈ **0.11 wear cycles** (about 89% less than the 100% baseline);
- 80% ≈ **0.06** (about 94% less);
- 60% ≈ **0.01** (about 99% less);
- 100% = **1.00 baseline**.

These values are a simple way to compare charge-limit choices, not measured degradation of the installed X9 battery. The tooltip explicitly says actual pack wear still varies with chemistry, real voltage mapping, temperature, charge rate and use. Firmware cycle count and ThinkControl's capacity-health trend remain separate real measurements.

The visual was simplified at the same time. Alpha.49's permanent green/amber/red regions and charge/pause glyphs made the card read like a diagram instead of a live battery control. Alpha.50 now uses one current-level fill whose color reacts to charging/limit state, with only two aligned threshold markers for resume and cap.

## Fan Auto: keep command intent stable while service state converges

The reported Max → Auto behavior exposed two separate latency/ownership issues rather than one visual toggle bug.

First, status snapshots can arrive while a serialized user write is still waiting or executing. Painting every snapshot immediately lets an older Max/Quiet result overwrite the user's just-selected Auto state, then jump forward again when the later response arrives. Alpha.50 therefore records one generation-scoped pending cooling intent. The UI shows that intent immediately and masks stale cooling-profile telemetry while that exact write runs. After a successful write, the same intent gets a short four-second confirmation lease: matching fresh telemetry clears it immediately, while an older Max/Quiet snapshot cannot bounce Home back before the service catches up. A rejection clears the optimistic state and requests fresh status instead of pretending Auto succeeded.

Second, the explicit service Auto path previously called the generic FanSupervisor Auto handoff before releasing a known live Lenovo firmware/full-speed override. On an X9 whose FanSupervisor was already logically Auto while Max was owned by the firmware coordinator, that recovery probe was unnecessary work ahead of the operation the user actually requested. Alpha.50 releases the known active firmware override first, then performs direct-provider cleanup only if direct state is actually owned. The wider stale-provider recovery remains available when there is no live firmware override.

This does not weaken the full-speed readback/ownership rules and does not create a periodic policy fight with Lenovo firmware.

## Experimental keyboard effects: Lenovo OSD and Windows loopback

Independent ThinkPad keyboard-backlight work documents the same Lenovo behavior seen on the reference machine: changing the backlight through the Lenovo PM/ACPI path can trigger the Lenovo `tposd.exe` notification even when no Fn key is simulated. The narrow practical mitigation is to snapshot visible `tposd` windows immediately before an automatic write and hide only newly visible windows for a short interval afterwards.

Reference implementation/evidence:

- https://github.com/4piu/thinkpad-kbd-light
- https://github.com/4piu/thinkpad-kbd-light/blob/master/src/osd.rs

Alpha.50 keeps suppression in the normal user-session UI process, not in the privileged Session-0 hardware service. Only automatic effect writes arm it. The watcher targets `tposd.exe` specifically, preserves windows that were already visible before the write, and expires after a short burst; it is not a global Lenovo-OSD kill switch.

The inert Audio effect had a separate software cause. NAudio's shared-mode WASAPI loopback can expose the endpoint format as `WaveFormatExtensible` even when the actual subtype is 32-bit IEEE float or PCM. Treating only a top-level `WaveFormatEncoding.IeeeFloat` value as audio makes those extensible buffers calculate as zero RMS. NAudio itself provides `WaveFormatExtensible.ToStandardWaveFormat()` for recognized PCM/IEEE-float subtypes:

- https://github.com/naudio/NAudio/blob/v2.2.1/NAudio.Core/Wave/WaveFormats/WaveFormatExtensible.cs

Alpha.50 normalizes extensible formats before RMS decoding, supports 16/24/32-bit PCM and 32-bit float, publishes the capture object before recording starts so the first buffer is not discarded, and restarts once after an unexpected capture stop only while Audio mode remains active. Effect thresholds use the smoothed current RMS relative to a decaying local peak plus a small absolute floor, so ordinary low-volume system audio can drive the three-state white backlight without treating background noise as music.
