# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.41`;
- immutable tag/release SHA: `6088955eeab54d1af6506780fa7707df17fe11c3`;
- published 2026-09-06 as an immutable prerelease;
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- alpha.41 is the preserved baseline for its startup ordering, X9 full-speed safety path, rejected per-fan target writer, installer/updater behavior and prior shell/crash fixes.

Current alpha.42 candidate:

- branch: `fix/alpha42-touchpad-input-reliability`;
- PR: #78, **Make Track Play/Pause and reverse close easier to trigger**;
- version: `v0.1.0-alpha.42`;
- base: immutable alpha.41 / `main` at `6088955eeab54d1af6506780fa7707df17fe11c3`;
- implementation/docs head validated before release freeze: `47c90f9ea662096a7134712e00bde0598e11de93`;
- `version.json.releaseReady=false` during implementation evidence collection; set it true only for the final frozen docs/version head after this record.

Alpha.42 is intentionally narrow. Real X9 use of alpha.41 still showed two physical Touchpad interaction problems: the integrated Track center Play/Pause target remained hard to trigger, especially when the user naturally tapped and held it, and reverse-close usually failed because its start target was too precise.

## Alpha.42 product delta

### Track center Play/Pause

- one continuous **Previous | Play/Pause | Next** Track lane remains the product model;
- the center start segment widens from 20% to **28%** (`0.36..0.64`);
- a quick center press still commits Play/Pause on release;
- a stationary center hold also commits after about **240 ms while the finger remains down**;
- the center movement envelope remains **8.75 mm**;
- the deliberate Previous/Next threshold remains **9.0 mm**;
- hold, release and skip share one guarded Track action state so exactly one action can commit for a contact;
- stale delayed hold tasks are invalidated when recognition claims, updates, releases or cancels the contact;
- a successful hold cannot toggle again on release and cannot subsequently also fire Previous/Next.

This improves hitability and feedback without adding another recognizer, timer owner, overlay or standalone Play/Pause setting.

### Reverse close

- the visible mirrored corner geometry is unchanged: guard → diagonal lane → rounded end-cap;
- with reverse close enabled, outward ownership can begin in the **inner half of the already-visible diagonal lane**, not only the tiny rounded cap;
- the outer guard remains a normal inward-launch start;
- no invisible hit target is added outside the rendered lane/cap geometry;
- outward claim still routes through the existing canonical hide-to-tray action;
- rejected corner ownership still stays locked until lift instead of falling through to a neighboring edge.

### Preserved alpha.41 baseline

Alpha.42 does **not** modify hardware, fan, startup, service, installer or updater contracts. In particular:

- X9 `fanX_target` remains physically rejected/read-only;
- exact-X9 `0x04020000` full-speed remains separately live-read/readback gated;
- no silent EC fallback is reintroduced;
- early tray-start Raw Input ownership remains unchanged;
- Compact/Advanced lifecycle, minimized-window recovery and `TargetParameterCountException` guards remain unchanged.

## Validation ownership

### CI owns

- repository hygiene;
- Release restore/build;
- Core/source regression tests;
- real Compact ↔ Advanced WPF lifecycle smoke;
- deterministic WPF visual-QA matrix and artifact upload.

### Package ThinkControl owns

- UI/service publish;
- compact payload checks;
- payload/bootstrap installer construction;
- non-elevating UI contract;
- service startup + named-pipe IPC;
- custom install-location preservation;
- in-place update and clean uninstall;
- oldest-supported `v0.1.0-alpha.14.1` updater compatibility;
- checksums and development artifact.

Do not recreate a third full installer workflow. Superseded PR runs may cancel; immutable/tag release packaging does not.

## Alpha.42 release gate

