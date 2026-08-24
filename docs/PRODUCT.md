# Product specification

ThinkControl is a capability-driven Windows laptop-control application for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry. It provides a compact tray interface for common controls and a resizable Advanced window for deeper controls, history and diagnostics.

Current prerelease: `v0.1.0-alpha.11`.

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

- detected device name;
- CPU/control temperature and short history when available;
- fan RPM and current fan state when available;
- Quiet, Balanced and Performance selection;
- display refresh controls;
- brightness and adaptive brightness;
- keyboard backlight level when supported;
- compact battery status;
- links to detailed pages and settings.

### Advanced window

Advanced contains Home, Performance, Fans, Sensors, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings.

All pages use one shared left content rail, spacing system, theme and responsive width rules. The interface is organized by capability rather than OEM.

## Performance

Windows power mode remains the generic OS-level surface. OEM policy coordination is optional and provider-specific.

On the verified X9 profile, ThinkControl also sends the reviewed Lenovo Intelligent Cooling semantic command after the Windows power-mode change:

| ThinkControl | Windows mode | Verified X9 Lenovo policy |
| --- | --- | --- |
| Quiet | Best efficiency | AC 502 / DC 507 |
| Balanced | Balanced | AC 503 / DC 508 |
| Performance | Best performance | AC 504 / DC 509 |

This is thermal-policy coordination, not fake direct fan-RPM/PWM control. Other laptop families must use their own verified provider instead of reusing X9 commands.

## Fans and temperatures

Fan pages consume generic fan/control-temperature capabilities. The underlying provider may differ per device.

On the verified X9 profile, the service supports:

- Lenovo Auto at `0x80`;
- manual levels `1` through `7`;
- current fan state from EC register `0x2F`;
- tachometer RPM from `0x84/0x85`;
- modern ThinkPad EC transport `0x1604/0x1600` with legacy `0x66/0x62` fallback;
- duplicate-write suppression, conservative polling and readback verification;
- return to Lenovo Auto during normal shutdown/disposal when manual ownership is active.

The existing supervised cooling profiles use bounded temperature smoothing, hysteresis/downshift dwell, discrete hardware levels and firmware fallback.

LibreHardwareMonitor/PawnIO is the preferred broad sensor path where supported. On the X9, verified read-only EC thermal values can provide a conservative control-temperature fallback if LHM does not expose a usable control domain. Generic EC thermal readings are not relabelled as CPU Package temperature.

ThinkControl never invents RPM, PWM percentages or temperature values.

## Display

Where Windows exposes the required capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness.

## Audio

System volume uses the normal Windows audio endpoint.

Dolby controls are provider-driven rather than Lenovo-specific. If the installed Dolby DAX build exposes semantic profile/tone operations with acceptable readback, ThinkControl can control them directly. It does not use guessed numeric IEQ mappings and never launches Dolby Access as an implicit fallback; only the explicit Open Dolby Access action opens it.

## Keyboard

The UI models hardware backlight levels and optional ThinkControl user-session effects independently from the backend.

Current Lenovo providers include established `IBMPmDrv` and `EnergyDrv` contracts plus a validated Lenovo keyboard component fallback. Each provider requires a successful read probe before writes are enabled. Other OEMs should supply their own provider behind the same keyboard capability instead of adding vendor-specific UI.

## Battery

ThinkControl can display percentage, charge/discharge state, live and smoothed watts, remaining/full-charge Wh, health, cycle count when exposed, filtered ETA and optional battery temperature when the real battery driver reports it.

Charge and discharge history is stored locally in a bounded file. Sessions include duration, start/end percentage, Wh added/used, average/peak power and percent/hour. Battery session views show aligned percentage and power timelines rather than a cluttered dual-Y-axis chart.

Static capacity values are cached instead of polled every status tick; live rate data remains frequent enough for responsive telemetry.

Charge-threshold control remains capability/provider dependent and is not faked when the OEM interface is unavailable.

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

Alpha.11 uses a small installer/bootstrap flow plus the application payload. In-app updates download setup + payload first, verify both against `SHA256SUMS.txt`, then request one explicit elevation handoff. Background update checks never install software or open UAC on their own.

Packaging CI tests build, application payload, installer, service startup and uninstall cleanup.

## Safety boundary

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private CPU tuning calls or automatic low-level write support for unknown machines.

New low-level features require a documented provider contract, a defined safety/recovery model and the narrowest appropriate device-profile scope.
