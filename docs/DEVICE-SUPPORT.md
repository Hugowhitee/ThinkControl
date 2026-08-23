# Device support

> [!IMPORTANT]
> This page applies to **ThinkControl `v0.1.0-alpha.1`** and describes the current compatibility model. A family being listed as **Beta / Untested** means ThinkControl knows safe provider candidates for that family; it does not mean every control is guaranteed to work on every model.

ThinkControl keeps the same main product areas visible across supported Windows laptops. Individual hardware controls activate from **capability detection**, not from an assumption that all Lenovo devices share one EC or driver interface.

## Compatibility levels

### ✅ Verified

A real machine/profile has been validated by the ThinkControl project and can use the exact providers enabled by that profile.

Current verified reference:

- **Lenovo ThinkPad X9-15 Gen 1** — machine types `21Q6 / 21Q7`.

### 🧪 Beta / Untested

ThinkControl recognizes the Lenovo family and probes established Lenovo provider types that are appropriate for it. The exact laptop has not yet been physically validated by ThinkControl.

A Beta device can still have working fan RPM, keyboard backlight and other controls if the provider proves itself. **Beta never means “guess the X9 EC layout”.**

### ⚪ Generic

The device has no known Lenovo family profile. Safe Windows-level features remain available. Lenovo-specific features are exposed only when a generic provider can be discovered without model-specific assumptions.

## Current support matrix

| Device family | Windows features | Fan telemetry | Keyboard backlight | Low-level fan control | Status |
| --- | --- | --- | --- | --- | --- |
| **ThinkPad X9-15 Gen 1 · 21Q6 / 21Q7** | ✅ | ✅ X9 EC + safe fallbacks | ✅ `IBMPmDrv` | ✅ Lenovo Auto + levels `1–7` | **Verified reference** |
| **Other ThinkPads** | ✅ | 🧪 Lenovo WMI/CIM when exposed | 🧪 `IBMPmDrv` when read/readback passes | Exact-profile only | **Beta / Untested** |
| **ThinkBook** | ✅ | 🧪 Lenovo WMI/CIM when exposed | 🧪 `EnergyDrv` when read/readback passes | Vendor provider only when independently supported | **Beta / Untested** |
| **Yoga** | ✅ | 🧪 Lenovo WMI/CIM when exposed | 🧪 compatible Lenovo PM provider when proven | Vendor provider only when independently supported | **Beta / Untested** |
| **IdeaPad** | ✅ | 🧪 Lenovo WMI/CIM when exposed | 🧪 `EnergyDrv` when proven | Vendor provider only when independently supported | **Beta / Untested** |
| **LOQ** | ✅ | 🧪 Lenovo WMI/CIM | 🧪 Lenovo PM provider | `LENOVO_GAMEZONE_DATA` only when firmware advertises support | **Beta / Untested** |
| **Legion** | ✅ | 🧪 Lenovo WMI/CIM | 🧪 Lenovo PM/lighting provider where exposed | `LENOVO_GAMEZONE_DATA` only when firmware advertises support | **Beta / Untested** |
| **Other Lenovo** | ✅ | 🧪 safe read-only discovery | 🧪 safe known-driver discovery | Disabled without a matching provider/profile | **Generic Lenovo Beta** |
| **Other Windows laptops** | ✅ where Windows exposes it | generic sensor providers only | Lenovo-specific backend unavailable | unavailable | **Generic** |

## Windows-level features

These capabilities can work without a model-specific Lenovo profile:

- Quiet / Balanced / Performance Windows power mode;
- display refresh-rate selection;
- automatic 60 Hz / maximum refresh policy;
- internal-panel brightness;
- adaptive brightness where supported;
- battery percentage and AC/battery state;
- charge/discharge rate in watts when ACPI exposes it;
- remaining/full battery energy in Wh and estimated health;
- smoothed time-to-full / time-remaining estimate;
- CPU/system temperature through trustworthy read-only providers;
- themes, tray operation, updates, startup settings and diagnostics.

If Windows does not expose a value, ThinkControl shows it as unavailable instead of inventing one.

## Lenovo provider discovery

ThinkControl `alpha.1` contains the beginning of a cross-Lenovo provider router.

### Keyboard

Known contracts include:

