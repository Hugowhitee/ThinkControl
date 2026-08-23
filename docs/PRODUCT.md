# ThinkControl product specification

ThinkControl is a lightweight Windows hardware companion for Lenovo ThinkPads. It provides fast access to everyday performance, cooling, display, keyboard and battery controls while keeping model-specific hardware access explicit and safety-gated.

This document describes the **current `v0.1.0-alpha.1` product**, followed by a separate roadmap section. The ThinkPad X9-15 Gen 1 is the reference device for the first public alpha.

## Product principles

ThinkControl is designed around five rules:

1. **Fast daily controls.** Common settings are available from a compact tray popup without opening a full OEM suite.
2. **Truthful telemetry.** Values are shown only when a real provider supplies them. Sensor names, fan states and compatibility levels are not invented.
3. **Capability-based architecture.** Support is evaluated per feature/provider rather than inferred from a Lenovo logo alone.
4. **Least privilege.** The normal UI runs as the user; privileged low-level hardware access belongs to the ThinkControl service.
5. **Safe fallback.** Known low-level operations have explicit safety rules and recovery behavior.

## Current UI

ThinkControl has two primary surfaces.

### Compact tray popup

The compact popup contains:

- detected device name;
- CPU temperature and a 60-second history when available;
- real fan RPM and current fan state when available;
- Quiet / Balanced / Performance selection;
- display refresh controls;
- brightness and adaptive brightness;
- keyboard backlight level;
- compact battery percentage, watts and ETA when available;
- direct navigation to detailed pages;
- a clear expand control for the Advanced window.

The current compact window is approximately 410 × 640 logical pixels and is rendered in CI in both dark and light themes to catch clipping/layout regressions.

### Advanced window

The Advanced window contains:

- Home;
- Performance;
- Fans;
- Display;
- Keyboard;
- Battery;
- System;
- Updates;
- Settings.

It is a normal resizable window. Closing/hiding it returns ThinkControl to the tray rather than terminating the application.

The main visual language is WPF/XAML with Material Symbols Outlined vector icons, compact spacing, thin borders and restrained ThinkPad-red accents.

## Performance profiles

ThinkControl exposes three user-facing Windows power modes:

- **Quiet** — Best efficiency;
- **Balanced** — Windows balanced mode;
- **Performance** — Best performance.

The current alpha uses the supported Windows power-mode API. It does **not** display invented dBA or wattage targets for these modes.

Lenovo LITS/thermal-policy coordination has been researched but is not yet treated as a completed production integration in `alpha.1`.

## Fan control

### Current `alpha.1` implementation

On the recognized ThinkPad X9-15 Gen 1 machine types `21Q6` and `21Q7`, the service implements:

- Lenovo Auto (`0x80`);
- discrete manual levels `1` through `7`;
- current fan state from EC register `0x2F`;
- real tachometer RPM from `0x84/0x85`;
- duplicate manual-write suppression;
- conservative tachometer polling;
- read-back verification;
- return to Lenovo Auto on normal release/service shutdown.

ThinkControl never exposes a fake 0–100% PWM slider. Fan-off `0x00` is blocked and the unverified `0x40` override family is never written.

### Not yet implemented in `alpha.1`

The following belong to the custom fan-curve roadmap and must not be described as completed behavior yet:

- temperature-driven custom fan curves;
- immediate-up / delayed-down curve logic;
- hysteresis and minimum hold timers for a curve engine;
- automatic conflict arbitration with every third-party fan controller;
- a separate crash guardian capable of restoring Auto after an ungraceful service crash.

The current alpha exposes truthful manual discrete control plus Lenovo Auto. A custom autonomous fan controller comes later.

## Display

Where Windows exposes the capability, ThinkControl provides:

- current and maximum refresh rate;
- Auto refresh behavior;
- explicit 60 Hz selection;
- panel-maximum selection;
- internal-panel brightness;
- adaptive brightness.

Auto refresh can use 60 Hz on battery and the panel maximum on AC when both modes exist.

## Keyboard

ThinkControl separates physical backlight levels from user-session effects.

Hardware levels:

