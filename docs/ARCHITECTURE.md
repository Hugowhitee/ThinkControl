# ThinkControl architecture

This document describes the current architecture at **v0.1.0-alpha.42**. `docs/RELEASE_READINESS.md` is the persistent release/commercial handoff; this file explains runtime boundaries and intentional compatibility debt.

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

The ThinkPad X9 path separates four concepts:

1. native fan telemetry;
2. Lenovo firmware thermal policy;
3. Lenovo's known global full-speed boolean semantic;
4. direct per-fan output writers.

`LENOVO_OTHER_METHOD` can expose real dual-fan `fanX_input` telemetry. Its experimental per-fan `fanX_target` writer remains read-only because physical alpha.38 testing failed its acceptance gate: fixed targets repeatedly re-kicked/waved and nominal 100% remained physically below naturally hot Lenovo Auto. VALID+GET+SET metadata and sane Fan Test ranges do not override that physical rejection. `EnergyDrv` remains read-only until its exact write contract is recovered and reviewed.

Alpha.41 added a different exact-X9 semantic: Lenovo Other Mode feature **`0x04020000`**, treated only as a boolean full-speed override. `LenovoOtherModeFullSpeedService` is restricted to verified `21Q6/21Q7`, requires a live boolean read immediately around the transition, respects an explicitly present capability row, writes only `0`/`1`, and verifies the resulting state by readback. This is not used as evidence that per-fan target RPM is safe and is not generalized into arbitrary feature-ID passthrough.

## Cooling model

The service distinguishes firmware policy, optional full-speed override, and direct-output providers.

### Firmware-policy profiles

`LenovoCoolingPolicyCoordinator` owns the exact-X9 semantic profile override while Lenovo firmware remains responsible for the closed-loop fan controller.

- **Auto** — release any ThinkControl-owned full-speed override, clear the cooling override and restore the latest Lenovo power-policy baseline.
- **Quiet** — first release any ThinkControl-owned full-speed override, then request Lenovo Quiet thermal policy.
- **Balanced** — first release any ThinkControl-owned full-speed override, then request Lenovo Balanced thermal policy.
- **Max cooling** — request Lenovo Performance policy, then request the exact-X9 `0x04020000 = 1` full-speed semantic when it is safely exposed and verify readback.

The coordinator tracks whether **ThinkControl itself actually changed full-speed state**. It never claims ownership merely because a read observes that Lenovo/another component already has the feature enabled. Lower profiles and Auto only perform release behavior appropriate to that ownership model; service disposal also best-effort releases ThinkControl-owned full speed before restoring firmware policy.

If the known full-speed feature is unavailable, non-writable, non-boolean or fails readback, the transition fails closed instead of guessing a larger RPM target, EC state or IOCTL. Quiet/Balanced remain ordinary Lenovo firmware-policy operations.

Before applying a built-in profile, `App.Cooling` seeds the coordinator with the current Windows power preference through `SetThermalMode`. Later Performance-page changes update that baseline while a cooling override remains active. Auto restores the latest baseline, so Performance and Fans do not continuously fight over the same Lenovo policy surface.

Firmware policy/full-speed profiles intentionally do not advertise applied percentage, EC state or editable curve semantics. Manual percentage tests, raw EC diagnostics and curve editing remain direct-provider features only.

### Direct-output profiles

The generic direct-output model remains:

- named `FanCurveDefinition` profiles;
- `SetCoolingCurve` for current direct curve writes;
- `SetFanPercent` for deliberate temporary output testing where supported;
- `ReturnFanToAuto` for firmware/OEM ownership;
- characterization operations only when the active direct provider advertises calibration.

`FanSupervisor` remains the sole owner of direct percentage/discrete fan writes. A physically accepted continuous target provider may receive percentages directly; a discrete provider may map semantic targets through measured output states. The rejected X9 `fanX_target` implementation remains blocked even though alpha.41 has a separate full-speed boolean path.

The service exposes `FanCalibrationSupported` and `FanCalibrationRequired` in `HardwareCapabilitySnapshot`. Firmware-policy/full-speed profiles do not require direct calibration. The calibration task card is visible only while a relevant direct provider requires it or is actively running.

## Fan ownership and telemetry

ThinkControl records only state it actually owns. Direct provider/channels are returned to OEM Auto on handoff/failure/disposal where supported. Target `0` on the rejected per-fan Other Mode path remains only for cleanup/reassertion of stale previously owned targets.

