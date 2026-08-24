<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg">
    <img alt="ThinkControl" src="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg" width="430">
  </picture>

  <p>Clean Windows controls, hardware telemetry and laptop tuning from one tray app.</p>

  [![Windows CI](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-source--available-555)](LICENSE)
  [![Buy me a coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-support-FFDD00)](https://buymeacoffee.com/hugowhite)

  **[Download](https://github.com/Hugowhitee/ThinkControl/releases)** ·
  **[Device support](docs/DEVICE-SUPPORT.md)** ·
  **[Documentation](docs/README.md)** ·
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**

  <p><strong>☕ Support ThinkControl</strong> · <a href="https://buymeacoffee.com/hugowhite">Buy me a coffee</a> — help fund testing on more laptops.</p>
</div>

## Current prerelease

ThinkControl `v0.1.0-alpha.6` is the current prerelease. Windows-owned controls stay device-neutral where possible; low-level fan, EC and keyboard writes remain capability-gated and only become available after the relevant provider/profile checks pass.

The ThinkPad X9-15 Gen 1 machine types `21Q6` and `21Q7` are the current physically verified low-level reference profile. ThinkControl targets Windows 11 x64 and .NET 10.

## Interface

ThinkControl has a compact notification-area surface for everyday controls and a resizable Advanced window for detailed tuning and telemetry. These are real WPF snapshots produced by the same renderer used in CI and stored with the repository, so the README does not depend on mutable release images.

<p align="center">
  <img src="docs/screenshots/compact-dark.png" alt="ThinkControl compact flyout" width="25%">
  &nbsp;
  <img src="docs/screenshots/advanced-home.png" alt="ThinkControl Home" width="71%">
</p>

<p align="center">
  <img src="docs/screenshots/advanced-touchpad.png" alt="ThinkControl Touchpad" width="32%">
  <img src="docs/screenshots/advanced-audio.png" alt="ThinkControl Audio" width="32%">
  <img src="docs/screenshots/advanced-battery.png" alt="ThinkControl Battery" width="32%">
</p>

The UI uses one shared theme and interaction system for pages, menus, sliders, gesture pop-ups, hover states and subtle press/transition motion. Pages return to the top when reopened, and responsive Advanced layouts stack before controls can run outside the usable width.

## Highlights

| Area | Current support |
| --- | --- |
| Home | Minimal overview with battery, CPU, fan telemetry, quick access to newer features, notifications and a compact project-support card |
| Power | Separate Battery and Plugged-in Efficiency, Balanced and Performance preferences |
| Cooling | Lenovo Auto plus supervised Silent, Normal and Cool curves on verified writable providers |
| Fans | Real RPM from available firmware/Windows/LibreHardwareMonitor/PawnIO providers; no invented telemetry |
| Sensors | Read-only CPU, GPU, storage, memory, battery, fan and other telemetry exposed by detected providers |
| Display | Refresh rate, Auto refresh, brightness and adaptive brightness through supported Windows APIs |
| Keyboard | Off, Low, High plus Auto, Breathing, Reactive and experimental Audio policies when a Lenovo provider passes checks |
| Audio | Windows system volume plus Dolby Dynamic, Movie, Music, Game and Voice; Balanced, Detailed, Warm and Off Intelligent Equalizer tones through direct DAX where exposed |
| Touchpad | Live red contact point + trail, configurable edge zones, speed-aware actions and live value/delta feedback |
| Haptics | Windows-style discrete feedback levels and intuitive Light → Medium → Firm click sensitivity |
| Battery | Percentage, watts, Wh, filtered ETA, health, charge/discharge history, session detail graphs and local drain-rate learning |
| Updates | Automatic release checks, persistent last-checked time, loading state, SHA-256 verified download and in-place update |
| Hardware setup | Service repair, PawnIO recovery, Lenovo driver guidance, provider re-detection and hardware-only support reports |

ThinkControl deliberately does **not** substitute fake PWM percentages, invented sensors, guessed fan registers or synthetic hardware values when a provider is unavailable.

## Touchpad

A red point follows the real Precision Touchpad contact position and a fading trail makes movement easy to understand while configuring zones. Touchpad regions brighten on hover and turn red only when selected.

| Edge | Default action |
| --- | --- |
| Left | Volume |
| Right | Brightness |
| Top | Media seek |
| Bottom | Off |

Available actions include volume, brightness, media seek, previous/next track, play/pause, mute, Task View, Show Desktop, keyboard backlight and performance mode.

Media seek coalesces target updates instead of sending one seek for every input frame. Slow movement remains precise; fast or long swipes progressively increase distance; the final target is flushed when the gesture ends. This avoids stacking overlapping requests in players such as Spotify.

During a gesture ThinkControl shows useful values such as `Volume · 58% · +5%`, `Brightness · 72% · -3%` or `Media seek · +18.4 s`. Configuration sliders show their current value and signed difference from default, and a small reset icon appears only when a slider differs from its default.

The gesture OSD follows the ThinkControl theme, supports light transparency and left/center/right placement above the Windows work area, and exposes interactive volume/brightness controls. Haptic feedback uses discrete Windows-like levels rather than misleading free percentages. Click sensitivity is presented **Light → Medium → Firm** from left to right while preserving the native Windows sensitivity value underneath.

## Hardware compatibility and recovery

The normal UI runs as the signed-in user. Privileged operations are isolated in `ThinkControl.Service` and exposed through semantic named-pipe commands rather than arbitrary EC/IOCTL passthrough.

Provider discovery is separated so a slow first hardware scan cannot make a running Windows service appear offline. The UI receives a fast service state while LibreHardwareMonitor/PawnIO/EC/keyboard discovery continues independently.

- Windows display, power, audio and battery features remain available independently of low-level providers.
- LibreHardwareMonitor/PawnIO sensor discovery is read-only and can recycle after a driver becomes available.
- Fan RPM can come from any trustworthy discovered telemetry provider.
- Lenovo keyboard control activates only after a known provider contract responds correctly.
- Direct EC fan writes remain restricted to reviewed device profiles.

Hardware Setup shows the ThinkControl service, **PawnIO hardware access** and Lenovo platform integration separately, with the relevant repair/install/retry action. PawnIO is treated as ready only when its driver/device and the LibreHardwareMonitor EC path are actually reachable.

For an unverified laptop, ThinkControl can prepare a **hardware-only device support report**. The flow explains `Detect → Compare → New data → Review → Share`, and sharing becomes useful only after new provider/capability information has actually been found. Reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs; nothing is silently uploaded.

See [Device support](docs/DEVICE-SUPPORT.md), [Cooling design](docs/COOLING-DESIGN.md) and [Hardware safety](docs/HARDWARE-SAFETY.md).

## ThinkPad X9-15 Gen 1

| Capability | Implementation |
| --- | --- |
| Power | Windows Efficiency, Balanced and Performance preferences stored independently for battery and AC |
| Lenovo thermal policy | X9 Lenovo Intelligent Thermal Solution coordination when available |
| Sensors | LibreHardwareMonitor/platform inventory with PawnIO recovery when required |
| Fan RPM | X9 EC tachometer plus read-only Lenovo/Windows/LHM fan discovery when available |
| Fan state | Verified X9 EC control register |
| Lenovo Auto | Firmware ownership with readback |
| Custom cooling | Silent, Normal and Cool using discrete ThinkPad levels 1–7, smoothing, hysteresis and safety handoff |
| Manual fan control | Advanced discrete levels 1–7 |
| Keyboard | Lenovo PM/EnergyDrv/provider contracts with readback and Lenovo Vantage maintenance guidance |
| Touchpad | Precision Touchpad visualization, edge gestures and X9 geometry fallback when HID physical units are missing |
| Haptics | Windows/HID capability discovery with discrete feedback and click-sensitivity controls |
| Dolby | Direct DAX profile and Intelligent Equalizer tone control when exposed by the installed driver |

Normal service/controller disposal attempts to return an active ThinkControl fan override to Lenovo Auto before low-level access closes. Technical findings are recorded in [X9-15 Gen 1 research](docs/research/x9-15-gen1.md).

## Audio and Dolby

Audio includes a live Windows system-volume slider, output-device state and mute control.

ThinkControl exposes Dolby Dynamic, Movie, Music, Game and Voice. It probes the registered Dolby DAX backend first and, when direct control is exposed, switches profiles without opening Dolby Access and performs readable-state checks where possible.

The Intelligent Equalizer choices match Lenovo/Dolby behavior: **Balanced, Detailed, Warm and Off**. They are tonal choices for the selected processing mode, not unrelated FPS/Racing/RTS/RPG presets. ThinkControl tries the exposed subprofile route and DAX IEQ setter before reporting the control unavailable. Dolby Access opens only when the user explicitly chooses **Open Dolby Access**.

## Battery history

ThinkControl stores a compact local battery history instead of retaining large telemetry buffers in RAM. Charge and discharge sessions are tracked independently.

A session can include duration, start/end percentage, Wh added or used, average/peak power and percent-per-hour charge/drain. Clicking a session opens a reusable ThinkControl telemetry detail sheet with stats and a graph; the component is designed to be reused for sensor history later.

Recent discharge-rate history acts only as a bounded stabilising input for battery-life ETA. Live telemetry remains dominant, so an old heavy workload does not permanently distort the estimate.

## Updates and installer

ThinkControl checks GitHub Releases automatically. The Updates page shows when it was **last checked**; a manual check enters a visible `Checking…` state and updates the timestamp only when the request completes.

When a newer release is found, ThinkControl downloads the installer to a temporary location, verifies it against `SHA256SUMS.txt`, and starts the verified setup in update mode. The normal update path does not send the user to a browser.

The installer recognises an existing installation, uses update wording, skips first-install shortcut questions, can close a running tray/UI instance, safely stops/re-registers the hardware service and can relaunch the app after an in-app update.

The setup itself stays small: the release uses a web bootstrap installer plus a separately verified application payload.

```text
ThinkControl-Setup-0.1.0-alpha.6.exe
ThinkControl-Payload-0.1.0-alpha.6.zip
SHA256SUMS.txt
```

## Reset and Windows links

Relevant Advanced pages have a small local reset action. Individual sliders expose a compact reset-to-default icon only when changed. **Reset all** lives separately in Settings as a clearly global action with confirmation rather than looking like another page reset button.

Controls also owned by Windows expose a subtle contextual **Open Windows settings** link where it is useful, such as Display and Touchpad, instead of duplicating links everywhere.

## Tray, notifications and Compact

ThinkControl is single-instance. Launching it again activates the existing process instead of creating another notification-area icon. Clicking the tray icon toggles the Compact surface open/closed, while desktop/start-menu launches restore the normal Advanced taskbar window when appropriate.

The notification-area icon is rendered from the same ThinkControl BrandMark used by the app. Advanced and Compact share a notification inbox; Advanced uses a subtle icon-only indicator in the sidebar footer rather than floating a button over Home. Hardware-attention messages lead to the relevant recovery action instead of immediately forcing Hardware Setup open.

Compact keeps quick controls concise and uses the same themed interaction system as Advanced rather than a default Windows dropdown.

## Support ThinkControl

ThinkControl is still early software and real-device reports are especially useful while support expands beyond the verified X9 reference profile. A compact visual **Buy me a coffee** card is visible on Home and a fuller support card lives in Settings. Development can also be supported at [buymeacoffee.com/hugowhite](https://buymeacoffee.com/hugowhite).

## Build and validation

Windows CI restores, builds, runs Core tests and renders real WPF snapshots at fixed Compact and Advanced sizes. Packaging CI builds the UI/service payload, verifies canonical branding, creates the small web bootstrapper and performs an actual silent **install → service Running → uninstall → cleanup** lifecycle test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Automated CI cannot prove physical laptop behavior. RPM, EC fan ownership, keyboard readback, touchpad HID reports, Dolby DAX behavior and haptic response still require real-device verification; ThinkControl reports those states instead of pretending CI validated them.

## Documentation

- [Documentation index](docs/README.md)
- [Device support](docs/DEVICE-SUPPORT.md)
- [Product specification](docs/PRODUCT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Cooling design](docs/COOLING-DESIGN.md)
- [Hardware safety](docs/HARDWARE-SAFETY.md)
- [Lenovo providers](docs/LENOVO-PROVIDERS.md)
- [Dependencies](docs/DEPENDENCIES.md)
- [Diagnostics and privacy](docs/DIAGNOSTICS.md)
- [Design system](docs/DESIGN.md)
