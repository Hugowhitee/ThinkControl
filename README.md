<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg">
    <img alt="ThinkControl" src="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg" width="430">
  </picture>

  <p><strong>A compact Windows laptop-control app for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry.</strong></p>

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

## ThinkControl alpha.11

ThinkControl is a lightweight Windows 11 companion for laptop controls that are normally spread across Windows Settings, Lenovo utilities and monitoring tools. It has a small notification-area popup for everyday changes and a resizable Advanced window for deeper controls and telemetry.

**Current prerelease:** `v0.1.0-alpha.11`  
**Reviewed low-level reference profile:** ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)  
**Platform:** Windows 11 x64 · .NET 10

Download the latest setup from **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**:

```text
ThinkControl-Setup-0.1.0-alpha.11.exe
```

## Interface

These previews are generated from the real WPF interface by release CI and published with alpha.11. They cover both the quick popup and the main control/telemetry surfaces rather than an older mock-up set.

<p align="center">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/compact-dark.png" alt="ThinkControl compact popup" width="25%">
  &nbsp;
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/advanced-home.png" alt="ThinkControl Home" width="71%">
</p>

<p align="center">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/advanced-touchpad.png" alt="ThinkControl Touchpad" width="48%">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/advanced-sensors.png" alt="ThinkControl Sensors" width="48%">
</p>

<p align="center">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/advanced-fans.png" alt="ThinkControl Fans" width="32%">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/advanced-audio.png" alt="ThinkControl Audio" width="32%">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/advanced-battery.png" alt="ThinkControl Battery" width="32%">
</p>

## Highlights

| Area | What ThinkControl provides |
| --- | --- |
| **Home** | Battery, CPU and fan overview plus quick access, notifications and support |
| **Performance** | Quiet, Balanced and Performance preferences with separate battery/AC behavior |
| **Fans** | Lenovo Auto, verified manual levels and supervised cooling profiles where a writable provider passes validation |
| **Sensors** | CPU, GPU, storage, memory, fan and other telemetry from trustworthy detected providers |
| **Battery** | Watts, Wh, health, filtered ETA, charge/discharge sessions, drain rate and history graphs |
| **Display** | Brightness, adaptive brightness, refresh rate and automatic 60 Hz / maximum switching |
| **Audio** | Windows system volume plus direct Dolby profile and Intelligent Equalizer controls where DAX exposes them |
| **Keyboard** | Hardware brightness plus Auto, Breathing, Reactive and experimental Audio effects where Lenovo control is available |
| **Touchpad** | Live touch point/trail, configurable edge gestures, haptics and themed gesture pop-ups |
| **Updates** | Automatic checks, Last checked time, verified in-app download and explicit one-click installer handoff |

ThinkControl does **not** invent missing RPM values, fake PWM percentages, guessed EC registers or synthetic sensor readings. Low-level controls remain visible but unavailable until their provider is actually detected and validated.

## Alpha.11 stabilization changes

Alpha.11 is a regression and hardware-reliability pass over alpha.10. The focus is on making existing features dependable and keeping failures understandable instead of layering on more speculative controls.

- Windows service state and ThinkControl IPC reachability are reported separately, so a running service no longer looks healthy when the app cannot actually reach it.
- PawnIO readiness checks distinguish installation, device access and provider/module readiness before suggesting a repair.
- The X9 fan path performs read-only EC discovery under the shared ThinkPad EC mutex, prioritizes the modern ThinkPad transport, validates RPM/readback and keeps unknown writes blocked.
- Fan telemetry prefers the verified X9 tachometer route over generic fallback sources; manual levels remain discrete and Lenovo Auto remains the recovery owner.
- LibreHardwareMonitor/PawnIO sensor discovery uses bounded retry/recycle behavior and selects real CPU/GPU temperature domains without relabelling generic ACPI thermal zones as CPU temperature.
- Lenovo keyboard control requires provider readback and backs off failed probes instead of repeatedly sending uncertain calls.
- Hardware Setup and Notifications now expose concrete repair states for service, PawnIO, sensors, fans and keyboard providers.
- Touchpad continuous controls coalesce volume/brightness writes, media seeking rejects stale asynchronous targets and the minimum-width layout is covered by deterministic visual QA.
- The visual-QA matrix now renders every Advanced page at normal, minimum and wide sizes plus Hardware Setup, Notifications and telemetry-detail states in the relevant themes.