- [x] Started from immutable alpha.41 / `main` at `6088955eeab54d1af6506780fa7707df17fe11c3`.
- [x] Kept the follow-up on one branch/PR (#78).
- [x] Widened Track center recognition and matching visual separators to 28%.
- [x] Added a bounded 240 ms stationary-hold commit while retaining quick release commit.
- [x] Preserved the 8.75 mm center envelope and 9 mm deliberate skip threshold.
- [x] Serialized hold/release/skip so one Track contact cannot double-toggle or also skip.
- [x] Expanded reverse-close ownership only within the inner half of the already-visible diagonal lane.
- [x] Preserved mirrored corner geometry, outer-guard inward launch and corner lockout semantics.
- [x] Added/updated Track policy, reverse-zone and source-level regression tests.
- [x] Updated README/Product/Architecture/Device Support/Alpha Testing for alpha.42.
- [x] Exact implementation/docs head passed CI: hygiene, zero-warning/zero-error Release build, all tests, ShellSmoke and WPF rendering.
- [x] Exact implementation/docs head passed Package ThinkControl including installer/service/IPC/update/uninstall and oldest-supported updater regression.
- [x] Downloaded and manually inspected exact-head WPF QA, especially Touchpad normal/minimum/wide/light and mirrored corner selected/live fixtures.
- [x] Recorded exact implementation-head run IDs, test/snapshot counts and artifact IDs/digests below.
- [x] Reviewed the focused diff for duplicate gesture/action owners, delayed-hold races and alpha.41 hardware/startup regressions.
- [ ] Set `version.json.releaseReady=true` only after implementation evidence is complete.
- [ ] Require CI + Package to pass again on the exact frozen docs/version head.
- [ ] Mark PR #78 ready; review comments/threads/checks and merge with the exact expected head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.42 commit and immutable alpha.41 remains unchanged.
- [ ] Verify `Promote release-ready main` creates immutable `v0.1.0-alpha.42` at the merged commit.
- [ ] Verify exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png` are published and published Setup/Payload checksums validate.

## Alpha.42 implementation evidence

The first PR attempt on head `d8d02a5908fb33e1f119bfc26ff4495ecc11beff` produced useful partial evidence: Package #1426 / run `34085672890` passed the complete candidate packaging path, while CI #1712 / run `34085672865` correctly stopped at repository hygiene because the version bump preceded the required current-version docs. No build/test/visual claim is taken from that failed CI run.

Exact implementation/docs head `47c90f9ea662096a7134712e00bde0598e11de93` then passed the complete required implementation gates:

- **CI #1718 / run `34086178927`**: repository hygiene passed; Release build succeeded with **0 warnings / 0 errors**; **173/173** Core/source tests passed; Compact/Advanced ShellSmoke passed; **85** WPF visual-QA snapshots rendered successfully.
- **WPF artifact `10005298128`** (`ThinkControl-Visual-QA`) has SHA-256 digest `8a03ec3b33434257e3353ee2a157761171ba952d256572d09586ee1815505cf8` and was downloaded and manually inspected.
- Visual review covered `advanced-touchpad.png`, `advanced-touchpad-min.png`, `advanced-touchpad-wide.png`, `advanced-touchpad-light.png`, both selected corner fixtures and both live corner fixtures. The Bottom Track wide fixture shows the widened center as part of the same continuous lane; Previous/Play-Pause/Next remain aligned inside it, the updated hold behavior copy is visible and unclipped, normal/min/light layouts remain clean, and left/right corner geometry remains an exact visual mirror. Alpha.42 intentionally changes reverse recognition inside the existing lane rather than painting a second reverse overlay.
- **Package #1432 / run `34086178924`** passed UI/service publish, payload/bootstrap construction, deep installer/service/IPC lifecycle, custom-location preservation, clean uninstall, checksum creation and immutable alpha.14.1 → alpha.42 updater compatibility.
- **Package artifact `10005304235`** (`ThinkControl-0.1.0-alpha.42-dev.1432`) has SHA-256 digest `f99a0ed1f657ade2f279080124932a105485e206e22434cb113d0b7ede03f505`.
- Focused diff review confirmed no hardware/provider/service/startup/installer source changes. Track still uses the existing recognizer/router; the delayed hold is generation-cancelled by claim/update/release/cancel and shares `_trackActionCommitted` with release/skip, preventing double commit. Reverse close changes only canonical start classification inside the already-visible corner lane.

Physical finger feel is intentionally not marked proven by these hosted results.

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove finger feel. Alpha.42 is specifically intended to address physical alpha.41 feedback, so do not convert automated tests into claims that the interaction now feels correct.

Real-pad checks after installing alpha.42:

- [ ] Repeated quick center taps toggle exactly once and are materially easier to hit than alpha.41.
- [ ] A stationary center hold toggles around 240 ms while the finger remains down.
- [ ] Releasing after a successful hold does not toggle a second time.
- [ ] Moving after a successful hold does not also fire Previous/Next.
- [ ] Normal deliberate ~9 mm+ Previous/Next swipes still work and do not accidentally toggle center first.
- [ ] Reverse close succeeds from several points across the inner half of the visible top-left lane.
- [ ] Reverse close succeeds equivalently on the mirrored top-right lane.
- [ ] The outer guard still launches inward and is not misclassified as reverse close.
- [ ] Reverse-close disabled means an outward lane swipe does not hide ThinkControl.
- [ ] No regressions in alpha.41 startup, fan, keyboard, Audio or shell behavior.

## Release workflow principles

For future releases:

- start from current `main` and inspect branches/PRs/releases first;
- keep one coherent release branch/PR;
- preserve existing owners rather than stacking duplicate providers, timers, overlays or input workers;
- treat physical evidence separately from hosted CI;
- keep startup/navigation independent of slow hardware discovery;
- freeze version/docs before final exact-head gates;
- inspect UI artifacts manually when UI changes;
- merge with an expected-head guard;
- verify post-merge promotion and immutable tag/asset checksums;
- never move an existing immutable release tag.

The reusable version-agnostic bootstrap is [`CHAT_STARTER.md`](CHAT_STARTER.md). It is not a mutable release-state source of truth.

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
- [x] Raw EC controls require explicit provider/model validation.
- [x] Setup distinguishes registration metadata from real provider/device readiness.
- [x] X9 fan semantics distinguish telemetry, firmware policy, narrow global full speed, direct writers and discrete fallbacks.
- [x] Fan calibration and Keyboard Effects are exposed as semantic capabilities.
- [x] A physically rejected direct writer can remain telemetry-only without falling back to a known-inferior writer.
- [ ] Continue replacing residual device-name assumptions outside narrowly justified recovery/safety paths.
- [ ] Never show EC/PWM/vendor wording unless the active provider exposes that exact semantic contract.
- [ ] Unknown hardware remains read-only/safe until a reviewed write provider is verified.

### Privacy-safe diagnostics and device learning

Diagnostics consent and licensing are separate. Opting out of diagnostics must never break a paid entitlement.

Never upload usernames, hostnames, serial numbers, personal files/paths/content, browser content, keystrokes, touch coordinates/trails, memory dumps or arbitrary raw logs.

- [ ] Shared redaction/schema layer powers preview and upload.
- [ ] Durable local crash journal remains source of truth; mark Reported only after server acknowledgement.
- [ ] Upload/retry is asynchronous/bounded and never blocks startup.
- [ ] Unknown-device learning uses passive normal-app evidence; no experimental writes merely for telemetry.
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
