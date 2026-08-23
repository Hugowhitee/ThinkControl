# Release checklist

This checklist is for the first public prerelease and later tagged builds.

## Automated gates

- [x] .NET solution restores and builds on Windows CI.
- [x] WPF UI snapshots render in dark and light themes.
- [x] Self-contained x64 UI publishes.
- [x] Self-contained x64 hardware service publishes.
- [x] Inno Setup produces a versioned installer.
- [x] Silent install succeeds in CI.
- [x] `ThinkControlService` reaches Running in CI.
- [x] Silent uninstall removes UI/service files and service registration.
- [x] SHA-256 checksum is generated.
- [x] Tagged prereleases use the visible title `ThinkControl v<version>`.

## Reference-device validation

The first alpha may be published as a prerelease before all items below are complete, but it must remain clearly labelled alpha until they pass on the ThinkPad X9-15 Gen 1 reference machine.

- [ ] Confirm installed service starts on the physical X9.
- [ ] Confirm CPU sensor name/value.
- [ ] Confirm fan RPM is stable with conservative polling.
- [ ] Confirm Lenovo Auto.
- [ ] Confirm manual fan levels 1–7.
- [ ] Confirm service/app close returns manual fan ownership safely.
- [ ] Confirm keyboard Off / Low / High.
- [ ] Tune and confirm Breathing against the real Lenovo fade.
- [ ] Confirm Auto and Reactive keyboard effects.
- [ ] Confirm sleep/resume behavior.
- [ ] Inspect installed UI at normal Windows scaling.

## Distribution

- [x] `version.json` is the source version.
- [x] Release-ready versions can create their exact `v<version>` tag from `main`.
- [x] The tag triggers the tested package workflow.
- [ ] Confirm the GitHub prerelease is visible after the release-candidate PR merges.
- [ ] Download and install the published asset once on the reference machine.

## Post-alpha work

- installer-managed pinned PawnIO prerequisite for devices that require it;
- additional ThinkPad provider validation;
- private opt-in diagnostics endpoint;
- autonomous custom fan-curve engine and stronger ungraceful-crash recovery.
