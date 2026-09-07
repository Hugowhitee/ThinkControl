# ThinkControl architecture

This document describes the current architecture at **v0.1.0-alpha.43**. `docs/RELEASE_READINESS.md` is the persistent release/commercial handoff; this file explains runtime boundaries and intentional compatibility debt. Immutable `v0.1.0-alpha.42` remains the release baseline underneath this candidate.

## Process boundary

ThinkControl is split into a normal-user WPF application and a privileged Windows service.

- `ThinkControl.UI` owns Compact/Advanced windows, settings, presentation state, user-session effects and orchestration.
- `ThinkControl.Service` owns privileged hardware access and exposes a narrow named-pipe IPC contract.
- `ThinkControl.Core` contains shared models, policies and protocol types that do not depend on WPF or hardware implementations.
- `ThinkControl.Hardware` contains provider implementations and verified low-level device behavior.
- `ThinkControl.DeviceProfiles` is the architectural boundary for device/profile data.

The UI remains `asInvoker`. Hardware operations that need elevated/device access belong in the service rather than causing repeated UAC prompts from the desktop process.

## Startup model

Startup has a strict critical-path boundary: establish cheap identity and configured user-session input before rich WPF/hardware discovery. Alpha.41 tightened this after comparing the runtime shape with lightweight helper apps such as G-Helper. The useful principle is **input/tray first, discovery later**; ThinkControl does not copy G-Helper's single-process privilege model.

`Start with Windows` remains one per-user HKCU Run entry launching `ThinkControl.UI.exe --tray`. During the earliest `Application.Startup` hook, `SystemStatusService.ReadStartupIdentity()` reads only firmware identity from `HKLM\HARDWARE\DESCRIPTION\System\BIOS`. If configured Touchpad gestures are enabled, `StartConfiguredTouchpadInputForStartup()` creates the existing gesture host and starts Raw Input immediately from that Startup hook instead of queueing registration behind normal WPF dispatcher shell work.

Normal visible launches retain first-paint protection: touch input registration may still use the existing deferred path where appropriate, and the painted bootstrap surface remains responsible for avoiding black/unpainted frames. Rich CPU/GPU/RAM/BIOS inventory continues on the background refresh path through `Task.Run`/cached WMI. `Application.Activated` remains a recovery path after device/session transitions, not the first-start owner.

Windows itself may schedule HKCU Run programs later than another helper. ThinkControl does not modify machine-wide Explorer startup-delay policy. If real-session evidence shows process creation itself is late, a different startup mechanism is a separate installer/update/uninstall contract decision.

## Hardware safety model

Hardware support is capability-driven. Unknown hardware remains read-only/safe until an operation has a reviewed provider and validation gate. Generic UI consumes semantic capability state; it must not infer write support, calibration requirements or effect support by parsing model names or diagnostic provider strings.

The ThinkPad X9 path separates five concepts:

1. native fan telemetry;
2. Lenovo firmware thermal policy;
3. Lenovo's known global full-speed boolean semantic;
4. direct per-fan output writers;
5. Lenovo's Standard/Long-Life battery charge-type semantic.

`LENOVO_OTHER_METHOD` can expose real dual-fan `fanX_input` telemetry. Its experimental per-fan `fanX_target` writer remains read-only because physical alpha.38 testing failed its acceptance gate. VALID+GET+SET metadata and sane Fan Test ranges do not override that physical rejection. `EnergyDrv` remains read-only until its exact write contract is recovered and reviewed.

Alpha.41 added Lenovo Other Mode feature `0x04020000` as a narrow boolean full-speed override. Alpha.43 adds a similarly narrow but independently gated battery-care provider for PSU charge type `0x03010001`: Standard (`0`) / Long Life (`1`). The battery provider requires exact X9 identity, an explicit capability row with VALID+GET+SET, live `0/1` state, only 80/100 product values and post-write readback. The UI never receives the raw Lenovo feature ID.

## Cooling model

The service distinguishes firmware policy, optional full-speed override, and direct-output providers.

### Firmware-policy profiles

`LenovoCoolingPolicyCoordinator` owns the exact-X9 semantic profile override while Lenovo firmware remains responsible for the closed-loop fan controller.

- **Auto** — release any ThinkControl-owned full-speed override, clear the cooling override and restore the latest Lenovo power-policy baseline.
- **Quiet** — first release any ThinkControl-owned full-speed override, then request Lenovo Quiet thermal policy.
- **Balanced** — first release any ThinkControl-owned full-speed override, then request Lenovo Balanced thermal policy.
- **Max cooling** — request Lenovo Performance policy, then request the exact-X9 `0x04020000 = 1` full-speed semantic when it is safely exposed and verify readback.

The coordinator tracks whether ThinkControl itself actually changed full-speed state. It never claims ownership merely because a read observes that Lenovo/another component already has the feature enabled.

