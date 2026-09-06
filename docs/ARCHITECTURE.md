# ThinkControl architecture

This document describes the current architecture at **v0.1.0-alpha.40**. `docs/RELEASE_READINESS.md` is the persistent release/commercial handoff; this file explains runtime boundaries and intentional compatibility debt.

## Process boundary

ThinkControl is split into a normal-user WPF application and a privileged Windows service.

- `ThinkControl.UI` owns Compact/Advanced windows, settings, presentation state, user-session effects and orchestration.
- `ThinkControl.Service` owns privileged hardware access and exposes a narrow named-pipe IPC contract.
- `ThinkControl.Core` contains shared models, policies and protocol types that do not depend on WPF or hardware implementations.
- `ThinkControl.Hardware` contains provider implementations and verified low-level device behavior.
- `ThinkControl.DeviceProfiles` is the architectural boundary for device/profile data. It intentionally remains small while capability logic is moved out of UI assumptions.

The UI must remain `asInvoker`. Hardware operations that need elevated/device access belong in the service rather than causing repeated UAC prompts from the desktop process.

## Startup model

Startup has a strict critical-path boundary: create the shell/tray and make configured background input usable before rich hardware discovery completes.

`Start with Windows` remains a single per-user HKCU Run entry that launches `ThinkControl.UI.exe --tray`. Alpha.39 established this path and alpha.40 preserves it unchanged. During `Application.Startup`, `App.ShellIcons` marks exactly one `SystemStatusService.Read()` as a fast preflight. That preflight reads firmware identity from `HKLM\HARDWARE\DESCRIPTION\System\BIOS` plus cheap Windows power state and returns placeholders for CPU/GPU/RAM/BIOS. The existing initial `RefreshStatusAsync` performs the full cached WMI inventory on a worker through `Task.Run`, so rich identity cannot block tray creation or raw-input readiness.

Enabled Touchpad gestures are application-level behavior, not page-level behavior. After startup yields, `StartConfiguredTouchpadInputForStartup` explicitly starts the gesture host. A silent `--tray` launch uses `DispatcherPriority.Background` because no visible WPF destination needs first-paint protection; ordinary page/shell starts retain `ContextIdle`. `Application.Activated` remains a recovery path for session/device transitions, but it is not the first-start owner because a tray-only process can remain unactivated indefinitely.

The Windows Run mechanism may have its own OS scheduling latency at sign-in. ThinkControl does not alter machine-wide Explorer startup-delay policy. If real-session evidence shows the process itself is launched late after the application-side critical path is fixed, changing the startup mechanism is a separate installer/update/uninstall contract change.

## Hardware safety model

Hardware support is capability-driven. Unknown hardware remains read-only/safe until an operation has a reviewed provider and validation gate. Generic UI consumes semantic capability state; it must not infer write support, calibration requirements or effect support by parsing model names or diagnostic provider strings.

The ThinkPad X9 path separates **firmware policy** from **direct fan output**. `LENOVO_OTHER_METHOD` can expose real dual-fan `fanX_input` telemetry, but its experimental target-RPM writer remains read-only because physical alpha.38 testing failed the writer's own acceptance gate: a fixed target produced repeated speed cycling/re-kick and nominal 100% remained below naturally hot firmware Auto. VALID+GET+SET metadata, sane Fan Test ranges and live channels remain useful read evidence but are not sufficient write authorization after that physical rejection. Lenovo `EnergyDrv` is likewise read-only until a matching X9 write contract is proven.

Built-in X9 cooling does **not** disappear with that rejection. `LenovoCoolingPolicyCoordinator` uses the already reviewed exact-X9 `LenovoThermalPolicyService` / LITSSvc semantic path for Quiet, Balanced and Max cooling. This keeps Lenovo firmware in the closed-loop fan controller rather than approximating its behavior with a fixed RPM target. The coordinator remembers the current power-mode policy as a restore baseline, lets a cooling profile temporarily take precedence, and restores the newest baseline when Auto is selected.

Fan ownership remains explicit. `FanSupervisor` owns direct-output providers only; the firmware-policy coordinator owns only the semantic Lenovo policy override. ThinkControl records direct provider/channels it actually takes over, returns those owned channels to Lenovo/OEM Auto on handoff/failure/disposal where supported, and does not infer ownership merely from reading an external manual-looking state. Target `0` on the rejected Other Mode path remains available for cleanup/reassertion of stale previously owned direct targets. Native two-fan evidence is latched for the current service lifetime so a rejected/native writer or transient OEM telemetry miss cannot silently re-enable the known-inferior EC writer.

