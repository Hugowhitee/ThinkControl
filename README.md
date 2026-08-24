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

ThinkControl is a lightweight Windows 11 companion for laptop controls that are normally spread across Windows Settings, OEM utilities and monitoring tools. It has a small notification-area popup for everyday changes and a resizable Advanced window for deeper controls and telemetry.

The product is capability-driven rather than tied to one laptop brand. Windows-safe features form the generic baseline; OEM, product-family and exact-model providers can add deeper hardware support without duplicating the main UI. The ThinkPad X9-15 Gen 1 is currently the physically reviewed low-level reference device, not the architectural product boundary.

**Current prerelease:** `v0.1.0-alpha.11`  
**Reviewed low-level reference profile:** ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)  
**Platform:** Windows 11 x64 · .NET 10

Download the latest setup from **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**. For a normal install, this is the only file to download:

```text
ThinkControl-Setup-0.1.0-alpha.11.exe
```

The release also contains a versioned application payload and `SHA256SUMS.txt`. Those are updater/verification infrastructure used so ThinkControl can download and verify the complete app **before** elevation; users do not need to extract or install them manually.

## Interface

The release overview is generated from the real WPF interface by release CI. Individual normal/minimum/wide screenshots remain internal visual-QA artifacts so the public download list stays compact.

<p align="center">
  <img src="https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.11/ui-overview.png" alt="ThinkControl interface overview: Compact, Home, Touchpad, Sensors, Fans, Audio, Battery and Hardware Setup" width="100%">
</p>

## Highlights

| Area | What ThinkControl provides |
| --- | --- |
| **Home** | Battery, CPU and fan overview plus quick access, notifications and support |
| **Performance** | Quiet, Balanced and Performance preferences with separate battery/AC behavior |
| **Fans** | Firmware/OEM Auto, verified manual levels and supervised cooling profiles where a writable provider passes validation |
| **Sensors** | CPU, GPU, storage, memory, fan and other telemetry from trustworthy detected providers |
| **Battery** | Watts, Wh, health, filtered ETA, charge/discharge sessions, `% + W` timelines, drain rate and local history |
| **Display** | Brightness, adaptive brightness, refresh rate and automatic 60 Hz / maximum switching |
| **Audio** | Windows system volume plus direct Dolby profile and Music IEQ controls where DAX exposes semantic readback |
| **Keyboard** | Hardware brightness plus Auto, Breathing, Reactive and experimental Audio effects where a verified provider is available |
| **Touchpad** | Live touch point/trail, configurable edge gestures, haptics and themed gesture pop-ups |
| **Updates** | Automatic checks, Last checked time, verified in-app download and explicit one-click installer handoff |

ThinkControl does **not** invent missing RPM values, fake PWM percentages, guessed EC registers or synthetic sensor readings. Low-level controls remain visible but unavailable until their provider is actually detected and validated.

## Alpha.11 stabilization changes

Alpha.11 is a regression and hardware-reliability pass over alpha.10. The focus is on making existing features dependable and keeping failures understandable instead of layering on more speculative controls.

- Windows service state and ThinkControl IPC reachability are reported separately, so a running service no longer looks healthy when the app cannot actually reach it.
- PawnIO readiness checks distinguish installation, device access and provider/module readiness before suggesting a repair.
- The verified X9 fan path performs read-only EC discovery under the shared ThinkPad EC mutex, prioritizes the modern `0x1604/0x1600` transport, validates RPM/readback and keeps unknown writes blocked.
- Fan telemetry prefers the verified X9 tachometer route over generic fallback sources; manual levels remain discrete and firmware/OEM Auto remains the recovery owner on that profile.
- LibreHardwareMonitor/PawnIO sensor discovery uses bounded retry/recycle behavior and selects real CPU/GPU temperature domains without relabelling generic ACPI thermal zones as CPU temperature. A verified read-only X9 EC thermal fallback can provide a conservative control-temperature source without pretending it is CPU Package.
- Lenovo keyboard control requires provider readback and backs off failed probes instead of repeatedly sending uncertain calls.
- Hardware Setup and Notifications expose concrete repair states for service, low-level access, sensors, fans and keyboard providers.
- Dolby control is consolidated around direct semantic DAX operations. Guessed numeric IEQ mappings and automatic Dolby Access launching are removed.
- Battery charge/discharge history now presents aligned battery-percentage and power timelines, while static capacity polling is cached to reduce background work.
- Touchpad continuous controls coalesce volume/brightness writes, media seeking rejects stale asynchronous targets and the minimum-width layout is covered by deterministic visual QA.
- The visual-QA matrix renders every Advanced page at normal, minimum and wide sizes plus provider-unavailable, Hardware Setup, Notifications, Audio and telemetry-detail states.

