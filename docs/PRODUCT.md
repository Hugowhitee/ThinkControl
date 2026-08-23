# Product specification

ThinkControl is a Windows utility for Lenovo laptop controls and hardware telemetry. It provides a compact tray interface for common settings and an Advanced window for less frequent controls and diagnostics.

Current prerelease: `v0.1.0-alpha.2`.

Reference device: Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

## Product goals

1. Keep common laptop controls quick to reach.
2. Show telemetry only when a real provider supplies it.
3. Detect support per capability instead of assuming all Lenovo laptops use the same hardware interface.
4. Keep the desktop UI unprivileged and isolate low-level operations in the Windows service.
5. Fail safely when a provider is missing, unsupported or returns an unexpected state.

## User interface

### Compact window

The tray window contains the controls and telemetry most likely to be used during normal operation:

- detected device name;
- CPU temperature and short history when available;
- fan RPM and current fan state when available;
- Quiet, Balanced and Performance selection;
- display refresh controls;
- brightness and adaptive brightness;
- keyboard backlight level;
- compact battery status;
- links to detailed pages and settings.

Compact is a fixed borderless flyout anchored above the notification area. It is not draggable. A single diagonal `↖` action opens Advanced.

### Advanced window

Advanced contains Home, Performance, Fans, Display, Keyboard, Battery, System, Updates and Settings.

It uses the native Windows title bar so Windows owns the app icon, minimize, maximize/restore, close, system menu and Snap Layouts. A `↘` action returns to Compact.

## Branding

The app, tray, installer and repository wordmarks use the approved ThinkControl v3 asset pack stored under `assets/brand/v3`.

CI checks that:

- the application ICO exactly matches the canonical v3 Windows icon;
- the tray ICO exactly matches the canonical v3 mark icon;
- README dark/light wordmarks exactly match the canonical v3 outlined wordmarks;
- the old hand-drawn 64×64 TC geometry is not present in the WPF `BrandMark` control.

## Performance

ThinkControl exposes three modes:

| ThinkControl | Windows mode | Verified X9 Lenovo policy |
| --- | --- | --- |
| Quiet | Best efficiency | AC 502 / DC 507 |
| Balanced | Balanced | AC 503 / DC 508 |
| Performance | Best performance | AC 504 / DC 509 |

Windows power mode remains the supported OS-level surface. On the verified X9 `21Q6/21Q7` profile, ThinkControl also sends the observed Lenovo Intelligent Cooling policy command through `LITSSvc` after the Windows change. This is thermal-policy coordination, not direct fan RPM control.

## Fans

On the verified X9 profile, the service supports:

- Lenovo Auto at `0x80`;
- manual levels `1` through `7`;
- current fan state from EC register `0x2F`;
- tachometer RPM from `0x84/0x85`;
- duplicate-write suppression;
- conservative RPM polling;
- readback verification;
- return to Lenovo Auto during normal service shutdown when manual control is active.

ThinkControl does not expose an arbitrary PWM percentage. Fan-off `0x00` is blocked and the unverified `0x40` override family is never written.

## Display

Where Windows exposes the required capability, ThinkControl supports current/maximum refresh rate, automatic refresh policy, explicit 60 Hz selection, panel maximum selection, internal display brightness and adaptive brightness.

Automatic refresh can use 60 Hz on battery and the panel maximum on AC power when both modes are available.

## Keyboard

Supported hardware levels are Off, Low and High. ThinkControl can build Auto, Breathing, Reactive and experimental Audio behavior on top of those real states.

Low-level keyboard writes require a recognized Lenovo provider and readback verification. The installed Lenovo Vantage ThinkKeyboard component can be used as a fallback when the direct Lenovo PM/EnergyDrv contract is unavailable.

## Battery

When Windows and ACPI expose the required data, ThinkControl can display charge percentage, charging/discharging state, live and filtered power in watts, remaining/full-charge energy in Wh, estimated health and filtered time remaining/time to full.

Time estimates use recent filtered samples so short power spikes do not cause large changes on every refresh.

Charge-threshold control is not implemented in `v0.1.0-alpha.2`.

## Compatibility

Compatibility is evaluated per provider and capability. Device profiles help select reasonable providers to probe, but a profile does not authorize arbitrary low-level writes.

The X9 `21Q6/21Q7` profile is the verified low-level reference. Other laptops can still use Windows-level features and supported read-only or reversible Lenovo providers when detected.

## Diagnostics and privacy

ThinkControl provides bounded local diagnostics, support-bundle export and structured GitHub bug reporting. Diagnostics use an allowlisted schema and exclude unique device identifiers and personal activity data.

Automatic private diagnostics upload is not enabled in the current release.

## Installation

`v0.1.0-alpha.2` uses a small Inno Setup web bootstrapper plus a separate framework-dependent release payload.

The bootstrapper verifies the matching payload SHA-256 before extraction, installs the .NET 10 Desktop Runtime only when missing, and offers pinned PawnIO 2.2.0 only on the verified X9 profile. CI tests the full bootstrap install, service startup and uninstall lifecycle.

See [Installer](../installer/README.md) and [Dependencies](DEPENDENCIES.md).

## Current scope limits

ThinkControl does not provide arbitrary EC register editing, arbitrary port I/O, arbitrary IOCTL passthrough, unverified fan-off/override states, private Intel IPF control calls, custom CPU power-limit controls, undervolting or automatic low-level write support for every Lenovo model.

New low-level features require a documented provider contract and a defined safety model.
