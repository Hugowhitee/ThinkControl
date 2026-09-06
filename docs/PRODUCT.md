# Product specification

ThinkControl is a capability-driven Windows laptop-control application for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry. It provides a Compact tray surface for common controls and a resizable Advanced window for deeper controls, history, setup and diagnostics.

Current prerelease candidate: `v0.1.0-alpha.40`.

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
11. Keep Windows startup useful before rich hardware discovery finishes: tray ownership and enabled background gestures must not depend on slow WMI or a visible WPF window activation.
12. Treat one visible interaction as one product affordance: do not expose duplicate settings/actions for the same Touchpad behavior.

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

All pages share one layout rail, spacing system, typography system, theme and semantic icon vocabulary. Page navigation resets stale scroll offsets so a revisited page reopens at its canonical header rail. Compact ↔ Advanced switching is a single-owner shell transition and is exercised by real WPF lifecycle smoke in CI.

## Performance and power

User-facing Windows power terminology is consistently **Efficiency / Balanced / Performance** even where internal Windows/provider contracts retain older enum names.

Battery and plugged-in preferences are stored separately. Compact and Home intentionally expose the battery preference as the quick control; the full Performance page is the source of truth for configuring both battery and AC behavior independently.

On the X9 firmware-policy cooling backend, ThinkControl keeps the active cooling profile and Windows performance preference as separate user-facing settings even though both coordinate through Lenovo thermal policy. Before a built-in fan profile is selected, the current power preference becomes the restore baseline. A later power-mode change updates that baseline without cancelling the fan profile; selecting Auto clears the cooling override and restores the latest baseline.

## Fans, PawnIO and temperatures

Fans consume generic fan/control-temperature capabilities. A provider may expose discrete states, a continuous target-RPM contract, an OEM-native thermal policy, or read-only telemetry; the UI must not assume one backend merely because `FanControl` exists. Calibration is an explicit capability and appears only when the active provider advertises it.

On the current X9 reference path, normal built-in profiles remain working product controls even though the alpha.38 direct target writer was physically rejected. The alpha.39 architecture, preserved by alpha.40, exposes a distinct `LenovoFirmwarePolicy` backend through the reviewed exact-X9 LITSSvc thermal-policy path:

- **Auto** clears the cooling override and returns to the current Lenovo power-policy baseline;
- **Quiet** requests Lenovo Quiet policy;
- **Balanced** requests Lenovo Balanced policy;
- **Max cooling** requests Lenovo Performance cooling policy.

Lenovo firmware remains responsible for the closed-loop fan ramp. This backend does not advertise fake percentages, RPM targets, EC states or custom direct-curve semantics. Custom curves/manual percentages remain unavailable until a physically accepted direct writer exists.

Lenovo `LENOVO_OTHER_METHOD` may still supply real per-fan `fanX_input` telemetry. Its experimental `fanX_target` writer remains read-only after physical testing reproduced repeated speed cycling/re-kick and a nominal maximum below naturally hot firmware Auto. Target `0` remains available for cleanup/reassertion of firmware Auto after previously owned state. `EnergyDrv` remains a separate read-only telemetry path until its writer contract is reviewed.

The classic seven-step ThinkPad EC path is provider-specific investigation/diagnostic behavior, not the product definition of 0–100%. Raw EC diagnostics appear only when that exact discrete semantic capability is active. Manual direct-output tests are bounded to 30 seconds and restore the previous profile or firmware Auto on failure. A completed calibration is provider state, not a permanent success card.

PawnIO registration, kernel-service readiness and actual device/provider access are modeled separately. A stale registry entry is not enough to call hardware access ready, and provider failure is not permission to guess another low-level writer.

See [Cooling Design](COOLING-DESIGN.md) for the canonical cooling/calibration contract.

## Display

Where Windows exposes the capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness. Unsupported policy is opened through supported Windows Settings surfaces rather than undocumented registry manipulation.

## Audio

Normal output, microphone and volume controls use Windows audio endpoints. Output/microphone writes are debounced while the Audio page is active; navigation clears transient drag/debounce ownership so stale writes cannot fire off-page.

Dolby controls are provider-driven rather than Lenovo-specific. Direct controls are enabled only when an installed DAX path exposes a semantic operation ThinkControl can verify; private profile IDs and IEQ mappings are not guessed.

## Keyboard

Hardware backlight states and user-session effects are separate capabilities. Off / Low / High are static hardware states where supported; Auto means a verified firmware/OEM contract, never a ThinkControl idle-dimming imitation.

Breathing, Reactive and Audio are bounded local effects and require `KeyboardEffects`. A fallback provider that cannot safely accept repeated changes does not advertise that capability. Saved effects are restored only after provider capability is known.