- Off;
- Low;
- High.

ThinkControl policies/effects:

- Auto;
- Breathing;
- Reactive;
- Audio reactive (experimental).

For `alpha.1`, low-level keyboard access is enabled only on the recognized X9 reference machine and uses the current Lenovo PM backend with read-after-write verification. Effects are policies over real discrete levels, not claimed firmware animation modes.

## Battery

Battery telemetry uses Windows ACPI/WMI data when available and can expose:

- charge percentage;
- charging/discharging state;
- instantaneous charge/discharge power in watts;
- filtered recent average power;
- remaining/full-charge energy in Wh;
- estimated battery health;
- estimated time remaining or time to full.

ETA uses a median-filtered recent power window plus a slow-moving weighted average so short CPU/charger spikes do not make the displayed time jump continuously.

Charge-threshold control is **not implemented in `alpha.1`**.

## Compatibility model

The UI/core knows three confidence states:

- **Verified**;
- **Experimental**;
- **Not validated**.

However, the current `alpha.1` low-level fan/keyboard service is still explicitly gated to the X9 `21Q6/21Q7` profile. Unknown ThinkPads keep the same app pages and can use Windows-level capabilities, but they do **not** currently receive writable Experimental EC/keyboard providers automatically.

See [Device support](DEVICE-SUPPORT.md) for the authoritative current matrix.

## Diagnostics and privacy

The current desktop app implements:

- bounded local diagnostics;
- allowlisted fields and redaction;
- Preview data;
- Export support bundle;
- Delete local diagnostics;
- structured public GitHub bug reports.

Private automatic diagnostics upload is not enabled yet because the project-side private endpoint does not exist. No GitHub PAT is embedded in the app.

## Installation and releases

The installer pipeline is implemented and tested in GitHub Actions. It builds a self-contained x64 Windows installer containing:

- ThinkControl UI;
- ThinkControl Windows service;
- self-contained .NET runtime payload.

CI smoke-tests installation, verifies that `ThinkControlService` reaches Running, uninstalls the package and confirms the service/files are removed.

At the time this specification was updated, **no public GitHub Release had been published yet**. `v0.1.0-alpha.1` is the first planned prerelease. Development installers are currently produced as GitHub Actions artifacts.

The tagged release workflow is already able to publish:

```text
ThinkControl v0.1.0-alpha.1
ThinkControl-Setup-0.1.0-alpha.1.exe
SHA256SUMS.txt
```

PawnIO prerequisite installation is not yet part of the one-click installer, so a clean reference machine may need that verified low-level prerequisite before X9 EC fan access becomes available.

## Current release blockers

Before `v0.1.0-alpha.1` should be treated as physically validated on the X9 reference machine, complete this on-device pass:

- confirm service starts after a normal installer run;
- confirm CPU telemetry source;
- confirm RPM appears and remains stable with conservative polling;
- test Lenovo Auto and manual levels `1–7`;
- confirm closing/stopping/uninstalling returns fan ownership safely;
- test keyboard Off / Low / High;
- tune Breathing timing against Lenovo's actual fade behavior;
- test Auto and Reactive effects;
- test sleep/resume;
- visually inspect the installed build at normal Windows scaling.

The packaging/installer lifecycle itself already passes CI.

## Roadmap after the first alpha

- physically validate the X9 release build;
- add installer-managed pinned PawnIO where required;
- expand known-provider discovery to additional ThinkPads;
- add safe read-only compatibility probes before any new model-specific writes;
- enable private opt-in diagnostics submission;
- implement the custom fan-curve engine with hysteresis/hold/fail-safe logic;
- add additional verified Lenovo battery and keyboard capabilities;
- continue accessibility and UI polish.

## Scope boundaries

ThinkControl intentionally does not provide:

- arbitrary EC/register editing;
- arbitrary IOCTL passthrough;
- unverified fan-off or override states;
- private Intel IPF calls;
- custom PL1/PL2 controls;
- undervolting;
- universal all-ThinkPads hardware-write support.

Features can move into the product only when they have a supported API or a device/provider-specific implementation with a documented safety model.
