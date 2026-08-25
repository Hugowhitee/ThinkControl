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

## ThinkControl alpha.15

ThinkControl is a lightweight Windows 11 companion for laptop controls that are normally spread across Windows Settings, OEM utilities and monitoring tools. It has a small notification-area popup for everyday changes and a resizable Advanced window for deeper controls and telemetry.

The product is capability-driven rather than tied to one laptop brand. Windows-safe features form the generic baseline; OEM, product-family and exact-model providers can add deeper hardware support without duplicating the main UI. The ThinkPad X9-15 Gen 1 is currently the physically reviewed low-level reference device, not the architectural product boundary.

**Current prerelease:** `v0.1.0-alpha.15`  
**Reviewed low-level reference profile:** ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)  
**Platform:** Windows 11 x64 · .NET 10

Download the latest setup from **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**. For a normal install, this is the only file to download:

```text
ThinkControl-Setup-0.1.0-alpha.15.exe
```

The release also contains a versioned application payload and `SHA256SUMS.txt`. Those are updater/verification infrastructure used so ThinkControl can download and verify the complete app **before** elevation; users do not need to extract or install them manually.

## Interface

Release CI renders the real WPF interface in dark/light themes at minimum, normal and wide window sizes. The complete screenshot gallery is kept as a CI visual-QA artifact rather than adding PNG files to the public release download list. Public releases intentionally contain only Setup, the updater payload and checksums.

## Highlights

| Area | What ThinkControl provides |
| --- | --- |
| **Home** | Battery, CPU and fan overview plus quick access, notifications and support |
| **Performance** | Quiet, Balanced and Performance preferences with separate battery/AC behavior |
| **Fans** | Firmware/OEM Auto, verified manual levels and supervised cooling profiles where a writable provider passes validation |
| **Sensors** | CPU, GPU, fan and other telemetry from trustworthy detected providers |
| **Battery** | Watts, Wh, health, filtered ETA, charge/discharge sessions, `% + W` timelines, drain rate and local history |
| **Display** | Brightness, adaptive brightness, refresh rate and automatic 60 Hz / maximum switching |
| **Audio** | Windows system volume plus direct Dolby profile and Music IEQ controls where DAX exposes semantic readback |
| **Keyboard** | Hardware brightness plus Auto, Breathing, Reactive and experimental Audio effects where a verified provider is available |
| **Touchpad** | Live touch point/trail, configurable edge gestures, haptics and themed gesture pop-ups |
| **Updates** | Automatic checks, Last checked time, SHA-256 verified download and explicit one-click installer handoff |

ThinkControl does **not** invent missing RPM values, fake PWM percentages, guessed EC registers or synthetic sensor readings. Low-level controls remain visible but unavailable until their provider is actually detected and validated.

## Alpha.15 stabilization changes

Alpha.15 is a reliability and performance pass over alpha.14.1. The focus is on fixing regressions and reducing background work before expanding the product further.

- The privileged hardware service creates its named-pipe endpoint before slow provider discovery and keeps a cached hardware snapshot, so the UI can distinguish a running Windows service from a working app connection.
- Service repair now verifies the same named-pipe `Ping` protocol the app uses instead of treating SCM `Running` as sufficient proof.
- LibreHardwareMonitor uses its PawnIO-backed provider path with PawnIO 2.2.0 as the minimum compatible low-level component. Installation, driver/device accessibility, provider readiness and actual telemetry remain separate states.
- The always-on runtime scheduler uses the Windows power manager and cheap display APIs instead of repeatedly running battery WMI, `powercfg` and full display discovery.
- Sensor discovery avoids unnecessary storage, battery, network, controller and PSU providers in the always-on path and uses bounded retry/recycle behavior rather than repeatedly hammering failed providers.
- On the verified X9 profile, real LibreHardwareMonitor/PawnIO fan telemetry is preferred when available. Direct EC tachometer and read-only thermal access are conservative fallbacks, and periodic EC control-register probing was removed from the normal status loop.
- Manual X9 fan writes remain restricted to `21Q6` / `21Q7`, verified by readback and returned to firmware/OEM Auto on supported failure/disposal paths.
- Lenovo keyboard control requires provider readback and failed probes are backed off rather than retried every status cycle.
- Hardware Setup and Notifications now surface root causes instead of multiplying one failed dependency into several identical repair actions.
- Update checking, Home and the Updates page share one release state. A user-triggered update downloads Setup + Payload + checksums, verifies SHA-256, shows the Windows elevation handoff and keeps update controls locked while the installer owns the swap.
- Automatic update checks never install or open UAC by themselves.
- Touchpad media seeking uses smaller per-frame deltas and coalesced GSMTC writes; browser sessions update more responsively while Spotify/Apple Music keep a conservative cadence.
- Gesture OSD placement, Home quick controls and hardware/sensor state propagation were tightened so unavailable states stay explicit rather than looking blank or stale.
- `version.json` is now the build version source of truth for normal builds as well as packaging, preventing stale hard-coded app versions from appearing in the UI.
- Windows CI builds with zero compiler warnings, runs the core test suite and renders the complete 52-snapshot visual-QA matrix before packaging.