## Touchpad

The Touchpad page shows real contact points, bounded recent trails, configurable precision edge gestures, deliberate top-corner launch zones, haptic settings where Windows/provider support exists, and bounded OSD feedback.

The editor/visualizer uses one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right. Edges and corners share one rendering owner and one idle/selected/hover/candidate/live grammar. Enabled top-corner launches use the canonical **guard → diagonal lane → rounded end-cap** geometry; the right side is an exact mirror of the left. Rejected corner ownership remains locked until lift and cannot fall through into a neighboring edge gesture.

### Track control

Track control is one coherent three-part edge affordance: **Previous | Play/Pause | Next**. There is no standalone current Play/Pause edge action, no separate Center play/pause setting and no second overlay/recognizer.

The center start segment occupies 20% of the lane. Alpha.40 improves real-pad reliability by reserving a contact that starts inside this segment as a tap candidate through up to **4.5 mm radial drift** for up to **700 ms**. Small off-axis movement therefore no longer hits the general direction classifier and gets rejected before lift. If movement exceeds that envelope, normal edge recognition resumes. Previous/Next keeps its existing deliberate **9 mm** skip threshold, so the larger tap envelope is not also a lower skip threshold.

Old serialized standalone `PlayPause` bindings remain readable and sanitize into Track control. This keeps user settings compatible while removing the duplicate menu choice from the current product model.

When assigning an edge action already used elsewhere, the editor swaps the two **action kinds** instead of clearing the old edge. Sensitivity and inversion are physical-edge tuning and remain with their respective edges during that swap.

Track OSD semantics follow familiar media players: the label reports current/resulting state while the glyph shows the available next action. **Playing shows pause bars; Paused shows the play triangle.** A virtual-key fallback that cannot report post-command state remains labelled `Playback toggled` rather than inventing state.

Live input has two rates by design: recognition consumes every raw HID frame, while WPF visualization coalesces to roughly display-refresh cadence. UI-only listeners attach only while the page is visible, while configured background recognition remains application-level behavior.

## Battery

ThinkControl can display percentage, charging state, live/smoothed watts, remaining/full-charge Wh, health, cycle count when exposed, filtered ETA and battery temperature only when a credible battery-specific provider supplies it. Charge/discharge history is local and bounded; Windows remains the owner of system sleep/screen/presence policy.

## Startup and shell reliability

A dedicated painted loading surface appears before synchronous startup work on normal visible launches. Rich WMI inventory is not on the process-start critical path: a fast firmware-registry/power preflight runs first and the full inventory refreshes on a worker.

`Start with Windows` launches the UI with `--tray`. Enabled Touchpad gestures are explicitly queued after startup yields and do not wait for `Application.Activated`, which a silent tray process may never receive. ThinkControl does not modify machine-wide Explorer startup-delay policy.

Opening Advanced restores minimized/hidden state before heavy-page work. Compact/Advanced transitions remain single-owner. WPF dispatcher overload use is guarded against the prior delayed `TargetParameterCountException` class.

## Compatibility

ThinkControl grows support from broad to specific:

```text
Windows generic → OEM generic → product family → exact model
```

Profiles select reasonable provider candidates. Providers own implementation, readback, lifecycle and write safety. Generic pages consume semantic capability fields rather than model/provider-detail strings.

Unknown/unverified laptops remain conservative. Windows-safe features may work, reviewed read-only providers may surface real telemetry, and hardware-specific writes remain unavailable until the relevant provider/device contract is verified.

Legacy server IPC and serialized settings values stay where required for the supported installed-client/settings floor. They are compatibility data, not permission to expose obsolete current UI choices.

## Diagnostics and privacy

ThinkControl separates compatibility learning, crash recovery and troubleshooting diagnostics. Support/report payloads use bounded allowlisted schemas and exclude serial numbers, usernames, hostnames, personal paths/content and raw touch trails. No automatic cloud compatibility/crash upload is part of alpha.40.

## Installation and updates

Alpha.40 uses the existing small installer/bootstrap plus application payload. In-app updates obtain Setup + Payload + checksums, verify the managed files and only then perform an explicit elevation handoff. Background checks never install software or trigger UAC by themselves.

Manual checks on Home and Updates publish one shared result and update one Last-checked timestamp owner immediately when the check completes; the timestamp is persisted for the next session.

Packaging/installer CI validates payload construction, custom-location install/update behavior, service startup/IPC, compatibility with the oldest supported updater fixture and uninstall cleanup. `version.json` remains the build/release version source of truth.

## Safety boundary

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private CPU tuning calls or automatic low-level write support for unknown machines.

New low-level features require a documented provider contract, a defined safety/recovery model, narrow identity/capability gating and test/physical evidence appropriate to the risk.
