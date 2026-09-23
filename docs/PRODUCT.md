# Product specification

ThinkControl is a capability-driven Windows laptop-control application for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry. It provides a Compact tray surface for common controls and a resizable Advanced window for deeper controls, history, setup and diagnostics.

Current source release target: `v0.1.0-alpha.48`.  
Current immutable prerelease: `v0.1.0-alpha.48`. Alpha.44 remains the hardware-behavior baseline for the narrow alpha.45–alpha.48 shell/interface maintenance series.

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
13. Keep startup-critical user-session infrastructure ahead of optional discovery/polish work; helper-app responsiveness is a product requirement, not a reason to collapse the privileged service boundary.
14. For cross-cutting safety modes, keep one canonical policy owner and compose existing controls through that policy instead of creating duplicate action systems.
15. Continuous Touchpad controls must advance from confirmed OS state with bounded queued lead; a delayed backend must never turn extra finger movement into a later runaway jump.

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
- one **Audio safety** selector (`Normal` / `Gesture lock` / `Silent`) grouped with Brightness/Volume rather than detached in the footer;
- direct links to Audio, Settings and the Advanced window.

Compact is a persistent utility surface while visible. Explicit close, tray-toggle and Compact/Advanced transitions hide it; unrelated focus changes do not. Audio safety remains one compact session state. Gesture lock blocks only ThinkControl touchpad audio/media gestures; Silent additionally keeps Windows output muted.

### Advanced

Advanced contains Home, Performance, Fans, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings. Detailed sensor telemetry opens from System instead of occupying a permanent navigation page.

All pages share one layout rail, spacing system, typography system, theme and semantic icon vocabulary. Page navigation resets stale scroll offsets so a revisited page reopens at its canonical header rail. Compact ↔ Advanced switching is a single-owner shell transition and is exercised by real WPF lifecycle smoke in CI.

Audio Safety has one canonical session owner. Advanced Home exposes the explanatory Normal / Media lock / Silent quick card, Compact exposes the same state as Media safety beside Volume, and the Audio page carries the deeper audio context. Settings does not duplicate the mode editor.

## Performance and power

User-facing Windows power terminology is consistently **Efficiency / Balanced / Performance** even where internal Windows/provider contracts retain older enum names.

Battery and plugged-in preferences are stored separately. Compact remains the single fast battery-profile selector; Advanced Home exposes both Battery and Plugged-in preferences because the space is available and the distinction matters. The full Performance page remains the detailed source of truth and reset surface for both.

On the X9 firmware cooling backend, ThinkControl keeps the active cooling profile and Windows performance preference as separate user-facing settings even though both coordinate through Lenovo policy. Before a built-in fan profile is selected, the current power preference becomes the restore baseline. A later power-mode change updates that baseline without cancelling the fan profile; selecting Auto clears ThinkControl-owned cooling overrides and restores the latest baseline.

Audio Safety is intentionally separate from cooling. `Silent` does **not** silently force fan Quiet. A future custom preset may compose those intents only if every changed subsystem has explicit capability, ownership and rollback semantics.

## Fans, PawnIO and temperatures

Fans consume generic fan/control-temperature capabilities. A provider may expose discrete states, a continuous target-RPM contract, an OEM-native thermal policy, a narrow semantic full-speed switch, or read-only telemetry; the UI must not assume one backend merely because `FanControl` exists. Calibration is an explicit capability and appears only when the active direct provider advertises it.

On the current X9 reference path, physical alpha.40 evidence showed an important distinction: Lenovo LITSSvc **Performance** policy can report high tachometer RPM while still remaining audibly/physically below Lenovo Auto's true high-cooling state. Alpha.41 therefore no longer treats Performance policy alone as literal maximum cooling.

The X9 built-ins now have these semantics:

