# Product specification

ThinkControl is a capability-driven Windows laptop-control application for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry. It provides a compact tray interface for common controls and a resizable full window for deeper controls, history and diagnostics.

Current prerelease: `v0.1.0-alpha.23`.

Current physically reviewed low-level reference: Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

The reference device is **not** the product boundary. ThinkControl is structured so Windows-safe features work broadly and OEM/family/model providers can be added independently for Lenovo, ASUS, Dell, HP, Acer, MSI and other laptop families.

## Product goals

1. Keep common laptop controls quick to reach.
2. Show telemetry only when a real provider supplies it.
3. Detect support per capability instead of assuming a brand or family shares one hardware interface.
4. Keep the desktop UI unprivileged and isolate low-level operations in the Windows service.
5. Fail safely when a provider is missing, unsupported or returns an unexpected state.
6. Keep the UI capability-first so adding another OEM does not require vendor-specific copies of Fans, Sensors, Battery or other pages.
7. Keep model-specific writes behind explicit identity gates and provider-owned allowlists.
8. Never expose an empty/black application surface while expensive startup discovery or view construction is in progress.

## Architecture direction

ThinkControl separates four concerns:

- **Core** — vendor-neutral capability/state contracts and product behavior.
- **UI** — capability-driven Windows interface; it should not contain hardware register knowledge.
- **Hardware/providers** — implementations for Windows and OEM interfaces, including validation/readback and safe write ownership.
- **Device profiles** — data that selects reasonable provider probes from generic → OEM → family → model scope.

See [`devices/README.md`](../devices/README.md) for the profile hierarchy.

Device profiles do not implement hardware control and cannot authorize arbitrary writes by themselves. This keeps future OEM expansion and optional provider modules independent from the product shell.

## User interface

### Compact window

The tray window contains the controls and telemetry most likely to be used during normal operation:

- three live metric slots, defaulting to Battery, CPU and Fans, with simple slot replacement rather than a heavy dashboard editor;
- current fan profile with RPM beneath it when telemetry exists;
- Efficiency, Balanced and Performance selection;
- display refresh controls;
- brightness and volume;
- keyboard backlight level when supported;
- direct links to detailed pages and settings.

Compact and full view use the same Google Material Symbols-based visual language. The view-toggle uses inward/outward diagonal arrows rather than a sidebar glyph.

### Full window

The full window contains Home, Performance, Fans, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings. Detailed sensor telemetry opens from System instead of occupying a permanent navigation page.

All pages use one shared left content rail, spacing system, theme and responsive width rules. The sidebar separates brand/utility actions from page navigation. The interface is organized by capability rather than OEM.

Compact ↔ Full switching is a single-owner transition: the destination is painted before the previous surface is hidden. Whole-window opacity animation is intentionally not used for this path.

## Performance

Windows power mode remains the generic OS-level surface. User-facing terminology is consistently **Efficiency / Balanced / Performance** even though some internal Windows/provider contracts retain older enum names.

On the verified X9 profile, ThinkControl also sends the reviewed Lenovo Intelligent Cooling semantic command after the Windows power-mode change:

| ThinkControl | Windows mode | Verified X9 Lenovo policy |
| --- | --- | --- |
| Efficiency | Best efficiency | AC 502 / DC 507 |
| Balanced | Balanced | AC 503 / DC 508 |
| Performance | Best performance | AC 504 / DC 509 |

This is thermal-policy coordination, not fake direct fan-RPM/PWM control. Other laptop families must use their own verified provider instead of reusing X9 commands.

## Fans and temperatures

Fan pages consume generic fan/control-temperature capabilities. The underlying provider may differ per device.

On the verified X9 profile, the service supports:

- Lenovo Auto at `0x80`;
- manual levels `1` through `7`;
- fan-control state validation/readback from EC register `0x2F` without continuously polling it in the normal status loop;
- tachometer RPM fallback from `0x84/0x85` when a broad hardware provider does not expose usable RPM;
- modern ThinkPad EC transport `0x1604/0x1600` with legacy `0x66/0x62` fallback;
- stale-output draining plus a bounded OBF-first / IBF-clear-compatible read-ready path for X9 EC readback;
- duplicate-write suppression, conservative fallback polling and readback verification;
- return to Lenovo Auto during normal shutdown/disposal when manual ownership is active.

Supervised cooling profiles use bounded temperature smoothing, hysteresis, minimum state dwell, slower downshift dwell, discrete hardware levels and firmware fallback. Large/hot cooling increases remain immediate while ordinary threshold noise is prevented from making the fan hunt between adjacent states.

LibreHardwareMonitor/PawnIO is the preferred broad sensor/fan path where supported. On the X9, verified read-only EC thermal values can provide a conservative control-temperature fallback if LHM does not expose a usable control domain. Generic EC thermal readings are not relabelled as CPU Package temperature.

