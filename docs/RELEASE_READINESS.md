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
- final deliberate-hold implementation head validated before release freeze: `57217763e94e93fa11473765feb90f63312bea10`;
- `version.json.releaseReady=false` during implementation evidence collection; the next version-only freeze may set it true now that this evidence is recorded;
- the earlier alpha.42 freeze was explicitly reopened after the user clarified that global Play/Pause must be difficult to trigger accidentally, especially in a school/classroom context.

Alpha.42 remains intentionally narrow. Real X9 use of alpha.41 showed two physical Touchpad interaction problems: the integrated Track center Play/Pause target remained hard to trigger, and reverse-close usually failed because its start target was too precise. The first alpha.42 implementation fixed hitability but made Play/Pause **too easy** by accepting a quick tap and auto-firing a hold while the finger was still down. That behavior was superseded before release.

## Alpha.42 product delta

### Track center Play/Pause — final candidate model

Track remains one continuous **Previous | Play/Pause | Next** lane with one recognizer/router owner.

The final alpha.42 model separates *where* the target is from *how deliberately* it activates:

- the center start segment remains widened from 20% to **28%** (`0.36..0.64`) so deliberate placement is easy;
- a **quick center tap does nothing**;
- Play/Pause requires a center-start contact held for at least **450 ms**;
- the contact may move at most **3 mm maximum radial excursion** while it remains a hold candidate;
- Play/Pause commits **only on release**, never automatically while the finger remains down;
- the recognizer preserves maximum excursion, so moving away and returning cannot erase earlier movement and re-arm the hold;
- once movement exceeds 3 mm, ordinary Track direction recognition resumes;
- the deliberate Previous/Next threshold remains unchanged at **9.0 mm**;
- a claimed Track swipe cannot downgrade into Play/Pause on release.

This deliberately favors accidental-playback prevention over the fastest possible toggle. It also follows a well-established touchpad interaction pattern rather than inventing another overlay: libinput-style hold gestures distinguish a static hold from a quick tap and allow small unavoidable single-finger deltas while cancelling into movement when intent changes. Public gesture projects also use hold for media Play/Pause. These references informed the interaction model only; ThinkControl imports no code or dependency from them.

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

## Superseded alpha.42 evidence

Earlier alpha.42 implementation/docs head `47c90f9ea662096a7134712e00bde0598e11de93` passed the complete software gates:

- CI #1718 / run `34086178927`: 0 warnings / 0 errors, 173/173 tests, ShellSmoke and 85 snapshots;
- WPF artifact `10005298128`, digest `8a03ec3b33434257e3353ee2a157761171ba952d256572d09586ee1815505cf8`, was downloaded and manually inspected;
- Package #1432 / run `34086178924` passed the full package/installer/service/updater path;
- Package artifact `10005304235`, digest `f99a0ed1f657ade2f279080124932a105485e206e22434cb113d0b7ede03f505`.

The subsequent frozen head `d164574ac1f69690cc143d16fe013ca1ae5b21bc` also passed final CI. **None of those runs approve the final release**, because they tested the superseded 240 ms auto-fire / quick-tap behavior. They remain regression baseline evidence only.

## Alpha.42 final implementation gate

