<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg">
    <img alt="ThinkControl" src="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg" width="430">
  </picture>

  <p><strong>A compact Windows laptop control app for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry.</strong></p>

  [![Windows CI](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-source--available-555)](LICENSE)

  **[Download](https://github.com/Hugowhitee/ThinkControl/releases)** ·
  **[Device support](docs/DEVICE-SUPPORT.md)** ·
  **[Documentation](docs/README.md)** ·
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**

  <br><br>
  <a href="https://buymeacoffee.com/hugowhite">
    <img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me a Coffee" height="44">
  </a>
</div>

## ThinkControl

ThinkControl is a lightweight Windows 10/11 companion that starts with verified Lenovo/ThinkPad hardware support while keeping Windows-generic capabilities and provider contracts usable across other laptops. It combines controls normally spread across Windows Settings, OEM utilities and monitoring tools into a fast Compact view and a resizable Advanced view.

**Current immutable prerelease:** `v0.1.0-alpha.58`

**Verified low-level reference:** ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)  
**Platform:** Windows 10 version 2004 (build 19041) or newer, x64 · .NET 10

Windows-safe controls can work on more systems, while direct EC/fan and OEM controls stay capability-gated. ThinkControl does not invent RPM values, PWM percentages, sensor readings or guessed hardware registers/IOCTLs. A model name is never the product-wide feature contract: generic UI consumes capabilities exposed by the active provider, while model-specific write logic remains isolated behind reviewed hardware providers.

## Interface

<p align="center">
  <a href="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.57/ui-overview.png">
    <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.57/ui-overview.png" alt="ThinkControl interface overview" width="920">
  </a>
</p>
<p align="center"><sub>Latest published interface overview. The complete dark/light, minimum/normal/wide matrix is generated again for every candidate.</sub></p>

ThinkControl has two primary surfaces:

- **Compact view** — quick telemetry and the controls you change most often.
- **Advanced view** — Home, Performance, Fans, Battery, Display, Audio, Keyboard, Touchpad, Modes, System, Updates and Settings.

The release pipeline renders the real WPF interface across dark/light themes and multiple viewport sizes. It also runs real Compact ↔ Advanced lifecycle smoke tests so shell regressions are not hidden by static screenshots.

## Install

Download the newest setup from **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**. The current setup is:

```text
ThinkControl-Setup-<version>.exe
```

For a normal install, Setup is the only file you need. A clean interactive install lets you choose the install location. Updates preserve the existing location automatically. Each public prerelease also includes the updater payload, `SHA256SUMS.txt` and `ui-overview.png`.

Updates are explicit: ThinkControl downloads Setup + Payload + checksums, verifies SHA-256, then asks Windows for elevation. Background checks never install software or open UAC by themselves.

## Main capabilities

| Area | What ThinkControl provides |
| --- | --- |
| **Home** | Live Battery, CPU, fan/RPM, power and sensor overview plus quick controls |
| **Performance** | Separate battery and plugged-in preferences using Windows power integration |
| **Fans** | Firmware/OEM Auto, Quiet and Balanced plus exact-capability-gated X9 Max cooling; custom curves and bounded direct tests only where a physically accepted direct writer exists |
| **Battery** | Watts, Wh, health, ETA, cycle telemetry, compact history, retention management and capability-gated charge-preservation thresholds |
| **Display** | Brightness, adaptive brightness, refresh rate, automatic refresh switching and Windows display shortcuts |
| **Audio** | Windows output/microphone control, session Audio Safety, plus semantic Dolby controls where the installed DAX provider safely exposes them |
| **Keyboard** | Verified hardware levels, firmware Auto where exposed, and bounded user-session effects only when the active provider advertises them |
| **Touchpad** | Live contact visualization, a unified six-zone edge/corner editor, haptics, optional integrated Track-center Play/Pause, action swapping and gesture feedback |
| **System** | Device/provider state, repair flow and detailed sensor telemetry |
| **Updates** | Shared update state, SHA-256 verification and explicit installer handoff |
| **Settings** | App behavior, Audio Safety details, appearance, diagnostics/privacy, battery-history retention and reset controls |

