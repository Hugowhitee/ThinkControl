# ThinkControl installer plan

ThinkControl should have a **small native bootstrap installer** as the normal download. The bootstrapper must not depend on .NET because one of its jobs is installing the .NET Desktop Runtime when it is missing.

The goal is a one-file, one-UAC setup experience while keeping third-party and OEM software ownership clear.

See `docs/DEPENDENCIES.md` for the dependency policy.

## Bootstrap responsibilities

1. Validate supported Windows version and CPU architecture.
2. Detect the .NET 10 Desktop Runtime.
3. Detect the machine identity needed for safe device-profile selection.
4. Fetch the ThinkControl release manifest over HTTPS.
5. Download the application/service payload.
6. Verify payload SHA-256 and, when signing is enabled, Authenticode.
7. Install/replace the ThinkControl UI and service.
8. Determine whether the **verified** selected device profile needs PawnIO for supported capabilities.
9. If needed and the user opted in, download and verify the pinned official PawnIO setup and invoke its normal silent install.
10. Preserve an explicit reboot-required state when PawnIO or another prerequisite requests one.
11. Configure optional start-with-Windows behavior.
12. Offer `Launch ThinkControl` on the completion page.

The bootstrapper is also the on-demand elevated updater path for replacing the Windows service. There is no permanent updater service.

## No circular runtime dependency

Do not implement the bootstrapper as a framework-dependent .NET executable.

Acceptable implementation directions include a small native installer/bootstrap technology that can:

- perform HTTPS downloads;
- validate SHA-256;
- validate Authenticode/WinTrust;
- execute prerequisite installers and inspect exit codes;
- install/start/replace the Windows service;
- roll back a partially applied ThinkControl update.

The exact packaging technology can be chosen once the release format is stable; the behavior contract above is more important than the installer framework.

## Default install flow

### Page 1 — Install

Keep this short. A normal user should not see a wall of driver jargon.

```text
ThinkControl
A lightweight hardware companion for Lenovo ThinkPads

Install location   C:\Program Files\ThinkControl

Hardware support
[x] Install recommended hardware access

[ Advanced ]                              [ Install ]
```

`Install recommended hardware access` means PawnIO only when the selected **verified device profile** has capabilities that need it. Unknown ThinkPads must not receive low-level access just because the manufacturer is Lenovo.

### UAC

Prefer one elevation for the whole setup rather than multiple unrelated prompts.

### Progress

Use concrete steps:

```text
Preparing ThinkControl
Installing Microsoft .NET Desktop Runtime
Installing ThinkControl
Installing hardware access
Starting ThinkControl service
```

Hide steps that are already satisfied.

### Completion

```text
ThinkControl is ready

Hardware access   Full
Device support    ThinkPad X9-15 Gen 1 · Verified

[x] Launch ThinkControl
[ Finish ]
```

If a restart is required:

```text
ThinkControl is installed
Restart Windows to finish hardware access.

[ Restart now ]  [ Later ]
```

Windows-level features may still be usable before the restart.

## .NET Desktop Runtime

ThinkControl targets .NET 10 WPF, so the required prerequisite is the **.NET 10 Desktop Runtime**, not the SDK.

Bootstrap behavior:

1. Detect a compatible installed desktop runtime.
2. If missing, obtain the official Microsoft installer.
3. Verify Authenticode trust/publisher.
4. Install elevated using Microsoft's supported unattended mechanism.
5. Re-check runtime readiness before installing/launching the framework-dependent ThinkControl UI.

Do not tell users to visit the .NET website manually during a normal install.

## PawnIO

PawnIO is a device-conditional prerequisite, not a universal ThinkControl dependency.

For the X9 reference backend it is expected to enable verified low-level capabilities such as accurate EC telemetry and direct fan control once those backends are implemented.

Rules:

- normal signed release only;
- never the unrestricted/developer installation mode;
- explicit checkbox/consent before adding the kernel driver;
- download from the official project release path;
- exact accepted version and SHA-256 come from the ThinkControl release manifest;
- verify trust before execution;
- invoke normal documented silent install (`-install -silent`);
- handle success, failure and reboot-required exit codes distinctly;
- after install, probe the actual PawnIO service/driver instead of assuming setup succeeded.

ThinkControl should not uninstall PawnIO automatically because it can be shared with other hardware-monitoring software.

## OEM Lenovo/Intel components

The installer **does not automatically install** Lenovo Intelligent Thermal Solution, Lenovo Power Management, Intel IPF, Vantage or Lenovo Service Bridge.

Reasons:

- packages are model-specific and vendor-owned;
- Windows Update / Lenovo may already own their servicing lifecycle;
- matching the wrong package is riskier than leaving a feature unavailable;
- Vantage and Service Bridge are optional integrations, not ThinkControl dependencies.

After installation, ThinkControl can show a readiness action such as:

```text
Lenovo Power Management    Missing
Keyboard backlight control is unavailable.
[ Open Lenovo Drivers ]
```

A future verified Lenovo package resolver may improve this, but it must resolve exact packages by machine type and never guess from display names.

## Release manifest

The manifest should separate ThinkControl-owned payloads from external prerequisites.

Conceptually:

```json
{
  "version": "0.1.0",
  "channel": "stable",
  "app": {
    "url": "...",
    "sha256": "..."
  },
  "prerequisites": {
    "dotnetDesktop": {
      "major": 10,
      "publisher": "Microsoft Corporation"
    },
    "pawnIo": {
      "version": "2.2.0",
      "url": "official release asset",
      "sha256": "pinned hash",
      "installArgs": "-install -silent"
    }
  }
}
```

Do not add a real third-party SHA until it has been independently verified from the exact release asset used by the build/release pipeline.

## Update flow

The UI checks GitHub Releases at a low frequency and shows an update badge only when a newer allowed channel is available.

On user approval:

1. UI downloads/verifies metadata or asks bootstrapper to do so.
2. Elevated bootstrapper stops the service safely.
3. Active direct fan control returns to Lenovo Auto before replacement.
4. Bootstrapper stages the new payload.
5. Verify.
6. Atomically replace where possible.
7. Restart service/UI.
8. Roll back if startup validation fails.

No background updater service is installed.

## Uninstall

The uninstaller removes ThinkControl-owned components and offers to preserve user profiles.

It does not remove shared Lenovo/Intel drivers.

PawnIO remains by default because another application may use it. A future explicit cleanup action may remove PawnIO only after dependency checks and user confirmation.