## Multi-device architecture

ThinkControl grows device support from broad to specific:

`Windows generic → OEM generic → product family → exact model`

Profiles select reasonable providers to probe; provider code owns implementation, readback, lifecycle and write safety. The main UI remains organized around capabilities rather than brands. This allows future OEM families to be added without turning ThinkControl into separate Lenovo/ASUS/Dell/etc. applications.

See [Device support](docs/DEVICE-SUPPORT.md) and the [device-profile hierarchy](devices/README.md).

## Updates

ThinkControl checks GitHub Releases shortly after startup and periodically afterwards. **Background checks never open UAC and never install anything by themselves.**

When an update is available, clicking **Install update** performs this flow:

`Downloading Setup + Payload + checksums → Verifying SHA-256 → Approve Windows prompt → Installing → Relaunch`

Both the small setup executable and application payload are downloaded **before** elevation and verified against `SHA256SUMS.txt`. ThinkControl stays open while files are downloaded and verified. If the Windows administrator prompt is cancelled or setup fails before handoff, the running installation remains usable and the updater does not loop prompts on startup.

The setup detects an existing installation, skips first-install shortcut questions in update mode, closes the running tray/UI instance only when replacement is ready, updates the hardware service and relaunches ThinkControl when installation completes.

## Touchpad

The Touchpad page shows the real contact point with a red marker and fading trail. Edge areas brighten on hover and turn red only when selected. Default actions are Volume on the left edge, Brightness on the right and Media seek on the top edge.

Slow movement stays precise while faster movement accelerates within bounded limits. Volume and brightness writes are coalesced rather than fanning one OS call out per touch frame. Media seek uses an accumulated target and a bounded write cadence so slow movement can remain precise without flooding Spotify or browser media sessions.

## Hardware recovery

ThinkControl separates normal Windows features from privileged/provider-specific hardware access. Hardware Setup distinguishes the layers rather than collapsing everything into one online/offline flag:

1. **ThinkControl hardware service** — Windows service state plus the app-to-service IPC connection.
2. **Low-level/sensor access** — required driver/device access and sensor-provider readiness, such as PawnIO/LHM where applicable.
3. **Model-specific provider** — exact-model telemetry/readback validation before a low-level write capability is enabled.
4. **OEM platform integration** — keyboard, thermal-policy or other OEM provider availability and readback.

If a required component is missing, controls remain visible and ThinkControl explains what is unavailable instead of leaving them silently disabled. The notification inbox prioritizes prerequisite/root-cause repairs before downstream provider retries.

The X9-15 Gen 1 (`21Q6` / `21Q7`) remains the reviewed low-level reference profile. Unknown direct fan writes stay blocked, and manual fan ownership returns to firmware/OEM Auto on supported disposal paths.

## Audio, battery and reset behavior

Audio includes a live Windows system-volume slider. Where Dolby DAX exposes direct semantic control, ThinkControl can switch supported main profiles and Music Intelligent Equalizer options without opening Dolby Access. Unsupported DAX builds remain explicit rather than falling back to guessed profile IDs.

Battery history stores compact local charge and discharge sessions with duration, Wh, average/peak power and percentage-per-hour statistics. Session views use separate aligned battery-level (%) and power (W) graphs instead of a cluttered dual Y axis. Battery temperature is only shown when the real battery driver exposes it.

Individual changed sliders expose a small reset-to-default action. Page-level defaults use a small **Defaults** action; **Reset all** is separated in Settings and requires confirmation. Relevant controls also include a subtle **Open Windows settings** link when Windows owns the same setting.

## Help improve device support

On unverified laptops, ThinkControl can prepare a hardware-only device report using:

`Detect → Compare → New data → Review → Share`

Sharing only becomes useful after new provider/capability information is found. Reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs; nothing is silently uploaded.

## Development and safety

Windows CI builds the solution, runs tests and renders the WPF visual-QA matrix. Packaging CI publishes framework-dependent UI/service payloads, builds the small web bootstrapper and performs a real silent **install → service Running → uninstall → cleanup** lifecycle test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Automated CI cannot prove physical-device behavior. Fan ownership/RPM, OEM keyboard readback, touchpad HID behavior, Dolby DAX behavior and haptic response still require validation on the actual target hardware before that exact model/provider is treated as fully proven.

See [Device support](docs/DEVICE-SUPPORT.md), [Hardware safety](docs/HARDWARE-SAFETY.md), [Diagnostics & privacy](docs/DIAGNOSTICS.md) and [X9-15 research](docs/research/x9-15-gen1.md).
