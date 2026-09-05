# Product specification

ThinkControl is a capability-driven Windows laptop-control application for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry. It provides a Compact tray surface for common controls and a resizable Advanced window for deeper controls, history, setup and diagnostics.

Current prerelease candidate: `v0.1.0-alpha.37`.

Current physically reviewed low-level reference: Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

The reference device is **not** the product boundary. Windows-safe features should work broadly, while OEM/family/model providers can be added independently for Lenovo, ASUS, Dell, HP, Acer, MSI and other laptop families.

## Product goals

1. Keep common laptop controls quick to reach.
2. Show telemetry only when a real provider supplies it.
3. Detect support per capability instead of assuming a brand/family shares one hardware interface.
4. Keep the desktop UI unprivileged and isolate low-level operations in the Windows service.
5. Fail safely when a provider is missing, incomplete, unsupported or returns an unexpected state.
6. Keep the UI capability-first so adding another OEM does not create vendor-specific copies of product pages.
7. Keep model-specific writes behind explicit identity gates, provider-owned allowlists and readback/safety rules.
8. Never expose an empty/black application surface while expensive startup discovery or view construction is in progress.
9. Keep setup state truthful: registration metadata, kernel/service readiness and actual provider/device access are distinct facts.
10. Keep high-rate device input away from the WPF layout/render critical path; live visualization may coalesce frames while recognition retains the full input stream.

Implementation boundaries are defined in [Architecture](ARCHITECTURE.md), low-level rules in [Hardware Safety](HARDWARE-SAFETY.md), current support in [Device Support](DEVICE-SUPPORT.md), and Lenovo implementation evidence in [Lenovo provider research](research/lenovo-providers.md) plus [X9 research](research/x9-15-gen1.md).

## Product surfaces

### Compact

Compact contains the controls and telemetry most useful during normal operation:

- three replaceable live metric slots, defaulting to Battery, CPU and Fans;
- current fan profile and RPM when real telemetry exists;
- battery Efficiency / Balanced / Performance preference;
- display refresh controls;
- brightness and volume;
- keyboard backlight when supported;
- direct links to Audio, Settings and the Advanced window.

Compact is a persistent utility surface while visible. Explicit close, tray-toggle and Compact/Advanced transitions hide it; unrelated focus changes do not.

### Advanced

Advanced contains Home, Performance, Fans, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings. Detailed sensor telemetry opens from System instead of occupying a permanent navigation page.

All pages share one layout rail, spacing system, typography system, theme and semantic icon vocabulary. Page navigation resets stale vertical/horizontal scroll offsets so a revisited page reopens at its canonical header rail. Page-level actions belong on the shared top-right action rail rather than feature-specific one-off margins.

Compact ↔ Advanced switching is a single-owner shell transition. The destination shell is restored and paints before the old surface disappears or a heavy destination page begins device work. The real WPF lifecycle is exercised by CI rather than inferred from screenshots alone.

## Performance and power

User-facing Windows power terminology is consistently **Efficiency / Balanced / Performance** even where internal Windows/provider contracts retain older enum names.

Battery and plugged-in preferences are stored separately. Compact and Home intentionally expose the **battery preference** as the quick control; the full Performance page is the source of truth for configuring both battery and AC behavior independently.

An OEM thermal-policy provider may coordinate with the selected Windows preference only when that semantic contract has been reviewed for the exact supported scope. Power mode is not treated as fake direct fan-RPM/PWM control.

## Fans, PawnIO and temperatures

Fans consume generic fan/control-temperature capabilities. The provider may expose discrete EC states, a continuous OEM target-RPM contract, an OEM-native thermal policy, or read-only telemetry; the UI must not assume one backend merely because `FanControl` exists.

On the X9 path, ThinkControl prefers Lenovo-native fan semantics. `LENOVO_OTHER_METHOD` can expose per-fan `fanX_input` and tunable `fanX_target` values: ThinkControl enables that writer only when exact-X9 identity, VALID+GET+SET capability data, sane Lenovo fan constraints and at least two live fan channels all pass. Percentages and curves then map independently across each fan's OEM-reported range, while target `0` is reserved for Lenovo Auto. Extra/phantom capability records do not block two real writable fans.

Lenovo `EnergyDrv` `QueryFanSpeed 0x83102570` is a separate read-only native telemetry path. It can provide Fan 1 / Fan 2 evidence without authorizing the still-unrecovered `ChangeFanSpeed 0x8310257C` writer. Once two native Lenovo fan channels have been proven in a hardware-service lifetime, a transient native read failure does not silently re-enable the known-inferior EC writer.

The classic seven-step ThinkPad EC path remains an exact-model fallback/investigation provider, not the product definition of 0–100%. Raw EC steps and transactional seven-step calibration appear only when that discrete provider is actually active. EC step 7 is not labelled as Lenovo's absolute physical maximum and the unsafe/ambiguous `0x40` family remains blocked.

