<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/thinkcontrol-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="docs/assets/thinkcontrol-logo-light.svg">
    <img alt="ThinkControl" src="docs/assets/thinkcontrol-logo-light.svg" width="430">
  </picture>
</p>

<p align="center">
  <strong>A lightweight hardware companion for Lenovo ThinkPads.</strong><br>
  Fast everyday controls, truthful telemetry and safety-gated hardware access from a compact Windows tray utility.
</p>

<p align="center">
  <a href="https://github.com/Hugowhitee/ThinkControl/releases"><strong>Download / Releases</strong></a>
  ·
  <a href="docs/DEVICE-SUPPORT.md">Supported devices</a>
  ·
  <a href="docs/HARDWARE-SAFETY.md">Hardware safety</a>
  ·
  <a href="docs/DIAGNOSTICS.md">Diagnostics</a>
  ·
  <a href="docs/UI_EDITING.md">Edit the UI</a>
  ·
  <a href="https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml">Report a bug</a>
</p>

## Current release

**Version:** `v0.1.0-alpha.1`  
**Channel:** Alpha / prerelease  
**Reference device:** Lenovo ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)

The installer and service lifecycle already pass Windows CI. This version is intentionally an **alpha** because the complete low-level X9 backend still needs its final physical validation pass on the reference laptop.

The release-candidate branch is marked `releaseReady`. When it merges to `main`, GitHub creates the exact tag `v0.1.0-alpha.1`; that tag runs the tested packaging workflow and publishes a prerelease titled **ThinkControl v0.1.0-alpha.1** with:

```text
ThinkControl-Setup-0.1.0-alpha.1.exe
SHA256SUMS.txt
```

If the release page does not show `v0.1.0-alpha.1` yet, the release-candidate has not merged or its publishing workflow is still running.

