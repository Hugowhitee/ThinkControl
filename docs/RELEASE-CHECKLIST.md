# Release checklist

This checklist applies to tagged prereleases and later stable builds.

## Automated checks

- [x] Restore and build the .NET solution on Windows CI.
- [x] Render real WPF UI snapshots in Light and Dark themes.
- [x] Publish framework-dependent x64 UI and hardware service.
- [x] Enforce the combined uncompressed payload size budget.
- [x] Create a separate compressed `ThinkControl-Payload-<version>.zip`.
- [x] Compute the payload SHA-256 before compiling Setup.
- [x] Build a versioned Inno Setup web bootstrapper with the exact payload URL/hash.
- [x] Enforce a 5 MB hard installer-size budget.
- [x] Smoke-test the bootstrapper using the same SHA-verified payload through the CI local override.
- [x] Confirm `ThinkControlService` reaches `Running`.
- [x] Complete a silent uninstall.
- [x] Confirm extracted UI/service files and service registration are removed.
- [x] Generate SHA-256 checksums for installer and payload.
- [x] Keep application, tag, release, installer and payload versions aligned.
- [x] Render the release overview and full WPF gallery for QA without publishing QA PNGs as release downloads.

## Installer prerequisites

- [x] Detect an installed compatible .NET 10 Desktop Runtime.
- [x] Pin the Microsoft x64 runtime download and SHA-256 when acquisition is needed.
- [x] Detect an exact write-capable device profile before offering direct low-level hardware access.
- [x] Pin PawnIO 2.2.0 to the official release URL and Winget-published SHA-256 where that provider is relevant.
- [x] Keep PawnIO failure capability-scoped rather than blocking unrelated features or unsupported models.
- [x] Do not remove shared PawnIO during ThinkControl uninstall.

## Branding and Windows shell

- [x] Approved ThinkControl v3 master geometry is stored under `assets/brand/v3`.
- [x] Executable/installer ICO is byte-for-byte the canonical v3 Windows icon.
- [x] Tray ICO is byte-for-byte the canonical v3 mark icon.
- [x] README dark/light wordmarks are byte-for-byte canonical v3 assets.
- [x] WPF `BrandMark` uses the exact traced 1536×1536 v3 master geometry.
- [x] Legacy hand-drawn 64×64 TC geometry is removed.
- [x] Unused legacy app branding asset is removed.
- [x] Package CI rejects branding drift and legacy TC geometry.
- [x] Compact remains a fixed non-draggable tray flyout.
- [x] Advanced uses the native Windows title bar and taskbar entry.

## Device-provider and X9 hardware policy

- [x] Device support follows `Windows generic → OEM → family → exact model` rather than making the UI model-specific.
- [x] Profiles select providers; provider code owns lifecycle, validation, readback and write safety.
- [x] Broad OEM/family profiles cannot authorize an unverified low-level write merely through data/config.
- [x] `21Q6` / `21Q7` parsing identifies the X9-15 Gen 1 as the first reviewed low-level reference profile.
- [x] Direct X9 fan writes remain restricted to that exact verified profile.
- [x] RPM uses a real EC tachometer route and conservative polling; a second fan is never synthesized.
- [x] Normal service/controller disposal returns manual fan control to firmware/OEM Auto where possible.
- [x] Lenovo keyboard writes use known provider contracts with readback.
- [x] Installed Vantage ThinkKeyboard components are fallback-only and must validate.
- [x] Windows Quiet/Balanced/Performance remains the primary power-mode surface.
- [x] Verified X9 additionally coordinates LITSSvc AC 502/503/504 and DC 507/508/509 policy commands.
- [x] X9 Lenovo policy is semantic, profile-gated and not described as direct fan RPM/PWM control.

## Integration regressions

- [x] Compatibility diagnostics renders once in Settings.
- [x] Commercial Vantage resolves/opens the installed app before any Store fallback.
- [x] Update checking reports a friendly release-channel state instead of a raw GitHub 404.
- [x] Compact/Advanced dock actions use matching diagonal direction icons.
- [x] Native Windows maximize/restore state is owned by the standard Advanced title bar.
- [x] Every Advanced page shares the same left-anchored responsive content rail.
- [x] Hardware Setup, Notifications and telemetry-detail surfaces are part of deterministic visual QA.

## Release publication

- [x] `version.json` is the release version source.
- [x] Release tags use the exact `v<version>` form and point at the intended release commit.
- [x] The tag-triggered package workflow rejects a tag/version mismatch before packaging.
- [x] The tagged workflow runs build, visual QA, payload creation, installer compilation and installer/service lifecycle smoke testing before publication.
- [x] GitHub Releases publish only `ThinkControl-Setup-<version>.exe`, `ThinkControl-Payload-<version>.zip` and `SHA256SUMS.txt` as managed release assets.
- [x] Users normally need only Setup; payload/checksum are updater and verification infrastructure.
- [x] The in-app updater requires all three managed assets and SHA-256 verifies Setup and Payload before elevation.

## Physical X9 validation after alpha.12 publication

- [ ] Install the published `v0.1.0-alpha.10` Setup first when explicitly testing the in-app updater, then update to `v0.1.0-alpha.12` from inside ThinkControl.
- [ ] For a normal clean install, install the published `v0.1.0-alpha.12` Setup directly.
- [ ] Confirm the installer is only a few MB and fetches the matching payload from GitHub.
- [ ] Verify installer/payload hashes against `SHA256SUMS.txt`.
- [ ] Confirm the installed footprint is far below the old duplicate-runtime layout.
- [ ] Confirm X9 is identified as the reviewed low-level profile, not Beta/Untested.
- [ ] Confirm Hardware Setup distinguishes service Running, IPC reachability, PawnIO device access and provider readiness correctly.
- [ ] Confirm CPU/control-temperature sensor source and value.
- [ ] Confirm stable fan RPM, firmware/OEM Auto and manual levels 1 through 7.
- [ ] Confirm Quiet/Balanced/Performance policy behavior on AC and battery.
- [ ] Confirm keyboard Off/Low/High and supported effect behavior.
- [ ] Confirm Commercial Vantage direct launch.
- [ ] Confirm alpha.10 → alpha.12 in-app update including download, SHA-256 verification, UAC approval, app close, install and relaunch behavior.
- [ ] Confirm cancelling the UAC prompt leaves the running install untouched and does not loop prompts.
- [ ] Test sleep/resume and verify manual fan ownership returns safely to firmware/OEM Auto.
- [ ] Inspect Compact/Advanced at 100, 125 and 150 percent scaling.
- [ ] Confirm Compact cannot be dragged.
- [ ] Confirm Advanced native title bar, v3 icon, Snap Layouts and maximize/restore.

## Later release work

- broader Lenovo and non-Lenovo model validation;
- private opt-in diagnostics submission;
- broader accessibility and real-device scaling validation;
- Windows 10/LTSC compatibility evaluation only if a supported customer need justifies a separate test matrix;
- Authenticode signing and mature rollback handling.
