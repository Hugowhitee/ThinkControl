<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/thinkcontrol-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="docs/assets/thinkcontrol-logo-light.svg">
    <img alt="ThinkControl" src="docs/assets/thinkcontrol-logo-light.svg" width="430">
  </picture>
</p>

<p align="center">
  <strong>A lightweight hardware companion for Lenovo ThinkPads.</strong><br>
  Fast everyday controls, trustworthy telemetry and verified ThinkPad hardware access without the weight of a full OEM suite.
</p>

<p align="center">
  <a href="https://github.com/Hugowhitee/ThinkControl/releases"><strong>Releases</strong></a>
  ·
  <a href="docs/DEVICE-SUPPORT.md">Device support</a>
  ·
  <a href="docs/HARDWARE-SAFETY.md">Hardware safety</a>
  ·
  <a href="docs/UI_EDITING.md">Edit the UI</a>
  ·
  <a href="https://github.com/Hugowhitee/ThinkControl/issues">Issues</a>
</p>

> **Current source version: `v0.1.0-alpha.1`**  
> Pre-release development build. The installer pipeline is functional, but the first public alpha is held until the current UI and X9 hardware backends have completed final on-device validation.

## What ThinkControl is

ThinkControl is a compact Windows tray utility inspired by the directness of G-Helper, but designed for Lenovo ThinkPads. The normal UI runs without administrator rights; a small Windows service owns only the verified low-level operations that actually require elevation.

The **ThinkPad X9-15 Gen 1** is the first reference machine. ThinkControl does not assume that every ThinkPad exposes the same EC registers, Lenovo services or keyboard controls: capabilities are enabled from verified device profiles and unknown machines stay safe by default.

## Highlights

- compact tray popup plus a larger resizable Advanced window
- Quiet / Balanced / Performance power modes
- CPU temperature with a lightweight 60-second sparkline
- real ThinkPad fan RPM and discrete fan level when a verified backend is available
- Lenovo Auto plus manual fan levels `1`–`7` on the verified X9 EC backend
- display refresh controls, brightness and adaptive brightness
- battery percentage, live charge/discharge power, Wh, health and a smoothed ETA
- keyboard backlight levels plus ThinkControl Auto / Breathing / Reactive / Audio policies when the hardware backend is verified
- system / light / dark themes
- start with Windows and tray operation
- GitHub Release update checks
- installer-managed Windows hardware service
- safe fallback to Lenovo Auto when direct fan ownership ends

## Supported devices

| Device | Machine type | Status | Low-level hardware support |
| --- | --- | --- | --- |
| **ThinkPad X9-15 Gen 1** | **21Q6 / 21Q7** | **Reference device · alpha validation** | X9 EC fan telemetry/control implemented; final on-device alpha validation still required |
| Other ThinkPads | varies | Safe / partial | Windows-safe features only unless an exact verified profile and backend exist |
| Non-Lenovo PCs | varies | Unsupported hardware control | ThinkControl does not enable Lenovo-specific writes |

### X9-15 Gen 1 capability status

| Feature | Status |
| --- | --- |
| Windows power modes | Implemented |
| CPU temperature | Implemented; exact source is shown when available |
| Fan RPM | Implemented through verified ThinkPad EC registers `0x84/0x85` |
| Fan state / level | Implemented through EC register `0x2F` |
| Lenovo Auto | Implemented as EC value `0x80` |
| Manual fan control | Implemented as discrete levels `1`–`7` only |
| Continuous 0–100% fan PWM | **Not supported / not faked** |
| Display refresh | Implemented |
| Brightness | Implemented where Windows exposes the internal panel control |
| Adaptive brightness | Implemented where Windows/platform support is exposed |
| Battery watts / Wh / health | Implemented from Windows ACPI battery telemetry where available |
| Charging / battery ETA | Implemented with smoothed recent-power estimation |
| Keyboard Off / Low / High | Backend present, but still treated as alpha hardware validation |
| Breathing / Reactive / Audio keyboard effects | Implemented as user-session policies; available only when the verified hardware-level backend is available |

ThinkControl never writes X9 fan-off `0x00` and does not expose the unverified `0x40` override state. Unknown ThinkPads never inherit X9 EC writes simply because they are Lenovo devices.

## Install

### Public releases