PawnIO prerequisite state is not inferred from an uninstall registry entry alone. ThinkControl distinguishes:

- whether compatible PawnIO registration exists;
- whether the PawnIO kernel service is registered;
- whether a demand-start driver is currently running;
- whether the hardware provider can actually open/verify the required device/module path.

A compatible registration with a missing kernel service is an incomplete installation and is presented as **repair**. A stopped demand-start kernel service can still be ready for provider probing. On the verified X9, repair is not considered a successful low-level recovery until the active provider path itself passes again; unrelated sensor or keyboard recovery cannot unlock fan writes.

Supervised cooling uses bounded smoothing, hysteresis, dwell time, immediate meaningful cooling increases and firmware fallback. Missing control telemetry/provider state or a thermal safety handoff returns ownership to OEM firmware.

Manual fan testing is temporary, restores the previous profile and falls back to firmware Auto if restoration cannot be proven. Provider ownership is explicit: ThinkControl returns only the OEM channels/provider state it actually took ownership of rather than inferring ownership from a readback that another utility may have created.

See [Cooling Design](COOLING-DESIGN.md) for the canonical cooling/calibration contract.

## Display

Where Windows exposes the capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness.

Unsupported Windows display policy is opened through supported Windows Settings surfaces rather than undocumented registry manipulation.

Runtime status uses cheap/cached paths. Slow WMI, display-capability and `powercfg` discovery is reserved for startup, explicit refresh or human-scale cache refresh rather than a fixed rapid cadence.

## Audio

Normal output, microphone and volume controls use Windows audio endpoints.

Output and microphone writes are debounced while the Audio page is active. Those timers and temporary drag flags are transient page state: hiding the page cancels pending writes and clears drag ownership so navigating away mid-drag cannot suppress future refreshes or apply a stale off-page microphone write.

Dolby controls are provider-driven rather than Lenovo-specific. Direct controls are enabled only when the installed DAX path exposes a semantic operation ThinkControl can verify; otherwise ThinkControl may open the official Dolby Access surface where appropriate. Private profile IDs/IEQ mappings are not guessed.

## Keyboard

Hardware backlight states and optional user-session effects are separate concepts. A backend must pass its read/probe contract before writes are enabled. Direct static changes and effects share serialized hardware ownership so one cannot silently overwrite/drop the other.

On a Lenovo backend that exposes the reviewed Vantage keyboard contract, `FirmwareAuto = 3` is treated as an observed OEM state, not a guessed direct-driver command. The normal keyboard-mode row exposes Off / Low / High / Auto. Selecting Auto requests Lenovo firmware Auto and requires readback verification.

Auto is **not** a ThinkControl effect and there is no software idle-dimming fallback. If Lenovo/OEM Auto cannot be set and verified, ThinkControl does not silently substitute a High → Low → Off policy while still labelling the result Auto.

Breathing, Reactive and Audio are separate bounded user-session effects. They require the stricter direct backlight provider and are deliberately unavailable through the Lenovo Vantage fallback because repeated Vantage writes can show Lenovo's own keyboard-brightness pop-up. Effects remain local, rate-limited and deduplicated; Reactive listens only while selected and Audio uses local loopback level data without storing audio.

Other OEMs should provide their own backend behind the same keyboard capability rather than adding vendor-specific page copies.

## Touchpad

The Touchpad page shows real contact points, bounded recent trails, configurable precision edge gestures, deliberate top-corner launch zones, haptic settings where Windows/provider support exists, and bounded OSD feedback.

A finger lift ends a visual trail segment. New contacts and implausibly large physical jumps do not draw fake connecting lines.

Track control prefers the active Windows media session and falls back safely where needed. Optional center Play/Pause uses a visible bounded center zone and deliberate low-travel hold/release; normal swipes still own Previous/Next.

The editor/visualizer uses one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right. Edges and corners share one rendering owner and one idle/selected/hover/candidate/live visual grammar. The former auxiliary corner overlay is non-interactive and does not own mouse selection.

Each enabled corner launch uses one canonical physical **guard → diagonal lane → rounded end-cap** shape. The visible quarter-circle guard is also the recognizer's real first-frame priority area, so slightly imperfect finger placement near the physical corner cannot accidentally become the neighboring top/side gesture. The lane and rounded end-cap are real usable areas too, not decorative overlays. If a corner launch is Off, it does not reserve runtime input from the edge recognizer.

The final right-corner geometry is mirrored from the canonical left-corner geometry rather than independently approximated. The center guide is a directional arrow, the inner end is a semicircular arc rather than a flat 90-degree cross-line/filled blob, and enabled Compact/Advanced actions show their semantic icon and text.

Runtime corner recognition remains intentionally separate from edge recognition and preserves the alpha.33 safety contract: corner launches and edge gestures are mutually exclusive per contact. A configured top corner owns a contact from the first candidate frame when the finger begins anywhere inside its visible guard/lane/cap area. If that corner candidate is rejected, the same still-down contact is locked out until lift and cannot be reinterpreted as an edge gesture.

