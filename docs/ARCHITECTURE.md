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

Before applying a built-in profile, `App.Cooling` seeds the coordinator with the current Windows power preference through `SetThermalMode`. Lenovo's reviewed thermal commands are source-specific: AC uses 502/503/504 and DC uses 507/508/509. Alpha.42 therefore treats a new power baseline as an event that must also **reassert the active cooling override for the current source**. Merely retaining `_overrideProfile = "Quiet"` in memory is not enough because Lenovo/Windows may have changed the physical OEM policy underneath it during AC/DC or resume transitions.

Startup restoration also distinguishes a saved preference from applied state. The saved `CoolingProfile` is not painted as active merely because it exists in `UserSettings`. The Fans selector follows service/runtime state until the saved profile has actually been applied. After the first successful restore, `App.Cooling` performs one bounded seven-second settle reassert to cover Lenovo login/service policy work that may finish shortly after ThinkControl first becomes available. This is intentionally a one-shot convergence step, not a recurring policy fight or polling loop.

Closing/restarting only the normal-user UI does not clear a firmware-policy profile. The privileged service owns that state and keeps Quiet/Balanced/Max active. Direct/manual fan output remains a different safety class and is returned to Auto when the UI exits. Normal service disposal still releases ThinkControl-owned firmware/full-speed state before hardware disposal.

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

For firmware profiles, telemetry is only truthful when the coordinator has successfully applied/reasserted the corresponding semantic policy in the current lifecycle. The UI no longer substitutes the persisted preference ID for a runtime Auto state during page initialization.

## Keyboard model

Keyboard brightness and effects deliberately have different ownership.

- Off / Low / High are static hardware states when the active provider supports them.
- Auto means a verified firmware/OEM Auto contract where one exists.
- Breathing / Reactive / Audio are ThinkControl user-session effects and require `KeyboardEffects`.
- A fallback provider that cannot safely accept repeated changes does not advertise effects.

Keyboard writes are serialized so firmware/static ownership and user-session animation do not fight each other. Saved effect state is restored only after provider capability is known.

## Touchpad model

The Advanced Touchpad editor exposes one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right. `TouchpadVisualizer` owns edge/corner rendering, selection and hit-testing. The right corner is an exact horizontal mirror of the left, and enabled corners share the same visual/recognition geometry rather than behaving like overlays.

Track control remains one continuous edge affordance with one recognizer/router owner. Standalone current Play/Pause is not an edge action; legacy serialized PlayPause values sanitize into Track control. Alpha.43 adds a **Track-local Play/Pause option**, not a second center recognizer or overlay. The option is shown only while editing an edge whose action is Track control.

When that option is enabled, the lane is **Previous | Play/Pause | Next**. The center uses the existing **28%** start region (`0.36..0.64`), requires at least **450 ms** of hold time and no more than **3 mm** maximum radial movement, and commits only on `Released`. Brief center taps are ignored. Release is deliberately the final intent confirmation: no delayed worker fires merely because the hold duration elapsed while a finger is still resting on the touchpad.

When the Track-local option is disabled, `TrackCenterPlayPauseEnabled` sanitizes false while the `PreviousNextTrack` binding remains in place. `TouchpadVisualizer.DrawTrackLane` therefore does not draw the center fill, separators or Play/Pause icon, and the generic Track visual path renders only the two directional Previous/Next cues. Recognition uses the same canonical configuration, so the center is not a hidden active target.

For backward compatibility the serialized/runtime `TrackCenterPlayPauseEnabled` member remains, while alpha.43 adds an explicit `TrackCenterPlayPauseDisabled` opt-out. The opt-out defaults false so existing alpha.42 configurations continue to get their integrated center action. Explicitly disabling it persists even if Track is temporarily moved/removed and later assigned again.

`EdgeGestureRecognizer` keeps a center-start contact in candidate state only through the 3 mm hold slop when center Play/Pause is enabled. While it remains a candidate, `_lastTotalTravelMm` stores the **maximum** radial excursion rather than the latest point, so moving away and returning cannot requalify a mobile contact as stationary. Once the hold slop is exceeded, normal edge direction recognition resumes; if the contact later reaches 9 mm, the existing Track swipe path can still fire Previous/Next.

