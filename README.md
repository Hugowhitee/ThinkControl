<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/thinkcontrol-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="docs/assets/thinkcontrol-logo-light.svg">
    <img alt="ThinkControl" src="docs/assets/thinkcontrol-logo-light.svg" width="430">
  </picture>

  <p>Windows controls and hardware telemetry for Lenovo laptops.</p>

  [![Windows CI](https://img.shields.io/github/actions/workflow/status/Hugowhitee/ThinkControl/ci.yml?branch=main&label=Windows%20CI&logo=windows11&logoColor=white)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial-555)](LICENSE)

  **[Download](https://github.com/Hugowhitee/ThinkControl/releases)** |
  **[Device support](docs/DEVICE-SUPPORT.md)** |
  **[Documentation](docs/README.md)** |
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**
</div>

## Current prerelease

ThinkControl `v0.1.0-alpha.2` is the current X9-focused prerelease. The ThinkPad X9-15 Gen 1 machine types `21Q6` and `21Q7` are the verified low-level reference profile. Other Lenovo families use capability detection and remain beta until their exact providers have been tested on physical hardware.

## What ThinkControl does

| Area | Current support |
| --- | --- |
| Performance | Quiet, Balanced and Performance through Windows power mode plus verified X9 Lenovo Intelligent Cooling policy coordination |
| Fans | Real RPM where a reliable tachometer exists; verified X9 Lenovo Auto and manual EC levels 1 to 7 |
| Display | Refresh rate, automatic refresh policy, brightness and adaptive brightness where Windows exposes them |
| Keyboard | Off, Low and High plus Auto, Breathing, Reactive and experimental Audio policies on supported Lenovo hardware |
| Battery | Percentage, charging state, live watts, Wh, health and filtered time estimates where available |
| System | Exact device identity, provider status, compatibility information and privacy-safe diagnostics |

ThinkControl never substitutes guessed fan percentages, noise values, fake wattage targets or invented sensor names for real hardware data.

## Interface

ThinkControl deliberately uses two Windows surfaces.

**Compact** is a fixed tray flyout for everyday controls. It stays anchored above the notification area and cannot be dragged around like a desktop window. The single diagonal `↖` action opens Advanced.

**Advanced** is a normal resizable Windows application window. Windows owns the native title bar, app icon, minimize, maximize/restore, close, system menu and Windows 11 Snap Layouts. A matching `↘` action returns to Compact.

The app, tray, installer and repository artwork use the canonical ThinkControl v3 asset pack under [`assets/brand/v3`](assets/brand/v3). CI rejects legacy hand-drawn TC geometry or copies that drift from the approved v3 ICO/SVG sources.

## Install

Download the small installer from [Releases](https://github.com/Hugowhitee/ThinkControl/releases):

```text
ThinkControl-Setup-0.1.0-alpha.2.exe
```

The installer is a web bootstrapper. It does **not** embed the ThinkControl application payload or duplicate .NET runtimes. Setup downloads the matching release payload:

```text
ThinkControl-Payload-0.1.0-alpha.2.zip
```

The payload SHA-256 is pinned inside that installer build and verified before extraction. Setup also:

1. checks for the .NET 10 Desktop Runtime and downloads the pinned Microsoft x64 runtime only when missing;
2. detects the local Lenovo SMBIOS identity;
3. on verified X9 `21Q6/21Q7`, offers the pinned PawnIO 2.2.0 prerequisite for EC fan telemetry/control;
4. installs the UI and privileged hardware service under Program Files;
5. registers and starts `ThinkControlService`;
6. creates the Start menu entry and optional desktop shortcut using the exact v3 Windows icon;
7. offers **Launch ThinkControl** when setup completes.

Package CI enforces a **5 MB hard ceiling** on the bootstrap installer and a separate size budget on the compressed application payload. The previous ~84 MB installer / ~300 MB installed-runtime duplication is no longer the packaging model.

See [Installer](installer/README.md) and [Dependencies](docs/DEPENDENCIES.md).

## ThinkPad X9-15 Gen 1

The X9 reference profile recognizes machine types `21Q6` and `21Q7` from Lenovo SMBIOS identity rather than serial number.

| Capability | Current implementation |
| --- | --- |
| Power mode | Windows Best efficiency / Balanced / Best performance with readback |
| Lenovo thermal policy | X9-only LITSSvc `502/503/504` on AC and `507/508/509` on battery |
| CPU temperature | Trustworthy sensor provider when present |
| Fan RPM | X9 EC tachometer registers `0x84/0x85` with conservative polling |
| Fan state | X9 EC register `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Discrete levels `1` to `7` |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Keyboard Off / Low / High | Lenovo PM/EnergyDrv contracts with readback and installed Vantage ThinkKeyboard fallback |
| Keyboard effects | User-session policies over verified hardware levels |
| Refresh / brightness | Windows display APIs |
| Battery power / Wh / health | Windows/ACPI when exposed |

Lenovo Intelligent Cooling commands are treated as **thermal policy**, not as fake direct fan-RPM control. Direct X9 fan control remains the verified EC backend.

Normal service/controller disposal attempts to return an active manual X9 fan level to Lenovo Auto before closing EC access.

Technical findings are recorded in [X9-15 Gen 1 research](docs/research/x9-15-gen1.md).

## Lenovo integration

ThinkControl prefers supported Windows surfaces and capability probes instead of assuming every Lenovo laptop has the same firmware interface.

Known provider families currently include:

- Windows power, display and battery APIs;
- `IBMPmDrv` and `EnergyDrv` keyboard contracts with readback verification;
- installed official Lenovo Vantage ThinkKeyboard components as a keyboard fallback;
- Lenovo read-only WMI/CIM fan telemetry where exposed;
- verified X9 Intelligent Cooling named-pipe policy commands;
- the exact verified X9 EC backend for direct fan access.

**Commercial Vantage** is launched through its installed Windows protocol/AUMID when available. The Microsoft Store is not used when a local Vantage installation can be resolved.

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
- normal shutdown attempts to return manual ownership to Lenovo Auto.

See [Hardware Safety](docs/HARDWARE-SAFETY.md).

## Updates

ThinkControl checks public GitHub Releases without a permanent updater service. A missing or temporarily unavailable release endpoint is reported as a normal release-channel state instead of surfacing a raw GitHub 404.

Installing a newer ThinkControl setup over an existing copy stops the old hardware service before replacing the payload, then updates and restarts the service registration.

## Build and validation

The repository targets .NET 10 and WPF. Windows CI performs restore/build plus real WPF snapshot rendering. Packaging CI builds the framework-dependent UI/service payload, verifies exact v3 branding, creates the external payload ZIP, builds the small web bootstrapper and performs a full bootstrap install → service Running → uninstall → cleanup lifecycle test.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
```

Physical X9 validation still matters for hardware claims; a green VM/CI build cannot prove fan, keyboard or Lenovo thermal behavior on the real laptop.

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