- [x] Started from immutable alpha.41 / `main` at `6088955eeab54d1af6506780fa7707df17fe11c3`.
- [x] Kept the follow-up on one branch/PR (#78).
- [x] Widened Track center recognition and matching visual separators to 28%.
- [x] Rejected the too-easy pre-freeze quick-tap / 240 ms auto-fire design before release.
- [x] Changed Play/Pause to deliberate **450 ms hold + release**.
- [x] Limited valid hold movement to **3 mm maximum radial excursion** and preserved that maximum even if the finger returns.
- [x] Preserved the **9 mm** Previous/Next threshold and normal swipe recognition after the hold slop is exceeded.
- [x] Removed delayed hold workers/concurrent auto-fire arbitration; release is the sole Play/Pause commit moment.
- [x] Expanded reverse-close ownership only within the inner half of the already-visible diagonal lane.
- [x] Preserved mirrored corner geometry, outer-guard inward launch and corner lockout semantics.
- [x] Added/updated Track policy, max-excursion, reverse-zone and source-level regression tests.
- [x] Updated README/Product/Architecture/Device Support/Alpha Testing for the final alpha.42 semantics.
- [x] Fresh exact implementation head passed CI: hygiene, zero-warning/zero-error Release build, all tests, ShellSmoke and WPF rendering.
- [x] Fresh exact implementation head passed Package ThinkControl including installer/service/IPC/update/uninstall and oldest-supported updater regression.
- [x] Downloaded and manually inspected fresh exact-head WPF QA, especially Touchpad normal/minimum/wide/light and mirrored corner selected/live fixtures.
- [x] Recorded the fresh implementation-head run IDs, test/snapshot counts and artifact IDs/digests below.
- [x] Reviewed the focused diff: only Touchpad/docs/tests/version files changed; no hardware/provider/service/startup/installer source was modified and no second gesture/action owner was added.
- [ ] Freeze `version.json.releaseReady=true`.
- [ ] Require CI + Package to pass again on the exact frozen docs/version head.
- [ ] Mark PR #78 ready; review comments/threads/checks and merge with the exact expected head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.42 commit and immutable alpha.41 remains unchanged.
- [ ] Verify `Promote release-ready main` creates immutable `v0.1.0-alpha.42` at the merged commit.
- [ ] Verify exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png` are published and published Setup/Payload checksums validate.

## Alpha.42 final implementation evidence

Exact deliberate-hold implementation head `57217763e94e93fa11473765feb90f63312bea10` passed both required PR pipelines:

- **CI #1737 / run `34089402864`**: repository hygiene passed; Release build succeeded with **0 warnings / 0 errors**; **173/173** Core/source tests passed; Compact/Advanced ShellSmoke passed; **85** WPF visual-QA snapshots rendered successfully.
- **WPF artifact `10006326748`** (`ThinkControl-Visual-QA`) has SHA-256 digest `f6b3d5af1f68942d80920940d79ace5e9392158ac724743a7c54fe481f6b3139` and was downloaded and manually inspected.
- Visual review covered `advanced-touchpad.png`, `advanced-touchpad-min.png`, `advanced-touchpad-wide.png`, `advanced-touchpad-light.png`, both selected corner fixtures and both live corner fixtures. The wide Bottom Track fixture keeps Previous / Play-Pause / Next inside one continuous band with the wider center integrated rather than overlaid. The editor help copy is visible and explicitly says to hold about half a second, release, and that quick taps are ignored. Normal/min/light layouts remain aligned and unclipped. Left/right corner selected/live geometry remains visually mirrored; reverse-close recognition changes only inside the already-rendered diagonal lane.
- **Package #1451 / run `34089402756`** passed UI/service publish, compact-payload checks, payload/bootstrap construction, deep installer/service/IPC lifecycle, custom-location preservation, clean uninstall, checksum creation and immutable alpha.14.1 → alpha.42 updater compatibility.
- **Package artifact `10006325268`** (`ThinkControl-0.1.0-alpha.42-dev.1451`) has SHA-256 digest `3597664f0a773aa35ed5a2997476e354dd961f1eca08e62da0f87dc8666639c1`.
- Focused source review confirmed the final center path is release-only: `GestureActionRouter` records one candidate timestamp and calls `ShouldCommitHold` only on release; there is no delayed auto-fire worker. `TrackCenterGesturePolicy` requires 450 ms, <=3 mm and center-start ownership. `EdgeGestureRecognizer` preserves maximum candidate excursion and resumes normal edge direction recognition beyond the hold slop. The 9 mm Track skip threshold remains unchanged. Reverse close changes only the canonical start classifier inside the visible corner lane.

Physical finger feel is intentionally not marked proven by these hosted results.

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove finger feel. Alpha.42 is specifically intended to address physical alpha.41 feedback, so do not convert automated tests into claims that the interaction now feels correct.

Real-pad checks after installing alpha.42:

- [ ] A quick center tap does **nothing** and cannot unexpectedly start playback.
- [ ] A deliberate roughly half-second center hold toggles exactly once **on release**.
- [ ] Nothing auto-fires while the finger is still being held down.
- [ ] Normal small stationary-finger jitter stays usable within the 3 mm hold slop.
- [ ] Moving beyond 3 mm disarms Play/Pause even if the finger returns near its start.
- [ ] Deliberate ~9 mm+ Previous/Next swipes still work and do not also toggle Play/Pause.
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