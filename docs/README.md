# ThinkControl documentation

This directory contains product, hardware and development documentation for ThinkControl.

For installation, start with the main [README](../README.md) and the [Releases](https://github.com/Hugowhitee/ThinkControl/releases) page.

## Documentation index

| Document | Purpose |
| --- | --- |
| [Device Support](DEVICE-SUPPORT.md) | Current device and capability support |
| [Lenovo Providers](LENOVO-PROVIDERS.md) | Lenovo driver, WMI and hardware provider model |
| [Hardware Safety](HARDWARE-SAFETY.md) | Rules for privileged and low-level hardware access |
| [Architecture](ARCHITECTURE.md) | Project boundaries, service architecture and IPC |
| [Product Specification](PRODUCT.md) | Current product behavior and scope |
| [Design System](DESIGN.md) | UI layout, typography, controls and visual rules |
| [UI Editing](UI_EDITING.md) | Editing WPF/XAML with Visual Studio and Blend |
| [Diagnostics and Privacy](DIAGNOSTICS.md) | Local diagnostics, redaction and support bundles |
| [Dependencies](DEPENDENCIES.md) | Runtime, Lenovo software and PawnIO requirements |
| [Alpha Testing](ALPHA-TESTING.md) | Physical validation on the X9 reference device |
| [Release Checklist](RELEASE-CHECKLIST.md) | Packaging and release checks |
| [v0.1 Acceptance](V0.1-ACCEPTANCE.md) | Acceptance criteria for the first alpha |

## Compatibility terminology

### Verified

The relevant capability has been tested on the actual device or provider combination used by ThinkControl. The current reference device is the ThinkPad X9-15 Gen 1 with machine type `21Q6` or `21Q7`.

### Beta

ThinkControl recognizes the device family and has known providers that are reasonable to probe, but the exact model has not been fully validated by this project. Individual controls still require a successful capability check.

### Generic

Only platform-independent providers are assumed. Windows-level features may work without any Lenovo-specific profile.

These labels describe validation depth. They do not grant low-level hardware access by themselves.

## Device profiles

Machine-readable Lenovo profiles live under `devices/Lenovo/`.

```text
devices/Lenovo/
|-- ThinkPad/
|   |-- X9-15-Gen1/
|   `-- _family/
|-- ThinkBook/_family/
|-- Yoga/_family/
|-- IdeaPad/_family/
|-- LOQ/_family/
|-- Legion/_family/
`-- _generic/
```

Profiles identify provider candidates. Exact EC registers or unverified write payloads are not loaded from remote metadata.

## Device research

The detailed X9 research record is available at [research/x9-15-gen1.md](research/x9-15-gen1.md). It documents the evidence used to establish the first verified low-level provider.

Most new devices should not require the same level of reverse engineering. Useful validation data normally includes the exact model and machine type, relevant Lenovo drivers and services, provider availability, plausible read-only telemetry and readback results for reversible controls.

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) for compatibility reports. An exported ThinkControl support bundle can be attached when needed.
