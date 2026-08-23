<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/thinkcontrol-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="docs/assets/thinkcontrol-logo-light.svg">
    <img alt="ThinkControl" src="docs/assets/thinkcontrol-logo-light.svg" width="430">
  </picture>

  <p><strong>Control. Quietly.</strong></p>
  <p>A lightweight Windows hardware companion for Lenovo laptops — tray-first, fast, honest about hardware support, and built around safe capability detection.</p>

  [![Windows CI](https://img.shields.io/github/actions/workflow/status/Hugowhitee/ThinkControl/ci.yml?branch=main&label=Windows%20CI&logo=github)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial-red)](LICENSE)

  **[Download ThinkControl](https://github.com/Hugowhitee/ThinkControl/releases)** ·
  **[Supported devices](docs/DEVICE-SUPPORT.md)** ·
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)** ·
  **[Documentation](docs/README.md)**
</div>

---

> [!IMPORTANT]
> **ThinkControl v0.1.0-alpha.1 is an early hardware-control release.** The Lenovo ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`) is the verified reference machine. Broader Lenovo families are shipped as **Beta / Untested** profiles and only activate a hardware provider after that provider passes its own safe capability checks.

## ✨ What ThinkControl is

ThinkControl puts the controls that matter day-to-day in a compact Windows tray utility instead of a large OEM suite.

- **Performance** — Quiet / Balanced / Performance Windows modes, with room for Lenovo thermal-policy coordination when a device exposes it.
- **Fans** — real RPM where a trustworthy tachometer exists; verified X9 fan levels `1–7`; no invented PWM percentages.
- **Display** — Auto refresh, fixed refresh rate, brightness and adaptive brightness where Windows exposes them.
- **Keyboard** — Off / Low / High plus ThinkControl Auto, Breathing, Reactive and experimental Audio effects over real Lenovo hardware levels.
- **Battery** — percentage, charging state, live watts, Wh, health and smoothed time-to-full / time-remaining estimates.
- **System** — device identity, hardware/provider status, compatibility state and diagnostics.
- **Tray-first UI** — compact popup for everyday controls and a larger Advanced window for detail pages.

ThinkControl does not fake unsupported sensors, dBA values, fan percentages or hardware capabilities.

## 📸 Interface

ThinkControl renders the **real WPF application** in Windows CI with safe design-time telemetry. The snapshot suite covers compact dark/light plus Home, Performance, Fans, Display, Keyboard, Battery and Settings.

Until release screenshots are promoted into this README, open the latest successful **Windows CI** run and download the `ThinkControl-UI-Snapshots` artifact.

> [!NOTE]
> Values visible in CI snapshots such as `44°C`, `2,050 RPM`, `78%` and `18.4 W` are design-time sample telemetry — never measurements from a user's laptop.

## ⬇️ Download & install

### Recommended

1. Open **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**.
2. Open **ThinkControl v0.1.0-alpha.1**.
3. Download `ThinkControl-Setup-0.1.0-alpha.1.exe`.
4. Optionally verify the installer against `SHA256SUMS.txt`.
5. Run setup and approve the Windows UAC prompt for the ThinkControl hardware service.
6. Leave **Launch ThinkControl** enabled on the final page.

The installer is self-contained; users do **not** need to install the .NET runtime separately.

> [!TIP]
> ThinkControl lives in the Windows tray. Left-click the tray icon for the compact view; use the expand control for Advanced.

### Drivers and Lenovo software

ThinkControl prefers hardware interfaces already supplied by Windows or Lenovo. On a clean Windows installation some device-specific controls can remain unavailable until the relevant Lenovo driver stack is installed.

Common providers ThinkControl can discover include:

| Provider | Typical use | ThinkControl behavior |
| --- | --- | --- |
| Windows power/display APIs | power mode, refresh, brightness | safe generic provider |
| `IBMPmDrv` | ThinkPad keyboard/power-management functions | known-state read + write/readback |
| `EnergyDrv` | ThinkBook / IdeaPad / Yoga / LOQ-style keyboard functions | known-state read + write/readback |
| `LENOVO_FAN_METHOD` | Lenovo fan RPM | read-only |
| `CIM_Tachometer` | generic exposed fan RPM | read-only |
| `LENOVO_GAMEZONE_DATA` | supported Lenovo performance/thermal capabilities | only when firmware advertises the capability |
| PawnIO + X9 EC profile | X9 fan telemetry/control | **verified X9 profile only** |

The current alpha installer does **not** silently download an unpinned PawnIO package. The application still installs and runs; only the X9 direct-EC fan backend may remain unavailable until its prerequisite is present.

## 💻 Compatibility

ThinkControl separates **device profile confidence** from **individual capability support**. A Beta device does not get a deliberately crippled interface — each page remains available, while individual controls activate only when their provider is present and passes validation.

| Family | Status | Expected providers in alpha.1 |
| --- | --- | --- |
| **ThinkPad X9-15 Gen 1 · 21Q6 / 21Q7** | ✅ **Verified reference** | Windows + IBMPmDrv + verified X9 EC |
| **ThinkPad family** | 🧪 **Beta / Untested** | Windows + IBMPmDrv/Vantage + Lenovo read-only fan telemetry |
| **ThinkBook family** | 🧪 **Beta / Untested** | Windows + EnergyDrv/Vantage + Lenovo WMI where exposed |
| **Yoga family** | 🧪 **Beta / Untested** | Windows + capability-probed Lenovo PM/WMI providers |
| **IdeaPad family** | 🧪 **Beta / Untested** | Windows + EnergyDrv/Vantage + Lenovo WMI where exposed |
| **LOQ family** | 🧪 **Beta / Untested** | Windows + EnergyDrv + `LENOVO_GAMEZONE_DATA` where supported |
| **Legion family** | 🧪 **Beta / Untested** | Windows + Lenovo GameZone/WMI providers where supported |
| Other Lenovo | 🧪 **Generic Lenovo Beta** | Windows + safe Lenovo provider discovery |
| Other Windows laptops | ⚪ **Generic** | Windows-level features only |

### What “Beta / Untested” means

A family profile means we know which **documented or independently established Lenovo provider families** are worth probing. It does **not** mean ThinkControl assumes the same EC layout or blindly writes the same values across every laptop.

For Beta profiles:

- read-only telemetry may activate when values are plausible;
- Lenovo keyboard control activates only after a known-state read, and writes must read back correctly;
- vendor WMI control is considered only when the firmware exposes the expected capability/method;
- direct EC fan writes remain disabled unless an exact machine profile explicitly verifies them.

See **[Device Support](docs/DEVICE-SUPPORT.md)** and **[Lenovo Provider Model](docs/LENOVO-PROVIDERS.md)**.

## 🔥 X9-15 Gen 1 reference support

The X9 is the hardware ThinkControl has been structured and diagnosed against most deeply.

| Capability | v0.1.0-alpha.1 |
| --- | --- |
| Windows power modes | ✅ Implemented |
| CPU temperature | ✅ When a trustworthy provider exists |
| Fan RPM | ✅ X9 EC `0x84/0x85` |
| Fan state | ✅ X9 EC `0x2F` |
| Lenovo Auto | ✅ `0x80` + readback |
| Manual fan level | ✅ `1–7` only |
| Fan-off `0x00` | ⛔ Blocked |
| Unverified `0x40` override | ⛔ Never written |
| Refresh / brightness | ✅ Windows APIs |
| Battery watts / Wh / health | ✅ Where ACPI exposes data |
| Smoothed charging ETA | ✅ Rolling median + slow moving average |
| Keyboard Off / Low / High | ✅ Lenovo PM provider + readback |
| Auto / Breathing / Reactive / Audio | ✅ ThinkControl user-session effects |

Detailed evidence lives in **[X9-15 Gen 1 research](docs/research/x9-15-gen1.md)**.

## 🧠 Why provider detection matters

Lenovo does not expose one universal laptop-control interface across every generation and product family. ThinkControl therefore routes each capability independently:

```text
Device identity
      ↓
Family profile (Verified / Beta / Generic)
      ↓
Capability probes
      ├─ Windows APIs
      ├─ Lenovo WMI / ACPI providers
      ├─ installed Lenovo PM drivers
      └─ exact machine backend when verified
      ↓
read / verify / expose control
```

A profile can help ThinkControl choose **what to probe**, but a profile alone can never authorize arbitrary EC/IOCTL writes.

## 🔋 Battery ETA

ThinkControl avoids the noisy “one sample = one ETA” approach. When Windows exposes energy and charge/discharge rate, it uses:

- recent power samples;
- a rolling median filter;
- a slow EWMA-style average;
- bounded movement between displayed estimates;
- separate charging, discharging and near-full behavior.

That keeps the estimate useful without making the UI jump every few seconds.

## ⌨️ Keyboard effects

Many Lenovo white-backlight keyboards expose discrete states rather than a verified `0–100%` brightness API. ThinkControl builds effects from the actual states:

- **Static** — Off / Low / High.
- **Auto** — High → Low → Off after idle periods.
- **Breathing** — rate-limited Low ↔ High; if Lenovo firmware fades those transitions, ThinkControl uses that smooth fade.
- **Reactive** — short High pulse after keyboard activity; typed keys are never stored.
- **Audio** — experimental local loopback level response; audio samples are not retained.

## 🛡️ Hardware safety

The normal UI runs as the signed-in user. A small Windows service owns only operations requiring elevation.

```text
ThinkControl.UI
      │ semantic named pipe
      ▼
ThinkControl.Service
      ├─ Windows APIs
      ├─ Lenovo capability providers
      └─ exact verified low-level device providers
```

The UI cannot request arbitrary EC addresses, raw port I/O or generic IOCTL passthrough.

For the X9 direct fan backend:

- Lenovo Auto is `0x80`;
- manual levels are limited to `1–7`;
- fan-off `0x00` is blocked;
- the `0x40` override family is never written;
- RPM polling is intentionally sparse because aggressive X9 EC tachometer polling was observed to disturb fan behavior;
- normal service shutdown attempts to return manual fan ownership to Lenovo Auto.

Read **[Hardware Safety](docs/HARDWARE-SAFETY.md)** before adding a new low-level provider.

## 🧪 Diagnostics & testing

Unknown/Beta devices can help improve compatibility without hiding their normal ThinkControl UI.

Local diagnostics are bounded and redacted and can include provider availability, operation outcome, compatibility state and sensor source. They intentionally exclude serial number, username, hostname, MAC address, disk serial, typed text and audio samples.

Available actions:

- Preview diagnostics;
- Export support bundle;
- Delete local diagnostics;
- open the structured GitHub bug-report form.

Automatic private diagnostics upload is **not enabled yet**; ThinkControl contains no GitHub PAT or private-repository credential.

## 🐛 Report a bug / help validate a laptop

Use the **[ThinkControl bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**.

It asks for a Lenovo family first, then the exact model/machine type. Only the essential fields are required, and screenshots/support bundles can be attached.

For Beta devices, a report that simply says **what works and what does not** is useful even when there is no crash.

## 🚀 Release quality

Every release candidate is expected to pass:

1. Windows restore/build/test;
2. real WPF snapshot rendering;
3. self-contained UI + service publish;
4. Inno Setup packaging;
5. silent install;
6. `ThinkControlService` reaches Running;
7. silent uninstall;
8. service registration and files disappear;
9. SHA-256 checksum generation.

Versioning stays aligned across the app, tag, release title and installer:

```text
app        0.1.0-alpha.1
tag        v0.1.0-alpha.1
release    ThinkControl v0.1.0-alpha.1
installer  ThinkControl-Setup-0.1.0-alpha.1.exe
```

See **[Release Checklist](docs/RELEASE-CHECKLIST.md)**.

## 🎨 Branding & UI

ThinkControl uses the TC / TrackPoint visual system: graphite surfaces, white/gray UI, and ThinkPad red as the single strong accent.

Normal interface symbols use a consistent local vector set; ThinkControl-specific identity/status artwork uses the custom asset language.

The WPF/XAML layout is deliberately separated from the privileged hardware layer, so the UI can also be edited visually in **Blend for Visual Studio**.

See **[UI Editing](docs/UI_EDITING.md)**.

## 📚 Documentation

The documentation has a single entry point: **[docs/README.md](docs/README.md)**.

Quick links:

- [Device support](docs/DEVICE-SUPPORT.md)
- [Lenovo provider model](docs/LENOVO-PROVIDERS.md)
- [Installation & dependencies](installer/README.md)
- [Hardware safety](docs/HARDWARE-SAFETY.md)
- [Diagnostics & privacy](docs/DIAGNOSTICS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [UI editing](docs/UI_EDITING.md)
- [X9-15 Gen 1 research](docs/research/x9-15-gen1.md)

## ⚖️ License

ThinkControl is **source-available for noncommercial use** under the **PolyForm Noncommercial License 1.0.0**.

You may study, modify and redistribute ThinkControl for permitted noncommercial purposes, but commercial use requires separate permission. Redistributions and modified versions must preserve the ThinkControl required notice so users can clearly see the original project this work is based on.

This is intentionally **not an OSI open-source license** because commercial use is restricted. See [LICENSE](LICENSE).

Third-party components keep their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

<div align="center">
  <sub>ThinkControl is an independent community project and is not affiliated with or endorsed by Lenovo.</sub>
</div>
