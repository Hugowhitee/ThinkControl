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

ThinkControl `v0.1.0-alpha.3` is the current prerelease. The ThinkPad X9-15 Gen 1 machine types `21Q6` and `21Q7` are the verified low-level reference profile. Other Lenovo devices use capability detection and remain beta until their exact hardware providers have been validated on physical hardware.

ThinkControl is a Windows 11 x64 application targeting .NET 10. Windows 10, Windows on ARM, Linux and macOS are not supported by the current release.

## Preview

The images below are rendered from the real WPF interface by the same snapshot tool used in CI.

<p align="center">
  <img src="docs/images/preview/compact-dark.png" alt="ThinkControl compact tray interface" width="330">
</p>

<p align="center">
  <img src="docs/images/preview/advanced-touchpad-wide.png" alt="ThinkControl touchpad gestures page" width="820">
</p>

<p align="center">
  <img src="docs/images/preview/advanced-display-wide.png" alt="ThinkControl display page" width="820">
</p>

## Features

| Area | Current support |
| --- | --- |
| Performance | Quiet, Balanced and Performance through Windows power mode, with verified Lenovo thermal-policy coordination where available |
| Fans | Real RPM when a trustworthy provider exists; verified X9 Lenovo Auto and manual EC levels 1 to 7 |
| Display | Refresh rate, automatic refresh policy, brightness and adaptive brightness through supported Windows APIs |
| Keyboard | Off, Low and High plus ThinkControl Auto, Breathing, Reactive and experimental Audio policies on supported Lenovo hardware |
| Touchpad | Precision Touchpad edge gestures with configurable zones, volume, brightness, relative media seek and additional ThinkControl actions |
| Haptics | Windows haptic feedback strength and click-force settings when the Precision Touchpad reports those capabilities |
| Battery | Percentage, live watts and Wh, filtered ETA, cycle count, local charge history, learned charging averages and reported health trend where available |
| System | Device identity, provider state, compatibility information, hardware recovery and privacy-safe diagnostics |

ThinkControl does not substitute guessed fan percentages, noise values, fake wattage targets or invented sensor names for unavailable hardware data.

## Interface

ThinkControl uses two surfaces.

**Compact** is a fixed tray flyout for daily controls. It stays anchored near the notification area. The header opens Advanced without turning Compact into a draggable desktop window.

**Advanced** is a resizable application window for detailed pages. Its content adapts to normal and maximized window sizes while keeping controls at readable widths. Navigation and page changes use short motion when Windows animations are enabled.

The app, installer and repository use the canonical ThinkControl v3 assets under [`assets/brand/v3`](assets/brand/v3). The custom C geometry is shared across the product and the `ontrol` suffix uses the approved optical spacing in both the SVG and WPF wordmark.

## Touchpad gestures

Touchpad gestures run in the signed-in user session and use Windows Precision Touchpad Raw HID input. The default preset is:

| Edge | Default action |
| --- | --- |
| Left | Volume |
| Right | Brightness |
| Top | Relative media seek |
| Bottom | Off |

Edge width, activation distance, continuation tolerance, sensitivity, direction and action are configurable. Palm rejection uses Precision Touchpad Confidence when the device exposes it. A second contact cancels an edge gesture so normal multi-touch behavior is not intentionally repurposed.

The cursor is captured only while ThinkControl is deciding or handling an edge gesture and is restored on completion, cancellation, timeout or shutdown. The Touchpad page includes a live test view for recognition diagnostics.

On Windows 11 24H2 or newer, ThinkControl also reads the operating system's Precision Touchpad haptic capabilities. Feedback intensity and click-force settings are enabled only when Windows reports that the hardware supports them.

## Hardware compatibility

The normal UI runs as the signed-in user. Privileged Lenovo hardware operations are isolated in `ThinkControl.Service` and exposed through semantic named-pipe requests rather than arbitrary EC or IOCTL passthrough.

The service can run on any supported Windows laptop. Providers are evaluated independently:

- Windows display, power and battery features are not tied to the X9 profile.
- CPU temperature is shown only when a trustworthy sensor provider returns it.
- Lenovo fan telemetry is read-only where a compatible Lenovo provider exposes it.
- Lenovo keyboard control activates only after a known provider passes its probe and readback checks.
- Direct EC fan writes remain restricted to the verified ThinkPad X9-15 Gen 1 `21Q6` and `21Q7` profile.

Unsupported controls stay unavailable rather than executing an unverified hardware path. See [Device support](docs/DEVICE-SUPPORT.md) and [Hardware safety](docs/HARDWARE-SAFETY.md).

