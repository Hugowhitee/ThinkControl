# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.36`;
- immutable tag/release SHA: `b9e194ec54d0539d5526a4acf515d6aac3a94ec3`;
- published 2026-09-05 as a prerelease;
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`.

Current `main` before the alpha.37 merge:

- `b9e194ec54d0539d5526a4acf515d6aac3a94ec3`;
- includes PR #70 Touchpad corner reliability/reverse-close work and PR #72 X9 dual-fan/native-provider hardening;
- matches the immutable `v0.1.0-alpha.36` tag.

Current release candidate:

- branch: `fix/touchpad-reverse-close-followup`;
- PR: #73 — **Fix touchpad reverse-close shell and visual QA regressions**;
- version: `v0.1.0-alpha.37`;
- release scope: two narrow post-alpha.36 review fixes in the existing Touchpad reverse-close path and its deterministic visual fixture;
- no fan/provider/device-support expansion and no change to the alpha.36 hardware safety model.

Alpha.37 exists because two P2 review findings on merged PR #70 remained valid on the alpha.36 source after release. The immutable alpha.36 tag is not moved or rewritten. This candidate fixes those issues through the existing shell-transition and Touchpad fixture owners instead of stacking another gesture worker, timer, overlay or shell path.

## Alpha.37 product delta

### Touchpad reverse-close / shell lifecycle

- `HideThinkControlToTray()` keeps ownership of the reverse-close transition.
- When Compact is visible, the transition uses the existing synchronous `MainWindow.HideForViewTransition()` path before `VerifyPrimarySurfaceState` and `shell.transition.completed` are recorded.
- The ordinary tray-toggle path still uses `HideAnimated()`; alpha.37 does not globally remove Compact animation.
- This prevents a successful reverse-close from being falsely recorded as `shell.exception` while the previous 95 ms Compact fade still left `IsVisible == true`.

### Touchpad visual QA

- The mirrored top-right reverse-close fixture is prepared directly instead of first composing the normal inward live fixture with the same contact ID.
- `PrepareReverseCornerForSnapshot` establishes the selected/configured corner from a clean non-live baseline before adding outward live points.
- The resulting deterministic trail represents only the reverse-close movement toward the physical corner.
- Source regression tests guard both the synchronous shell handoff and clean reverse-fixture construction.

### Preserved alpha.36 architecture

Alpha.37 does **not** alter the alpha.36 feature/hardware model:

- the unified Top/Bottom/Left/Right/Top-left/Top-right Touchpad editor remains canonical;
- corner guard → diagonal lane → rounded end-cap geometry and reject-until-lift ownership remain unchanged;
- raw recognition still receives full-rate input while WPF visualization is coalesced;
- Lenovo Other Mode target-RPM remains the preferred exact-X9 fan writer when its capability/safety gates pass;
- EnergyDrv remains read-only until its X9 write encoding is proven;
- the seven-step EC writer remains the exact-model fallback/investigation path;
- firmware/OEM Auto handoff, provider ownership and unknown-device fail-closed rules remain unchanged.

## Alpha.36 immutable baseline

Alpha.36 completed the post-alpha.35 corner integration and X9 dual-fan/native-provider hardening. Its final candidate passed the required CI and Package gates before merge, then PR #72 was merged to `main` at `b9e194ec54d0539d5526a4acf515d6aac3a94ec3`. `v0.1.0-alpha.36` was subsequently created at that exact commit and published with exactly the four managed release assets. That tag/release is immutable and remains the regression baseline immediately before alpha.37.

Physical X9 behavior is still a separate evidence class. Hosted CI proves source/build/lifecycle/package behavior; it does not prove real fan response, haptic feel or accidental gesture rates.

## Validation ownership

### CI owns

- repository hygiene;
- solution restore/build;
- Core tests, including source regression guards;
- real Compact ↔ Advanced WPF lifecycle smoke;
- deterministic WPF visual-QA matrix + artifact upload.

### Package ThinkControl owns

- UI/service publish;
- compact payload-size checks;
- bootstrap installer build;
- non-elevating UI contract;
- sibling-payload discovery;
- custom install-location preservation;
- service startup and named-pipe IPC v1;
- current in-place update path;
- clean uninstall and ThinkControl-owned state cleanup;
- immutable oldest-supported `v0.1.0-alpha.14.1` → candidate updater regression;
- checksums and development artifact.

Do not recreate a third full installer workflow. CI and Package are the current required PR gates. Superseded PR runs cancel; immutable/tag release packaging does not.

## Alpha.37 release gate

- [x] Recovered current `main`, immutable alpha.36 release/tag, open PRs/branches, AGENTS and the active architecture/product/device/testing/release docs before editing.
- [x] Confirmed there was no active feature PR/branch to reuse before creating the single alpha.37 follow-up branch/PR.
- [x] Reproduced both unresolved PR #70 P2 findings against current alpha.36 source rather than assuming the old review was stale.
- [x] Reverse-close remains on the canonical `HideThinkControlToTray` shell owner; no new timer, callback worker or duplicate shell transition was introduced.
- [x] Normal tray-toggle animation remains unchanged; only the transition-owned Compact hide is synchronous.
- [x] The reverse visual fixture now starts from a clean non-live corner baseline without adding a new visualizer/trail-reset API.
- [x] Added source regression guards for the shell handoff and reverse fixture.
- [x] No hardware/provider/service write paths changed; alpha.36 fan safety gates remain intact.
- [x] `version.json`, README and active docs are being frozen at `v0.1.0-alpha.37` for the final candidate.
- [ ] Final changed-file review confirms only intended reverse-close/tests/version/docs changes.
- [ ] Final exact PR-head CI passes build/tests, ShellSmoke and visual-QA matrix.
- [ ] Inspect the final Touchpad left inward/right reverse selected/live screenshots at representative theme/width states; verify the right live trail is outward-only and no layout/symmetry regression appears.
- [ ] Final exact PR-head Package ThinkControl passes publish/installer/service/IPC/update/uninstall/alpha.14.1 regression checks.
- [ ] Review final PR comments/checks and resolve the two superseded PR #70 review findings with the alpha.37 fix evidence.
- [ ] Squash-merge PR #73 with the exact expected final head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.37 commit and alpha.36 remains unchanged.
- [ ] Verify `Promote release-ready main` creates `v0.1.0-alpha.37` at the merged commit.
- [ ] Verify alpha.37 is an immutable prerelease with exactly Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png` and valid published checksums.

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove these. Alpha.37 may be published with these still open because unsupported/unproven hardware paths fail closed or use the already-reviewed alpha.36 provider contracts, but the results must remain documented honestly.

