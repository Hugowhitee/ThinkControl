# Release checklist

This checklist applies to tagged prereleases and later stable builds.

## Automated checks

- [x] Restore and build the .NET solution on Windows CI.
- [x] Render WPF UI snapshots in Light and Dark themes.
- [x] Publish the framework-dependent x64 UI.
- [x] Publish the framework-dependent x64 hardware service.
- [x] Enforce the combined payload size budget.
- [x] Build a versioned Inno Setup bootstrap installer.
- [x] Enforce the installer size budget.
- [x] Complete a silent install in package validation.
- [x] Confirm `ThinkControlService` reaches `Running`.
- [x] Complete a silent uninstall.
- [x] Confirm ThinkControl files and service registration are removed.
- [x] Generate a SHA-256 checksum.
- [x] Keep the application, tag, release and installer versions aligned.

## Installer prerequisites

- [x] Detect an installed compatible .NET 10 Desktop Runtime.
- [x] Pin the Microsoft x64 runtime download and SHA-256 when acquisition is needed.
- [x] Detect the X9 reference machine type before offering direct EC hardware access.
- [x] Pin PawnIO 2.2.0 to the official release URL and Winget-published SHA-256.
- [x] Keep PawnIO failure capability-scoped rather than blocking unrelated ThinkControl features.
- [x] Do not remove shared PawnIO during ThinkControl uninstall.

## Branding and Windows shell

- [x] Approved ThinkControl v3 master geometry is stored in the repository.
- [x] Executable and installer use the approved Windows app-icon export.
- [x] Tray uses the approved transparent/status icon exports.
- [x] README uses the approved vector wordmark rather than the legacy recreation.
- [x] Compact remains a fixed tray flyout.
- [x] Advanced uses the native Windows title bar and taskbar entry.

## X9 reference-device validation

The first alpha remains a prerelease until the relevant hardware behavior has been checked on the ThinkPad X9-15 Gen 1.

- [ ] Confirm setup recognizes machine type `21Q6` / `21Q7` and the app reports the verified profile.
- [ ] Confirm the installed service starts normally.
- [ ] Confirm the CPU sensor source and value.
- [ ] Confirm fan RPM remains stable with conservative polling.
- [ ] Confirm Lenovo Auto.
- [ ] Confirm manual fan levels 1 through 7.
- [ ] Confirm normal app and service shutdown returns manual fan control safely.
- [ ] Confirm keyboard Off, Low and High using the available Lenovo provider/fallback.
- [ ] Tune and confirm Breathing against the physical keyboard transition.
- [ ] Confirm Auto and Reactive keyboard effects.
- [ ] Confirm Commercial Vantage opens the installed application directly.
- [ ] Confirm update checking does not surface a raw 404.
- [ ] Test sleep and resume behavior.
- [ ] Inspect the installed UI at common Windows scaling levels.
- [ ] Confirm Compact cannot be dragged away from its tray position.
- [ ] Confirm Advanced native minimize/maximize/restore/close and Snap Layout behavior.

## Distribution

- [x] `version.json` is the release version source.
- [x] `releaseReady` can keep an unfinished version from being auto-published.
- [x] Release-ready builds can create the exact `v<version>` tag from `main`.
- [x] The tag starts the tested packaging workflow.
- [ ] Install the final published release asset on the X9 reference device.
- [ ] Verify the published checksum against the downloaded installer.

## Documentation

Before a release is promoted beyond alpha:

- [x] README packaging description matches the bootstrap installer architecture.
- [x] Dependency documentation matches .NET/PawnIO acquisition behavior.
- [x] Architecture documentation matches Compact versus native Advanced chrome.
- [ ] Device Support reflects all physically tested hardware states.
- [ ] Hardware Safety reflects every writable provider in the release.
- [ ] Third-party notices include every redistributed dependency that requires notice.

## Later release work

The following items are not required for making the first alpha downloadable, but are expected before a mature release where applicable:

- additional Lenovo model validation;
- private opt-in diagnostics submission;
- autonomous fan curves with lifecycle and recovery safeguards;
- broader accessibility and real-device scaling validation;
- Authenticode signing and mature update/rollback handling.