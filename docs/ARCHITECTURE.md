# ThinkControl architecture

This document describes the current architecture at **v0.1.0-alpha.39**. `docs/RELEASE_READINESS.md` is the persistent release/commercial handoff; this file explains runtime boundaries and intentional compatibility debt.

## Process boundary

ThinkControl is split into a normal-user WPF application and a privileged Windows service.

- `ThinkControl.UI` owns Compact/Advanced windows, settings, presentation state, user-session effects and orchestration.
- `ThinkControl.Service` owns privileged hardware access and exposes a narrow named-pipe IPC contract.
- `ThinkControl.Core` contains shared models, policies and protocol types that do not depend on WPF or hardware implementations.
- `ThinkControl.Hardware` contains provider implementations and verified low-level device behavior.
- `ThinkControl.DeviceProfiles` is the architectural boundary for device/profile data. It intentionally remains small while capability logic is moved out of UI assumptions.

The UI must remain `asInvoker`. Hardware operations that need elevated/device access belong in the service rather than causing repeated UAC prompts from the desktop process.

## Hardware safety model

Hardware support is capability-driven. Unknown hardware remains read-only/safe until an operation has a reviewed provider and validation gate. Generic UI consumes semantic capability state; it must not infer write support, calibration requirements or effect support by parsing model names or diagnostic provider strings.

The ThinkPad X9 path currently prefers Lenovo-native **telemetry** over direct EC writes. `LENOVO_OTHER_METHOD` can expose real dual-fan `fanX_input` channels, but its experimental target-RPM writer is held read-only in alpha.39 because physical alpha.38 testing failed the writer's own acceptance gate: a fixed target produced repeated speed cycling/re-kick and nominal 100% remained below naturally hot firmware Auto. VALID+GET+SET metadata, sane Fan Test ranges and live channels remain useful evidence but are not sufficient product write authorization after that physical rejection. Lenovo `EnergyDrv` is likewise read-only until a matching X9 write contract is proven.

Fan ownership is explicit. ThinkControl records the provider/channels it actually takes over, returns those owned channels to Lenovo/OEM Auto on handoff/failure/disposal where supported, and does not infer ownership merely from reading an external manual-looking state. Target `0` on the rejected Other Mode path remains available for cleanup/reassertion of Auto after previously owned state. Native two-fan evidence is latched for the current service lifetime so a rejected/native writer or transient OEM telemetry miss cannot silently re-enable the known-inferior EC writer.

Repeated provider discovery is avoided where possible. The service keeps provider state; the UI consumes bounded status snapshots and uses targeted refresh operations for sensors, keyboard and full provider recovery.

## Cooling model

The current UI has one fan-curve model:

- named `FanCurveDefinition` profiles;
- `SetCoolingCurve` for current curve writes;
- `SetFanPercent` for deliberate temporary output testing where supported;
- `ReturnFanToAuto` for firmware/OEM ownership;
- characterization operations only when the active provider advertises a calibration workflow.

`FanSupervisor` is the sole owner of ThinkControl fan writes. A physically accepted continuous target-RPM provider may receive percentages directly; a discrete provider may map the same semantic targets through a measured output-state mapping. Raw EC states/calibration remain provider-specific diagnostics rather than a generic fan-control assumption. On the X9 alpha.39 path, no writable fan provider is advertised merely because the Other Mode metadata is write-capable.

The service exposes `FanCalibrationSupported` and `FanCalibrationRequired` in `HardwareCapabilitySnapshot`. `App.Cooling` converts those service capabilities plus characterization progress into the generic `FanCalibrationUiState`. The Fans page and Inbox consume that state; they do not independently decide that a specific model must calibrate. The calibration task card is visible only while calibration is required or actively running; a ready mapping is ordinary provider state, not a permanent top-of-page success card.

Manual fan UI is a bounded diagnostic surface. Percentage targets and provider-specific raw states run through the same 30-second temporary-test/automatic-restore contract. The surface is hidden when no verified writable provider exists. Raw EC diagnostics appear only when the active provider explicitly advertises the discrete-EC semantic contract.

The service still accepts older IPC operations such as `SetCoolingProfile`, `SetCustomCoolingCurve` and `MarkFanLevelAudible` for the supported installed-client compatibility floor. Those are **legacy server compatibility endpoints**, not current UI APIs. Do not remove them merely because the current `HardwareServiceClient` no longer calls them; removal requires an explicit updater/client-floor decision and compatibility test update.

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

Track control is also owned entirely by `TouchpadVisualizer`. It is one continuous edge lane with three semantic segments: **Previous | Play/Pause | Next**. Previous/Next remain deliberate swipes; the center 20% of the same band is the visible Play/Pause tap segment. `TrackCenterGesturePolicy` accepts only a short low-travel tap there, with movement tolerance below the general edge-claim threshold. The old serialized center flag remains readable for settings compatibility but is derived from the Track binding at runtime; there is no separate menu switch, floating pill or second visual owner.

Runtime gesture recognition remains intentionally stricter than editor selection. Enabled corner launches use the same visible guard → diagonal lane → rounded end-cap geometry as the recognizer. A corner candidate owns the contact from the first eligible frame and rejected corner input remains locked out until lift instead of falling through into a neighboring edge gesture. Optional reverse-close starts from the rounded inner cap and uses the same ownership/intent rules rather than a second gesture worker.

Reverse-close routes into the canonical application hide-to-tray transition. Compact uses the transition-owned synchronous hide before final shell-state verification; normal user-triggered tray toggling keeps its separate animation path. Visual-QA reverse fixtures are built from a clean non-live corner baseline so an outward trail cannot inherit an earlier inward contact segment.

Raw HID input stays available to recognition at full rate. WPF visualization is coalesced and page listeners only remain attached while the Touchpad page is visible, preventing rendering work from becoming an application-wide input tax.

## Audio lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state, not durable application state; they are cleared when the Audio page becomes hidden so navigation during a drag cannot suppress later refreshes or apply a stale microphone write off-page.

## Status, diagnostics and discovery

`HardwareServiceClient` caches a short last-known-good status snapshot and backs off after a confirmed offline service state. It publishes bounded status/operation events to the application instead of letting individual pages create competing service polling loops.

Diagnostics are local-first. Compatibility sharing remains explicit, sanitized and separate from hardware control. Raw touch coordinates, personal file content, usernames, serial numbers and arbitrary memory/log dumps are outside the intended upload schema.

Current fan-percent and fan-curve writes are classified as fan-control diagnostic operations rather than falling through to generic hardware events. Bounded X9 fan samples reuse already-observed service status and preserve provider/source distinctions without starting a second hardware polling loop. Removed current-client cooling wrappers are guarded by source tests so the service-only legacy compatibility surface cannot silently leak back into the modern UI client.

## Update state

Home and Updates share one application update result. Completed manual checks also share one Last-checked timestamp owner: the in-memory value is refreshed immediately and persisted for the next session. Page reconstruction reads that owner rather than maintaining a second timestamp path.

## Release and compatibility boundaries

The installer is a small bootstrapper plus a separately versioned payload. CI exercises build/tests, real WPF shell smoke, visual snapshots, package size, installer/service lifecycle and the legacy updater fixture.

PR CI and package workflows cancel superseded runs for the same PR/ref so stale branch commits do not waste Windows runners. Immutable/tag release packaging remains outside that cancellation behavior.

A cleanup is not permission to remove compatibility code blindly. Current-client dead code should be deleted; server-side legacy protocol handlers stay until the minimum supported installed client no longer needs them and the updater compatibility fixture is intentionally advanced.