- **Auto** clears ThinkControl-owned cooling overrides and returns to the current Lenovo power-policy baseline;
- **Quiet** requests Lenovo Quiet policy;
- **Balanced** requests Lenovo Balanced policy;
- **Max cooling** first requests Lenovo Performance policy, then requires the exact X9 to expose Lenovo Other Mode's known global full-speed boolean `0x04020000` by a safe live read. If that exact semantic cannot be verified, Max fails explicitly rather than pretending Performance policy is physical maximum.

The global full-speed path is not a guessed RPM/PWM value. Upstream Lenovo/Linux work and independent modern Lenovo tooling use the same known feature as a boolean full-speed override. ThinkControl restricts it to the verified X9 identity, rejects an explicitly invalid/readonly capability row, requires the exact feature to return a live `0`/`1` value immediately before the write, accepts only values `0` and `1`, and verifies the requested state by readback. ThinkControl remembers only a full-speed state it actually changed. Its owned state is released before lower profiles, Auto and normal service disposal. A pre-existing full-speed state owned by another utility is not silently stolen by ordinary profile transitions.

Lenovo `LENOVO_OTHER_METHOD` still supplies real per-fan `fanX_input` telemetry where available. Its experimental per-fan `fanX_target` writer remains read-only after physical testing reproduced repeated speed cycling/re-kick and a nominal target ceiling below naturally hot firmware Auto. Alpha.43 does not increase those targets or use the inferior EC writer to simulate maximum cooling. `EnergyDrv` remains a separate read-only telemetry path until its writer contract is reviewed.

The classic seven-step ThinkPad EC path is provider-specific investigation/diagnostic behavior, not the product definition of 0–100%. Raw EC diagnostics appear only when that exact discrete semantic capability is active. Manual direct-output tests are bounded to 30 seconds and restore the previous profile or firmware Auto on failure. A completed calibration is provider state, not a permanent success card.

PawnIO registration, kernel-service readiness and actual device/provider access are modeled separately. A stale registry entry is not enough to call hardware access ready, and provider failure is not permission to guess another low-level writer.

See [Cooling Design](COOLING-DESIGN.md) for the canonical cooling/calibration contract.

## Display

Where Windows exposes the capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness. Unsupported policy is opened through supported Windows Settings surfaces rather than undocumented registry manipulation.

## Audio

Normal output, microphone and volume controls use Windows audio endpoints. Output/microphone writes are debounced while the Audio page is active; navigation clears transient drag/debounce ownership so stale writes cannot fire off-page.

### Audio Safety

Alpha.43 adds one Windows-generic session policy for situations such as tests, lectures and quiet work:

- **Normal** — existing ThinkControl output/media controls behave normally.
- **Media lock** — blocks ThinkControl Touchpad Volume, Media scrub, Previous/Next and integrated Play/Pause. It does not mute Windows audio, stop media deliberately started elsewhere or change the current output volume.
- **Silent** — includes Media lock, mutes the active Windows render endpoint and blocks ThinkControl output volume/unmute writes while active.

Microphone input remains independent. Silent does not automatically mute, lower or otherwise change the capture endpoint.

Silent tracks mute ownership per output endpoint encountered during the session. ThinkControl records the endpoint's prior mute state once, enforces mute while that endpoint is the active default, and restores only the recorded prior state when leaving Silent or on orderly app exit. If an endpoint has disappeared, ThinkControl does not guess another endpoint to modify.

A default-output change while Silent is active reuses the application's existing bounded status cadence to converge the newly active output into the same policy. No second audio polling loop is added.

The mode is deliberately **session-only in alpha.43**. Restart returns to Normal because a new process cannot truthfully claim ownership of mute state established by a previous process. Persisted phone-style Focus Modes/presets are therefore not part of this release.

Blocked Touchpad actions get bounded `Media locked`/`Silent` feedback rather than simply appearing broken. Entering Media lock or Silent also cancels a currently active Touchpad audio action so an in-flight gesture cannot continue writing after the policy changes.

Dolby controls remain provider-driven rather than Lenovo-specific. Direct controls are enabled only when an installed DAX path exposes a semantic operation ThinkControl can verify; private profile IDs and IEQ mappings are not guessed.

