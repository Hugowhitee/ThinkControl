<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg">
    <img alt="ThinkControl" src="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg" width="430">
  </picture>

  <p><strong>A compact ThinkPad-first Windows control app for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry.</strong></p>

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

## ThinkControl alpha.23

ThinkControl is a lightweight Windows 10/11 companion focused first on Lenovo and ThinkPad laptops. It brings controls normally spread across Windows Settings, Lenovo utilities and monitoring tools into a compact popup and a resizable full view.

**Current prerelease:** `v0.1.0-alpha.23`  
**Verified low-level reference:** ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)  
**Platform:** Windows 10 version 2004 (build 19041) or newer, x64 · .NET 10

Windows-safe controls can work on more systems, but direct EC/fan and OEM controls remain capability-gated. ThinkControl does not invent RPM values, PWM percentages, sensor readings or guessed hardware registers.

## Install

Download the newest setup from **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**:

```text
ThinkControl-Setup-0.1.0-alpha.23.exe
```

For a normal install, Setup is the only file you need. Public prereleases also include the updater payload, SHA-256 checksums and a compact `ui-overview.png` so the current interface is visible directly on the release page. The complete dark/light, minimum/normal/wide screenshot matrix remains a CI visual-QA artifact rather than a long public screenshot list.

Updates are explicit: ThinkControl downloads Setup + Payload + checksums, verifies SHA-256, then asks Windows for elevation. Background update checks never install or open UAC by themselves.

## Interface

ThinkControl has two surfaces:

- **Compact view** — quick telemetry and the controls you change most often.
- **Full view** — Home, Performance, Fans, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings.

The full sidebar keeps branding and utility actions separate from page navigation. Compact and full view use the same Google Material Symbols-based icon language and can be switched without restarting ThinkControl.

Release CI renders the real WPF interface in dark and light themes at minimum, normal and wide window sizes. It also runs a real Compact → Full → Compact transition smoke so shell regressions are not hidden by static screenshots.

## Highlights

| Area | What ThinkControl provides |
| --- | --- |
| **Home** | Dense live overview for Battery, CPU, fan mode/RPM, power and sensors plus quick controls |
| **Performance** | Efficiency, Balanced and Performance behavior with Windows power integration |
| **Fans** | Lenovo/OEM Auto, verified manual levels and supervised cooling profiles where a writable provider passes validation |
| **Battery** | Watts, Wh, health, ETA, temperature when genuinely exposed, local charge/discharge history and a direct Screen & sleep / Power & battery shortcut |
| **Display** | Brightness, adaptive brightness, refresh rate, automatic refresh switching and Windows Night light access |
| **Audio** | Windows output/microphone control plus semantic Dolby controls where the installed DAX provider safely exposes them |
| **Keyboard** | Hardware brightness and supported effects where a verified Lenovo provider is available |
| **Touchpad** | Live touch visualization, configurable edge gestures, haptics and lightweight gesture feedback |
| **System** | Device/provider state, required-component repair flow, diagnostics and detailed sensor telemetry |
| **Updates** | Shared update state, SHA-256 verification and explicit installer handoff |

## Alpha.23 reliability and UI pass

Alpha.23 is primarily a shell/runtime regression fix on top of the larger alpha.22 interface pass.

- The dedicated ThinkControl loading window is painted **before** slow WMI/device preflight and remains visible until the real destination has rendered. A large blank/black app surface is no longer an accepted loading state.
- Compact ↔ Full view switching has one transition owner. The destination is painted before the previous surface is hidden, and whole-window opacity fades were removed from this path.
- CI now exercises the real Compact → Full → Compact route repeatedly in addition to rendering static screenshots.
- Compact/full actions use inward/outward diagonal-arrow glyphs rather than the misleading sidebar-layout icon.
- Touchpad trails are segmented by real contact lifetimes and large physical jumps, so lifting a finger and touching elsewhere never draws a straight line across the pad.
- During continuous edge gestures, direction is emphasized with `+` / `−`; after release the final value remains briefly and fades. New input immediately clears old feedback.
- Previous/next media feedback reports the actual action instead of a generic `Triggered` label.
- Touchpad sensitivity uses the official Google Material Symbols `tune` glyph and reset actions align with their setting value instead of shifting slider geometry.
- Battery includes a direct shortcut to Windows **Power & battery → Screen & sleep**, including Windows-owned presence-sensing options where the device supports them.
- Public docs and release-asset descriptions are synchronized with the current product instead of referring to alpha.19-era UI.

## Hardware safety

ThinkControl grows support from broad to specific:

`Windows generic → OEM generic → product family → exact model`

Profiles decide which providers are reasonable to probe. Providers own implementation, readback, lifecycle and write safety. Low-level controls remain visible but unavailable until the exact capability is detected and validated.

On the verified X9-15 profile:

- direct fan writes stay restricted to reviewed machine types;
- writes are read back and supervised;
- failed/disposed manual ownership returns to Lenovo/OEM Auto where supported;
- real provider telemetry is preferred over fallback probes;
- sensor/provider failure is reported explicitly rather than replaced by synthetic values.

See **[Device support](docs/DEVICE-SUPPORT.md)** and **[Hardware safety](docs/HARDWARE-SAFETY.md)**.

## Touchpad

The Touchpad page visualizes the live contact point and recent movement. Default edge actions are designed around quick one-finger controls such as Volume, Brightness and Media seek.

Continuous actions are bounded and coalesced rather than issuing an OS write for every raw touch frame. Media seeking uses an accumulated target and controlled write cadence so slow movement can stay precise without flooding Spotify or browser media sessions.

The visual trail represents actual continuous contact. A new contact starts a new segment; an implausibly large physical jump also breaks the segment instead of drawing a fake connecting line.

## Battery and Windows power controls

Battery history is stored locally and grouped into compact day/session summaries. Session details keep battery level (%) and power (W) as separate aligned graphs rather than inventing a dual-axis relationship.

Windows remains the owner of system-level screen/sleep and presence-sensing policy. ThinkControl links directly to the Windows **Power & battery** page for those controls instead of reproducing undocumented registry behavior.

## Audio and Dolby

ThinkControl always keeps the normal Windows audio path usable. Direct Dolby controls are enabled only when the installed DAX provider exposes a semantic interface ThinkControl can verify. On compatible OEM DAX3 systems, the bounded Dolby Access bridge can open the official app for a requested profile action and close it only when ThinkControl launched it.

Unsupported DAX builds remain explicit; ThinkControl does not guess private Dolby profile IDs.

## Diagnostics and privacy

On unverified laptops, ThinkControl can prepare a hardware-only compatibility report. Reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs. Nothing is uploaded automatically; sharing to GitHub remains an explicit user action.

See **[Diagnostics & privacy](docs/DIAGNOSTICS.md)**.

## Development

Windows CI restores and builds the solution, runs the core tests, executes the real shell-transition smoke and renders the complete WPF visual-QA matrix before packaging.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Packaging and installer workflows additionally validate payload construction, clean install, service start/IPC, in-place update behavior and uninstall cleanup before a prerelease is promoted.

Automated CI cannot prove physical-device behavior. Fan ownership/RPM, OEM keyboard readback, touchpad HID behavior, Dolby DAX behavior and haptic response still require validation on the actual target hardware before that exact driver/firmware combination is treated as physically confirmed.

See **[Documentation](docs/README.md)** · **[Device support](docs/DEVICE-SUPPORT.md)** · **[X9-15 research](docs/research/x9-15-gen1.md)**.