## Multi-device architecture

ThinkControl grows device support from broad to specific:

`Windows generic → OEM generic → product family → exact model`

Profiles select reasonable providers to probe; provider code owns implementation, readback, lifecycle and write safety. The main UI remains organized around capabilities rather than brands. This allows future OEM families to be added without turning ThinkControl into separate Lenovo/ASUS/Dell/etc. applications.

See [Device support](docs/DEVICE-SUPPORT.md) and the [device-profile hierarchy](devices/README.md).

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

ThinkControl separates normal Windows features from privileged/provider-specific hardware access. Hardware Setup distinguishes the layers rather than collapsing everything into one online/offline flag:

1. **ThinkControl hardware service** — Windows service state plus the app-to-service connection.
2. **Low-level/sensor access** — required driver/device access and sensor-provider readiness, such as PawnIO/LHM where applicable.
3. **Model-specific provider** — exact-model telemetry/readback validation before a low-level write capability is enabled.
4. **OEM platform integration** — keyboard, thermal-policy or other OEM provider availability and readback.

If a required component is missing, controls remain visible and ThinkControl explains what is unavailable instead of leaving them silently disabled. The notification inbox can take users directly to the relevant repair or retry action.

The X9-15 Gen 1 (`21Q6` / `21Q7`) remains the reviewed low-level reference profile. Its unknown direct fan writes stay blocked, and manual fan ownership returns to firmware/OEM Auto on supported disposal paths.

## Audio, battery and reset behavior

Audio includes a live Windows system-volume slider. Where Dolby DAX exposes direct semantic control, ThinkControl can switch supported main profiles and Music Intelligent Equalizer options without opening Dolby Access. Unsupported DAX builds remain explicit rather than falling back to guessed profile IDs.

Battery history stores compact local charge and discharge sessions with duration, Wh, average/peak power and percentage-per-hour statistics. Session views use separate aligned battery-level (%) and power (W) graphs instead of a cluttered dual Y axis. Battery temperature is only shown when the real battery driver exposes it.

Individual changed sliders expose a small reset-to-default action. Page-level defaults use a small **Defaults** action; **Reset all** is separated in Settings and requires confirmation. Relevant controls also include a subtle **Open Windows settings** link when Windows owns the same setting.

## Help improve device support

On unverified laptops, ThinkControl can prepare a hardware-only device report using:

`Detect → Compare → New data → Review → Share`

Sharing only becomes useful after new provider/capability information is found. Reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs; nothing is silently uploaded.

## Development and safety

Windows CI builds the solution, runs tests and renders the WPF visual-QA matrix. Packaging CI publishes UI/service payloads, builds the web bootstrapper and performs a real silent **install → service Running → uninstall → cleanup** lifecycle test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Automated CI cannot prove physical-device behavior. Fan ownership/RPM, OEM keyboard readback, touchpad HID behavior, Dolby DAX behavior and haptic response still require validation on the actual target hardware before that exact model/provider is treated as fully proven.

See [Device support](docs/DEVICE-SUPPORT.md), [Hardware safety](docs/HARDWARE-SAFETY.md), [Diagnostics & privacy](docs/DIAGNOSTICS.md) and [X9-15 research](docs/research/x9-15-gen1.md).