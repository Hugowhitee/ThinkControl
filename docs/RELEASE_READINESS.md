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

Draft PR #71 (`fix/x9-dual-fan-control-stability`) is an exact-X9 hardware investigation and is **not release-ready**. Physical testing now gives three important facts. First, the branch can expose both physical fan RPMs on the X9, which confirms the old single-tachometer product model was incomplete. Second, the tested `dev.1191` candidate still identified the writable path as `Fallback · verified X9 discrete EC telemetry/control`, so the modern `LENOVO_OTHER_METHOD` target-RPM writer did **not** pass its capability/live-read gate on this X9. Third, EC Max Cooling remained softer than naturally hot Lenovo Auto and could still have a faint electronic/buzzy character. The legacy EC writer therefore must not be promoted as the finished X9 control solution merely because dual-fan telemetry improved.

PR #71 now treats Lenovo-native fan interfaces as the product boundary. `LENOVO_OTHER_METHOD` remains the preferred semantic target-RPM writer when a verified X9 actually exposes at least two VALID+GET+SET fan channels with sane Lenovo-reported constraints and live reads. In addition, the branch probes Lenovo `EnergyDrv` **read-only** through the publicly recovered `QueryFanSpeed` IOCTL `0x83102570` for zero-based fan indices 0/1. If two native Lenovo fan channels are available through Other Mode or EnergyDrv, those readings take priority over direct EC tachometers and the known-inferior EC fan writer is no longer advertised: Lenovo Auto keeps fan ownership until a matching OEM write contract is physically validated. EC access may remain only for the existing read-only thermal fallback when richer temperature telemetry is unavailable.

That native two-fan proof is now latched for the lifetime of the hardware service. Once an exact X9 has successfully exposed two native Lenovo fan channels, a later transient OEM telemetry miss or explicit provider refresh cannot silently re-authorize the EC writer. The current live RPM may temporarily fall back/be unavailable, but `FanControlKind` remains non-writable until a validated OEM writer returns. EC cleanup is also ownership-aware: merely observing another utility's manual-looking EC state no longer makes ThinkControl its owner; automatic handoff is limited to an EC/OEM provider this controller actually took ownership of.

Public Lenovo reverse-engineering establishes a separate `EnergyDrv` `ChangeFanSpeed` IOCTL `0x8310257C`, but the meaning/encoding of its `dwFanCtrlCmd` input has not yet been recovered for the X9. Public evidence also separates this from older `EnergyDrv` maintenance/full-speed families such as dust control `0x831020C0` and a family-specific legacy ITS/Geek overlay `0x8310213C`; neither is permission to guess a smooth X9 target. The existing capture tooling therefore gathers `EnergyDrv`/LITSSVC and installed Lenovo binary evidence without invoking fan writes. PR #71 remains blocked until the exact X9 OEM writer is recovered and a physical candidate demonstrates Lenovo-like smoothness, useful high-cooling range, truthful dual-fan telemetry and reliable Auto handoff.

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
- [ ] Fan RPM/control recovery on the verified X9 path after repair.
- [x] PR #71 `dev.1191`: the physical X9 displayed two real fan RPM channels under the EC investigation path; the old one-fan telemetry model is no longer sufficient.
- [x] PR #71 `dev.1191`: the physical provider remained `Fallback · verified X9 discrete EC telemetry/control`; `LENOVO_OTHER_METHOD` did not become the writable provider on that candidate.
- [x] PR #71 `dev.1191`: EC Max Cooling remained softer than naturally hot Lenovo Auto and could still have a faint electronic/buzzy acoustic character. Treat this as negative evidence for EC-as-product-control, not as a completed fan fix.
- [ ] PR #71 native-telemetry candidate: verify whether `EnergyDrv` `QueryFanSpeed(0/1)` exposes the same two fan channels without direct EC tachometer reads. If it does, the Fans page must report Lenovo OEM fan telemetry and fan-control capability must remain disabled until an OEM writer passes validation.
- [ ] PR #71 native-telemetry candidate: after native two-fan proof, temporarily losing one/both OEM reads or refreshing providers must **not** make EC fan controls reappear during the same service lifetime; the native safety boundary stays latched.
- [ ] PR #71 native-telemetry candidate: Lenovo Auto remains smooth while the Fans page refreshes native telemetry; observation must not introduce the previous wave/re-kick behavior.
- [ ] Recover the exact X9 OEM write semantics from installed Lenovo binaries/captured state. Public evidence proves `EnergyDrv` `ChangeFanSpeed 0x8310257C` exists but does **not** define the X9 `dwFanCtrlCmd` encoding. Do not brute-force it.
- [ ] Keep `0x831020C0` dust/high-speed and family-specific `0x8310213C` ITS/Geek overlays separate from `0x8310257C`; do not substitute those maintenance/full-speed contracts for smooth X9 target-RPM control.
- [ ] If a writable `LENOVO_OTHER_METHOD` target-RPM interface is ever detected on a later X9 firmware/software stack, manual 25/50/75/100% must produce plausible two-fan telemetry, smooth settling and no previous wave/re-kick behavior; targets must remain within capability-reported constraints.
- [ ] Compare any final OEM-controlled 100%/maximum directly with a naturally hot Lenovo Auto state. Acceptance requires the useful high-cooling range to be materially equivalent; a writer that still tops out below hot Auto is **not** the finished fix.
- [ ] Repeated return-to-Auto handoffs from the final OEM writer succeed and do not leave one fan on a stale target, stall a fan or create persistent fan-to-fan divergence.
- [ ] Capture read-only `lenovo-auto-hot`/`lenovo-auto-cool` EnergyDrv/LITSSVC evidence with `tools/research/Capture-LenovoAuto.ps1 -BundleRelevantOemBinaries`, correlate fan values with the physical machine and inspect the selected Lenovo binaries before implementing another writer.

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
- [ ] Keep fan semantics distinct: OEM thermal policy, read-only telemetry, discrete EC states, continuous target-RPM/percentage control.
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
