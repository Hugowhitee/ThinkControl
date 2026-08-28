# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release: alpha.32 hardware and touchpad hardening

`v0.1.0-alpha.31` is the immutable baseline. The active single release PR is #62 and targets **`v0.1.0-alpha.32`**.

The product-code pre-readiness candidate `4fc46ae48003be5365ce65104cee997fe01b1f08` passed CI, Package ThinkControl and Installer reliability on the same head. Its generated WPF gallery was manually inspected after earlier screenshot review caught and fixed Battery header ordering, touchpad editor ambiguity and stale Keyboard Auto copy. Release-owned metadata is now being frozen; promotion still requires all gates to pass again on the final readiness head.

### Implemented in alpha.32

- [x] PawnIO bootstrap state separates uninstall registration, compatible version, kernel-service registration/running state and provider/device readiness.
- [x] Compatible PawnIO registration with a missing kernel service is classified as repair, not a generic fan retry or a healthy installation.
- [x] Demand-start PawnIO may be registered but stopped before a provider opens the device; continuous running is not required.
- [x] PawnIO install/repair uses the pinned package and SHA-256 verification before elevation.
- [x] After PawnIO repair, verified X9 fan recovery requires the real EC fan-control/readback capability; unrelated providers cannot make fan setup report success.
- [x] X9 fan writes remain fail-closed behind exact identity, provider and readback gates.
- [x] Lenovo keyboard Auto prefers the reviewed Vantage/OEM `FirmwareAuto = 3` semantic path and requires readback verification.
- [x] No guessed direct-driver Auto IOCTL/payload was introduced; ThinkControl software Auto remains a bounded fallback.
- [x] Keyboard UI copy describes Lenovo Auto as preferred/verified and the High → Low → Off behavior as fallback-only.
- [x] Touchpad corner and edge selection are mutually exclusive in the editor.
- [x] Configured corners own matching contacts from the first candidate frame.
- [x] Rejected corner contacts are locked out until lift and cannot fall through into an edge with the same finger.
- [x] Corner launch visuals are symmetric/quieter and use the same physical Core lane geometry as recognition.
- [x] The live-corner visual-QA fixture reflects corner ownership instead of leaving an edge visually selected.
- [x] Right-side touchpad track-center geometry is corrected.
- [x] Advanced pages reset stale scroll offsets when revisited.
- [x] Battery uses the shared title → subtitle → top-action rail without Windows-link augmentation reordering its title.
- [x] Shared page/reset actions use the common top-right action pattern.
- [x] Repository hygiene, real shell smoke, WPF visual QA, packaging and installer lifecycle remain part of the release gate.

### Pre-readiness gate

Passed on the product-code candidate `4fc46ae48003be5365ce65104cee997fe01b1f08`:

- [x] Repository hygiene.
- [x] Release restore/build and Core unit tests.
- [x] Real Compact → Advanced → Compact WPF shell smoke.
- [x] WPF visual-QA matrix render.
- [x] Package ThinkControl workflow.
- [x] Installer reliability workflow, including deep install/service/IPC and legacy updater compatibility smoke.
- [x] Manual screenshot review across representative Home/Battery/Keyboard/Touchpad/Fans, minimum/normal/wide, setup/error and light/dark states.
- [x] Touchpad exclusivity regression test covers first-frame corner ownership, rejection, lift-required lockout and normal recognition after lift.
- [x] Dead-code/reference/docs audit found no release-blocking duplicate implementation or generated tracked artifact in the alpha.32 scope.

### Promotion gate

Only the **final readiness head** may be merged/released:

- [x] Freeze README/Product/release-readiness documentation to alpha.32.
- [ ] Set `version.json` to `0.1.0-alpha.32` with `releaseReady=true`.
- [ ] Re-run CI + Package + Installer reliability on that exact final readiness head.
- [ ] Inspect the final-head generated UI artifact for release-version/header drift.
- [ ] Squash-merge PR #62 to `main` using the expected final head SHA.
- [ ] Verify `main` points to the returned squash SHA.
- [ ] Verify the `Promote release-ready main` workflow succeeds for the squash SHA.
- [ ] Verify tag `v0.1.0-alpha.32` points to that exact `main` SHA.
- [ ] Verify the GitHub prerelease exists and contains exactly `ThinkControl-Setup-0.1.0-alpha.32.exe`, `ThinkControl-Payload-0.1.0-alpha.32.zip`, `SHA256SUMS.txt` and `ui-overview.png`.
- [ ] Verify the published release checksums succeed and assets are non-empty.

### Physical X9 follow-up — not an automated release claim

Hosted CI cannot prove the following and this document must not mark them complete without a real 21Q6/21Q7 test:

