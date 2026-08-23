# ThinkControl installer

ThinkControl `v0.1.0-alpha.1` uses a self-contained x64 Inno Setup package.

## Release file

The current release installer is named:

```text
ThinkControl-Setup-0.1.0-alpha.1.exe
```

It contains:

- the ThinkControl WPF application;
- `ThinkControlService`;
- the .NET runtime required by both published applications;
- required ThinkControl notices;
- service registration, startup and uninstall logic.

Users do not need the .NET SDK or Desktop Runtime before installing ThinkControl.

## Installation behavior

Setup:

1. requests administrator permission;
2. installs ThinkControl under Program Files by default;
3. installs the UI and service payloads;
4. registers `ThinkControlService`;
5. starts the service;
6. offers to launch ThinkControl when setup completes;
7. creates a normal Windows uninstall entry.

During an upgrade, setup can request that a running ThinkControl UI process is closed before files are replaced.

## CI package test

The packaging workflow performs a full Windows lifecycle check:

```text
Build UI and service
        |
        v
Build installer
        |
        v
Silent install
        |
        v
Verify UI and service files
        |
        v
Verify ThinkControlService is Running
        |
        v
Silent uninstall
        |
        v
Verify files and service registration are removed
        |
        v
Generate SHA256SUMS.txt
```

Tagged releases use the same packaging path after the build and test stages pass.

## PawnIO

PawnIO is required for the current X9 EC fan backend, but `alpha.1` does not install it automatically.

A clean ThinkControl installation therefore remains valid when PawnIO is absent. Only the affected X9 EC capability is unavailable.

Before automated PawnIO installation is added, setup must:

- use a pinned accepted release;
- verify the exact package or checksum;
- use the normal signed distribution;
- clearly identify that a kernel hardware-access driver will be installed;
- report restart requirements separately from installation failures;
- verify the provider after installation.

ThinkControl should not remove PawnIO automatically during uninstall because other applications may use it.

## Lenovo and Intel components

ThinkControl Setup does not bundle or replace OEM platform software such as:

- Lenovo Power Management;
- Lenovo Intelligent Thermal Solution;
- Intel Innovation Platform Framework;
- Lenovo Vantage;
- Lenovo Service Bridge.

These components remain vendor-owned. ThinkControl may detect and use an installed provider when the relevant contract is supported.

## Release naming

`version.json` is the version source for the repository.

For `v0.1.0-alpha.1`:

```text
Tag       v0.1.0-alpha.1
Release   ThinkControl v0.1.0-alpha.1
Installer ThinkControl-Setup-0.1.0-alpha.1.exe
Checksum  SHA256SUMS.txt
```

Versions containing `alpha`, `beta` or another prerelease suffix are published as GitHub prereleases.

## Updates

The application can check GitHub Releases for a newer version. ThinkControl does not install a permanent updater service.

A more automated upgrade or rollback experience can be added later without changing the current service ownership model.

## Uninstall

The uninstaller removes ThinkControl-owned application files and unregisters the ThinkControl Windows service. It does not remove PawnIO or Lenovo and Intel platform software.

## Future packaging

A later release may use a smaller framework-dependent bootstrap installer. That would reduce initial package size but add runtime acquisition, network verification and rollback requirements.

The current self-contained package is retained while the hardware and release paths are still being validated.
