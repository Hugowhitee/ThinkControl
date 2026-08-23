# Device support

> **Applies to ThinkControl `v0.1.0-alpha.1`.** This page describes what the current executable actually does. Future compatibility ideas live in the roadmap, not in the support table.

ThinkControl keeps the same main product areas visible across Windows laptops, but hardware-specific controls are only enabled when the current build has a backend that is safe to use on that device.

## Current support matrix

| Device | Machine type | Windows-level features | Fan telemetry/control | Keyboard backlight | Status in `alpha.1` |
| --- | --- | --- | --- | --- | --- |
| **Lenovo ThinkPad X9-15 Gen 1** | **21Q6 / 21Q7** | Available where Windows exposes them | X9 EC backend implemented | Lenovo PM backend implemented | **Reference device · on-device alpha validation still required** |
| Other ThinkPads | varies | Available where Windows exposes them | **Not enabled in alpha.1** | **Not enabled in alpha.1** | Not validated |
| Other Lenovo laptops | varies | Available where Windows exposes them | Not enabled | Not enabled | Not validated |
| Other Windows laptops | varies | Available where Windows exposes them | Lenovo-specific backend unavailable | Lenovo-specific backend unavailable | Not validated |

The application is not a separate “lite edition” on an unvalidated machine: Performance, Fans, Display, Keyboard, Battery, System, Updates and Settings remain visible. In the current alpha, however, an unavailable low-level provider is shown as unavailable rather than being enabled speculatively.

That distinction matters: **same UI does not mean blind hardware writes.**

## What works without a model-specific ThinkPad backend

These capabilities use Windows APIs/telemetry and can work on any Windows laptop that exposes the relevant interface:

- Quiet / Balanced / Performance Windows power mode;
- display refresh-rate selection;
- automatic 60 Hz / maximum refresh policy;
- internal-panel brightness;
- adaptive brightness where Windows/platform support exists;
- battery percentage and AC/battery state;
- live charge/discharge rate in watts when ACPI battery telemetry exposes it;
- remaining/full battery energy in Wh and health where available;
- smoothed charge/discharge ETA;
- CPU/system temperature through safe read-only sensor providers where available;
- themes, tray operation, updates, startup settings and diagnostics.

If Windows does not expose a value, ThinkControl shows it as unavailable instead of inventing one.

## ThinkPad X9-15 Gen 1 · 21Q6 / 21Q7

The current low-level service explicitly recognizes Lenovo machine types `21Q6` and `21Q7` before allowing X9-specific writes.

| Capability | Alpha.1 implementation |
| --- | --- |
| Fan RPM | Read from X9 EC tachometer registers `0x84/0x85`; polling is deliberately sparse |
| Fan state | Read from X9 EC register `0x2F` |
| Lenovo Auto | Write `0x80` and verify read-back |
| Manual fan control | Discrete levels `1` through `7`; duplicate writes are suppressed |
| Fan off | `0x00` is blocked |
| Unverified override | `0x40` family is never written |
| Service exit | Manual ownership is returned to Lenovo Auto during normal service shutdown/disposal |
| Keyboard Off / Low / High | Lenovo PM driver backend with read-after-write verification |
| Keyboard Auto | ThinkControl user-session policy over Off / Low / High |
| Breathing | Rate-limited Low ↔ High policy; uses Lenovo's own transition behavior if firmware fades between levels |
| Reactive | Local keyboard-activity pulse; typed key contents are not stored |
| Audio | Experimental local loopback RMS response; audio samples are not stored |

The code and installer build successfully in CI, but **the current ThinkControl service/UI combination still needs a final physical alpha pass on the reference X9 before this backend should be called release-validated**. That pass includes RPM visibility, levels `1–7`, Lenovo Auto recovery, keyboard levels/effects, sleep/resume and uninstall/service cleanup on the actual machine.

## What “Experimental” means

ThinkControl has compatibility-state types for `Verified`, `Experimental` and `Not validated`, and diagnostics are designed to collect evidence for future devices.

**In `v0.1.0-alpha.1`, ThinkControl does not yet automatically promote unknown ThinkPads into writable Experimental fan/keyboard providers.** The current production low-level controller is still X9-gated.

A future provider may become Experimental only when it is compiled into ThinkControl and has all of the following:

1. a non-destructive/read-only discovery path;
2. structurally valid and plausible returned state;
3. conflict detection where relevant;
4. known semantic writes rather than arbitrary register/IOCTL access;
5. mandatory verification/read-back where applicable;
6. a defined restore/fail-safe path.

This future expansion must happen provider by provider. A remote metadata file can never turn arbitrary EC addresses or IOCTLs into executable writes.

## Device identification

Compatibility matching may use non-unique hardware information such as:

- manufacturer;
- model/product name;
- machine type/model code;
- BIOS version when relevant;
- ACPI device IDs;
- presence/version of required providers;
- Windows display capabilities.

ThinkControl does not need a laptop serial number, asset tag, MAC address or disk serial for support matching.

## Diagnostics for unsupported devices

The current app includes:

- bounded local diagnostic event recording;
- redaction/allowlisting;
- Preview data;
- Export support bundle;
- Delete local diagnostics;
- a structured GitHub bug-report form.

Automatic upload to the planned private diagnostics inbox is **not implemented yet**, so `Send diagnostics now` remains unavailable until a project-side endpoint exists. No GitHub PAT or private-repository credential is embedded in the desktop app.

See [Diagnostics and privacy](DIAGNOSTICS.md).

## Reporting a device

Use the structured issue form:

[Open a ThinkControl bug report](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)

The form supports common ThinkPad families plus **Other ThinkPad**, **Other Lenovo** and **Other laptop / PC**, followed by a free-form exact model field. Screenshots and exported support bundles can be attached when useful.

## Compatibility roadmap

After the X9 alpha is physically validated, compatibility work can expand in this order:

1. identify ThinkPads that expose an already-known provider;
2. add safe read-only probes and diagnostics;
3. validate telemetry on real machines;
4. enable writes only for provider/device combinations with read-back and recovery evidence;
5. promote those capabilities from Not validated → Experimental → Verified.

The goal remains broad ThinkPad support, but the support table above is the authoritative description of what the current build does today.