## Updates

ThinkControl checks GitHub Releases shortly after startup and periodically afterwards. **Background checks never open UAC and never install anything by themselves.**

When an update is available, clicking **Install update** performs this flow:

`Downloading setup + payload → Verifying SHA-256 → Approve Windows prompt → Installing → Relaunch`

Both the small setup executable and application payload are downloaded **before** elevation and verified against `SHA256SUMS.txt`. ThinkControl stays open while the files are downloaded and verified. If the Windows administrator prompt is cancelled or setup fails, the app remains usable and does not automatically reopen the installer on the next startup.

The setup detects an existing installation, skips first-install shortcut questions in update mode, closes the running tray/UI instance only when replacement is ready, updates the hardware service and relaunches ThinkControl when installation completes.

## Touchpad

The Touchpad page shows the real contact point with a red marker and fading trail. Edge areas brighten on hover and turn red only when selected. Default actions are Volume on the left edge, Brightness on the right and Media seek on the top edge.

Slow movement stays precise while faster movement changes the value more aggressively. During a gesture ThinkControl shows useful feedback such as `Volume · 58% · +5%`, `Brightness · 72% · −3%` or `Media seek · +18.4 s`. Haptic feedback uses discrete levels and click sensitivity is presented **Light → Medium → Firm**.

## Hardware recovery

ThinkControl separates normal Windows features from privileged hardware access. Hardware Setup checks these layers independently:

1. **ThinkControl hardware service** — Windows service state plus the app-to-service connection.
2. **PawnIO / sensor access** — driver presence, usable device access and LibreHardwareMonitor provider readiness.
3. **ThinkPad X9 EC** — read-only discovery and telemetry validation before any profile-gated manual fan write is enabled.
4. **Lenovo platform integration** — keyboard provider availability and readback.

If a required component is missing, controls remain visible and ThinkControl explains what is unavailable instead of leaving them silently disabled. The notification inbox can take you directly to the relevant repair or retry action.

The X9-15 Gen 1 (`21Q6` / `21Q7`) remains the reviewed low-level reference profile. Unknown direct fan writes stay blocked, and manual fan ownership returns to Lenovo Auto on supported disposal paths.

## Audio, battery and reset behavior

Audio includes a live Windows system-volume slider. Where Dolby DAX exposes direct control, ThinkControl can switch supported main profiles and Intelligent Equalizer options without opening Dolby Access. ThinkControl does not synthesize Dolby options that the installed DAX provider does not expose.

Battery history stores compact local charge and discharge sessions with duration, Wh, power and percentage-per-hour statistics. A reusable telemetry detail sheet shows session graphs and metrics.

Individual changed sliders expose a small reset-to-default action. Page-level defaults use a small **Defaults** action; **Reset all** is separated in Settings and requires confirmation. Relevant controls also include a subtle **Open Windows settings** link when Windows owns the same setting.

## Help improve device support

On unverified laptops, ThinkControl can prepare a hardware-only device report using:

`Detect → Compare → New data → Review → Share`

Sharing only becomes useful after new provider/capability information is found. Reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs; nothing is silently uploaded.

## Development and safety

Windows CI builds the solution, runs Core tests and renders the WPF visual-QA matrix. Packaging CI publishes UI/service payloads, builds the web bootstrapper and performs a real silent **install → service Running → uninstall → cleanup** lifecycle test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Automated CI cannot prove physical-device behavior. Fan ownership/RPM, Lenovo keyboard readback, touchpad HID behavior, Dolby DAX behavior and haptic response still require validation on the actual target laptop before support is treated as fully proven.

See [Device support](docs/DEVICE-SUPPORT.md), [Hardware safety](docs/HARDWARE-SAFETY.md), [Lenovo providers](docs/LENOVO-PROVIDERS.md), [Diagnostics & privacy](docs/DIAGNOSTICS.md) and [X9-15 research](docs/research/x9-15-gen1.md).
