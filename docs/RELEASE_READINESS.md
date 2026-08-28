# ThinkControl release-readiness roadmap

This is the handoff checklist for future ThinkControl development chats. Keep it current. Do not call a milestone complete until the stated validation is real.

## Current release: alpha.30 hotfix / polish

### User-facing regressions
- [x] Home battery visual lifted/enlarged and remaining-time text given its own readable line.
- [x] Compact quick-control ComboBoxes have enough vertical room; bottom border must not clip.
- [x] Compact no longer auto-hides simply because focus moves to Chrome or another app. Explicit close/tray toggle still hides it.
- [x] Advanced keeps normal Windows focus behavior. It is not Topmost and must not auto-hide on deactivation.
- [x] Track control prefers the active Windows media session for Previous / Next / Play-Pause, with media-key fallback.
- [x] Optional Track-center Play/Pause uses a visible center zone plus a bounded deliberate hold/release and low-travel guard.
- [x] Track control carries the physical start position so off-center stationary touches cannot trigger the center action.
- [x] Shared ThinkControl scrollbar replaces stock WPF scrollbars on scrollable app surfaces.

### Touchpad corner launch zones
- [x] Launch zones are a separate subsystem from the four precision edge gestures.
- [x] Only `Off`, `Compact`, and `Advanced` are valid corner actions.
- [x] Both corners default to Off for existing and new users.
- [x] A corner launch requires a start inside the configured top-corner zone plus deliberate diagonal inward travel.
- [x] Corner tap alone does nothing.
- [x] Normal vertical scrolling / along-edge movement from a corner does nothing.
- [x] Edge action picker no longer offers ThinkControl launch actions; launch actions live only in the corner-launch UI.
- [x] Compact and Advanced use dedicated simple window glyphs; do not reuse the old Compact gesture icon.
- [ ] Fresh snapshot must visibly show both configured corner zones and an active corner launch.
- [ ] Manually inspect corner-zone shape, spacing, neutral state, active accent state, and minimum-width Touchpad layout.

### Manual fan safety
- [x] First manual Apply remembers the currently selected cooling profile.
- [x] Manual percentage and raw verified EC-step tests share one temporary-test lifecycle.
- [x] `End test` immediately restores the prior profile.
- [x] Manual tests auto-end after 30 seconds.
- [x] Leaving the Fans page ends the manual test.
- [x] Profile selector is disabled while a manual test is active.
- [x] Restore falls back to firmware Auto if the saved profile cannot be restored.
- [x] Raw EC controls and X9 calibration UI are hidden unless the verified X9 EC provider path is active.
- [ ] Add/inspect a deterministic visual-QA state showing the active temporary test, countdown, and End test control.

### Alpha.30 release gate
- [x] Build / unit tests / WPF shell smoke / package / installer reliability have passed on an intermediate alpha.30 head.
- [ ] Re-run all three exact-head workflows after the final QA/installer changes.
- [ ] Manually inspect fresh Home, Compact, Fans, Touchpad, minimum-width, light-theme, OSD and scrollbar screenshots.
- [ ] Keep `releaseReady=false` until all above checks pass.
- [ ] Set `releaseReady=true` only after the pre-readiness exact-head gate is green.
- [ ] Re-run CI + package + installer on the readiness head.
- [ ] Squash-merge the one PR.
- [ ] Verify `main` equals the squash SHA.
- [ ] Verify `v0.1.0-alpha.30` points exactly at that `main` SHA.
- [ ] Verify immutable prerelease and exactly the managed assets/checksums.

## Installer / updater / uninstaller before public paid release

### Install experience
- [ ] Explicitly keep the Inno Setup directory-selection page enabled. Do not rely on installer defaults.
- [ ] Clean install allows changing the install location.
- [ ] Update preserves the existing/custom install location.
- [ ] Silent updater uses the existing location without prompting.
- [ ] Install can optionally create desktop shortcut and start with Windows.
- [ ] Diagnostics/device-improvement consent is clearly shown during install and is also available in Settings.
- [ ] Install failure never destroys the previous working payload; staged update rollback remains tested.
- [ ] Signed installer and binaries before commercial/public release.
- [ ] SmartScreen/reputation plan documented and tested.

### Uninstall cleanup
- [ ] Stop and delete `ThinkControlService` reliably.
- [ ] Remove startup Run entry, shortcuts, install payload, update staging and backup payloads.
- [ ] Remove `%LOCALAPPDATA%\ThinkControl` (settings, diagnostics, crash queue, battery/update state, prepared device reports) when performing a full clean uninstall.
- [ ] Remove `%PROGRAMDATA%\ThinkControl` service logs on full clean uninstall.
- [ ] Remove `HKCU\Software\ThinkControl` local preferences/consent state on full clean uninstall.
- [ ] Future account/auth credentials must live in Windows Credential Manager / OS-protected storage and be removed on sign-out/full uninstall.
- [ ] Verify uninstall leaves no running service/process, startup entry, shortcuts, update staging or owned local data.
- [ ] Add clean-install → update → uninstall → reinstall automated smoke where feasible.

## Capability-driven hardware UI

