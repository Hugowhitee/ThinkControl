# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release: alpha.31 stabilization

`v0.1.0-alpha.30` is already immutable. Post-alpha.30 fixes therefore target **`v0.1.0-alpha.31`**. Keep `version.json.releaseReady=false` until the pre-readiness exact-head gate and visual inspection are complete.

### Implemented in the active PR

- [x] Root-cause fix for the `TargetParameterCountException` dispatcher crash path.
- [x] Compact remains topmost while visible; ThinkControl-owned modal/notification surfaces can stay above it.
- [x] Painted startup/transition feedback avoids dead/black shell periods.
- [x] Home and Compact use one battery-power quick preference; full Performance owns separate battery/AC settings.
- [x] Home battery/Performance layout is minimum-width safe and aligned with neighboring metrics/cards.
- [x] Touchpad top-corner launches use shared physical Core/UI geometry instead of hidden square targets.
- [x] Track Previous/Next requires deliberate travel; optional center Play/Pause has a bounded visible target and stateful OSD.
- [x] Keyboard Auto no longer depends on unrelated effect capability; hardware/effect writes share serialized ownership.
- [x] X9 fan calibration is transactional and accepts only complete/plausible seven-state tachometer evidence.
- [x] Raw X9 EC/calibration UI requires the verified provider and required capabilities.
- [x] Useless subjective fan-audibility UI removed.
- [x] Release-specific UI partials consolidated into permanent owners.
- [x] Repository/docs cleanup removed duplicate checklists, historical verification Markdown and committed visual-QA screenshots.
- [x] Repository-hygiene CI rejects broken local docs links, tracked generated output, stale version entry points and reintroduction of known obsolete files.

### Pre-readiness gate

All items must pass on the **same exact head**:

- [ ] Repository hygiene.
- [ ] Release restore/build and Core unit tests.
- [ ] Real Compact → Advanced → Compact WPF shell smoke.
- [ ] WPF visual-QA matrix render.
- [ ] Package ThinkControl workflow.
- [ ] Installer reliability workflow.
- [ ] Manual screenshot review: Home min/normal/wide; Touchpad normal/wide/inward-active; Compact dark/light/battery; Fans normal/manual-test/unavailable; startup; updates/attention; representative unavailable/error states.
- [ ] Dead-code/reference/docs audit has no unexplained duplicate implementation or stale tracked artifact.

Crash issue #60 stays open until the published alpha.31 can be physically retested. Hosted shell smoke proves the software route, not the field report.

### Promotion gate

Only after the pre-readiness gate:

- [ ] Set `version.json.releaseReady=true`.
- [ ] Re-run CI + Package + Installer reliability on that exact readiness head.
- [ ] Squash-merge the one release PR to `main`.
- [ ] Verify `main` equals the squash SHA.
- [ ] Verify `v0.1.0-alpha.31` points at that exact `main` SHA.
- [ ] Verify immutable release contains exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`, with valid checksums.
- [ ] Install/retest on the physical X9; close crash #60 only if the reported workflow no longer reproduces.

## Commercial/public release program

Do **not** mix this backend/licensing program into an alpha stabilization PR. Implement it in bounded phases with a threat model and migration plan first.

### Installer, updater and signing

- [ ] Preserve custom install location across interactive/silent updates.
- [ ] Failed staged update cannot destroy the last working payload; rollback stays tested.
- [ ] Full uninstall removes ThinkControl-owned service/process/startup/shortcuts/staging/local data according to explicit user policy and supports clean reinstall.
- [ ] Sign binaries/installer and document/test SmartScreen reputation strategy.
- [ ] Keep intentional legacy-updater compatibility until the supported installed-client floor no longer needs it.

### Capability-driven hardware architecture

- [x] Windows-generic UI is vendor-neutral.
- [x] Raw X9 EC controls require the verified X9 provider path.
- [ ] Continue replacing device-name assumptions with explicit capabilities.
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

A green compiler is not release readiness. Promotion requires the exact-head repository/build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review, and only then release publication.