- [ ] Install alpha.37 on machine type `21Q6`/`21Q7` and record the Fans provider/detail line before changing fan state.
- [ ] If **Lenovo Other Mode direct target-RPM** activates, record whether it used canonical Capability Data or `exact-X9 direct-ID fallback`, then verify two plausible live fan channels plus manual 25/50/75/100% settling.
- [ ] Confirm direct OEM targets do not reproduce the earlier repeating wave/re-kick/buzzy character.
- [ ] Compare OEM 100% with naturally hot Lenovo Auto; Lenovo Fan Test max is reference/self-test data and may not be the absolute physical ceiling, so do not infer equivalence from metadata alone.
- [ ] Repeatedly return direct OEM ownership to Auto and confirm both owned channels release cleanly with no stale target/stall/persistent divergence.
- [ ] If only EnergyDrv native telemetry appears, confirm Fan 1/Fan 2 plausibly track physical sound while controls stay read-only rather than silently restoring EC after native proof.
- [ ] Confirm a provider refresh/transient OEM telemetry miss does not re-enable EC during the same hardware-service lifetime after native two-fan proof.
- [ ] If only the EC fallback remains, treat its profiles as fallback behavior; do not claim Max Cooling equals Lenovo Auto's physical maximum.
- [ ] Export a support bundle after the test so bounded provider/fan samples can be compared with physical observations.
- [ ] Capture `lenovo-auto-hot` / `lenovo-auto-cool` evidence if the exact EnergyDrv writer still needs recovery.
- [ ] Continue issue #60 field observation for `TargetParameterCountException`; source regression is guarded but issue closure needs real-world evidence.
- [ ] Verify Touchpad corner guard/reverse-close feel on the real haptic pad, including left/right symmetry and accidental-trigger rate.
- [ ] With Compact visible, verify reverse-close hides it cleanly in real use and does not produce a false shell failure diagnostic.
- [ ] Verify Audio navigation mid-drag, Lenovo keyboard Auto/Fn+Space, direct keyboard effects and PawnIO repair/restart on the physical machine.

