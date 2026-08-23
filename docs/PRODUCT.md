# Product specification

ThinkControl is a Windows utility for Lenovo laptop controls and hardware telemetry. It provides a compact tray interface for common settings and an Advanced window for less frequent controls and diagnostics.

Current release: `v0.1.0-alpha.1`.

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

### Advanced window

The Advanced window contains the following pages:

- Home
- Performance
- Fans
- Display
- Keyboard
- Battery
- System
- Updates
- Settings

It is a normal resizable WPF window. Closing the UI keeps ThinkControl available in the notification area unless the user explicitly quits the application.

## Performance

ThinkControl exposes three Windows power modes:

| ThinkControl | Windows mode |
| --- | --- |
| Quiet | Best efficiency |
| Balanced | Balanced |
| Performance | Best performance |

The current alpha uses Windows power mode APIs. Lenovo thermal-policy coordination remains separate work and is not presented as a completed feature.

## Fans

### X9 implementation

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

### Future fan control

The first alpha does not include an autonomous temperature-based fan curve. A future curve engine will need its own hysteresis, hold timing, conflict detection, sleep/resume handling and recovery rules before it can be enabled.

## Display

Where Windows exposes the required capability, ThinkControl supports:

- current and maximum refresh rate;
- automatic refresh policy;
- explicit 60 Hz selection;
- panel maximum selection;
- internal display brightness;
- adaptive brightness.

Automatic refresh can use 60 Hz on battery and the panel maximum on AC power when both modes are available.

## Keyboard

Supported hardware levels are Off, Low and High.

ThinkControl can build user-session behavior on top of those states:

- Auto
- Breathing
- Reactive
- Audio reactive, experimental

The effects are software policies over actual hardware levels. They are not presented as native firmware animation modes or a continuous 0 to 100 percent backlight API.

Low-level keyboard writes require a recognized provider and readback verification.

## Battery

When Windows and ACPI expose the required data, ThinkControl can display:

- charge percentage;
- charging or discharging state;
- live power in watts;
- filtered recent power;
- remaining and full-charge energy in Wh;
- estimated battery health;
- estimated time remaining or time to full.

Time estimates use filtered recent samples so short power spikes do not cause large changes on every refresh.

Charge-threshold control is not implemented in `v0.1.0-alpha.1`.

## Compatibility

Compatibility is evaluated per provider and capability. Device profiles help select reasonable providers to probe, but a profile does not authorize arbitrary low-level writes.

The X9 `21Q6/21Q7` profile is the current verified low-level reference. Other laptops can still use Windows-level features and supported read-only or reversible Lenovo providers when detected.

See [Device Support](DEVICE-SUPPORT.md).

## Diagnostics and privacy

ThinkControl provides bounded local diagnostics, support-bundle export and structured GitHub bug reporting. Diagnostics use an allowlisted schema and exclude unique device identifiers and personal activity data.

Automatic private diagnostics upload is not enabled in the current release.

See [Diagnostics and Privacy](DIAGNOSTICS.md).

## Installation

The release installer contains the WPF UI, Windows service and required .NET runtime. CI verifies installation, service startup and uninstall.

PawnIO is not installed automatically in the current alpha. The application remains usable without it, but the X9 EC fan backend may be unavailable until the prerequisite is installed.

See [Installer](../installer/README.md) and [Dependencies](DEPENDENCIES.md).

## Current scope limits

ThinkControl does not provide:

- arbitrary EC register editing;
- arbitrary port I/O;
- arbitrary IOCTL passthrough;
- unverified fan-off or override states;
- private Intel IPF control calls;
- custom CPU power-limit controls;
- undervolting;
- automatic low-level write support for every Lenovo model.

New low-level features require a documented provider contract and a defined safety model.

## Roadmap

Planned work after the first alpha includes:

- broader physical validation across Lenovo models;
- installer-managed, pinned PawnIO setup where required;
- additional Lenovo provider support;
- touchpad edge gestures and haptic settings;
- custom fan curves after lifecycle and recovery requirements are complete;
- additional battery and keyboard capabilities;
- continued accessibility and UI refinement.