## Keyboard

Hardware backlight states and user-session effects are separate capabilities. Off / Low / High are static hardware states where supported; Auto means a verified firmware/OEM contract, never a ThinkControl idle-dimming imitation.

Breathing, Reactive and Audio are bounded local effects and require `KeyboardEffects`. A fallback provider that cannot safely accept repeated changes does not advertise that capability. Saved effects are restored only after provider capability is known.


Keyboard effects are labeled **EXPERIMENTAL**. When the active provider explicitly advertises repeated-write effect capability, ThinkControl uses that reviewed path. When only ordinary static Off / Low / High control exists, the user may deliberately enable a **session-only experimental fallback** after a warning. That fallback does not unlock a new command surface: it reuses the existing bounded, deduplicated backlight writes and may still produce OEM brightness pop-ups, ignored writes or less-smooth animation. The opt-in is never persisted across ThinkControl restarts.

## Touchpad

The Touchpad page shows real contact points, bounded recent trails, configurable precision edge gestures, deliberate top-corner launch zones, haptic settings where Windows/provider support exists, and bounded OSD feedback.

The editor/visualizer uses one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right. Edges and corners share one rendering owner and one idle/selected/hover/candidate/live grammar. Enabled top-corner launches use the canonical **guard → diagonal lane → rounded end-cap** geometry; the right side is an exact mirror of the left. Rejected corner ownership remains locked until lift and cannot fall through into a neighboring edge gesture.

### Track control

Track control stays one coherent edge affordance with one recognizer/router owner. There is no standalone current Play/Pause edge action and no second center overlay/recognizer. Old serialized standalone `PlayPause` bindings remain readable and sanitize into Track control.

Alpha.43 adds one **Play / Pause** switch inside the selected Track-control editor. It is a Track option, not a second action. Existing alpha.42 configurations default to enabled so upgrades preserve the integrated center behavior.

With the switch **on**, the lane is **Previous | Play/Pause | Next**. The visible center segment is **28%** of the selected Track edge (`0.36..0.64`). A quick center tap does **not** toggle media. Play/Pause requires a center-start contact to remain held for at least **450 ms**, stay within **3 mm** maximum radial movement, and then release.

The action deliberately commits **on release**. Reaching 450 ms while the finger is still down is not enough. Release acts as the final intent confirmation and prevents a resting/incidental touch from automatically starting global media merely because a timer expired. There is no delayed auto-fire worker.

The recognizer records the **maximum** center excursion while the contact remains a candidate. Moving away and returning near the start cannot make a previously mobile contact look stationary at release. If movement exceeds the 3 mm hold slop, center Play/Pause is disarmed and normal Track direction recognition resumes. Previous/Next requires a deliberate **12 mm** swipe and commits only on release. A Track swipe never also toggles Play/Pause on release.

With the switch **off**, Track remains assigned but the center control disappears both visually and behaviorally. The center fill, separators and Play/Pause icon are not drawn; only Previous and Next remain in the edge lane. The explicit opt-out preference survives temporarily moving/removing Track so the user's choice is restored if Track is assigned again.

When assigning an edge action already used elsewhere, the editor swaps the two action kinds instead of clearing the old edge; sensitivity and inversion remain physical-edge tuning and stay with their edges.

Track OSD semantics follow familiar media players: the label reports current/resulting state while the glyph shows the available next action. **Playing shows pause bars; Paused shows the play triangle.** A virtual-key fallback that cannot report post-command state remains labelled `Playback toggled` rather than inventing state.

### Corner launch and reverse close

Top-corner launch geometry stays unchanged and mirrored. When **Reverse swipe closes ThinkControl** is enabled, alpha.42 no longer requires the first reverse frame to land only inside the small rounded end-cap. The **inner half of the already-visible diagonal lane** is a valid reverse-close start target. The outer corner guard still belongs to the normal inward launch, and no invisible hit target exists outside the rendered lane/cap geometry.