`GestureActionRouter` owns the temporal half of the enabled-center contract with one `Stopwatch` timestamp from the original Track candidate. It evaluates the hold only on `Released`; there is no delayed worker, no auto-fire while the finger is still down, and no concurrent hold-vs-release arbitration state. A claimed Track swipe sets `_trackStayedCandidate=false`, so it cannot downgrade into Play/Pause on lift. Previous/Next keeps the unchanged **9 mm** threshold.

Occupied edge assignment continues to swap action kinds rather than clearing the previous edge. Sensitivity and inversion remain properties of the physical edge.

Track OSD semantics remain: resulting **Playing → pause bars**, **Paused → play triangle**, and ambiguous virtual-key fallback stays `Playback toggled`.

Enabled corner launches still use the canonical guard → diagonal lane → rounded end-cap recognizer geometry. Alpha.42 changed reverse-close start classification so, when reverse close is enabled, the inner half of the **already-visible** diagonal lane is accepted as an outward start instead of requiring the small rounded cap. The outer guard remains an inward-launch start and no hidden geometry is added. Rejected corner ownership remains locked until lift and outward claim routes through the existing canonical hide-to-tray transition.

Raw HID recognition receives every frame while WPF visualization is coalesced. UI-only listeners remain attached only while the Touchpad page is visible; configured gesture recognition is application-level and starts during silent Windows startup.

## Audio Safety model

Alpha.43 adds one Windows-generic, **session-level** policy owner for preventing accidental ThinkControl audio/media actions. It deliberately does not create a second Touchpad recognizer, a separate Mute edge action or a general phone-style Focus Modes framework.

`ThinkControl.Core.Audio.AudioSafetyPolicy` defines three semantic modes:

- **Normal** — no Audio Safety restrictions;
- **Media lock** — block ThinkControl Touchpad Volume, Media scrub and Track commands while leaving deliberate Windows/app audio untouched;
- **Silent** — includes Media lock, requires the active Windows render endpoint to be muted, and blocks ThinkControl output-volume/unmute writes.

`AudioSafetyService` is the canonical UI-process state owner. `AudioSafetyRuntimeState` is a process-local fail-closed read gate used by Windows output helpers; `AudioSafetyService` is its only writer. The mode is intentionally **not persisted in alpha.43**. Every new process starts in Normal, avoiding the unsound situation where a new process claims ownership of mute state established by an earlier process.

Entering Silent obtains the default Windows render/multimedia endpoint through semantic Core Audio APIs, records that endpoint's prior mute state once, mutes it when needed, and does not publish Silent if the initial mute cannot be established. While Silent remains active, the app reuses the existing hardware/status observation cadence to call `EnsureSilentOutput()`. A single interlocked worker can then apply the same policy if Windows changes the default render endpoint; no second DispatcherTimer or permanent audio polling loop is created.

Mute ownership is per endpoint ID. When ThinkControl encounters an output during Silent it remembers the prior mute state only once. Leaving Silent or orderly app disposal restores those recorded states. If an endpoint disappeared, ThinkControl does not guess a replacement endpoint to restore. Microphone (`DataFlow.Capture`) remains independent and is not automatically muted or changed by Audio Safety.

The Touchpad router checks the canonical policy at the existing action boundary. Volume, Media scrub, Previous/Next and integrated Play/Pause are suppressed in Media lock/Silent and receive bounded `Media locked`/`Silent` feedback instead of silently appearing broken. Entering a lock also cancels any currently owned Touchpad audio action so a gesture that began in Normal cannot continue writing after the policy changes.

Windows output helpers also check `AudioSafetyRuntimeState` immediately before output writes, so Silent is not merely a disabled UI control. Compact and Advanced Audio controls fail closed through the same semantic gate. The microphone path passes `DataFlow.Capture` and is deliberately outside that output-only restriction.

Audio Safety currently composes no fan profile. A future user preset may request both Audio Safety and Quiet, but that would require explicit transactional ownership/restore semantics and capability gating rather than coupling cooling to silence implicitly.

## Audio page lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state and are cleared when the Audio page becomes hidden so stale off-page writes cannot fire later. Core Audio endpoint enumeration remains off the dispatcher because some OEM stacks can block.

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
