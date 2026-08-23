# Release checklist

This checklist applies to tagged prereleases and later stable builds.

## Automated checks

- [x] Restore and build the .NET solution on Windows CI.
- [x] Render WPF UI snapshots in Light and Dark themes.
- [x] Publish the self-contained x64 UI.
- [x] Publish the self-contained x64 hardware service.
- [x] Build a versioned Inno Setup installer.
- [x] Complete a silent install in CI.
- [x] Confirm `ThinkControlService` reaches `Running`.
- [x] Complete a silent uninstall.
- [x] Confirm ThinkControl files and service registration are removed.
- [x] Generate a SHA-256 checksum.
- [x] Keep the application, tag, release and installer versions aligned.

## X9 reference-device validation

The first alpha remains a prerelease until the relevant hardware behavior has been checked on the ThinkPad X9-15 Gen 1.

- [ ] Confirm the installed service starts normally.
- [ ] Confirm the CPU sensor source and value.
- [ ] Confirm fan RPM remains stable with conservative polling.
- [ ] Confirm Lenovo Auto.
- [ ] Confirm manual fan levels 1 through 7.
- [ ] Confirm normal app and service shutdown returns manual fan control safely.
- [ ] Confirm keyboard Off, Low and High.
- [ ] Tune and confirm Breathing against the physical keyboard transition.
- [ ] Confirm Auto and Reactive keyboard effects.
- [ ] Confirm sleep and resume behavior.
- [ ] Inspect the installed UI at common Windows scaling levels.

## Distribution

- [x] `version.json` is the release version source.
- [x] Release-ready builds can create the exact `v<version>` tag from `main`.
- [x] The tag starts the tested packaging workflow.
- [ ] Install the published release asset on the X9 reference device.
- [ ] Verify the published checksum against the downloaded installer.

## Documentation

Before a release is promoted beyond alpha:

- [ ] README support claims match the actual release.
- [ ] Device Support reflects the tested hardware state.
- [ ] Hardware Safety reflects all writable providers in the release.
- [ ] Installer documentation matches the shipped prerequisite behavior.
- [ ] Third-party notices include every redistributed dependency that requires notice.

## Later release work

The following items are not required for making the first alpha downloadable, but are expected before a mature release where applicable:

- installer-managed pinned PawnIO setup;
- additional Lenovo model validation;
- private opt-in diagnostics submission;
- autonomous fan curves with lifecycle and recovery safeguards;
- broader accessibility and real-device scaling validation.