Live input has two rates by design: recognition consumes every raw HID frame, while WPF visualization coalesces to roughly display-refresh cadence. UI-only listeners attach only while the page is visible, while configured background recognition remains application-level behavior.

## Battery

ThinkControl can display percentage, charging state, live/smoothed watts, remaining/full-charge Wh, health, cycle count when exposed, filtered ETA and battery temperature only when a credible battery-specific provider supplies it. Charge/discharge history is local and bounded; Windows remains the owner of system sleep/screen/presence policy.

Battery health history samples firmware-reported full-charge capacity versus design capacity at most once per day when both values are available. This sampling is independent of completing a charging session or reaching 100%, so an intentional 80–90% preservation threshold does not need to be disabled for the trend to learn. The trend is capacity telemetry, not a fabricated wear/lifetime prediction.


Battery Preservation mirrors the verified OEM threshold state into the main app model. The Battery page shows the active start/stop window and plain-language behavior (for example, **Charging paused near 90% · resumes below 80%**). Applying or disabling a preset produces a short passive confirmation, and a later real charging transition while still on AC produces a passive pause/resume notification. Startup only establishes a baseline, so ThinkControl does not invent a transition notification merely because the app launched while already paused.

## Startup and shell reliability

A dedicated painted loading surface appears before synchronous startup work on normal visible launches. Rich WMI inventory is not on the process-start critical path: a fast firmware-registry/power preflight runs first and the full inventory refreshes on a worker.

`Start with Windows` launches the UI with `--tray`. Alpha.41 treats configured edge gestures like hotkey infrastructure rather than page content: the earliest `Application.Startup` hook reads only cheap registry identity and then registers configured Raw Input immediately for a tray-only launch. Compact-window construction, diagnostics, service status and rich hardware discovery come later. Visible launches still defer the HID probe until WPF reaches `ContextIdle` so first paint remains responsive.

This is the useful architectural lesson from lightweight helper applications such as G-Helper: essential user-session input/tray behavior comes first and heavier detection is background work. ThinkControl does **not** copy G-Helper's single-process hardware architecture or change startup ownership merely for similarity; its privileged Windows service remains an intentional safety boundary.

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

ThinkControl separates compatibility learning, crash recovery and troubleshooting diagnostics. Support/report payloads use bounded allowlisted schemas and exclude serial numbers, usernames, hostnames, personal paths/content and raw touch trails. No automatic cloud compatibility/crash upload is part of alpha.43.

## Installation and updates

The current alpha series uses the existing small installer/bootstrap plus application payload. In-app updates obtain Setup + Payload + checksums, verify the managed files and only then perform an explicit elevation handoff. Background checks never install software or trigger UAC by themselves.

When automatic update checks are enabled, ThinkControl performs a stale-gated check shortly after startup and may re-check on app activation or resume once the last attempt is at least four hours old. There is no permanent update polling timer. Tray-only startup remains silent while no window is visible; if a check finds a newer release, the first later visible Compact or Advanced session offers **Install now** or **Later** and keeps that decision prompt visible until one is chosen. Later suppresses repeat interruption for that exact version but leaves the release visible in Notifications and Updates. A newer version can prompt again.

Manual checks on Home and Updates publish one shared result and update one Last-checked timestamp owner immediately when the check completes; the timestamp is persisted for the next session. When Home already knows an update is available, its update affordance opens Updates directly instead of performing another redundant check.

Packaging/installer CI validates payload construction, custom-location install/update behavior, service startup/IPC, compatibility with the oldest supported updater fixture and uninstall cleanup. `version.json` remains the build/release version source of truth.

## Safety boundary

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private CPU tuning calls or automatic low-level write support for unknown machines.

New low-level features require a documented provider contract, a defined safety/recovery model, narrow identity/capability gating and test/physical evidence appropriate to the risk. Alpha.41's full-speed path uses one already-documented semantic ID and exact boolean values only; it is not permission to broaden Lenovo Other Mode writes. Audio Safety is a Windows user-session policy and does not weaken these hardware boundaries.