- [x] Generic Windows controls are vendor-neutral.
- [x] Current raw X9 EC controls are hidden on non-X9 devices.
- [ ] Replace device-name assumptions with explicit provider capabilities where possible.
- [ ] Model fan backends separately: discrete EC levels, PWM/percentage target, OEM-native thermal policy, read-only telemetry.
- [ ] Never show EC wording/controls unless an active verified provider exposes EC control.
- [ ] Never show PWM wording/controls unless the provider exposes PWM/percentage semantics.
- [ ] Vendor links/copy only appear for the detected manufacturer and relevant installed/provider state.
- [ ] Unsupported/unknown devices stay safe/read-only until a provider is verified.

## Privacy-safe automatic diagnostics and compatibility learning

Goal: make compatibility and crash reports useful without requiring GitHub/manual report creation, while keeping the payload intentionally small and privacy-safe.

### Consent model
- [ ] Installer presents a clear `Help improve ThinkControl` option, selected by default for normal interactive installs.
- [ ] Explain exactly what may be sent before installation: app version, Windows version, manufacturer/product/machine type/BIOS, capability/provider families, bounded operation outcomes, sanitized crash exception/stack, and anonymous installation/device identifier.
- [ ] Explicitly exclude serial numbers, usernames, hostnames, user documents, browser/app content, keystrokes, touch coordinates/trails, raw memory dumps, unrelated logs and full file paths.
- [ ] Settings exposes the same control and allows opt-out at any time.
- [ ] Opt-out stops future uploads and clears queued unsent telemetry if requested.
- [ ] Separate essential licensing/account network traffic from optional diagnostics consent.

### Crash reporting
- [ ] Keep the durable local crash journal as the source of truth.
- [ ] Add a redaction/schema layer used by both preview and upload.
- [ ] With diagnostics enabled, upload a bounded crash envelope automatically on the next healthy startup.
- [ ] Mark the local crash record `Reported` only after the server acknowledges the exact report ID/hash.
- [ ] Retry with bounded backoff; never block app startup.
- [ ] Deduplicate repeated crash fingerprints server-side and track affected version/device families.
- [ ] Settings shows last report time/status and a privacy preview.

### New-device compatibility learning
- [ ] Unknown devices enter a visible but unobtrusive `Learning device support` state.
- [ ] Collection runs in background from normal app use; no experimental hardware writes merely for telemetry.
- [ ] Ask the user a small physical-verification question only when software evidence cannot prove something important (for example, whether fan RPM audibly changed).
- [ ] Upload redacted capability evidence automatically when diagnostics consent is enabled.
- [ ] One device sample is never enough to mark a new hardware profile verified.
- [ ] Server groups evidence by manufacturer/product/machine-type/BIOS/provider fingerprint.
- [ ] Promotion states: `Observed` → `Candidate` → `Verified` → `Regression watch`.
- [ ] Require multiple independent installations and consistent evidence before automatically promoting a profile to Verified; hardware-write support requires a stricter threshold than read-only telemetry.
- [ ] Conflicting evidence keeps the profile Candidate and creates a review item instead of silently changing writes for everybody.
- [ ] Signed/versioned device-profile manifest is delivered separately from app releases so support can improve without shipping a new EXE when safe.

## Accounts, paid licensing and source transition

Do not bolt this directly into alpha.30. Build it as a separate commercial-release phase with a backend threat model first.

### Account/auth
- [ ] Account is optional during early testing, mandatory only when paid licensing is enabled.
- [ ] Support Google sign-in plus normal email/account sign-in through a proper OAuth/OIDC provider.
- [ ] Use Authorization Code + PKCE/system browser for desktop login; never embed a Google password form.
- [ ] Store refresh/session secrets only in OS-protected storage, never plain JSON/settings.
- [ ] Account page shows signed-in identity, license state, devices and sign-out.

### License model
- [ ] Define product tiers and exact offline behavior before coding enforcement.
- [ ] Purchases create server-side entitlements attached to the account, not a reusable local serial alone.
- [ ] Desktop receives a short-lived signed entitlement/token and can operate offline for a reasonable grace period.
- [ ] Device activation limits and self-service deactivation are defined.
- [ ] License checks fail gracefully: do not disable safety-critical restore/Auto behavior because the network is down.
- [ ] Updates/download authorization and release distribution strategy are defined before the public repository is made private.
- [ ] Never place payment provider secrets or signing private keys in the desktop client.

### Payments/backend
- [ ] Backend owns users, entitlements, device activations, sanitized telemetry ingestion, compatibility evidence and profile promotion.
- [ ] Payment provider webhooks are the authority for purchase/refund/subscription entitlement state.
- [ ] Admin/review tooling exists for compatibility candidates and conflicting hardware evidence.
- [ ] Audit logging, rate limiting, abuse controls, data retention and deletion/export flows are implemented.

### Source-code transition
- [ ] Do not make the repo private until public builds/updater no longer depend on GitHub-public source/release URLs that would break.
- [ ] Decide what remains public (website/privacy docs/changelog, perhaps profile schema/client notices) versus private product source.
- [ ] Migrate release assets/update manifest to a distribution endpoint appropriate for paid users before privatizing the repo.
- [ ] Rotate any credentials/tokens that were ever exposed before the private-source transition.
- [ ] Add commercial license/EULA/privacy policy before accepting payment.

## Release principle

A green compiler is not release readiness. Every promoted build needs: exact-head build/tests, real WPF lifecycle smoke, visual QA, package/installer/updater verification, capability-safety review, and only then release promotion.