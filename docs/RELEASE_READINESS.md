# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release: alpha.34 unified Touchpad zones and Keyboard Auto cleanup

`v0.1.0-alpha.33` is the immutable production baseline. The active single release PR is #65 and targets **`v0.1.0-alpha.34`**.

The implementation/visual-QA head `ad17495a1b9403bec832be51780d3224ec95f0c9` passed CI, Package ThinkControl and Installer reliability on the same SHA. That evidence includes repository hygiene, restore/build, Core tests, real Compact ↔ Advanced shell smoke, the WPF snapshot renderer, package/bootstrap creation, service lifecycle smoke, deep installer/IPC smoke and legacy updater compatibility. The corrected WPF visual-QA artifact from CI run 1369 was manually inspected: top-left and top-right selected/live corner states use exact mirrored final geometry, the selected corner editor remains stable during live ownership, top-left shows `Compact`, top-right shows `Advanced`, normal/minimum/wide Touchpad layouts remain stable, and the Keyboard page exposes Auto only in the normal hardware-mode row while Effects contains Breathing / Reactive / Audio.

Release metadata is now frozen to alpha.34. README and Product document the six-zone Touchpad selection/rendering model, preserved runtime corner/edge recognition exclusivity, and the keyboard contract that Lenovo/OEM Auto must be verified rather than silently replaced by a software idle policy. `version.json` is `0.1.0-alpha.34` with `releaseReady=true`.

This handoff update is the **last planned branch content change**. Before squash merge, CI + Package + Installer reliability must all be green on the exact PR head containing this handoff commit. No implementation, QA-fixture or documentation changes should be added afterward; if the head moves, repeat the exact-head gate.

### Implemented in alpha.34

- [x] Touchpad editor selection is one six-zone model: Top, Bottom, Left, Right, Top-left and Top-right.
- [x] `TouchpadVisualizer` owns edge/corner selection, rendering and hit-testing; the auxiliary corner overlay is non-interactive and no longer competes for mouse ownership.
- [x] Corner hit-testing takes priority only inside the deliberate diagonal corner lane; normal edge selection remains available outside it.
- [x] Runtime corner-vs-edge gesture recognition, first-candidate corner ownership, reject-until-lift lockout and deliberate diagonal thresholds remain unchanged from alpha.33.
- [x] Left/right corner guides are generated from one canonical final geometry and the right side is an exact mirror of the left.
- [x] Edge and corner zones share one idle/selected/hover/candidate/live visual grammar instead of looking like separate gesture systems.
- [x] Live corner ownership remains visual-only for editor state: the selected editor stays in layout and is dimmed/disabled without reflow while a finger is moving.
- [x] WPF visual QA renders top-left/top-right selected and live fixtures and executes a symmetry assertion against the real visualizer.
- [x] CI explicitly requires those four corner-state PNGs so future snapshot changes cannot silently drop the release-specific QA coverage.
- [x] Snapshot fixtures preserve injected Compact/Advanced corner actions instead of re-reading the live host configuration and displaying misleading `Off` values.
- [x] Keyboard Auto is removed from the separate Effects card.
- [x] The normal Off / Low / High / Auto row keeps Lenovo firmware Auto as the OEM mode and requires successful set/readback verification.
- [x] The old software idle-dimming Auto fallback is removed; ThinkControl no longer labels a High → Low → Off loop as Auto when OEM Auto is unavailable.
- [x] Breathing, Reactive and Audio remain user-session effects and require the direct backlight provider; the Lenovo Vantage fallback stays excluded to avoid repeated Lenovo keyboard-brightness pop-ups.
- [x] Alpha.33 shell restore, successful-update confirmation, high-rate Touchpad coalescing, page-visibility listener lifecycle and WPF dispatcher crash protections remain intact.
- [x] No fan/PawnIO/EC write-safety contract is loosened by this release.

### Automated release evidence

Passed on implementation head `ad17495a1b9403bec832be51780d3224ec95f0c9` before metadata freeze:

- [x] Repository hygiene.
- [x] Release restore/build and Core tests, including Touchpad zone-selection/corner policy regressions and the existing WPF dispatcher scheduling source guard.
- [x] Real Compact ↔ Advanced WPF shell smoke.
- [x] WPF visual-QA matrix render with exact corner-symmetry and live-editor-stability assertions.
- [x] Top-left/top-right selected/live screenshots manually inspected after the snapshot-state correction.
- [x] Keyboard page manually inspected with Auto only in the hardware row and Breathing / Reactive / Audio under Effects.
- [x] Package ThinkControl workflow, including bootstrap installer/service lifecycle smoke.
- [x] Installer reliability workflow, including deep install/service/IPC and exact legacy updater compatibility smoke.

### Promotion gate

- [x] Freeze README/Product documentation to alpha.34.
- [x] Set `version.json` to `0.1.0-alpha.34` with `releaseReady=true`.
- [x] Update this single persistent release-readiness handoff for alpha.34.
- [x] Replace the unmergeable draft PR #64 with non-draft PR #65 on the exact same branch/base after the connected ready-for-review mutation failed at GitHub's GraphQL schema layer.
- [ ] Require CI + Package + Installer reliability green on the exact PR #65 head containing this final handoff commit; make no further branch changes afterward.
- [ ] Confirm PR #65 has no unexpected changed files and is still based on the immutable alpha.33 `main` baseline.
- [ ] Squash-merge PR #65 to `main` using that exact expected head SHA.
- [ ] Verify `main` points to the returned squash SHA.
- [ ] Verify the `Promote release-ready main` workflow succeeds for the squash SHA.
- [ ] Verify tag `v0.1.0-alpha.34` points to that exact `main` SHA.
- [ ] Verify the immutable GitHub prerelease exists and contains exactly `ThinkControl-Setup-0.1.0-alpha.34.exe`, `ThinkControl-Payload-0.1.0-alpha.34.zip`, `SHA256SUMS.txt` and `ui-overview.png`.
- [ ] Verify the published release checksums succeed and assets are non-empty/downloadable.
- [ ] Remove the merged alpha.34 branch after immutable release verification.

### Physical X9 follow-up — not an automated release claim

Hosted CI cannot prove the following and this document must not mark them complete without a real 21Q6/21Q7 test:

- [ ] The unified six-zone Touchpad editor feels natural on the physical X9 haptic pad and corner selection does not steal ordinary edge selection outside the intended diagonal lane.
- [ ] Top-left and top-right corner launches feel equally sized/sensitive in real touch use, not only in rendered geometry.
- [ ] Live corner Candidate/Claimed/Active frames do not produce visible page movement or sluggishness under a real high-rate touch stream.
- [ ] Lenovo OEM keyboard Auto works without ThinkControl substituting a software idle mode, and Fn+Space/readback remain in agreement through normal use/restart.
- [ ] Breathing/Reactive/Audio on a direct provider do not produce Lenovo keyboard pop-ups; effects remain unavailable when only the Vantage fallback is active.
- [ ] Issue #60 `TargetParameterCountException` does not recur during normal shell/notification use; keep the issue open until field evidence supports closure.
- [ ] Clean PawnIO reinstall/repair after stale/missing kernel-service state.
- [ ] Restart/UAC path and provider refresh after PawnIO repair.
- [ ] Fan RPM/control recovery on the verified X9 EC path after repair.

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