Open the **[ThinkControl Releases page](https://github.com/Hugowhitee/ThinkControl/releases)** and download the installer named like:

```text
ThinkControl-Setup-0.1.0-alpha.1.exe
```

Then:

1. Run the setup file.
2. Approve the Windows UAC prompt for installing the ThinkControl hardware service.
3. Leave **Launch ThinkControl** enabled on the final page.
4. ThinkControl starts from the Windows tray; use the expand control for the Advanced window.

The current package is self-contained, so a normal installation does **not** require manually installing the .NET SDK or runtime first.

### Hardware access / drivers

ThinkControl-owned files and its Windows service are installed by the setup program. Lenovo and Intel OEM packages remain vendor-owned and are diagnosed rather than blindly replaced.

The X9 fan backend uses **PawnIO** for low-level EC access. The final one-install flow will detect the verified X9 profile and offer the pinned, signed PawnIO prerequisite automatically. Until that prerequisite step is enabled in a tagged alpha, fan controls may show **Limited hardware access** on a clean Windows installation even though display, battery and other Windows-level features continue to work.

ThinkControl does not require Lenovo Vantage or Lenovo Service Bridge for normal operation.

## Screenshots

Runtime screenshots will live in `docs/screenshots/` after the first alpha has been visually rendered and validated on Windows. Early concept/mockup images are deliberately **not** presented here as if they were screenshots of the running application.

Planned snapshot set:

- Compact tray popup — dark theme
- Compact tray popup — light theme
- Advanced Home
- Advanced Fans
- Advanced Keyboard effects
- Advanced Battery telemetry
- Installer / completion screen

## Safety model

ThinkControl deliberately separates UI from hardware access:

```text
ThinkControl.UI              normal user
        |
        | authenticated semantic named pipe
        v
ThinkControl.Service         elevated Windows service
        |
        +-- Windows APIs
        +-- verified Lenovo/platform APIs
        +-- PawnIO + verified ThinkPad EC profile
```

The UI cannot request arbitrary port writes, arbitrary EC registers or generic IOCTL passthrough. Low-level commands are semantic operations such as `SetFanLevel`, `ReturnFanToAuto` and verified keyboard-level changes.

For the X9 fan controller:

- `0x80` = Lenovo BIOS/EC Auto
- manual levels = `1` through `7`
- `0x00` fan-off = blocked
- `0x40` override/disengaged = blocked
- RPM telemetry is intentionally sampled conservatively because aggressive tachometer reads previously correlated with audible fan cadence
- service shutdown attempts to restore Lenovo Auto if ThinkControl owned a manual fan level

See **[Hardware Safety](docs/HARDWARE-SAFETY.md)** for the full contract.

## Battery ETA

ThinkControl does not simply divide one noisy instantaneous watt reading by remaining capacity. When Windows exposes charge/discharge rate and battery energy, ThinkControl uses recent samples, a median filter and a slow-moving weighted average for its ETA. This keeps the estimate responsive to a real change while avoiding constant large jumps caused by short CPU or charger-power spikes.

The UI labels unavailable values as unavailable rather than inventing a number.

## Keyboard effects

The X9 keyboard appears to expose discrete hardware levels rather than a verified continuous brightness value. ThinkControl therefore treats effects as policies over real hardware levels:

- **Auto** — idle-aware backlight policy
- **Breathing** — rate-limited `Low ↔ High`; intended to use Lenovo's own smooth transition between levels when present
- **Reactive** — brief high-level pulse after keyboard activity
- **Audio** — experimental local loopback RMS response

Input contents and audio samples are not stored. The privileged service receives only semantic backlight-level requests.

## Updates and versions

The repository version is stored centrally and release builds use explicit semantic versions such as:

```text
ThinkControl v0.1.0-alpha.1
ThinkControl v0.1.0
```

Tagged builds create a GitHub Release with the same visible version, the versioned installer and a `SHA256SUMS.txt` checksum. Development builds remain GitHub Actions artifacts and are not presented as stable releases.

**[View all releases →](https://github.com/Hugowhitee/ThinkControl/releases)**

## Development and UI editing

ThinkControl uses WPF / XAML on .NET 10. Most visual work is deliberately kept in XAML so the interface can be edited with **Blend for Visual Studio** without touching fan-control logic.

Useful files:

```text
src/ThinkControl.UI/MainWindow.xaml
src/ThinkControl.UI/AdvancedWindow.xaml
src/ThinkControl.UI/Controls/KeyboardEffectsPanel.xaml
src/ThinkControl.UI/Controls/BatteryTelemetryPanel.xaml
src/ThinkControl.UI/App.xaml
```

See **[UI Editing](docs/UI_EDITING.md)** for the safe visual-editing workflow.

## Repository layout

```text
ThinkControl/
  src/
    ThinkControl.UI/             WPF tray + Advanced UI
    ThinkControl.Service/        privileged Windows service
    ThinkControl.Core/           shared contracts / IPC
    ThinkControl.Hardware/       hardware providers
    ThinkControl.DeviceProfiles/ verified capability profiles
  devices/
    Lenovo/ThinkPad/X9-15-Gen1/
  docs/
    research/
    assets/
    screenshots/
  installer/
  tests/
  .github/workflows/
```

## Documentation

- [Product scope](docs/PRODUCT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hardware safety](docs/HARDWARE-SAFETY.md)
- [Device support](docs/DEVICE-SUPPORT.md)
- [Dependencies](docs/DEPENDENCIES.md)
- [Design rules](docs/DESIGN.md)
- [UI editing](docs/UI_EDITING.md)
- [X9-15 Gen 1 research](docs/research/x9-15-gen1.md)
- [Installer behavior](installer/README.md)

## Reference research

`Hugowhitee/X9-Helper` and `Hugowhitee/Thinkpad_Fancontrol` remain research references only. ThinkControl owns a separate architecture and does not automatically inherit older assumptions or unsafe experiments.

## License

MIT — see [LICENSE](LICENSE).
