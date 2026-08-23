<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/thinkcontrol-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="docs/assets/thinkcontrol-logo-light.svg">
    <img alt="ThinkControl" src="docs/assets/thinkcontrol-logo-light.svg" width="430">
  </picture>

  <p>Windows controls and hardware telemetry for Lenovo laptops.</p>

  [![Windows CI](https://img.shields.io/github/actions/workflow/status/Hugowhitee/ThinkControl/ci.yml?branch=main&label=Windows%20CI&logo=windows11&logoColor=white)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Status](https://img.shields.io/badge/status-alpha-orange)](docs/ALPHA-TESTING.md)
  [![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial-555)](LICENSE)

  **[Releases](https://github.com/Hugowhitee/ThinkControl/releases)** |
  **[Device support](docs/DEVICE-SUPPORT.md)** |
  **[Documentation](docs/README.md)** |
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**
</div>

## Status

ThinkControl `v0.1.0-alpha.1` is being finalized as the first prerelease. The ThinkPad X9-15 Gen 1 (`21Q6` and `21Q7`) is the reference device for the first low-level hardware implementation. Other Lenovo families use capability detection and remain beta until tested on the exact model/provider combination.

`releaseReady` remains disabled until the packaged build passes its final validation. The Releases page may therefore be empty while the alpha is being prepared.

## Features

| Area | Current support |
| --- | --- |
| Performance | Quiet, Balanced and Performance through the effective Windows power-mode surface used by the Lenovo thermal stack |
| Fans | Real RPM where a reliable tachometer exists; verified X9 Lenovo Auto and manual levels 1 to 7 |
| Display | Refresh rate, automatic refresh policy, brightness and adaptive brightness where Windows exposes them |
| Keyboard | Off, Low and High plus Auto, Breathing, Reactive and experimental Audio policies on supported Lenovo hardware |
| Battery | Percentage, charging state, live watts, energy in Wh, health and filtered time estimates where available |
| System | Device identity, provider status, compatibility information and diagnostics |

ThinkControl never substitutes guessed fan percentages, noise values or sensor names for real hardware data.

## Interface

ThinkControl deliberately uses two different Windows surfaces:

- **Compact** is a fixed tray flyout for everyday controls. It stays anchored above the notification area and does not behave like a movable desktop window.
- **Advanced** is a normal resizable Windows application window. Windows owns its title bar, app icon, minimize/maximize/restore/close controls, system menu and Snap Layouts.

The compact expand control opens Advanced; the Advanced sidebar contains one directional control to return to the tray flyout.

## Install

The release download is a small x64 bootstrap-style Inno Setup executable:

```text
ThinkControl-Setup-0.1.0-alpha.1.exe
```

Setup performs the prerequisite work itself:

1. installs the ThinkControl UI and privileged hardware service;
2. checks for the .NET 10 Desktop Runtime;
3. if needed, downloads the official Microsoft .NET Desktop Runtime `10.0.10` x64 installer and verifies its pinned SHA-256 before execution;
4. on the verified X9 profile, offers **X9 hardware access (PawnIO 2.2.0)** and verifies the exact official PawnIO release asset against the published Winget-package SHA-256;
5. registers and starts `ThinkControlService`;
6. offers **Launch ThinkControl** when setup completes.

The .NET runtime and PawnIO are downloaded only when needed; they are not duplicated inside the ThinkControl installer. If X9 hardware access cannot be installed, ThinkControl still installs and keeps independent Windows/Lenovo features available while explaining the limitation.

See [installer/README.md](installer/README.md) and [Dependencies](docs/DEPENDENCIES.md).

## ThinkPad X9-15 Gen 1

The X9 reference profile recognizes machine types `21Q6` and `21Q7` from Lenovo SMBIOS identity rather than serial number.

| Capability | Current implementation |
| --- | --- |
| Windows power mode | Effective overlay + user-configured AC/DC mode with readback |
| CPU temperature | Trustworthy sensor provider when present |
| Fan RPM | X9 EC tachometer registers `0x84/0x85` with conservative polling |
| Fan state | X9 EC register `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Discrete levels `1` to `7` |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Keyboard Off / Low / High | Lenovo PM/EnergyDrv provider with readback; installed Lenovo Vantage ThinkKeyboard add-in is a fallback |
| Keyboard effects | User-session policies over verified hardware levels |
| Refresh / brightness | Windows display APIs |
| Battery power / Wh / health | Windows/ACPI when exposed |

An important safety invariant is that normal service/controller disposal returns active manual X9 fan control to Lenovo Auto before closing the EC provider.

Technical findings are recorded in [X9-15 Gen 1 research](docs/research/x9-15-gen1.md).

## Lenovo integration

ThinkControl prefers supported Windows surfaces and capability probes instead of assuming every Lenovo laptop has the same firmware interface.

Known provider families currently include:

- Windows power, display and battery APIs;
- `IBMPmDrv` and `EnergyDrv` keyboard contracts with readback verification;
- installed official Lenovo Vantage ThinkKeyboard components as a keyboard fallback;
- Lenovo read-only WMI/CIM fan telemetry where exposed;
- the exact verified X9 EC backend for direct X9 fan access.

**Commercial Vantage** is launched through its installed Windows protocol/AUMID when available. The Microsoft Store is only a fallback when Windows cannot find an installed Vantage app.

See [Lenovo Providers](docs/LENOVO-PROVIDERS.md).

## Hardware safety

The normal WPF application runs as the signed-in user. Privileged hardware operations are isolated in `ThinkControl.Service` and exposed through semantic named-pipe operations rather than arbitrary EC, port or IOCTL passthrough.

For the verified X9 fan backend:

- Lenovo Auto is `0x80`;
- manual levels are limited to `1` through `7`;
- fan-off `0x00` is blocked;
- the unverified `0x40` family is never written;
- writes use readback where supported;
- RPM polling is intentionally conservative;
- normal provider/service disposal attempts to return manual ownership to Lenovo Auto.

See [Hardware Safety](docs/HARDWARE-SAFETY.md).

## Updates

ThinkControl checks GitHub Releases without a permanent updater service. Before the first public release exists, the Updates page reports that no public release has been published instead of surfacing a raw GitHub 404.

Installing a newer ThinkControl setup over an existing copy stops the old hardware service safely before replacing its files. Inno Setup uses its normal application-closing flow for a running tray process.

## Build and validation

The repository targets .NET 10 and WPF. To conserve Windows runner minutes, normal feature-branch pushes do not run CI. The Windows CI workflow runs for pull requests and `main` and performs restore/build plus real WPF snapshot rendering.

The separate package workflow is reserved for an explicit packaging check or a version tag. It publishes framework-dependent UI/service payloads, enforces payload/installer size budgets, builds Inno Setup, performs a silent install, waits for `ThinkControlService` to reach `Running`, uninstalls, verifies cleanup and generates `SHA256SUMS.txt`.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
```

Physical X9 validation still matters for hardware claims; a green VM/CI build cannot prove a fan or keyboard provider on the real laptop.

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
