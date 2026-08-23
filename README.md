<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/thinkcontrol-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="docs/assets/thinkcontrol-logo-light.svg">
    <img alt="ThinkControl" src="docs/assets/thinkcontrol-logo-light.svg" width="430">
  </picture>

  <p>Windows controls and hardware telemetry for Lenovo laptops.</p>

  [![Windows CI](https://img.shields.io/github/actions/workflow/status/Hugowhitee/ThinkControl/ci.yml?branch=main&label=Windows%20CI&logo=github)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial-red)](LICENSE)

  **[Download](https://github.com/Hugowhitee/ThinkControl/releases)** |
  **[Device support](docs/DEVICE-SUPPORT.md)** |
  **[Documentation](docs/README.md)** |
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**
</div>

## Status

ThinkControl `v0.1.0-alpha.1` is a prerelease. The ThinkPad X9-15 Gen 1 (`21Q6` and `21Q7`) is the reference device used for the first hardware implementation. Other Lenovo families use capability detection and should be treated as beta until tested on the exact model.

## Features

ThinkControl runs from the Windows notification area and provides a compact view for common controls plus a larger Advanced window for detailed settings.

| Area | Current support |
| --- | --- |
| Performance | Quiet, Balanced and Performance Windows power modes |
| Fans | Real RPM where a reliable source is available; X9 Lenovo Auto and manual levels 1 to 7 |
| Display | Refresh rate, automatic refresh policy, brightness and adaptive brightness where Windows exposes them |
| Keyboard | Off, Low and High plus Auto, Breathing, Reactive and experimental Audio effects on supported Lenovo hardware |
| Battery | Percentage, charging state, power in watts, energy in Wh, health and filtered time estimates where available |
| System | Device identity, provider status, compatibility information and diagnostics |

ThinkControl does not substitute estimated percentages for fan RPM or expose controls that have no working backend.

## Install

1. Open [GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases).
2. Download `ThinkControl-Setup-0.1.0-alpha.1.exe` from the current release.
3. Optionally verify it with `SHA256SUMS.txt`.
4. Run the installer and approve the Windows administrator prompt required to install the hardware service.
5. Leave `Launch ThinkControl` enabled if you want to start it immediately.

The installer is self-contained. A separate .NET runtime is not required.

Some low-level X9 fan functions currently depend on PawnIO. ThinkControl still runs without it; only the affected hardware backend remains unavailable.

See [installer/README.md](installer/README.md) for packaging and dependency details.

## Device support

Support is evaluated per capability. A recognized model family helps ThinkControl choose appropriate providers, but it does not authorize model-specific hardware writes by itself.

| Device | Status | Notes |
| --- | --- | --- |
| ThinkPad X9-15 Gen 1, `21Q6` / `21Q7` | Verified reference | Windows APIs, Lenovo PM driver and the X9 EC fan backend |
| Other ThinkPads | Beta | Windows features and supported Lenovo providers when detected |
| ThinkBook, Yoga and IdeaPad | Beta | Windows features plus compatible Lenovo providers when detected |
| LOQ and Legion | Beta | Windows features and supported Lenovo WMI providers where available |
| Other Lenovo laptops | Beta | Windows features plus conservative provider discovery |
| Other Windows laptops | Generic | Windows-level features only |

See [Device Support](docs/DEVICE-SUPPORT.md) for the detailed matrix.

## ThinkPad X9-15 Gen 1

The X9 is the current reference device for low-level fan and keyboard work.

| Capability | Current state |
| --- | --- |
| Windows power modes | Implemented |
| CPU temperature | Available when a trustworthy sensor provider is present |
| Fan RPM | X9 EC tachometer registers `0x84/0x85` |
| Fan state | X9 EC register `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Levels `1` to `7` |
| Fan off | Blocked |
| Unverified `0x40` override | Never written |
| Refresh rate and brightness | Windows APIs |
| Battery power, energy and health | Available when ACPI exposes the required data |
| Keyboard Off, Low and High | Lenovo PM provider with readback |
| Keyboard effects | User-session policies over the supported hardware levels |

Technical findings are recorded in [X9-15 Gen 1 research](docs/research/x9-15-gen1.md).

## Hardware safety

The normal UI runs as the signed-in user. Privileged hardware operations are isolated in `ThinkControl.Service` and exposed to the UI through semantic commands rather than raw EC, port or IOCTL access.

For the verified X9 fan backend:

- Lenovo Auto is `0x80`.
- Manual levels are limited to `1` through `7`.
- Fan-off `0x00` is blocked.
- The unverified `0x40` family is never written.
- Writes are read back where supported.
- RPM polling is intentionally conservative.
- Normal service shutdown attempts to return manual fan control to Lenovo Auto.

See [Hardware Safety](docs/HARDWARE-SAFETY.md) before changing a low-level provider.

## Architecture

```text
ThinkControl.UI
      |
      | named pipe
      v
ThinkControl.Service
      |
      +-- Windows APIs
      +-- Lenovo capability providers
      +-- verified device-specific providers
```

The UI never receives a generic raw hardware-write interface. Device-specific register knowledge stays inside the hardware layer.

See [Architecture](docs/ARCHITECTURE.md) for project boundaries and IPC details.

## Diagnostics and privacy

ThinkControl can keep bounded local diagnostics for compatibility work and support bundles. The diagnostic schema excludes serial numbers, usernames, hostnames, MAC addresses, disk serials, typed text and audio samples.

Automatic private diagnostics upload is not enabled in the current release.

See [Diagnostics and Privacy](docs/DIAGNOSTICS.md).

## Build and test

The repository uses .NET 10 and WPF. Release candidates are built and tested on Windows in GitHub Actions, including UI snapshots, installer creation, service startup and uninstall checks.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
dotnet test ThinkControl.slnx -c Release
```

Release packaging uses Inno Setup. Version metadata is stored in `version.json`.

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
