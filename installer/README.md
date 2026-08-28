# ThinkControl installer

ThinkControl uses a small x64 Inno Setup bootstrapper plus a separate framework-dependent application payload. `version.json` is the release version source of truth; this document describes the lifecycle contract rather than copying a particular alpha's filenames/hashes.

## Managed release assets

A release contains exactly:

```text
ThinkControl-Setup-<version>.exe
ThinkControl-Payload-<version>.zip
SHA256SUMS.txt
ui-overview.png
```

The complete visual-QA matrix remains an Actions artifact. Only the composed overview is a public release asset.

## Install contract

Setup:

1. requests elevation once for install/service work;
2. ensures the required .NET Desktop Runtime is present using the pinned values in `ThinkControl.iss`;
3. downloads the matching payload from the release endpoint;
4. verifies the payload SHA-256 compiled into that Setup build;
5. extracts only the verified UI/service payload;
6. registers and starts `ThinkControlService`;
7. creates the selected shortcuts/startup entry;
8. optionally launches ThinkControl when the interactive installation completes.

A clean interactive install allows selecting a destination directory. Existing/update installs preserve the previously selected location. Silent in-app updates reuse the existing installation location instead of assuming Program Files.

The installer is device-neutral. Optional hardware-provider setup/repair happens in the app and cannot grant unknown low-level write capability merely by installing a driver.

## Payload verification

Packaging creates the payload first, hashes it, then compiles its exact release URL and SHA-256 into Setup. A payload from another build/version therefore cannot pass the bootstrapper's integrity check.

CI may use the installer's local payload override, but the override must still match the compile-time hash. This exercises the same extraction/service/uninstall path before public assets exist.

## Hardware/provider prerequisites

Provider readiness is capability-specific. Missing PawnIO/Lenovo/OEM components may limit a related capability while Windows-generic features continue to work.

Installing a provider does not authorize firmware/EC writes. Writable behavior remains gated by reviewed provider code, capability checks, device/profile identity and recovery/readback rules.

Exact third-party runtime/provider versions and SHA-256 pins live in the installer source/workflows so there is one executable source of truth rather than duplicated values in Markdown.

## Update contract

The in-app updater stages Setup + Payload + checksums, verifies managed files and then performs one explicit elevation handoff. Background update checks never install software or open UAC automatically.

Before replacement, Setup closes the running UI when needed, stops the service, updates the verified payload and restarts service registration. Existing custom install location is preserved.

The repository intentionally keeps the legacy-updater compatibility smoke while released clients still depend on that contract. Do not delete the compatibility path just because its original alpha number is old.

## Uninstall contract

Normal full uninstall stops/removes `ThinkControlService`, owned shortcuts/startup state, install/update staging and ThinkControl-owned local application/service data according to the current installer policy. Shared OEM software/providers are not removed merely because ThinkControl used them.

Fan ownership cleanup remains a product safety concern: normal service/controller shutdown attempts to restore OEM/firmware ownership before low-level access closes.

## Validation

Pull requests touching application, installer, version, updater or branding code are covered by the package/installer workflows. The release path additionally requires:

- restore/build and tests;
- real Compact ↔ Advanced WPF shell smoke;
- visual-QA rendering/inspection;
- payload/hash/bootstrap construction;
- custom-location clean install;
- service start and IPC;
- in-place update/location preservation;
- intentionally supported legacy-updater compatibility;
- uninstall cleanup;
- immutable release asset/checksum verification.

See [release readiness](../docs/RELEASE_READINESS.md) for unfinished commercial/public-release work. The workflows and `ThinkControl.iss` are authoritative for executable packaging details.