Before applying a built-in profile, `App.Cooling` seeds the coordinator with the current Windows power preference through `SetThermalMode`. Lenovo's reviewed thermal commands are source-specific. Alpha.42 therefore treats a new power baseline as an event that must also reassert the active cooling override for the current source.

Startup restoration distinguishes a saved preference from applied state. The saved `CoolingProfile` is not painted as active merely because it exists in `UserSettings`. After the first successful restore, `App.Cooling` performs one bounded seven-second settle reassert to cover Lenovo login/service policy work. This is a one-shot convergence step, not a polling loop.

Closing/restarting only the normal-user UI does not clear a firmware-policy profile. The privileged service owns that state. Direct/manual fan output remains a different safety class and is returned to Auto when the UI exits. Normal service disposal still releases ThinkControl-owned firmware/full-speed state before hardware disposal.

### Direct-output profiles

The generic direct-output model remains:

- named `FanCurveDefinition` profiles;
- `SetCoolingCurve` for current direct curve writes;
- `SetFanPercent` for deliberate temporary output testing where supported;
- `ReturnFanToAuto` for firmware/OEM ownership;
- characterization operations only when the active direct provider advertises calibration.

`FanSupervisor` remains the sole owner of direct percentage/discrete fan writes. The rejected X9 `fanX_target` implementation remains blocked even though alpha.41 has a separate full-speed boolean path.

The service exposes `FanCalibrationSupported` and `FanCalibrationRequired` in `HardwareCapabilitySnapshot`. Firmware-policy/full-speed profiles do not require direct calibration.

## Fan ownership and telemetry

ThinkControl records only state it actually owns. Direct provider/channels are returned to OEM Auto on handoff/failure/disposal where supported. Target `0` on the rejected per-fan Other Mode path remains only for cleanup/reassertion of stale previously owned targets.

Native two-fan evidence is latched for the current service lifetime so a transient OEM telemetry miss cannot silently re-enable the known-inferior EC writer. Fan RPM telemetry is evidence about tachometer speed, not proof that a selected policy equals Lenovo's strongest physical cooling state.

## Battery charge-protection model

`LenovoBatteryChargeProtectionService` is a provider, not a generic threshold calculator. For the verified X9 it knows exactly one Lenovo Other Mode attribute: `0x03010001`.

The hardware provider maps that firmware semantic to product language:

- raw `0` -> **Full charge · 100%**;
- raw `1` -> **Battery care · 80%**.

A write is accepted only for product values `80` and `100`. `ThinkControl.Service` exposes a semantic `SetBatteryChargeLimit` IPC operation and adds `BatteryChargeProtection` plus the current verified limit/source/detail to status. The normal-user WPF app never invokes WMI methods or supplies a raw OEM attribute/value.

Battery-protection status uses the service's existing request-driven status model. A small 30-second provider cache avoids turning Battery-page refresh into repeated WMI discovery. A successful transition invalidates that cache and returns a freshly read status immediately.

The Battery page subscribes to the existing `HardwareClient.StatusObserved` event only while loaded. It does not start a second timer. The dropdown stays disabled unless the service advertises `BatteryChargeProtection = true`. A live state whose capability row is missing can therefore be displayed read-only without silently becoming write-authorized.

The charge-protection preference is not duplicated into `UserSettings` in alpha.43. Firmware readback remains source of truth. This avoids the same class of bug that previously let a saved fan preference look active before hardware had actually applied it.

Other OEMs can later implement their own semantic battery provider and supported limit set without changing the shared Battery page into vendor-specific code.

## Battery history model

`BatteryHistoryService` stores local charge/discharge sessions with sparse detailed points plus compact summaries. The UI is aggregation-first: day -> session -> detail window. It is not a raw chronological log.

Retention has two layers:

- detailed graphs: normalized user choice (currently surfaced as 7 / 14 / 30 days);
- compact summaries: one year under `BatteryHistoryRetentionPolicy.SummaryRetentionDays`.

Automatic compaction clears old point arrays while preserving session summaries, health trend inputs and useful learned estimates. This is preferable to making users manually delete a growing list merely to keep storage bounded.

`Manage history` owns destructive/retention actions. Reset is explicit and warns that local session summaries, graphs, health trend and learned charge/discharge priors are cleared. Firmware battery health, cycle count and OEM charge-protection state are not part of this local history document and are unaffected.

## Keyboard model

Keyboard brightness and effects deliberately have different ownership.

- Off / Low / High are static hardware states when the active provider supports them.
- Auto means a verified firmware/OEM Auto contract where one exists.
- Breathing / Reactive / Audio are ThinkControl user-session effects and require `KeyboardEffects`.
- A fallback provider that cannot safely accept repeated changes does not advertise effects.

Keyboard writes are serialized so firmware/static ownership and user-session animation do not fight each other. Saved effect state is restored only after provider capability is known.

## Touchpad model

The Advanced Touchpad editor exposes one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right. `TouchpadVisualizer` owns edge/corner rendering, selection and hit-testing. The right corner is an exact horizontal mirror of the left.

