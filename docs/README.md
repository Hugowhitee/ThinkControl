# ThinkControl documentation

This is the technical documentation hub for ThinkControl.

If you only want to install the app, start at the main **[README](../README.md)** and **[Releases](https://github.com/Hugowhitee/ThinkControl/releases)** page.

## Start here

| Topic | Document | Use it for |
| --- | --- | --- |
| Device compatibility | [Device Support](DEVICE-SUPPORT.md) | Verified, Beta / Untested and generic device behavior |
| Lenovo hardware backends | [Lenovo Provider Model](LENOVO-PROVIDERS.md) | How ThinkControl discovers Lenovo capabilities safely |
| Safety rules | [Hardware Safety](HARDWARE-SAFETY.md) | Rules for EC, drivers, IOCTLs and hardware writes |
| Architecture | [Architecture](ARCHITECTURE.md) | UI/service/core/hardware boundaries |
| Product behavior | [Product Specification](PRODUCT.md) | What ThinkControl is intended to do |
| UI | [Design](DESIGN.md) · [UI Editing](UI_EDITING.md) | Visual rules and Blend/XAML editing |
| Diagnostics | [Diagnostics & Privacy](DIAGNOSTICS.md) | Local logs, redaction and support bundles |
| Dependencies | [Dependencies](DEPENDENCIES.md) | Windows/Lenovo/PawnIO dependency model |
| Release | [Release Checklist](RELEASE-CHECKLIST.md) | Packaging and prerelease acceptance |
| Alpha testing | [Alpha Testing](ALPHA-TESTING.md) | Physical validation and reporting |

## Device research

### Verified reference

- **[ThinkPad X9-15 Gen 1 · 21Q6 / 21Q7](research/x9-15-gen1.md)** — detailed hardware research, including EC fan registers, LITS/ThinkSmartSense observations, Lenovo PM driver findings and negative fan-interface results.

The X9 research record is intentionally much deeper than a normal Beta profile. New Lenovo families do **not** need the same reverse-engineering depth before ThinkControl can offer Windows-level features or capability-probed Lenovo providers.

## Compatibility levels

### ✅ Verified

A real machine has been tested and the relevant hardware provider behavior has been validated. The first reference profile is the ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`).

### 🧪 Beta / Untested

ThinkControl recognizes the Lenovo family and knows which established providers are appropriate to probe, but that exact model has not been physically validated by the ThinkControl project.

Beta does **not** authorize guessed low-level writes. Each provider must still pass its own probe/read/readback contract.

### ⚪ Generic

ThinkControl has no Lenovo family profile for the machine. Safe Windows APIs can still work; Lenovo-specific hardware controls activate only if a generic provider contract can prove itself without model-specific assumptions.

## Family profiles

Machine-readable profiles live under `devices/Lenovo/`:

```text
devices/Lenovo/
├─ ThinkPad/
│  ├─ X9-15-Gen1/        verified exact profile
│  └─ _family/           Beta / Untested family profile
├─ ThinkBook/_family/
├─ Yoga/_family/
├─ IdeaPad/_family/
├─ LOQ/_family/
├─ Legion/_family/
└─ _generic/
```

Family profiles define **provider candidates**, not unrestricted device access.

## Contributing device support

Useful evidence for a new Lenovo model is usually much smaller than the original X9 investigation:

1. exact product name and four-character Lenovo machine type;
2. which Lenovo services/drivers are present;
3. which safe WMI/provider probes exist;
4. whether read-only telemetry returns plausible values;
5. whether a reversible control can be read back after a change;
6. a support bundle or bug report from the real machine.

A deep ACPI/driver trace is only needed when no established provider exists or a capability behaves differently from known Lenovo families.

Use the **[bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)** for validation reports and attach a ThinkControl support bundle when useful.