- [ ] Clean PawnIO reinstall/repair after stale/missing kernel-service state.
- [ ] Restart/UAC path and provider refresh after PawnIO repair.
- [ ] Fan RPM/control recovery on the verified X9 EC path after repair.
- [ ] Lenovo OEM Auto, Fn+Space and readback stay in agreement through normal use/restart.
- [ ] Real touchpad corner/edge feel and rejection behavior are comfortable on the physical haptic pad.
- [ ] Close any related field crash/hardware issue only after its reported workflow no longer reproduces.

## Commercial/public release program

Do **not** mix this backend/licensing program into an alpha stabilization/hardware-hardening PR. Implement it in bounded phases with a threat model and migration plan first.

### Installer, updater and signing

- [x] Preserve custom install location across the currently supported in-place update path.
- [x] Exercise clean install, service start/IPC, update compatibility and uninstall cleanup in CI.
- [ ] Failed staged update cannot destroy the last working payload; rollback stays tested.
- [ ] Define explicit uninstall policy for ThinkControl-owned runtime/local data before a commercial release.
- [ ] Sign binaries/installer and document/test SmartScreen reputation strategy.
- [ ] Keep intentional legacy-updater compatibility until the supported installed-client floor no longer needs it.

### Capability-driven hardware architecture

- [x] Windows-generic UI is vendor-neutral.
- [x] Raw X9 EC controls require the verified X9 provider path.
- [x] Setup distinguishes installation metadata from real provider readiness.
- [ ] Continue replacing residual device-name assumptions with explicit capabilities.
- [ ] Keep fan semantics distinct: OEM thermal policy, read-only telemetry, discrete EC states, continuous percentage/PWM.
- [ ] Never show EC/PWM/vendor wording unless the active provider exposes that exact semantic capability.
- [ ] Unknown hardware remains read-only/safe until a reviewed write provider is verified.

### Privacy-safe diagnostics and device learning

Diagnostics consent and licensing are separate. Opting out of diagnostics must never break a paid entitlement.

Allowed future upload schema should be intentionally small: app/Windows version, non-unique manufacturer/product/machine type/BIOS context, capability/provider families, bounded semantic operation outcomes, sanitized crash exception/stack and anonymous installation/device identifiers.

Never upload usernames, hostnames, serial numbers, personal files/paths/content, browser content, keystrokes, touch coordinates/trails, memory dumps or arbitrary raw logs.

- [ ] Shared redaction/schema layer powers preview and upload.
- [ ] Durable local crash journal remains source of truth; mark `Reported` only after server acknowledgement of exact report/hash.
- [ ] Upload/retry is asynchronous, bounded and never blocks startup.
- [ ] Unknown-device learning uses passive normal-app evidence; no experimental write probing merely for telemetry.
- [ ] Confidence states: `Observed → Candidate → Verified → Regression watch`.
- [ ] Multiple independent consistent installations are required for promotion; risky writes use a stricter threshold than read-only detection.
- [ ] Conflicting evidence blocks automatic promotion and creates review work.
- [ ] Any remote device/profile manifest is signed/versioned and cannot inject arbitrary hardware-write instructions.

### Accounts and licensing

- [ ] Define product tiers, activation limits and offline grace behavior before enforcement code.
- [ ] Use OAuth/OIDC Authorization Code + PKCE through the system browser; never embed provider password forms.
- [ ] Store refresh/session secrets only in OS-protected storage.
- [ ] Purchases create server-side entitlements; desktop receives short-lived signed entitlement state.
- [ ] License/network failure never disables safety-critical restore/firmware Auto behavior.
- [ ] Device activation/deactivation is self-service.
- [ ] Payment/signing secrets never ship in the desktop client.

### Backend/payments

- [ ] Backend owns users, entitlements, device activations, sanitized telemetry ingestion, compatibility evidence and profile promotion.
- [ ] Payment-provider webhooks are authoritative for purchase/refund/subscription state.
- [ ] Admin/review tooling exists for compatibility candidates/conflicts.
- [ ] Add audit logging, rate limiting, abuse controls, retention and deletion/export flows.

### Source/release transition

Do not make the source repository private while updater/build distribution still depends on public GitHub release URLs.

- [ ] Decide public versus private surfaces (website/privacy/changelog/schema as appropriate).
- [ ] Move release assets/update manifest to a paid-user-compatible distribution endpoint before privatizing source.
- [ ] Rotate credentials/tokens that were ever exposed.
- [ ] Add commercial license/EULA/privacy policy before accepting payment.

## Release principle

A green compiler is not release readiness. Promotion requires the exact-head repository/build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review, and only then release publication. Physical hardware behavior remains a separate evidence class and must never be inferred from hosted CI.
