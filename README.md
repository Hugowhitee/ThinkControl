# ThinkControl

**A lightweight hardware companion for Lenovo ThinkPads.**

ThinkControl is a small Windows tray utility for fast, trustworthy control of everyday ThinkPad hardware features. The ThinkPad X9-15 Gen 1 (21Q6/21Q7) is the first reference machine, but the codebase is designed around capability detection and verified device profiles rather than model-name assumptions.

## Product principles

- **Capabilities, not assumptions.** Features appear only when a verified backend exists.
- **Safe by default.** Unknown ThinkPads get Windows-level read-only/safe features; unverified hardware writes stay disabled.
- **Small daily-driver UI.** Quiet / Balanced / Performance, temperature, real RPM, display, keyboard and battery status should be fast to reach.
- **Privilege separation.** The WPF UI runs as the user. A small privileged service owns verified low-level hardware operations.
- **Fail back to Lenovo.** Direct fan control must return to Lenovo Auto on failure, service stop, sleep/hibernate and shutdown.
- **No fake precision.** The X9 fan interface exposes discrete EC levels, not continuous 0-100% PWM.

## Repository status

The repository is currently in the **v0.1 foundation phase**. This phase intentionally contains no EC write implementation. It establishes architecture, device-support rules, safety boundaries, build tooling and the first X9 device profile before hardware features are added.

See:

- `docs/PRODUCT.md` — product scope and roadmap
- `docs/ARCHITECTURE.md` — process, privilege and provider architecture
- `docs/HARDWARE-SAFETY.md` — mandatory hardware-write safety rules
- `docs/DEVICE-SUPPORT.md` — capability and device-profile model
- `docs/DESIGN.md` — UI rules
- `docs/V0.1-ACCEPTANCE.md` — explicit v0.1 completion criteria
- `docs/research/x9-15-gen1.md` — preserved X9 hardware research

## Planned structure

```text
ThinkControl/
  src/
    ThinkControl.UI/
    ThinkControl.Service/
    ThinkControl.Core/
    ThinkControl.Hardware/
    ThinkControl.DeviceProfiles/
  devices/
    Lenovo/ThinkPad/X9-15-Gen1/
  docs/
    research/
  installer/
  tests/
  .github/workflows/
```

## Technology baseline

- Windows desktop app: WPF
- Runtime: .NET 10 LTS
- Hardware privilege boundary: Windows service
- Local IPC: Windows-authenticated named pipe
- Low-level access, when required and verified: PawnIO rather than a custom kernel driver
- Distribution goal: small bootstrap installer + downloaded app payload
- Updates: GitHub Releases, explicit user action, no permanent updater service

## Reference repositories

`Hugowhitee/X9-Helper` and `Hugowhitee/Thinkpad_Fancontrol` are research sources only. ThinkControl does not inherit their architecture by default.