- **`IBMPmDrv`** — common on ThinkPads;
- **`EnergyDrv`** — used across multiple ThinkBook / IdeaPad / Yoga / LOQ-style platforms.

The driver is not selected merely because the model name looks compatible. ThinkControl first executes the known GET contract and requires a recognized Off / Low / High state. A write is accepted only when the state can be read back correctly afterward.

### Fan RPM

Read-only fan telemetry is probed through:

1. the exact verified X9 EC tachometer when that profile is active;
2. Lenovo `LENOVO_FAN_METHOD` / `Fan_GetCurrentFanSpeed`;
3. Lenovo `Lenovo_DT_GetCPUFan` / `Lenovo_DT_GetSYSFan` classes;
4. Windows `CIM_Tachometer`;
5. other trustworthy read-only sensor providers.

Missing WMI classes are treated as a normal “not supported here” result.

### Lenovo GameZone WMI

Legion, LOQ and some adjacent Lenovo platforms expose `LENOVO_GAMEZONE_DATA`. ThinkControl treats this as a capability-driven vendor provider: the class and relevant support query must exist before a control can be considered.

The mere presence of the class never authorizes every method on every Lenovo device.

See **[Lenovo Provider Model](LENOVO-PROVIDERS.md)** for the provider rules and family profiles.

## ThinkPad X9-15 Gen 1 · 21Q6 / 21Q7

The direct X9 fan backend is still more strictly gated than all Beta providers.

| Capability | Alpha.1 implementation |
| --- | --- |
| Fan RPM | X9 EC tachometer registers `0x84/0x85`; sparse polling |
| Fan state | X9 EC register `0x2F` |
| Lenovo Auto | `0x80` + readback verification |
| Manual fan control | discrete levels `1–7`; duplicate writes suppressed |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Service exit | normal shutdown attempts to return manual control to Lenovo Auto |
| Keyboard Off / Low / High | Lenovo PM driver + readback verification |
| Keyboard Auto | ThinkControl user-session policy over real hardware states |
| Breathing | rate-limited Low ↔ High |
| Reactive | local keyboard-activity pulse; no typed contents retained |
| Audio | experimental local loopback RMS; no audio retained |

The original X9 diagnostics went much deeper than a normal device profile because they were used to establish the first safe low-level ThinkControl backend. The findings are preserved in **[X9-15 Gen 1 research](research/x9-15-gen1.md)**.

## Why other Lenovo models do not need the same research depth

Most devices can be added or improved using existing provider knowledge instead of repeating the complete X9 investigation.

A normal Beta validation needs roughly:

1. exact Lenovo product name and four-character machine type;
2. inventory of relevant Lenovo drivers/services;
3. safe WMI/provider existence checks;
4. plausible read-only telemetry;
5. readback of reversible controls;
6. a report/support bundle from the actual laptop.

Deep ACPI dumps, driver analysis or Process Monitor traces are reserved for features for which no established Lenovo provider is available.

## Device identification

Support matching may use:

- manufacturer;
- model/product name;
- Lenovo machine type/model code;
- BIOS version when relevant;
- ACPI/PnP device IDs;
- presence/version of Lenovo providers;
- Windows display capabilities.

ThinkControl does **not** need a laptop serial number, asset tag, MAC address or disk serial for compatibility matching.

## Diagnostics for Beta and unknown devices

The app includes bounded and redacted local diagnostics with:

- provider/capability status;
- semantic operation outcome;
- Preview data;
- Export support bundle;
- Delete local diagnostics;
- structured GitHub bug reporting.

Automatic private diagnostics upload is **not enabled yet**. No GitHub PAT/private-repository secret is embedded in the desktop application.

See **[Diagnostics & Privacy](DIAGNOSTICS.md)**.

## Reporting a device

Use the structured form:

**[Open a ThinkControl bug report](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**

For a Beta laptop, a short report of **which controls work, which are unavailable and the exact model/machine type** can be enough to move compatibility forward. Attach an exported support bundle when useful.

## Promotion path

A device/capability normally progresses as:

```text
Generic
   ↓
Beta / Untested family provider
   ↓
Beta tested on real machine
   ↓
Verified exact/family capability
```

Writes are promoted capability by capability. Remote metadata can never turn an arbitrary EC register or unknown IOCTL into an executable write.