## Hardware safety and device support

ThinkControl grows support from broad to specific:

`Windows generic → OEM generic → product family → exact model`

Profiles decide which providers are reasonable to probe. Providers own implementation, readback, lifecycle and write safety. Generic UI consumes semantic capabilities rather than inferring support from a device name or provider-detail string. Low-level controls remain unavailable until the exact capability is detected and validated.

On the current X9-15 reference path:

- native Lenovo dual-fan RPM telemetry is retained when real channels are exposed;
- Quiet/Balanced use the reviewed X9 Lenovo firmware thermal-policy contract and are reasserted after startup/source/resume transitions when selected;
- Max cooling uses the known Lenovo Other Mode full-speed boolean only if this exact X9 itself returns a safe live boolean contract; otherwise the request fails explicitly rather than pretending Performance policy is literal maximum;
- the alpha.38 Lenovo Other Mode per-fan target-RPM writer remains read-only after failing physical smoothness/range acceptance;
- the native-OEM telemetry safety latch prevents silent fallback to the known-inferior seven-step EC writer;
- custom curves and manual percentages remain direct-writer features and are not faked through the firmware/full-speed semantic backend;
- raw seven-step EC behavior remains an explicit provider-specific diagnostic contract where genuinely active and validated, not the normal X9 product backend;
- charge-threshold writes are restricted to the verified X9 identity plus the installed Lenovo PWRMGRV battery configuration and a live `\\.\IBMPmDrv` device; requests are bounded to five-percent start/stop pairs and failures request rollback rather than trying another EC/ACPI path;
- real provider telemetry is preferred over fallback probes;
- calibration requires real tachometer evidence and never persists a partial failed run;
- PawnIO registration/service/device readiness is distinguished instead of collapsed into one registry check;
- sensor/provider failure is reported explicitly rather than replaced by synthetic values.

Audio Safety is Windows-generic and does not grant or alter any low-level hardware capability. Automated CI does **not** prove physical-device behavior. The current candidate still needs real-X9 confirmation of Touchpad feel, cooling persistence and the physical battery threshold behavior, plus a real Windows audio check for Silent/default-output transitions. Hosted tests can prove policy routing, threshold validation/rollback architecture, ownership bookkeeping, build and deterministic UI behavior but cannot substitute for physical audio, fan or charging evidence.

See **[Device support](docs/DEVICE-SUPPORT.md)** and **[Hardware safety](docs/HARDWARE-SAFETY.md)**.

## Diagnostics and privacy

Diagnostics are separated into compatibility learning, crash recovery and troubleshooting data. Compatibility reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs. Nothing is silently uploaded by the alpha client; opening a pre-filled GitHub report is explicit.

See **[Diagnostics & privacy](docs/DIAGNOSTICS.md)**.

## Development and validation

Windows CI runs repository-hygiene checks, restores and builds the solution, runs Core tests, executes the real shell-transition smoke and renders the WPF visual-QA matrix before packaging.

```powershell
.\tools\repository-hygiene.ps1
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Packaging and installer workflows additionally validate payload construction, custom-location clean install, service start/IPC, updater compatibility and uninstall cleanup before a prerelease is promoted. Release candidates are merged only after **CI and Package ThinkControl both pass on the exact final PR head**; UI-changing candidates also require manual inspection of that head's generated WPF artifact. Current release-gate status and remaining real-device checks are tracked in **[Release readiness](docs/RELEASE_READINESS.md)**.

See **[Documentation](docs/README.md)** · **[Product specification](docs/PRODUCT.md)** · **[Release readiness](docs/RELEASE_READINESS.md)** · **[X9-15 research](docs/research/x9-15-gen1.md)** · **[alpha.43 battery preservation research](docs/research/x9-alpha43-battery-care.md)**.