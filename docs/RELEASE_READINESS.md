# ThinkControl release-readiness roadmap

This is the handoff checklist for active ThinkControl stabilization/release work. Keep it current. The evergreen packaging procedure lives in [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md); this file tracks what is still blocking the current release and the larger work that follows it.

## Current release: alpha.31 stabilization

`v0.1.0-alpha.30` is already an immutable GitHub prerelease. The post-alpha.30 cleanup/fixes therefore target **`v0.1.0-alpha.31`**. `version.json.releaseReady` must stay `false` until the pre-readiness exact-head gate and visual inspection are complete.

### Stabilization work completed in the active PR

- [x] TargetParameterCountException shell path replaced with parameterless dispatcher callbacks and covered by real Compact ↔ Advanced lifecycle smoke.
- [x] Compact remains a persistent topmost utility while visible without forcing owned ThinkControl notifications behind it.
- [x] Cold startup has a painted loading surface instead of exposing an empty/black native window while discovery/rendering completes.
- [x] Home and Compact share one explicit quick-power contract: battery preference only; full Performance owns separate battery/AC configuration.
- [x] Home battery/Performance copy has minimum-width-safe layout and spacing.
- [x] Touchpad corner launches use one Core physical geometry for recognition, drawing and UI hit-testing; no hidden square target remains.
- [x] Corner launch actions are limited to Off / Compact / Advanced and remain separate from precision edge actions.
- [x] Track Previous/Next requires deliberate travel; optional center Play/Pause uses a visible bounded low-travel hold/release zone.
- [x] Media feedback reflects the resulting Playing/Paused state instead of a fixed symbol.
- [x] Keyboard Auto/effect writes and direct level writes share serialized ownership so one path cannot silently drop the other.
- [x] X9 fan calibration is transactional: seven complete EC states, three spaced tachometer samples per state, validation before replace, previous known-good data preserved on failure/cancel.
- [x] Fan calibration/raw EC UI requires the verified X9 provider plus actual fan-write, control-temperature and tachometer capabilities.
- [x] Subjective fan-audibility control removed from the user path because it did not participate in output mapping.
- [x] Release-specific Home/Compact/Touchpad polish owners consolidated into permanent feature/layout owners.
- [x] `System.Management` dependency version aligned across projects; no other unused package/project reference was found in the dependency audit.
- [x] Branch hygiene no longer hard-codes already-deleted historical alpha/temp branch names.
- [x] PRODUCT / Cooling / docs index ownership consolidated so implementation claims are not maintained in several stale documents.
- [x] Repository-hygiene CI gate checks local Markdown links, tracked generated artifacts, version drift and reintroduction of consolidated one-off partials.
- [x] Visual-QA fixture no longer claims a detected Precision Touchpad while simultaneously rendering a no-touchpad haptic state.

### Alpha.31 pre-readiness gate

- [ ] Exact current head: repository hygiene succeeds.
- [ ] Exact current head: Release build and Core unit tests succeed.
- [ ] Exact current head: real Compact → Advanced → Compact shell smoke succeeds.
- [ ] Exact current head: WPF visual-QA matrix renders successfully.
- [ ] Exact current head: Package ThinkControl workflow succeeds.
- [ ] Exact current head: Installer reliability workflow succeeds.
- [ ] Manually inspect fresh Home minimum/normal/wide screenshots for clipping/alignment.
- [ ] Manually inspect fresh Touchpad normal/wide/inward-active screenshots for corner geometry, center zone and coherent haptic fixture state.
- [ ] Manually inspect Compact dark/light/on-battery, Fans normal/manual-test/unavailable, startup loading, updates/attention and representative error/unavailable states.
- [ ] Keep crash issue #60 open until alpha.31 is available for physical retest; automated shell smoke proves the code path, not the physical report resolved in the field.

### Promotion gate

Only after every pre-readiness item above is complete:

- [ ] Set `version.json.releaseReady=true`.
- [ ] Re-run CI + Package ThinkControl + Installer reliability on that exact readiness head.
- [ ] Squash-merge the single release PR to `main`.
- [ ] Verify `main` equals the squash SHA and branch hygiene removes the merged feature branch.
- [ ] Verify `v0.1.0-alpha.31` points exactly at the promoted `main` SHA.
- [ ] Verify the immutable prerelease contains exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`, with valid checksums.
- [ ] Download/launch the published installer for the physical X9 retest; only then close crash #60 if the reported workflow no longer reproduces.

## Before a public paid release

Alpha releases may keep shipping through the current GitHub prerelease path. The following work is a separate commercial-release program and must not be mixed into an alpha stabilization PR.

### Installer / updater / uninstaller

- [ ] Keep clean-install destination selection explicit and preserve a custom location during updates/silent updater handoff.
- [ ] Define optional desktop shortcut/startup choices and diagnostics consent during install.
- [ ] Prove failed staged updates cannot destroy the previous working payload.
- [ ] Sign installer and binaries; document/test SmartScreen reputation strategy.
- [ ] Full uninstall removes ThinkControl service/process/startup/shortcuts/update staging and user-selected owned local data, then proves clean reinstall.

### Capability-driven hardware UI

- [x] Generic Windows controls are vendor-neutral.
- [x] Raw X9 EC controls are hidden outside the verified X9 provider path.
- [ ] Continue replacing model-name assumptions with explicit provider capabilities where possible.
- [ ] Model discrete EC, PWM/percentage, OEM-native thermal policy and read-only telemetry as distinct provider semantics.
- [ ] Never show EC/PWM/vendor-specific wording unless the active provider actually exposes that semantic capability.
- [ ] Unknown devices remain safe/read-only until the relevant write provider contract is verified.

## Privacy-safe diagnostics and compatibility learning

Goal: make compatibility/crash evidence useful without GitHub/manual report creation while keeping payloads intentionally small and privacy-safe.

### Consent and schema

- [ ] Installer and Settings expose the same clear `Help improve ThinkControl` control.
- [ ] Define a strict allowlisted upload schema: app/Windows version, manufacturer/product/machine type/BIOS, capability/provider families, bounded operation outcomes, sanitized crash exception/stack and anonymous installation/device identifier.
- [ ] Explicitly exclude serial numbers, usernames, hostnames, user documents, browser/app content, keystrokes, touch coordinates/trails, raw memory dumps, unrelated logs and full file paths.
- [ ] Opt-out stops future uploads and can clear queued unsent optional telemetry.
- [ ] Keep essential licensing/account traffic separate from optional diagnostics consent.

### Crash reporting

- [ ] Durable local crash journal remains source of truth.
- [ ] Shared redaction/schema layer powers both privacy preview and upload.
- [ ] With consent enabled, upload a bounded crash envelope on a later healthy startup without blocking startup.
- [ ] Mark a local crash `Reported` only after server acknowledgement of the exact report/hash.
- [ ] Bounded retry/backoff plus server-side fingerprint deduplication.
- [ ] Settings shows last report status/time and privacy preview.

### New-device compatibility learning

- [ ] Unknown devices expose an unobtrusive `Learning device support` state.
- [ ] Passive collection comes from normal app use; no experimental hardware writes merely for telemetry.
- [ ] Ask a small physical-verification question only when software evidence cannot prove a relevant behavior.
- [ ] Server groups redacted evidence by manufacturer/product/machine-type/BIOS/provider fingerprint.
- [ ] Promotion states: `Observed` → `Candidate` → `Verified` → `Regression watch`.
- [ ] Multiple independent consistent installations are required before promotion; write support has a stricter threshold than read-only telemetry.
- [ ] Conflicting evidence stays Candidate and creates review work rather than silently changing writes for everyone.
- [ ] Signed/versioned device-profile distribution is designed before remote profiles can affect product behavior.

## Accounts, licensing and source transition

Do not bolt this into an alpha stabilization release. Build it as a separate commercial phase with a backend threat model first.

### Account/auth and licensing

- [ ] Define whether/when an account becomes mandatory and the exact offline behavior before coding enforcement.
- [ ] Use a proper OAuth/OIDC provider with Authorization Code + PKCE/system-browser login; never embed provider passwords.
- [ ] Store refresh/session secrets only in OS-protected storage.
- [ ] Purchases create server-side entitlements; desktop receives short-lived signed entitlement state with a reasonable offline grace period.
- [ ] Define activation limits/self-service deactivation.
- [ ] License/network failure never disables safety-critical firmware restore/Auto behavior.
- [ ] Never ship payment/signing secrets in the desktop client.

### Backend and payments

- [ ] Backend owns users, entitlements, device activations, sanitized telemetry ingestion, compatibility evidence and profile promotion.
- [ ] Payment-provider webhooks are authoritative for purchase/refund/subscription state.
- [ ] Admin/review tooling exists for compatibility candidates/conflicts.
- [ ] Audit logging, rate limiting, abuse controls, retention and deletion/export flows are implemented.

### Source-code transition

- [ ] Do not make the repository private while public builds/updater still depend on public GitHub source/release URLs.
- [ ] Decide what remains public versus private product source.
- [ ] Migrate release assets/update manifest to a paid-user-compatible distribution endpoint before privatizing.
- [ ] Rotate credentials/tokens that were ever exposed before the transition.
- [ ] Add commercial license/EULA/privacy policy before accepting payment.

## Release principle

A green compiler is not release readiness. Every promoted build needs the exact-head repository/build/test gates, real WPF lifecycle smoke, inspected visual QA, package/installer/updater verification, capability-safety review and only then release promotion.