ThinkControl never invents RPM, PWM percentages or temperature values.

## Display

Where Windows exposes the required capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness.

Night light is opened through the supported Windows Settings URI rather than undocumented CloudStore/registry manipulation.

Always-on runtime status uses cheap Windows APIs and cached capabilities. Slow WMI/`powercfg` discovery is reserved for startup, explicit refresh or human-scale cache refreshes rather than every UI status tick.

## Audio

System output and microphone controls use normal Windows audio endpoints.

Dolby controls are provider-driven rather than Lenovo-specific. If the installed Dolby DAX build exposes semantic profile/tone operations with acceptable readback, ThinkControl controls them directly. On compatible OEM DAX3 systems where direct semantic setters are unavailable, a bounded Dolby Access bridge may open the official app for the requested profile action and closes it only when ThinkControl launched it. ThinkControl does not guess private profile IDs or undocumented IEQ mappings.

## Keyboard

The UI models hardware backlight levels and optional ThinkControl user-session effects independently from the backend.

Current Lenovo providers include established `IBMPmDrv` and `EnergyDrv` contracts plus a validated Lenovo keyboard component fallback. Each provider requires a successful read probe before writes are enabled and failed probes are backed off. Direct static level changes wait for an in-flight effect write instead of being silently dropped. Other OEMs should supply their own provider behind the same keyboard capability instead of adding vendor-specific UI.

## Touchpad

The Touchpad page shows real contact points, bounded recent trails, edge actions and haptic settings. A finger lift always ends the current visual trail segment; a new contact or an implausibly large physical jump never draws a fake straight connecting line across the touchpad.

Continuous gesture feedback emphasizes direction while the gesture is active and briefly shows the final absolute value after release before fading. New input clears old feedback immediately. Previous/next media actions report the actual action instead of a generic `Triggered` state.

Sensitivity/reset affordances use the same Google Material Symbols language as the rest of ThinkControl and do not reserve asymmetric space beside the slider track.

## Battery and Windows power controls

ThinkControl can display percentage, charge/discharge state, live and smoothed watts, remaining/full-charge Wh, health, cycle count when exposed, filtered ETA and optional battery temperature when the real battery driver reports it.

Charge and discharge history is stored locally in a bounded file. Sessions include duration, start/end percentage, Wh added/used, average/peak power and percent/hour. Battery session views show aligned percentage and power timelines rather than a cluttered dual-Y-axis chart.

Windows remains the owner of system screen/sleep and presence-sensing policy. ThinkControl provides a direct **Power & battery / Screen & sleep** shortcut instead of duplicating undocumented registry settings.

The always-on battery path uses the Windows power manager rather than fixed-cadence battery WMI. Slow/static battery metadata remains cached and explicit. Charge-threshold control remains capability/provider dependent and is not faked when the OEM interface is unavailable.

## Startup and shell reliability

The dedicated ThinkControl loading window is painted from the earliest `Application.Startup` path, before synchronous WMI/SMBIOS preflight. It stays above the destination until the real Compact/full surface has completed a render pass.

The release gate includes a real WPF Compact → Full → Compact smoke in addition to static screenshots. A view transition must keep one real surface visible and must not terminate the app when a destination constructor/layout path fails.

## Compatibility

Windows-safe capabilities are the baseline for all laptops. OEM/family/model profiles only add provider preferences or explicitly reviewed control contracts.

The current profile hierarchy is:

```text
Windows generic capability
→ OEM generic
→ product family
→ exact model
```

The X9 `21Q6/21Q7` profile is the current verified low-level reference. Other laptops can already use compatible Windows features and safe detected providers; broader model support is expected to grow through the profile/provider architecture.

## Diagnostics and privacy

ThinkControl provides bounded local diagnostics, support-bundle export and structured bug reporting. Diagnostics use an allowlisted schema and exclude unique device identifiers and personal activity data.

## Installation and updates

Alpha.23 uses a small installer/bootstrap flow plus the application payload. In-app updates download Setup + Payload + `SHA256SUMS.txt` first, verify the managed files, then request one explicit elevation handoff. Background update checks never install software or open UAC on their own.

Public prereleases contain Setup, Payload, checksums and one composed `ui-overview.png`; the full screenshot matrix remains a CI visual-QA artifact. Packaging CI tests build, application payload, installer, service startup and uninstall cleanup. A deeper pull-request gate also validates named-pipe IPC plus an in-place reinstall/update path. `version.json` is the build and release version source of truth.

## Safety boundary

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private CPU tuning calls or automatic low-level write support for unknown machines.

New low-level features require a documented provider contract, a defined safety/recovery model and the narrowest appropriate device-profile scope.