Track control remains one continuous edge affordance with one recognizer/router owner. Standalone current Play/Pause is not an edge action; legacy serialized PlayPause values sanitize into Track control. Alpha.43 adds a **Track-local Play/Pause option**, not a second center recognizer or overlay.

When enabled, the lane is **Previous | Play/Pause | Next**. The center uses the 28% start region (`0.36..0.64`), requires at least 450 ms of hold time and no more than 3 mm maximum radial movement, and commits only on `Released`. Brief center taps are ignored; nothing auto-fires while the finger is still down.

When disabled, `TrackCenterPlayPauseEnabled` sanitizes false while the `PreviousNextTrack` binding remains in place. The visualizer does not draw the center fill, separators or Play/Pause icon, and recognition uses the same canonical configuration.

The explicit opt-out defaults false so existing alpha.42 configurations keep their integrated center action. Explicitly disabling it persists even if Track is temporarily moved/removed and later assigned again.

Once the hold slop is exceeded, normal edge direction recognition resumes; if the contact later reaches 9 mm, Previous/Next can still fire. A claimed Track swipe cannot also become Play/Pause on lift.

Occupied edge assignment continues to swap action kinds rather than clearing the previous edge. Sensitivity and inversion remain properties of the physical edge.

Enabled corner launches still use the canonical guard -> diagonal lane -> rounded end-cap recognizer geometry. Reverse close accepts the inner half of the already-visible diagonal lane when enabled; the outer guard remains an inward-launch start and no hidden geometry is added.

Raw HID recognition receives every frame while WPF visualization is coalesced. Configured gesture recognition is application-level and starts during silent Windows startup.

## Audio Safety model

Alpha.43 adds one Windows-generic, **session-level** policy owner for preventing accidental ThinkControl audio/media actions. It deliberately does not create a second Touchpad recognizer, separate Mute edge action or phone-style Focus Modes framework.

`ThinkControl.Core.Audio.AudioSafetyPolicy` defines three modes:

- **Normal** — no Audio Safety restrictions;
- **Media lock** — block ThinkControl Touchpad Volume, Media scrub and Track commands while leaving deliberate Windows/app audio untouched;
- **Silent** — includes Media lock, requires the active Windows render endpoint to be muted, and blocks ThinkControl output-volume/unmute writes.

`AudioSafetyService` is the canonical UI-process state owner. The mode is intentionally not persisted in alpha.43. Every new process starts in Normal so it cannot falsely claim ownership of mute state established by an earlier process.

Entering Silent records the default render endpoint's prior mute state once, mutes it when needed and does not publish Silent if the initial mute cannot be established. While Silent remains active, the app reuses the existing status cadence to converge if Windows changes the default render endpoint; no second timer is created.

Mute ownership is per endpoint ID. Leaving Silent/orderly app disposal restores only recorded states. Microphone (`DataFlow.Capture`) remains independent.

The Touchpad router checks the policy at the existing action boundary. Volume, Media scrub, Previous/Next and integrated Play/Pause are suppressed in Media lock/Silent. Windows output helpers also check `AudioSafetyRuntimeState` immediately before output writes, so Silent is not merely a disabled UI control.

Audio Safety currently composes no fan profile. A future user preset may request both Audio Safety and Quiet, but that would require explicit transactional ownership/restore semantics and capability gating rather than coupling cooling to silence implicitly.

## Audio page lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state and are cleared when the Audio page becomes hidden so stale off-page writes cannot fire later. Core Audio endpoint enumeration remains off the dispatcher because some OEM stacks can block.

## Status, diagnostics and discovery

`HardwareServiceClient` caches a short last-known-good status snapshot and backs off after a confirmed offline service state. It publishes bounded status/operation events instead of letting pages create competing polling loops.

Diagnostics are local-first and sanitized. Raw touch coordinates, personal file content, usernames, serial numbers and arbitrary memory/log dumps are outside the intended upload schema.

Battery charge-protection writes are classified as `battery.charge_limit_set` semantic hardware operations. Diagnostics do not expose a generic Lenovo feature writer.

Repeated provider discovery is avoided where possible. The service keeps provider state; the UI consumes bounded status snapshots and uses targeted refresh operations for sensors, keyboard and full provider recovery.

## Update state

Home and Updates share one application update result and one Last-checked timestamp owner. Completed manual checks refresh that in-memory value immediately and persist it for the next session.

## Release and compatibility boundaries

The installer is a small bootstrapper plus a separately versioned payload. CI exercises build/tests, real WPF shell smoke and visual snapshots. Package ThinkControl owns publish/payload/bootstrap plus installer/service/IPC/update/uninstall compatibility.

PR CI and Package runs may cancel superseded heads; immutable/tag release packaging remains outside that cancellation behavior.

A cleanup is not permission to remove compatibility code blindly. Server-side legacy protocol handlers and serialized enum values remain where required until the supported installed-client/settings floor is deliberately advanced.