## ThinkPad X9-15 Gen 1

The verified X9 profile currently includes:

| Capability | Implementation |
| --- | --- |
| Power mode | Windows Best efficiency, Balanced and Best performance with readback |
| Lenovo thermal policy | X9 LITSSvc policy commands on AC and battery |
| CPU temperature | Trustworthy sensor provider when present |
| Fan RPM | X9 EC tachometer registers `0x84/0x85`, polled conservatively |
| Fan state | X9 EC register `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Discrete levels `1` to `7` |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Keyboard | Lenovo driver/provider contracts with readback and installed Vantage fallback where usable |
| Touchpad | Precision Touchpad gestures; X9 geometry fallback is 135 x 80 mm if physical HID units are unavailable |
| Haptics | Official Windows touchpad settings when supported by the installed Windows build and device |

Normal service/controller disposal attempts to return an active manual X9 fan level to Lenovo Auto before closing low-level access.

Technical findings are recorded in [X9-15 Gen 1 research](docs/research/x9-15-gen1.md).

## Hardware setup

The installer itself is device-neutral. It installs ThinkControl, the required .NET Desktop Runtime when missing, and `ThinkControlService`.

After startup, **Hardware Setup** checks the local service and the providers relevant to the detected device. On a verified X9 `21Q6/21Q7`, ThinkControl can offer the pinned PawnIO prerequisite needed by the verified direct EC backend. Other laptops are not offered that low-level component merely because they are made by Lenovo.

Hardware Setup is also available from Settings so the service or a required provider can be repaired later without reinstalling the whole application.

## Battery history and ETA

Charging ETA waits for stable samples instead of publishing a one-sample estimate during charger negotiation. It blends recent filtered charge power with observed Wh progress and may use prior completed sessions as a bounded short-lived prior. Current measurements take over as a session develops.

Battery history stays local in `%LocalAppData%\ThinkControl`. Retention is bounded, corrupt files are quarantined and writes are atomic. Charge-limit controls are not faked: until a verified Lenovo battery write provider is available, ThinkControl leaves firmware charge protection untouched and can open Commercial Vantage instead.

## Install

Download the small installer from [Releases](https://github.com/Hugowhitee/ThinkControl/releases):

```text
ThinkControl-Setup-0.1.0-alpha.3.exe
```

The installer is a web bootstrapper. It downloads the matching release payload:

```text
ThinkControl-Payload-0.1.0-alpha.3.zip
```

The payload SHA-256 is compiled into the installer and verified before extraction. Setup checks for the x64 .NET 10 Desktop Runtime, installs the ThinkControl UI and service under Program Files, registers `ThinkControlService`, creates shortcuts and can launch ThinkControl after completion.

Package CI enforces a 5 MB hard ceiling on the bootstrap installer, a 20 MB ceiling on the compressed application payload and a 65 MB ceiling on the framework-dependent installed UI plus service payload.

See [Installer](installer/README.md) and [Dependencies](docs/DEPENDENCIES.md).

## Updates

ThinkControl checks public GitHub Releases without a permanent updater service. Installing a newer setup stops the old hardware service before replacing the verified payload, then updates and restarts the service registration.

## Build and validation

The repository targets .NET 10 and WPF. Windows CI restores, builds, runs Core tests and renders real WPF snapshots. Packaging CI builds the framework-dependent UI/service payload, verifies canonical branding, creates the external payload ZIP, compiles the small web bootstrapper and performs an install, service-running and uninstall smoke test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
```

A green CI run cannot prove physical Lenovo hardware behavior. RPM, EC fan control, keyboard providers, Precision Touchpad reports and haptics still require testing on the real target device.

## Documentation

- [Documentation index](docs/README.md)
- [Device support](docs/DEVICE-SUPPORT.md)
- [Product specification](docs/PRODUCT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hardware safety](docs/HARDWARE-SAFETY.md)
- [Lenovo providers](docs/LENOVO-PROVIDERS.md)
- [Dependencies](docs/DEPENDENCIES.md)
- [Diagnostics and privacy](docs/DIAGNOSTICS.md)
- [Design system](docs/DESIGN.md)
- [Alpha testing](docs/ALPHA-TESTING.md)
- [Release checklist](docs/RELEASE-CHECKLIST.md)

## License

ThinkControl is source-available for noncommercial use under the [PolyForm Noncommercial License 1.0.0](LICENSE). Commercial use requires separate permission.

Third-party software and artwork retain their own licenses. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

ThinkControl is an independent community project and is not affiliated with or endorsed by Lenovo.
