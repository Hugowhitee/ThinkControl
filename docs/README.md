# ThinkControl documentation

Keep this directory small and current. Historical alpha checklists, generated screenshots and release-verification transcripts belong in Git history, GitHub Releases or Actions artifacts rather than as competing sources of truth.

## Current product and engineering

| Document | Purpose |
| --- | --- |
| [Product](PRODUCT.md) | Current user-facing product behavior and scope |
| [Architecture](ARCHITECTURE.md) | Project boundaries, IPC, provider ownership and shell model |
| [Device support](DEVICE-SUPPORT.md) | Current capability/support matrix and validation levels |
| [Hardware safety](HARDWARE-SAFETY.md) | Non-negotiable rules for privileged and low-level hardware access |
| [Cooling design](COOLING-DESIGN.md) | Fan ownership, supervised curves, temporary tests and calibration |
| [Design system](DESIGN.md) | Shared UI hierarchy, typography, icons and layout rules |
| [Diagnostics and privacy](DIAGNOSTICS.md) | Local diagnostics, redaction and reporting boundaries |
| [Device profiles](../devices/README.md) | Generic → OEM → family → model profile architecture |

## Release and development

| Document | Purpose |
| --- | --- |
| [Release readiness](RELEASE_READINESS.md) | **Single persistent roadmap/handoff** for unfinished release and commercial-readiness work |
| [Alpha testing](ALPHA-TESTING.md) | Physical validation checklist for the current X9 reference build |
| [Visual QA](VISUAL-QA.md) | Deterministic WPF screenshot contract and review rules |
| [Installer](../installer/README.md) | Installer/updater/uninstaller lifecycle and packaging contract |
| [Agent workflow](../AGENTS.md) | Repository rules for contributors and coding agents |

The executable release gates live in `.github/workflows/`, `tools/` and the test projects. If prose conflicts with an executable safety/release gate, investigate the mismatch rather than creating another checklist.

## Hardware research/reference

These files preserve evidence that still explains provider behavior. They are reference material, not general authorization for hardware writes.

| Document | Purpose |
| --- | --- |
| [ThinkPad X9-15 Gen 1 research](research/x9-15-gen1.md) | Physical/driver evidence behind the verified X9 provider |
| [Lenovo provider research](research/lenovo-providers.md) | Known Lenovo provider families and capability boundaries |

## Documentation ownership

- `README.md` at repository root describes the current public product.
- `PRODUCT.md` describes current behavior, not release history.
- `RELEASE_READINESS.md` is the only tracked forward-looking release checklist/roadmap.
- `ALPHA-TESTING.md` is the physical-device validation checklist.
- Published release history and checksums live with immutable GitHub Releases and Actions, not copied Markdown files.
- Generated visual-QA screenshots remain short-lived CI artifacts; do not commit screenshot dumps under `docs/`.
- Model-specific registers, commands and observations belong in provider research/safety documentation, not generic UI/product docs.
