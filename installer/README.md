# ThinkControl installer

> **Current packaging for `v0.1.0-alpha.1`.** The first alpha uses a self-contained x64 Inno Setup installer. The earlier small bootstrapper design is a future optimization, not the current download.

## Current alpha installer

The release pipeline publishes one Windows setup file:

```text
ThinkControl-Setup-0.1.0-alpha.1.exe
```

It contains:

- self-contained ThinkControl WPF UI;
- self-contained ThinkControl Windows service;
- the .NET runtime required by those published binaries;
- ThinkControl third-party notices;
- service installation/start/uninstall logic.

A user therefore does **not** need to install the .NET SDK or Desktop Runtime manually before running the alpha setup.

## Install behavior

The current Inno Setup package:

1. requires administrative elevation for installation;
2. installs ThinkControl under Program Files by default;
3. installs the UI and hardware service as separate payload directories;
4. registers `ThinkControlService` as the Windows service;
5. starts the service;
6. offers **Launch ThinkControl** on the completion page;
7. provides a normal Windows uninstaller.

If a previous ThinkControl UI process is running during upgrade, setup can request that it be closed rather than silently failing with a vague “currently running” state.

## What CI verifies

The `Package ThinkControl` workflow performs a real Windows lifecycle test on every release candidate:

```text
build UI + service
       ↓
build Inno Setup installer
       ↓
silent install into a clean test directory
       ↓
verify ThinkControl.UI.exe
verify ThinkControl.Service.exe
       ↓
wait for ThinkControlService = Running
       ↓
silent uninstall
       ↓
verify files removed
verify service registration removed
       ↓
generate SHA256SUMS.txt
```

The public prerelease is created only by the tag packaging path after the same build/package logic succeeds.

## PawnIO in alpha.1

PawnIO is required for the current X9 EC fan backend, but **alpha.1 does not yet install PawnIO automatically**.

This means a clean installation can successfully install/run ThinkControl while X9 fan EC telemetry/control remains unavailable until the verified PawnIO prerequisite exists on the machine.

ThinkControl must not silently fetch an unpinned kernel driver just to make the first installer look more complete.

Future one-click prerequisite handling must:

- use the normal signed PawnIO distribution only;
- use an exact accepted version;
- pin and verify the exact downloaded asset/hash;
- verify publisher/trust where available;
- tell the user that hardware access installs a kernel driver;
- distinguish success, failure and restart-required states;
- probe the actual provider after installation.

PawnIO should not be removed automatically on ThinkControl uninstall because other monitoring software can share it.

## Lenovo / Intel components

The setup does not bundle or replace Lenovo/Intel platform software such as:

- Lenovo Power Management;
- Lenovo Intelligent Thermal Solution;
- Intel Innovation Platform Framework;
- Lenovo Vantage;
- Lenovo Service Bridge.

These remain vendor-owned. ThinkControl diagnoses missing capabilities or links to official support rather than mirroring model-specific OEM installers without a verified package-resolution flow.

## Release naming

`version.json` is the source version. A release-ready merge to `main` creates the exact tag:

```text
v0.1.0-alpha.1
```

The tag packaging workflow verifies the version match and publishes:

```text
Release title: ThinkControl v0.1.0-alpha.1
Installer:     ThinkControl-Setup-0.1.0-alpha.1.exe
Checksum:      SHA256SUMS.txt
```

Versions containing `-alpha`, `-beta`, etc. are created as GitHub prereleases.

## Updates

The current UI can check GitHub Releases for a newer version. ThinkControl does not install a permanent updater service.

A polished in-app upgrade/rollback flow can be expanded later; alpha.1 focuses on a reliable normal installer/uninstaller and explicit release downloads.

## Uninstall

The current uninstaller removes ThinkControl-owned UI/service files and unregisters the ThinkControl Windows service. The lifecycle is smoke-tested in CI.

It does not remove Lenovo/Intel platform components or PawnIO.

## Future smaller bootstrapper

A later stable release can replace the self-contained setup with a small native/bootstrap download that installs/detects the .NET Desktop Runtime and downloads a verified framework-dependent ThinkControl payload.

That design can reduce the initial installer size, but it adds another network/update/rollback layer. It is intentionally deferred until the core hardware and release path are stable.

If implemented later, it must verify all downloaded payloads and remain an on-demand updater path rather than introducing an always-running updater service.