**[Open ThinkControl Releases →](https://github.com/Hugowhitee/ThinkControl/releases)**

## What ThinkControl does

ThinkControl is a tray-first Windows utility inspired by the directness of G-Helper, but built around ThinkPad hardware and Windows APIs. The normal UI runs as the signed-in user. A small Windows service owns only low-level operations that actually require elevation.

### Included in alpha.1

- compact tray popup and resizable Advanced window;
- Quiet / Balanced / Performance Windows power modes;
- CPU temperature with an approximately 60-second sparkline when a trustworthy sensor is available;
- real fan RPM and fan state on the X9 backend;
- Lenovo Auto plus discrete X9 fan levels `1–7`;
- display refresh-rate control and Auto 60 Hz / maximum policy;
- internal-display brightness and adaptive brightness where Windows exposes them;
- battery percentage, charging state, live watts, Wh, health and smoothed ETA where ACPI telemetry exposes them;
- keyboard Off / Low / High on the current X9 Lenovo PM backend;
- ThinkControl keyboard Auto / Breathing / Reactive / experimental Audio policies;
- system / light / dark themes;
- start with Windows and tray operation;
- GitHub Release update checking;
- local redacted compatibility diagnostics with Preview / Export / Delete;
- structured GitHub bug-report form;
- self-contained x64 installer with Windows service installation/removal.

ThinkControl deliberately does **not** invent continuous fan PWM, dBA values, missing sensor precision or unsupported hardware capabilities.

## Supported devices

### Current alpha.1 matrix

| Device | Windows-level features | Fan telemetry/control | Keyboard hardware control | Status |
| --- | --- | --- | --- | --- |
| **ThinkPad X9-15 Gen 1 · 21Q6 / 21Q7** | Available where Windows exposes them | X9 backend implemented | X9 Lenovo PM backend implemented | **Reference device · final physical alpha validation pending** |
| Other ThinkPads | Available where Windows exposes them | **Not enabled in alpha.1** | **Not enabled in alpha.1** | Not validated |
| Other Lenovo laptops | Available where Windows exposes them | Not enabled | Not enabled | Not validated |
| Other Windows laptops | Available where Windows exposes them | Lenovo-specific backend unavailable | Lenovo-specific backend unavailable | Not validated |

An unvalidated laptop still gets the same main ThinkControl pages. It is not a separate crippled edition. However, **alpha.1 does not automatically enable unknown EC or Lenovo PM writes** just because a laptop is a ThinkPad.

The core contains `Verified`, `Experimental` and `Not validated` compatibility states for future provider expansion, but the current production low-level service remains explicitly gated to the X9 `21Q6/21Q7` profile.

See the authoritative **[Device Support](docs/DEVICE-SUPPORT.md)** page for exact current behavior and the compatibility roadmap.

## X9-15 Gen 1 hardware status

| Capability | Alpha.1 state |
| --- | --- |
| Windows power modes | Implemented |
| CPU temperature | Implemented when a trustworthy provider is available; source is shown |
| Fan RPM | X9 EC `0x84/0x85` implementation |
| Fan state | X9 EC `0x2F` implementation |
| Lenovo Auto | `0x80`, with read-back verification |
| Manual fan control | Discrete levels `1–7` |
| Continuous 0–100% PWM | **Not exposed / not faked** |
| Fan-off `0x00` | **Blocked** |
| Unverified `0x40` override | **Never written** |
| Display refresh | Implemented |
| Brightness | Implemented where Windows exposes internal-panel control |
| Adaptive brightness | Implemented where platform support exists |
| Battery watts / Wh / health | Implemented where Windows ACPI exposes the values |
| Charging / battery ETA | Implemented with smoothed recent-power estimation |
| Keyboard Off / Low / High | Implemented on current X9 backend with read-after-write verification |
| Keyboard Auto / Breathing / Reactive / Audio | Implemented as user-session policies over real hardware levels |

### Still not part of alpha.1

- autonomous temperature-driven custom fan curves;
- hysteresis / delayed-down / minimum-hold curve engine;
- universal conflict arbitration for other EC fan controllers;
- an ungraceful-crash guardian independent of the Windows service lifecycle;
- automatic writable Experimental backends for unknown ThinkPads;
- private automatic diagnostics upload;
- battery charge-threshold control;
- automatic pinned PawnIO prerequisite installation.

Those are roadmap items, not hidden completed features.

## UI snapshots

ThinkControl renders the **actual built WPF UI** in CI using safe design-time telemetry. These are not Figma/mockup images and are specifically used to catch clipping, bad spacing and icon regressions before merge.

The current snapshot set includes:

- Compact — dark;
- Compact — light;
- Advanced Home — dark/light;
- Performance;
- Fans;
- Display;
- Keyboard;
- Battery;
- Settings / diagnostics.

In GitHub Actions, open the latest successful **CI** run and download the artifact named:

```text
ThinkControl-UI-Snapshots
```

Sample values such as `44°C`, `2,050 RPM`, `78%` and `18.4 W` in those images are **design-time sample telemetry**, not measurements from a laptop.

The UI currently uses dependency-free **Google Material Symbols Outlined** vector geometry. Third-party attribution is in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Install

### GitHub prerelease

1. Open **[Releases](https://github.com/Hugowhitee/ThinkControl/releases)**.
2. Open `ThinkControl v0.1.0-alpha.1`.
3. Download `ThinkControl-Setup-0.1.0-alpha.1.exe`.
4. Optionally verify it against `SHA256SUMS.txt`.
5. Run the installer and approve the Windows UAC prompt for the ThinkControl hardware service.
6. Leave **Launch ThinkControl** enabled on the final page.
7. ThinkControl starts from the tray; use the expand button for Advanced.

The package is self-contained. Users do not need to install the .NET SDK/runtime separately.

### What the installer already tests automatically

Every release candidate packages and smoke-tests:

```text
install
  ↓
UI + service files present
  ↓
ThinkControlService → Running
  ↓
uninstall
  ↓
UI + service removed
  ↓
service registration removed
```

A SHA-256 checksum is produced only after that path succeeds.

### PawnIO / X9 EC access

The X9 fan backend uses PawnIO for verified low-level EC access. The current one-click installer does **not yet install the pinned PawnIO prerequisite automatically**. On a clean Windows installation, ThinkControl can therefore install and run successfully while X9 fan access remains limited until that prerequisite is present.

That limitation is documented rather than silently downloading a driver from an unpinned source.

## Battery ETA

ThinkControl does not copy a noisy one-sample Windows ETA. When ACPI exposes battery energy and charge/discharge rate, it calculates ETA from:

- recent power samples;
- a rolling median filter;
- a slow-moving weighted average;
- bounded per-update changes.

This keeps the estimate responsive while preventing short CPU/charger spikes from making the displayed time jump every two seconds.

## Keyboard effects

The X9 exposes discrete backlight levels rather than a verified 0–100% brightness API. ThinkControl therefore builds effects from real levels:

- **Auto** — High → Low → Off as the user becomes idle;
- **Breathing** — rate-limited `Low ↔ High`, using Lenovo's own smooth transition if firmware fades between the two;
- **Reactive** — temporary pulse after keyboard activity;
- **Audio** — experimental local loopback RMS response.

Typed key values are never logged. Audio samples are not retained.

## Compatibility diagnostics

The desktop app currently provides local, bounded and redacted compatibility diagnostics:

- Preview data;
- Export support bundle;
- Delete local diagnostics;
- semantic operation/error events;
- no serial number, username, hostname, MAC, disk serial, typed text or audio samples.

Automatic submission to a private project inbox is **not enabled yet**. The desktop application contains no GitHub PAT/private-repository credential.

For normal public bugs use the **[ThinkControl bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**. It supports common ThinkPad families, Other ThinkPad, Other Lenovo and Other laptop/PC, plus a free-form exact-model field and optional attachments.

## Safety model

```text
ThinkControl.UI              normal user
        |
        | semantic named pipe
        v
ThinkControl.Service         elevated Windows service
        |
        +-- Windows APIs
        +-- compiled Lenovo providers
        +-- PawnIO + explicit X9 EC profile
```

The UI cannot request arbitrary EC addresses, port I/O or generic IOCTL passthrough. For the current X9 fan path:

- Auto = `0x80`;
- manual = `1–7`;
- `0x00` = blocked;
- `0x40` family = never written;
- tachometer polling is conservative;
- normal service shutdown/disposal attempts to return manual fan ownership to Lenovo Auto.

See [Hardware Safety](docs/HARDWARE-SAFETY.md).

## Versioning and releases

`version.json` is the source of truth. `alpha.1` is explicitly marked `releaseReady` for the first prerelease candidate.

A merge to `main` creates the exact version tag only when that flag is true. The existing tag packaging workflow then checks that the tag matches `version.json`, builds/smoke-tests the installer and publishes the matching GitHub prerelease.

This prevents arbitrary tag names and keeps these aligned:

```text
app version      0.1.0-alpha.1
Git tag          v0.1.0-alpha.1
release title    ThinkControl v0.1.0-alpha.1
installer        ThinkControl-Setup-0.1.0-alpha.1.exe
```

See [Release checklist](docs/RELEASE-CHECKLIST.md).

## Edit the UI

ThinkControl uses .NET 10 WPF/XAML. Visual layout is deliberately separated from the hardware service so the interface can be edited in **Blend for Visual Studio** without touching EC/fan logic.

Main files:

```text
src/ThinkControl.UI/MainWindow.xaml
src/ThinkControl.UI/AdvancedWindow.xaml
src/ThinkControl.UI/Controls/KeyboardEffectsPanel.xaml
src/ThinkControl.UI/Controls/BatteryTelemetryPanel.xaml
src/ThinkControl.UI/Controls/DiagnosticsPanel.xaml
src/ThinkControl.UI/Resources/MaterialSymbols.xaml
src/ThinkControl.UI/App.xaml
```

See [UI Editing](docs/UI_EDITING.md).

## Documentation

- [Current device support](docs/DEVICE-SUPPORT.md)
- [Product specification](docs/PRODUCT.md)
- [Release checklist](docs/RELEASE-CHECKLIST.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hardware safety](docs/HARDWARE-SAFETY.md)
- [Diagnostics & privacy](docs/DIAGNOSTICS.md)
- [Dependencies](docs/DEPENDENCIES.md)
- [Design rules](docs/DESIGN.md)
- [UI editing](docs/UI_EDITING.md)
- [X9-15 Gen 1 research](docs/research/x9-15-gen1.md)
- [Installer behavior](installer/README.md)

## License

ThinkControl is MIT licensed. See [LICENSE](LICENSE).

ThinkControl redistributes a small curated subset of Google Material Symbols Outlined under Apache License 2.0. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
