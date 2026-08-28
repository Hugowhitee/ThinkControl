# Product specification

ThinkControl is a capability-driven Windows laptop-control application for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry. It provides a Compact tray surface for common controls and a resizable Advanced window for deeper controls, history, setup and diagnostics.

Current prerelease candidate: `v0.1.0-alpha.31`.

Current physically reviewed low-level reference: Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

The reference device is **not** the product boundary. Windows-safe features should work broadly, while OEM/family/model providers can be added independently for Lenovo, ASUS, Dell, HP, Acer, MSI and other laptop families.

## Product goals

1. Keep common laptop controls quick to reach.
2. Show telemetry only when a real provider supplies it.
3. Detect support per capability instead of assuming a brand/family shares one hardware interface.
4. Keep the desktop UI unprivileged and isolate low-level operations in the Windows service.
5. Fail safely when a provider is missing, unsupported or returns an unexpected state.
6. Keep the UI capability-first so adding another OEM does not create vendor-specific copies of product pages.
7. Keep model-specific writes behind explicit identity gates, provider-owned allowlists and readback/safety rules.
8. Never expose an empty/black application surface while expensive startup discovery or view construction is in progress.

Implementation boundaries are defined in [Architecture](ARCHITECTURE.md), low-level rules in [Hardware Safety](HARDWARE-SAFETY.md), current support in [Device Support](DEVICE-SUPPORT.md), and Lenovo implementation evidence in [Lenovo Providers](LENOVO-PROVIDERS.md) plus [X9 research](research/x9-15-gen1.md).

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

Compact is a persistent utility surface while visible. It does not disappear merely because focus moves to another application. Explicit close, tray-toggle and Compact/Advanced transitions still hide it.

### Advanced

Advanced contains Home, Performance, Fans, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings. Detailed sensor telemetry opens from System instead of occupying a permanent navigation page.

All pages share one layout rail, spacing system, typography system, theme and semantic icon vocabulary. Advanced is a normal Windows application window with normal focus, taskbar, Snap and caption behavior.

Compact ↔ Advanced switching is a single-owner shell transition. The destination paints before the old surface disappears, and the real WPF lifecycle is exercised by CI rather than inferred from screenshots alone.

## Performance and power

User-facing Windows power terminology is consistently **Efficiency / Balanced / Performance** even where internal Windows/provider contracts retain older enum names.

Battery and plugged-in preferences are stored separately. Compact and Home intentionally expose the **battery preference** as the quick control; the full Performance page is the source of truth for configuring both battery and AC behavior independently.

An OEM thermal-policy provider may coordinate with the selected Windows preference only when that semantic contract has been reviewed for the exact supported scope. Power mode is not treated as fake direct fan-RPM/PWM control.

## Fans and temperatures

Fans consume generic fan/control-temperature capabilities. The provider may expose discrete EC states, a percentage/PWM target, an OEM-native thermal policy, or read-only telemetry; the UI must not assume one backend merely because `FanControl` exists.

The verified X9 provider uses discrete fan states and supervised curves. ThinkControl maps user-facing targets onto verified hardware output states rather than pretending the EC exposes continuous PWM.

Supervised cooling uses bounded smoothing, hysteresis, dwell time, immediate meaningful cooling increases and firmware fallback. Missing control telemetry/provider state or a thermal safety handoff returns ownership to OEM firmware.

Manual fan testing is temporary, restores the previous profile and falls back to firmware Auto if restoration cannot be proven. X9 raw EC stepping/calibration is shown only when the verified X9 provider path and required capabilities are actually active.

Calibration is transactional: a new mapping replaces the previous one only after all seven verified X9 states have complete, plausible tachometer evidence. Failed, cancelled or unsafe runs never persist partial calibration.

See [Cooling Design](COOLING-DESIGN.md) for the canonical cooling/calibration contract.

## Display

Where Windows exposes the capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness.

Unsupported Windows display policy is opened through supported Windows Settings surfaces rather than undocumented registry manipulation.

Runtime status uses cheap/cached paths. Slow WMI, display-capability and `powercfg` discovery is reserved for startup, explicit refresh or human-scale cache refresh rather than a fixed rapid cadence.

## Audio

Normal output, microphone and volume controls use Windows audio endpoints.

Dolby controls are provider-driven rather than Lenovo-specific. Direct controls are enabled only when the installed DAX path exposes a semantic operation ThinkControl can verify; otherwise ThinkControl may open the official Dolby Access surface where appropriate. Private profile IDs/IEQ mappings are not guessed.

## Keyboard

Hardware backlight and optional user-session effects are separate concepts. A backend must pass its read/probe contract before writes are enabled. Direct static changes and effects share serialized hardware ownership so one cannot silently overwrite/drop the other.

Other OEMs should provide their own backend behind the same keyboard capability rather than adding vendor-specific UI.

## Touchpad

The Touchpad page shows real contact points, bounded recent trails, configurable precision edge gestures, separate optional corner-launch lanes, haptic settings where Windows/provider support exists, and bounded OSD feedback.

A finger lift ends a visual trail segment. New contacts and implausibly large physical jumps do not draw fake connecting lines.

Track control prefers the active Windows media session and falls back safely where needed. Optional center Play/Pause uses a visible bounded center zone and deliberate low-travel hold/release; normal swipes still own Previous/Next. Optional top-corner launches use the exact same physical lane geometry in Core recognition and UI visualization/hit-testing.

## Battery

ThinkControl can display percentage, charging state, live/smoothed watts, remaining/full-charge Wh, health, cycle count when exposed, filtered ETA and battery temperature only when a credible battery-specific sensor/provider supplies it.

Charge/discharge history is local and bounded. Windows remains the owner of system sleep/screen/presence policy; ThinkControl links to supported Windows settings instead of duplicating undocumented policy.

## Startup and shell reliability

A dedicated painted loading surface appears before synchronous startup discovery and remains until the destination has completed a render pass. Whole-window fade tricks are not used to hide an unpainted native WPF window.

The release gate includes real Compact → Advanced → Compact shell smoke plus deterministic screenshots across minimum/normal/wide widths, themes and important unavailable/error states.

## Compatibility

ThinkControl grows support from broad to specific:

```text
Windows generic → OEM generic → product family → exact model
```

Profiles select reasonable provider candidates. Providers own implementation, readback, lifecycle and write safety. Profiles cannot authorize arbitrary low-level writes by themselves.

Unknown/unverified laptops remain capability-driven and conservative. Windows-safe features may work, read-only providers may surface real telemetry, and hardware-specific writes remain unavailable until the relevant provider/device contract is verified.

## Diagnostics and privacy

ThinkControl separates compatibility learning, crash recovery and troubleshooting diagnostics. Local crash history remains the durable source of truth. Support/report payloads use bounded allowlisted schemas and exclude serial numbers, usernames, hostnames, personal paths/content and raw touch trails.

No automatic cloud compatibility/crash upload is part of alpha.31; future telemetry/account work is tracked separately in [Release Readiness](RELEASE_READINESS.md).

## Installation and updates

Alpha.31 uses the existing small installer/bootstrap plus application payload. In-app updates obtain Setup + Payload + checksums, verify the managed files and only then perform an explicit elevation handoff. Background checks never install software or trigger UAC by themselves.

Packaging/installer CI validates payload construction, custom-location clean install, service startup/IPC, in-place update behavior and uninstall cleanup. `version.json` remains the build/release version source of truth.

## Safety boundary

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private CPU tuning calls or automatic low-level write support for unknown machines.

New low-level features require a documented provider contract, a defined safety/recovery model, narrow identity/capability gating and test/physical evidence appropriate to the risk.