Native two-fan evidence is latched for the current service lifetime so a transient OEM telemetry miss cannot silently re-enable the known-inferior EC writer. Fan RPM telemetry is evidence about tachometer speed, not proof that a selected policy equals Lenovo's strongest physical cooling state; alpha.40 physical feedback specifically showed that high-looking RPM telemetry can coexist with weaker airflow than naturally hot Auto.

## Keyboard model

Keyboard brightness and effects deliberately have different ownership.

- Off / Low / High are static hardware states when the active provider supports them.
- Auto means a verified firmware/OEM Auto contract where one exists.
- Breathing / Reactive / Audio are ThinkControl user-session effects and require `KeyboardEffects`.
- A fallback provider that cannot safely accept repeated changes does not advertise effects.

Keyboard writes are serialized so firmware/static ownership and user-session animation do not fight each other. Saved effect state is restored only after provider capability is known.

## Touchpad model

The Advanced Touchpad editor exposes one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right. `TouchpadVisualizer` owns edge/corner rendering, selection and hit-testing. The right corner is an exact horizontal mirror of the left, and enabled corners share the same visual/recognition geometry rather than behaving like overlays.

Track control is one continuous edge lane with three semantic segments: **Previous | Play/Pause | Next**. There is no standalone current Play/Pause edge action or second center toggle/recognizer. Legacy serialized PlayPause values sanitize into Track control.

Alpha.42 keeps one recognizer and one action router but makes the center button less precise. `TrackCenterGesturePolicy` defines a visible **28%** center start region (`0.36..0.64`), the existing **8.75 mm** center movement envelope, and the unchanged **9 mm** deliberate Previous/Next threshold. A quick center contact commits on release. A stationary center contact also schedules a bounded **240 ms** hold commit so Play/Pause can fire while the finger is still down.

`GestureActionRouter` serializes the hold, release and skip paths with one Track action state. A candidate generation invalidates stale delayed hold tasks when the recognizer claims, updates, releases or cancels the gesture. `_trackActionCommitted` ensures that whichever path wins is the only action for that contact: a hold cannot toggle again on lift or subsequently also become Previous/Next. This is action arbitration inside the existing router, not another gesture/input owner.

Occupied edge assignment continues to swap action kinds rather than clearing the previous edge. Sensitivity and inversion remain properties of the physical edge.

Track OSD semantics remain: resulting **Playing → pause bars**, **Paused → play triangle**, and ambiguous virtual-key fallback stays `Playback toggled`.

Enabled corner launches still use the canonical guard → diagonal lane → rounded end-cap recognizer geometry. Alpha.42 changes only reverse-close start classification: when reverse close is enabled, the inner half of the **already-visible** diagonal lane is accepted as an outward start instead of requiring the small rounded cap. The outer guard remains an inward-launch start and no hidden geometry is added. Rejected corner ownership remains locked until lift and outward claim routes through the existing canonical hide-to-tray transition.

Raw HID recognition receives every frame while WPF visualization is coalesced. UI-only listeners remain attached only while the Touchpad page is visible; configured gesture recognition is application-level and starts during silent Windows startup.

## Audio lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state and are cleared when the Audio page becomes hidden so stale off-page writes cannot fire later.

## Status, diagnostics and discovery

`HardwareServiceClient` caches a short last-known-good status snapshot and backs off after a confirmed offline service state. It publishes bounded status/operation events instead of letting pages create competing polling loops.

Diagnostics are local-first and sanitized. Raw touch coordinates, personal file content, usernames, serial numbers and arbitrary memory/log dumps are outside the intended upload schema.

Repeated provider discovery is avoided where possible. The service keeps provider state; the UI consumes bounded status snapshots and uses targeted refresh operations for sensors, keyboard and full provider recovery.

## Update state

Home and Updates share one application update result and one Last-checked timestamp owner. Completed manual checks refresh that in-memory value immediately and persist it for the next session.

## Release and compatibility boundaries

The installer is a small bootstrapper plus a separately versioned payload. CI exercises build/tests, real WPF shell smoke and visual snapshots. Package ThinkControl owns publish/payload/bootstrap plus installer/service/IPC/update/uninstall compatibility.

PR CI and Package runs may cancel superseded heads; immutable/tag release packaging remains outside that cancellation behavior.

A cleanup is not permission to remove compatibility code blindly. Server-side legacy protocol handlers and serialized enum values remain where required until the supported installed-client/settings floor is deliberately advanced.