Repeated provider discovery is avoided where possible. The service keeps provider state; the UI consumes bounded status snapshots and uses targeted refresh operations for sensors, keyboard and full provider recovery.

## Cooling model

The service distinguishes two cooling capability families.

### Firmware-policy profiles

On the verified X9, the service can expose `FanControlKind = LenovoFirmwarePolicy` even though no direct target writer is authorized. The current UI then keeps these built-ins available:

- Auto — clear the cooling override and restore the current Lenovo power-policy baseline;
- Quiet — Lenovo Quiet thermal policy;
- Balanced — Lenovo Balanced thermal policy;
- Max cooling — Lenovo Performance cooling policy.

`SetCoolingProfile` is therefore a current semantic UI operation for the firmware-policy backend. Before applying a built-in profile, `App.Cooling` seeds the coordinator with the current Windows power preference through the existing `SetThermalMode` semantic operation. A later Performance-page change updates that baseline while the cooling override stays active, preventing the two product surfaces from fighting over the same Lenovo policy channel.

Firmware policy intentionally does not advertise applied percentage, EC state or editable curve semantics. The Fans page hides manual percentage tests, raw EC diagnostics and curve editing on this backend. Compact/Home still expose the working built-in profiles.

### Direct-output profiles

The generic direct-output model remains:

- named `FanCurveDefinition` profiles;
- `SetCoolingCurve` for current direct curve writes;
- `SetFanPercent` for deliberate temporary output testing where supported;
- `ReturnFanToAuto` for firmware/OEM ownership;
- characterization operations only when the active direct provider advertises a calibration workflow.

`FanSupervisor` is the sole owner of ThinkControl direct fan writes. A physically accepted continuous target-RPM provider may receive percentages directly; a discrete provider may map the same semantic targets through a measured output-state mapping. Raw EC states/calibration remain provider-specific diagnostics rather than a generic fan-control assumption. On the X9 alpha.40 path, the rejected Other Mode writer is not re-authorized merely because its metadata says SET.

The service exposes `FanCalibrationSupported` and `FanCalibrationRequired` in `HardwareCapabilitySnapshot`. `App.Cooling` converts those service capabilities plus characterization progress into the generic `FanCalibrationUiState`. Firmware-policy profiles do not need this direct-output calibration. The calibration task card is visible only while a relevant provider requires it or is actively running; a ready mapping is ordinary provider state, not a permanent top-of-page success card.

Manual direct-output UI is a bounded diagnostic surface. Percentage targets and provider-specific raw states run through the same 30-second temporary-test/automatic-restore contract. The surface is hidden on the firmware-policy backend and whenever no verified direct writer exists. Raw EC diagnostics appear only when the active provider explicitly advertises the discrete-EC semantic contract.

The service still accepts `SetCustomCoolingCurve` and `MarkFanLevelAudible` for the supported installed-client compatibility floor. Those remain legacy server compatibility endpoints. `SetCoolingProfile` remains a current semantic operation for built-in firmware-policy profiles. Removing any endpoint requires an explicit updater/client-floor decision and compatibility-test update.

## Keyboard model

Keyboard brightness and effects deliberately have different ownership.

- Off / Low / High are static hardware states when the active provider supports them.
- Auto means a verified firmware/OEM Auto contract where one exists. ThinkControl does not emulate Auto with a High → Low → Off idle loop.
- Breathing / Reactive / Audio are ThinkControl user-session effects and require the active provider to advertise `KeyboardEffects`.
- A fallback provider that cannot safely accept repeated changes does not advertise effects; the current Lenovo Vantage fallback is one such implementation because repeated writes can show OEM brightness pop-ups.

Keyboard writes are serialized by the keyboard/effect coordinator so firmware/static ownership and user-session animation do not fight each other. Saved effect state is restored only after the provider capability is known.

## Touchpad model

The Advanced Touchpad editor exposes one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right.

`TouchpadVisualizer` owns edge/corner rendering, selection and hit-testing. Corner geometry comes from one canonical source and the right side is an exact horizontal mirror of the left. Edge visual bands are clipped around enabled corner geometry so the corners do not behave or look like a second overlay system. The legacy auxiliary overlay does not own zone selection.