## Release workflow principles

For future releases:

- start from current `main` and inspect branches/PRs/releases first;
- keep one coherent release branch/PR;
- preserve capability boundaries and existing owners rather than stacking duplicate providers/timers/overlays;
- distinguish current-client dead code from intentionally retained updater/service compatibility;
- freeze version/docs before final exact-head gates;
- inspect UI artifacts manually when UI changes;
- merge with an expected-head guard;
- verify post-merge promotion and immutable tag/asset checksums;
- never move an existing immutable release tag.

The reusable version-agnostic bootstrap is [`CHAT_STARTER.md`](CHAT_STARTER.md). It is not a source of mutable release facts; GitHub + this handoff remain authoritative.

## Commercial/public release program

Do **not** mix commercial backend/licensing work into alpha hardware stabilization.

### Installer, updater and signing

- [x] Preserve custom install location across supported in-place update.
- [x] Exercise install, service start/IPC, update compatibility and uninstall in CI/Package.
- [ ] Failed staged update cannot destroy the last working payload; rollback remains tested.
- [ ] Define explicit uninstall policy for ThinkControl-owned local/runtime data.
- [ ] Sign binaries/installer and document/test SmartScreen reputation strategy.
- [ ] Keep legacy updater compatibility until the supported installed-client floor is deliberately advanced.

### Capability-driven hardware architecture

- [x] Windows-generic UI is vendor-neutral.
- [x] Raw X9 EC controls require exact-model/provider validation.
- [x] Setup distinguishes registration metadata from real provider/device readiness.
- [x] X9 fan semantics distinguish native target-RPM, native read-only telemetry and discrete EC fallback.
- [ ] Continue replacing residual device-name assumptions with explicit capabilities.
- [ ] Never show EC/PWM/vendor wording unless the active provider exposes that exact semantic contract.
- [ ] Unknown hardware remains read-only/safe until a reviewed write provider is verified.

### Privacy-safe diagnostics and device learning

Diagnostics consent and licensing are separate. Opting out of diagnostics must never break a paid entitlement.

Allowed future upload data must stay intentionally small: app/Windows version, non-unique manufacturer/product/machine type/BIOS context, capability/provider families, bounded semantic operation outcomes, sanitized crash exception/stack and anonymous installation/device identifiers.

Never upload usernames, hostnames, serial numbers, personal files/paths/content, browser content, keystrokes, touch coordinates/trails, memory dumps or arbitrary raw logs.

- [ ] Shared redaction/schema layer powers preview and upload.
- [ ] Durable local crash journal remains source of truth; mark Reported only after server acknowledgement.
- [ ] Upload/retry is asynchronous/bounded and never blocks startup.
- [ ] Unknown-device learning uses passive normal-app evidence; no experimental write probing merely for telemetry.
- [ ] Confidence states: `Observed → Candidate → Verified → Regression watch`.
- [ ] Conflicting evidence blocks automatic promotion.
- [ ] Any remote device/profile manifest is signed/versioned and cannot inject arbitrary hardware-write instructions.

### Accounts, licensing and backend

- [ ] Define tiers, activation limits and offline grace behavior before enforcement code.
- [ ] Use OAuth/OIDC Authorization Code + PKCE through the system browser.
- [ ] Store refresh/session secrets only in OS-protected storage.
- [ ] Purchases create server-side entitlements; desktop receives short-lived signed entitlement state.
- [ ] License/network failure never disables safety-critical restore/firmware Auto behavior.
- [ ] Device activation/deactivation is self-service.
- [ ] Payment/signing secrets never ship in the desktop client.
- [ ] Payment-provider webhooks are authoritative for purchase/refund/subscription state.
- [ ] Add audit logging, rate limiting, retention and deletion/export flows.

### Source/release transition

Do not make source private while updater/build distribution still depends on public GitHub release URLs.

- [ ] Decide public versus private surfaces.
- [ ] Move release assets/update manifest to a paid-user-compatible distribution endpoint before privatizing source.
- [ ] Rotate credentials/tokens that were ever exposed.
- [ ] Add commercial license/EULA/privacy policy before accepting payment.

## Release principle

A green compiler is not release readiness. Promotion requires exact-head build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review and immutable release verification. Physical hardware behavior remains a separate evidence class and must never be invented from hosted CI.
