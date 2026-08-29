# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current repository/release state

Published production prerelease:

- `v0.1.0-alpha.35`
- immutable tag/release SHA: `b6d2fb7a0a19d65dbac070f1c3f54fb9a662c6eb`
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`

Current `main` is intentionally ahead of that immutable release because post-release fixes/maintenance were merged without republishing or moving the alpha.35 tag.

Current `main` after PR #70:

- `aa697799319b8b942e58834649f7fed25bc33b85`
- `version.json` still records `0.1.0-alpha.35` with `releaseReady=true`
- PR #70 merged the touchpad corner-gesture reliability/reverse-close work after exact-head CI + Package validation
- `Promote release-ready main` must continue treating the existing alpha.35 release as immutable; it must never retag that release to newer `main`

Draft PR #71 (`fix/x9-dual-fan-control-stability`) is an exact-X9 hardware investigation and is **not release-ready**. Physical testing found the first candidate somewhat improved managed fan acoustics, but also proved that manual step 7/100% is not equivalent to hot Lenovo Auto and exposed stale RPM presentation after fan-state changes. The PR now keeps `0x2F` fan control shared, treats selector `0x31` as managed-mode tachometer evidence only, invalidates stale RPM immediately after state changes, adds bounded redacted dual-fan diagnostics, and adds a read-only Lenovo Auto/LITSSVC capture script. It remains blocked on a new real-device validation pass.

The next actual product release must be prepared deliberately from current `main` with a new version/release scope. Do not treat post-release commits as a reason to rewrite or republish alpha.35.

## Current validation ownership

The old standalone **Installer reliability** workflow has been consolidated into the Package path. Do not recreate a third full candidate build unless a future measured need justifies it.

### CI owns

- repository hygiene;
- solution restore/build;
- Core tests;
- real Compact ↔ Advanced WPF shell lifecycle smoke;
- the deterministic WPF visual-QA matrix and artifact upload.

`ThinkControl.ShellSmoke` is part of the solution build so its execution uses `--no-build --no-restore` instead of silently compiling another WPF copy.

### Package ThinkControl owns

- current UI/service candidate publish;
- compact payload-size checks and payload archive;
- bootstrap installer compilation;
- non-elevating UI manifest/project contract;
- installer-owned UAC/update handoff contract;
- sibling-payload auto-discovery;
- custom install-location persistence;
- service startup and named-pipe IPC v1 verification;
- current in-place update path;
- clean uninstall and ThinkControl-owned state cleanup;
- the real immutable oldest-supported `v0.1.0-alpha.14.1` → candidate updater regression;
- candidate checksums/development artifact.

The immutable alpha.14.1 fixture is a **support-floor regression fixture**, not the current tester version. It stays until the supported updater floor is deliberately advanced. The small immutable fixture may be cached; do not replace it with a newer release merely to make the workflow look newer.

For ordinary PR Package runs, visual snapshots are not rendered a second time. CI owns PR visual QA. Tagged/versioned release packaging still owns the release overview needed for the public release asset set.

### Measured validation improvement

On the final PR #68 head `1c6764da774a2fea4496c722439c4e28c3b69791`:

- CI 1421 / run `33260403035` passed in roughly 1m27;
- Package 1145 / run `33260403034` passed in roughly 1m52 end-to-end on that runner, including the deep installer/IPC smoke and the real alpha.14.1 upgrade regression;
- the two gates run concurrently, so practical PR wait is governed by the slower gate rather than the sum.

The exact Package log confirmed:

- `UI stays non-elevating` contract passed;
- sibling-payload discovery passed;
- `Ping + GetStatus` IPC v1 passed;
- current in-place update and clean uninstall lifecycle passed;
- real immutable alpha.14.1 → candidate swapped UI/service and restored IPC.

A NuGet cache experiment was removed after measurement showed that restoring/saving the large cache made wall-clock time worse. Performance changes must be evidence-based; do not add caches just because caching sounds faster.

PR #68 was squash-merged with the expected head guard. Post-merge CI 1422 passed on `659bc1de087dade48d402814e6814bce1487a91d`, and Promote release-ready main verified the existing immutable alpha.35 release/checksums/four assets without retagging it.

## Release workflow principles

For the next coherent release:

- [ ] Start from current `main` and inspect open PRs/branches before creating anything new.
- [ ] Keep one active branch / one PR for the coherent release scope.
- [ ] Read this handoff plus `AGENTS.md`, Architecture, Product, Device Support and Alpha Testing before changing behavior.
- [ ] Preserve current known-good architecture and safety boundaries; do not stack duplicate helpers/providers/timers/visual layers over existing owners.
- [ ] Distinguish current-client dead code from intentionally retained service/updater compatibility before deleting old-looking paths.
- [ ] Run the CURRENT required workflows on the exact final PR head. At present that means CI + Package; if workflow ownership changes later, follow the executable workflows rather than stale prose.
- [ ] Inspect UI artifacts manually when UI changes; generated-success alone is not visual QA.
- [ ] Review the final changed-file list/diff for unrelated changes and verify new code is actually referenced/reachable.
- [ ] Freeze version/docs only when the release scope is actually ready.
- [ ] Merge with an exact expected-head guard where supported.
- [ ] Verify post-merge `main` and the current promotion/release workflow.
- [ ] For a new version, verify the immutable tag points to the intended release commit and the public release has exactly the managed asset set with valid checksums.
- [ ] Never move an existing immutable release tag to a different commit.
- [ ] Let merged same-repository feature branches be deleted after verification.

The reusable version-agnostic prompt bootstrap for future coding chats lives in [`CHAT_STARTER.md`](CHAT_STARTER.md). It is **not** a release-state source of truth; it intentionally instructs new chats to recover mutable facts from GitHub and this handoff.

## Physical X9 follow-up — not an automated release claim

Hosted CI cannot prove the following and this document must not mark them complete without a real 21Q6/21Q7 test:

- [ ] Audio output and microphone continue refreshing after navigating away mid-drag and back on the physical X9.
- [ ] No delayed off-page Audio write causes a visible volume/microphone jump after navigation.
- [ ] The unified six-zone Touchpad editor feels natural on the physical X9 haptic pad and corner selection does not steal ordinary edge selection outside the intended diagonal lane.
- [ ] Top-left and top-right corner launches feel equally sized/sensitive in real touch use.
- [ ] Live corner Candidate/Claimed/Active frames do not produce visible page movement or sluggishness under a real high-rate touch stream.
- [ ] Lenovo OEM keyboard Auto works without ThinkControl substituting a software idle mode, and Fn+Space/readback remain in agreement through normal use/restart.
- [ ] Breathing/Reactive/Audio on a direct provider do not produce Lenovo keyboard pop-ups; effects remain unavailable when only the Vantage fallback is active.
- [ ] Issue #60 `TargetParameterCountException` does not recur during normal shell/notification use; keep the issue open until field evidence supports closure.
- [ ] Clean PawnIO reinstall/repair after stale/missing kernel-service state.
- [ ] Restart/UAC path and provider refresh after PawnIO repair.
- [ ] Fan RPM/control recovery on the verified X9 EC path after repair.
- [ ] PR #71 latest candidate: Lenovo Auto stays smooth when ThinkControl is merely open; ordinary Auto discovery does not touch selector `0x31`.
- [ ] PR #71 latest candidate: entering Quiet/Balanced/Max Cooling produces plausible Fan 1/Fan 2 readings without the previous wave/beating behavior or fan stalls.
- [ ] PR #71 latest candidate: changing fan state clears the old RPM immediately and a settled replacement appears within the bounded managed telemetry cadence instead of showing an old ~3800 RPM while the fan is already quiet.
- [ ] PR #71 latest candidate: manual 100% is validated as standard EC step 7 only; do not claim it equals Lenovo Auto's hottest/absolute physical fan ceiling.
- [ ] Capture and compare read-only `lenovo-auto-hot`, `lenovo-auto-cool` and managed step-7 evidence with `tools/research/Capture-LenovoAuto.ps1` before inferring any additional LITS/EC control contract.

## Commercial/public release program

Do **not** mix this backend/licensing program into an alpha stabilization/hardware-hardening PR. Implement it in bounded phases with a threat model and migration plan first.

### Installer, updater and signing

- [x] Preserve custom install location across the currently supported in-place update path.
- [x] Exercise clean install, service start/IPC, update compatibility and uninstall cleanup in CI/Package validation.
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

A green compiler is not release readiness. Promotion requires exact-head repository/build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review, and only then release publication. Physical hardware behavior remains a separate evidence class and must never be inferred from hosted CI.