Track control is also owned entirely by the existing visualizer/recognizer/router stack. It is one continuous edge lane with three semantic segments: **Previous | Play/Pause | Next**. The center 20% of the band is the visible Play/Pause start segment; Previous/Next remains the surrounding swipe interaction. There is no standalone Play/Pause action in the current edge menu and no second center toggle, overlay or recognizer. The old enum/serialized values remain readable only for compatibility and sanitize into Track control.

Alpha.40 gives center taps a dedicated recognition envelope rather than lowering the normal gesture thresholds. When a one-finger Track candidate **starts inside the center segment**, `EdgeGestureRecognizer` keeps it in Candidate through up to **4.5 mm radial movement**, regardless of small off-axis finger drift. A lift within **700 ms** can then satisfy `TrackCenterGesturePolicy`. Movement beyond that envelope resumes the existing normal edge direction/claim path. The action router still requires the established **9 mm** Track swipe threshold before Previous/Next is sent. This ordering fixes the former dead zone where a normal tap could cross the ~2 mm general claim threshold and be rejected as wrong-direction before lift, without making tiny movements count as skips.

The action editor treats each non-Off action as one physical affordance. If the user selects an action already assigned to another edge, the two **action kinds swap** rather than clearing the previous edge. Sensitivity and inversion remain properties of the physical edge and therefore stay with their original edges during a swap. If the selected edge was Off, moving an occupied action naturally leaves Off behind because there is no second active action to exchange.

The Track popup reports resulting playback state while its glyph communicates the next familiar media action: **Playing → pause bars** and **Paused → play triangle**. If only the virtual-key fallback succeeds and post-command state is unknown, the OSD remains intentionally state-ambiguous rather than inventing a result.

Runtime corner recognition remains intentionally separate from edge recognition. Enabled corner launches use the same visible guard → diagonal lane → rounded end-cap geometry as the recognizer. A corner candidate owns the contact from the first eligible frame and rejected corner input remains locked out until lift instead of falling through into a neighboring edge gesture. Optional reverse-close starts from the rounded inner cap and uses the same ownership/intent rules rather than a second gesture worker.

Reverse-close routes into the canonical application hide-to-tray transition. Compact uses the transition-owned synchronous hide before final shell-state verification; normal user-triggered tray toggling keeps its separate animation path. Visual-QA reverse fixtures are built from a clean non-live corner baseline so an outward trail cannot inherit an earlier inward contact segment.

Raw HID input stays available to recognition at full rate. WPF visualization is coalesced and page listeners only remain attached while the Touchpad page is visible, preventing rendering work from becoming an application-wide input tax. Gesture recognition itself remains active in the background when configured and is started explicitly at silent tray startup as described above.

## Audio lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state, not durable application state; they are cleared when the Audio page becomes hidden so navigation during a drag cannot suppress later refreshes or apply a stale microphone write off-page.

## Status, diagnostics and discovery

`HardwareServiceClient` caches a short last-known-good status snapshot and backs off after a confirmed offline service state. It publishes bounded status/operation events to the application instead of letting individual pages create competing service polling loops.

Diagnostics are local-first. Compatibility sharing remains explicit, sanitized and separate from hardware control. Raw touch coordinates, personal file content, usernames, serial numbers and arbitrary memory/log dumps are outside the intended upload schema.

Direct fan-percent and fan-curve writes are classified as fan-control diagnostic operations rather than falling through to generic hardware events. Bounded X9 fan samples reuse already-observed service status and preserve provider/source distinctions without starting a second hardware polling loop. Firmware-profile changes remain semantic policy operations and do not claim direct RPM ownership.

## Update state

Home and Updates share one application update result. Completed manual checks also share one Last-checked timestamp owner: the in-memory value is refreshed immediately and persisted for the next session. Page reconstruction reads that owner rather than maintaining a second timestamp path.

## Release and compatibility boundaries

The installer is a small bootstrapper plus a separately versioned payload. CI exercises build/tests, real WPF shell smoke, visual snapshots, package size, installer/service lifecycle and the legacy updater fixture.

PR CI and package workflows cancel superseded runs for the same PR/ref so stale branch commits do not waste Windows runners. Immutable/tag release packaging remains outside that cancellation behavior.

A cleanup is not permission to remove compatibility code blindly. Current-client dead code should be deleted; server-side legacy protocol handlers and serialized enum values stay where required until the minimum supported installed client/settings floor no longer needs them and that compatibility decision is intentionally advanced.
