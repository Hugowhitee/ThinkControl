<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg">
    <img alt="ThinkControl" src="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg" width="430">
  </picture>

  <p>Windows controls and hardware telemetry for Lenovo laptops.</p>

  [![Windows CI](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial-555)](LICENSE)

  **[Download](https://github.com/Hugowhitee/ThinkControl/releases)** |
  **[Device support](docs/DEVICE-SUPPORT.md)** |
  **[Documentation](docs/README.md)** |
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**
</div>

## Current prerelease

ThinkControl `v0.1.0-alpha.4` is the current prerelease. The ThinkPad X9-15 Gen 1 machine types `21Q6` and `21Q7` are the verified low-level reference profile. Other Lenovo devices use capability detection and remain beta until their exact hardware providers have been validated on physical hardware.

ThinkControl is a Windows 11 x64 application targeting .NET 10. Windows 10, Windows on ARM, Linux and macOS are not supported by the current release.

## Features

| Area | Current support |
| --- | --- |
| Power | Separate Battery and Plugged-in preferences using Windows Efficiency, Balanced and Performance behavior |
| Cooling | Global Lenovo Auto, Silent, Normal and Cool behavior; custom X9 profiles use a supervised discrete 1–7 fan curve rather than fake PWM percentages |
| Fans | Real RPM from trustworthy providers, multi-fan telemetry when the platform exposes it, manual X9 levels 1–7 and optional fan characterization |
| Sensors | Read-only CPU, GPU, storage, memory, battery and other telemetry exposed by supported providers, with reusable hoverable time-series graphs |
| Audio | Official Dolby profile names Dynamic, Movie, Music, Game and Voice when Dolby Access is available; profile switching uses the installed Dolby UI rather than editing undocumented state files |
| Display | Refresh rate, automatic refresh policy, brightness and adaptive brightness through supported Windows APIs |
| Keyboard | Off, Low and High plus ThinkControl Auto, Breathing, Reactive and experimental Audio policies on supported Lenovo hardware |
| Touchpad | Precision Touchpad edge gestures with configurable zones, volume, brightness, relative media seek and additional ThinkControl actions |
| Haptics | Windows and HID-discovered haptic feedback/click-force controls when the Precision Touchpad exposes those capabilities |
| Battery | Percentage, live watts and Wh, filtered ETA, cycle count, local charge history, learned charging averages and reported health trend where available |
| System | Device identity, provider state, compatibility information, hardware recovery and privacy-safe diagnostics |

ThinkControl does not substitute guessed fan percentages, noise values, fake wattage targets or invented sensor names for unavailable hardware data.

## Interface

ThinkControl uses two surfaces.

**Compact** is a fixed 410 × 640 tray flyout for daily status and navigation. It stays anchored near the notification area and surfaces battery/charging information, CPU/fan state, quick **Auto / Silent / Normal / Cool** fan-noise profiles and concise links without becoming another crowded control panel.

**Advanced** is a resizable application window for detailed pages. Every page uses the same left content rail at normal, minimum and wide window sizes. Navigation is ordered by task flow: Home, Performance, Fans, Sensors, Battery, Display, Audio, Keyboard, Touchpad, System, Updates and Settings.

The app, installer and repository use the canonical ThinkControl v3 assets under [`assets/brand/v3`](assets/brand/v3). The custom C geometry is shared across the product and the `ontrol` suffix uses the approved optical spacing in both the SVG and WPF wordmark.

## Power and cooling

Power policy and fan behavior are intentionally separate.

- **Battery power** and **Plugged-in power** each remember their own Efficiency, Balanced or Performance preference.
- **Cooling** is global: Lenovo Auto, Silent, Normal or Cool. A Silent choice therefore remains Silent when the charger is connected or removed.
- Lenovo Auto returns ownership to firmware. Custom cooling is available only when ThinkControl has both a verified writable fan provider and a valid control-temperature sensor.
- Normal curve decisions use a smoothed canonical CPU/GPU thermal input, while high raw temperatures trigger a firmware safety handoff instead of trying to out-control Lenovo firmware.

On the verified X9 provider, direct writes remain discrete ThinkPad levels `1`–`7`. Level `0` and the `0x40` disengaged/full-speed family remain blocked.

### Fan characterization

The Fans page can characterize the verified fan levels on the real machine. ThinkControl records the RPM exposed for each real tachometer, estimates stability and can remember the first level the user considers clearly audible. Unstable steps can be avoided when a thermally safe higher stable level exists.

A second fan is never synthesized. If Windows/Lenovo exposes two tachometers, both can be shown; otherwise ThinkControl reports only the fan telemetry it can actually read.

## Sensors and graphs

`SensorHub` collects read-only hardware telemetry from LibreHardwareMonitor plus platform-specific providers. The Sensors page exposes the real hardware name, sensor name, value, type and provider source.

Cooling deliberately uses only marked canonical thermal domains. SSD, battery and unrelated board temperatures remain visible but are not averaged into CPU/GPU fan control, so a cool secondary component cannot hide a hot processor.

The shared time-series chart is used by Battery and Sensors. It provides time/value axes plus hover crosshair and nearest-value selection, and the visual-QA renderer exercises these pages at multiple fixed window sizes.

## Dolby audio

On the ThinkPad X9-15 Gen 1 Lenovo documents the Dolby profiles **Dynamic, Movie, Music, Game and Voice**. ThinkControl exposes those official names when Dolby Access is installed.

The current safe switching provider uses Windows UI Automation against the installed Dolby Access application. ThinkControl does not patch Dolby LocalState files or guess an undocumented DAX profile-number mapping. If Dolby Access is missing, ThinkControl can open its Microsoft Store listing; if the OEM Dolby backend is absent, the UI points the user toward the Lenovo audio driver instead of pretending the Store app alone supplies the processing backend.

## Touchpad gestures

Touchpad gestures run in the signed-in user session and use Windows Precision Touchpad Raw HID input. The default preset is:

| Edge | Default action |
| --- | --- |
| Left | Volume |
| Right | Brightness |
| Top | Relative media seek |
| Bottom | Off |

A gesture must begin at a configured edge. Merely moving an already-active pointer into the edge no longer causes ThinkControl to claim the cursor. Edge width, activation distance, continuation tolerance, sensitivity, direction and action remain configurable under advanced tuning.

The cursor is captured only after a candidate has moved far enough in the expected direction and is restored on completion, cancellation, timeout or shutdown. A second contact cancels an edge gesture so normal multi-touch behavior is not intentionally repurposed.

On supported Windows 11 builds ThinkControl also probes Precision Touchpad haptic capabilities. Feedback intensity and click-force settings stay visible but are enabled only when Windows/HID evidence shows the local touchpad exposes the corresponding control.

## Hardware compatibility

The normal UI runs as the signed-in user. Privileged Lenovo hardware operations are isolated in `ThinkControl.Service` and exposed through semantic named-pipe requests rather than arbitrary EC or IOCTL passthrough.

The service can run on any supported Windows laptop. Providers are evaluated independently:

- Windows display, power and battery features are not tied to the X9 profile.
- Sensor telemetry is read-only and capability discovered.
- Lenovo fan telemetry remains read-only where a compatible provider exposes it.
- Lenovo keyboard control activates only after a known provider passes its probe and readback checks.
- Direct EC fan writes remain restricted to the verified ThinkPad X9-15 Gen 1 `21Q6` and `21Q7` profile.
- ThinkControl does not use the old dual-fan EC selector `0x31` generically and does not guess an unknown X9 Fan 2 register.

Unsupported controls stay unavailable rather than executing an unverified hardware path. See [Device support](docs/DEVICE-SUPPORT.md), [Cooling design](docs/COOLING-DESIGN.md) and [Hardware safety](docs/HARDWARE-SAFETY.md).

## ThinkPad X9-15 Gen 1

The verified X9 profile currently includes:

| Capability | Implementation |
| --- | --- |
| Power mode | Windows Efficiency, Balanced and Performance preferences stored independently for battery and AC |
| Lenovo thermal policy | X9 LITSSvc power/thermal coordination on the currently active power source |
| Sensors | LibreHardwareMonitor/platform inventory with canonical CPU/GPU control temperatures when available |
| Fan RPM | X9 EC tachometer `0x84/0x85` plus read-only Lenovo/Windows fan discovery when available |
| Fan state | X9 EC register `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Custom cooling | Service-owned Silent, Normal and Cool curves using levels `1`–`7`, smoothing, hysteresis and safety handoff |
| Manual fan control | Advanced discrete levels `1` to `7` |
| Fan characterization | Per-level real RPM/stability samples and optional audible-threshold marker |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Keyboard | Lenovo driver/provider contracts with readback and installed Vantage fallback where usable |
| Touchpad | Precision Touchpad gestures; X9 geometry fallback is 135 x 80 mm if physical HID units are unavailable |
| Haptics | Windows/HID capability discovery with controls enabled only when supported locally |
| Dolby | Dynamic, Movie, Music, Game and Voice through installed Dolby Access when available |

Normal service/controller disposal attempts to return an active ThinkControl fan override to Lenovo Auto before closing low-level access.

Technical findings are recorded in [X9-15 Gen 1 research](docs/research/x9-15-gen1.md).

## Hardware setup

The installer itself is device-neutral. It installs ThinkControl, the required .NET Desktop Runtime when missing, and `ThinkControlService`.

After startup, **Hardware Setup** checks the local service and the providers relevant to the detected device. On a verified X9 `21Q6/21Q7`, ThinkControl can offer the pinned PawnIO prerequisite needed by the verified direct EC backend. Other laptops are not offered that low-level component merely because they are made by Lenovo.

Hardware Setup is also available from Settings so the service or a required provider can be repaired later without reinstalling the whole application.

## Battery history and ETA

Charging ETA waits for stable samples instead of publishing a one-sample estimate during charger negotiation. It blends recent filtered charge power with observed Wh progress and may use prior completed sessions as a bounded short-lived prior. Current measurements take over as a session develops.

The active charge graph uses a fixed time window with the newest sample arriving from the right. Completed sessions are fitted to their full duration. Hovering a time-series graph reveals the selected timestamp and value without permanently cluttering the plot.

Battery history stays local in `%LocalAppData%\ThinkControl`. Retention is bounded, corrupt files are quarantined and writes are atomic. Charge-limit controls are not faked: until a verified Lenovo battery write provider is available, ThinkControl leaves firmware charge protection untouched and can open Lenovo Vantage instead.

## Install

Download the small installer from [Releases](https://github.com/Hugowhitee/ThinkControl/releases):

```text
ThinkControl-Setup-0.1.0-alpha.4.exe
```

The installer is a web bootstrapper. It downloads the matching release payload:

```text
ThinkControl-Payload-0.1.0-alpha.4.zip
```

The payload SHA-256 is compiled into the installer and verified before extraction. Setup checks for the x64 .NET 10 Desktop Runtime, installs the ThinkControl UI and service under Program Files, registers `ThinkControlService`, creates shortcuts and can launch ThinkControl after completion.

Package CI enforces a 5 MB hard ceiling on the bootstrap installer, a 20 MB ceiling on the compressed application payload and a 65 MB ceiling on the framework-dependent installed UI plus service payload.

See [Installer](installer/README.md) and [Dependencies](docs/DEPENDENCIES.md).

## Updates

ThinkControl checks public GitHub Releases automatically after startup and periodically while running. The Updates page still provides a manual **Check now** action. Installing a newer setup stops the old hardware service before replacing the verified payload, then updates and restarts the service registration.

## Build and visual validation

The repository targets .NET 10 and WPF. Windows CI restores, builds, runs Core tests and renders real WPF snapshots. Packaging CI builds the framework-dependent UI/service payload, verifies canonical branding, creates the external payload ZIP, compiles the small web bootstrapper and performs an install, service-running and uninstall smoke test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

The visual-QA command renders the real 410 × 640 Compact flyout plus every Advanced page in deterministic normal/minimum/wide states. Pull-request CI uploads the gallery as a `ThinkControl-Visual-QA` artifact. Generated screenshots are not maintained on a separate repository branch.

A green CI run cannot prove physical Lenovo hardware behavior. RPM, EC fan control, keyboard providers, Precision Touchpad reports, Dolby UI state and haptics still require testing on the real target device.

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
- [Visual QA](docs/VISUAL-QA.md)
- [Alpha testing](docs/ALPHA-TESTING.md)
- [Release checklist](docs/RELEASE-CHECKLIST.md)

## License

ThinkControl is source-available for noncommercial use under the [PolyForm Noncommercial License 1.0.0](LICENSE). Commercial use requires separate permission.

Third-party software and artwork retain their own licenses. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

ThinkControl is an independent community project and is not affiliated with or endorsed by Lenovo.