Each corner can independently enable **Reverse swipe closes ThinkControl**. With that option enabled, the rounded inner cap becomes the outward start target: a deliberate diagonal swipe back toward the physical corner hides the currently visible Compact or Advanced shell. With the option disabled, the cap remains part of the normal inward launch area. The reverse path uses the same recognizer ownership, diagonal-intent and reject-until-lift rules as the launch path; it does not add a parallel gesture worker or overlay.

The gesture-owned hide-to-tray operation uses the canonical shell-transition owner. Compact is hidden synchronously for that transition before shell-state verification/diagnostics are recorded; the separate normal tray-toggle path may still animate. This keeps reverse-close lifecycle diagnostics aligned with the state the user actually sees.

Transient live corner ownership must not collapse/re-expand page layout while a finger is moving. The selected editor remains in place and is dimmed/disabled during live corner ownership. CI renders selected/live states for both corners, covers the normal inward state on the left and the opt-in reverse-close state on the mirrored right, and asserts mirrored final geometry plus unchanged editor layout across live frames. The reverse fixture starts from a clean non-live corner baseline so its trail represents only the outward close gesture.

Live input has two rates by design: recognition consumes every raw HID frame, while WPF visualization coalesces to roughly display-refresh cadence and publishes all-up frames immediately. Raw-input/HID registration is deferred until after the visible shell/page has painted. UI-only corner/gesture listeners attach only while the Touchpad page is visible so leaving the page cannot keep high-rate dispatcher work alive elsewhere in Advanced.

## Battery

ThinkControl can display percentage, charging state, live/smoothed watts, remaining/full-charge Wh, health, cycle count when exposed, filtered ETA and battery temperature only when a credible battery-specific sensor/provider supplies it.

Charge/discharge history is local and bounded. Windows remains the owner of system sleep/screen/presence policy; ThinkControl links to supported Windows settings instead of duplicating undocumented policy. Battery uses the same title/subtitle/top-action rail as other Advanced pages.

## Startup and shell reliability

A dedicated painted loading surface appears before synchronous startup discovery and remains until the destination has completed a render pass. Whole-window fade tricks are not used to hide an unpainted native WPF window.

Opening Advanced restores a minimized/hidden full window before requested heavy-page work begins. A passive successful-update confirmation can be dismissed directly; a deliberate Compact → Advanced transition clears it, while an update restart that already opens into Advanced still shows the confirmation once.

WPF dispatcher work must use strongly understood overloads. In particular, `Dispatcher.BeginInvoke` calls with a priority pass `DispatcherPriority` first; putting it after a zero-argument delegate can bind it into `params object[]` and produce a delayed `TargetParameterCountException` through `DynamicInvoke`. CI includes a source regression guard for that crash class.

The release gate includes real Compact → Advanced → Compact shell smoke plus deterministic screenshots across minimum/normal/wide widths, themes and important unavailable/error states.

## Compatibility

ThinkControl grows support from broad to specific:

```text
Windows generic → OEM generic → product family → exact model
```

Profiles select reasonable provider candidates. Providers own implementation, readback, lifecycle and write safety. Profiles cannot authorize arbitrary low-level writes by themselves.

Unknown/unverified laptops remain capability-driven and conservative. Windows-safe features may work, read-only providers may surface real telemetry, and hardware-specific writes remain unavailable until the relevant provider/device contract is verified.

The current desktop client uses the graph-based cooling API. Older service-side cooling IPC handlers remain intentionally available for the supported installed-client/updater compatibility floor; they are legacy server compatibility endpoints, not current UI features.

## Diagnostics and privacy

ThinkControl separates compatibility learning, crash recovery and troubleshooting diagnostics. Local crash history remains the durable source of truth. Support/report payloads use bounded allowlisted schemas and exclude serial numbers, usernames, hostnames, personal paths/content and raw touch trails.

No automatic cloud compatibility/crash upload is part of alpha.37; future telemetry/account work is tracked separately in [Release Readiness](RELEASE_READINESS.md). The immutable `v0.1.0-alpha.36` release is the production baseline immediately before this candidate.

## Installation and updates

Alpha.37 uses the existing small installer/bootstrap plus application payload. In-app updates obtain Setup + Payload + checksums, verify the managed files and only then perform an explicit elevation handoff. Background checks never install software or trigger UAC by themselves.

Packaging/installer CI validates payload construction, custom-location clean install, service startup/IPC, in-place update behavior, compatibility with the legacy updater fixture and uninstall cleanup. `version.json` remains the build/release version source of truth.

## Safety boundary

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private CPU tuning calls or automatic low-level write support for unknown machines.

New low-level features require a documented provider contract, a defined safety/recovery model, narrow identity/capability gating and test/physical evidence appropriate to the risk.
