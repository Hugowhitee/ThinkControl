# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.38`;
- immutable tag/release SHA: `7fa4f8507d5118e94e851c02787cabf7938b8ff9`;
- published 2026-09-05 as a prerelease;
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`.

Current alpha.39 candidate:

- branch: `fix/alpha39-touchpad-bottom-edge`;
- PR: #75 — **Finish Touchpad bottom lane and harden fan test UX**;
- version: `v0.1.0-alpha.39`;
- base: immutable alpha.38 / `main` at `7fa4f8507d5118e94e851c02787cabf7938b8ff9`;
- `version.json.releaseReady` remains `false` until the implementation head passes CI + Package and its WPF artifacts are manually inspected;
- no new guessed low-level write contract is authorized by this candidate.

Alpha.39 exists for two evidence-backed reasons after alpha.38 shipped: the Touchpad bottom Track lane still looked like three unrelated controls, and real X9 testing failed the experimental Lenovo Other Mode target-RPM writer's previously documented physical acceptance criteria.

## Alpha.39 product delta

### Touchpad bottom-edge Track contract

- Track control is one continuous selected edge band with **Previous | Play/Pause | Next** rendered inside the same lane.
- The old rounded/floating center Play/Pause pill is removed.
- Previous/Next no longer float outside the lane.
- The Play/Pause center segment spans 20% of the lane instead of the previous 12% target.
- Center tap timing is slightly more forgiving while movement tolerance stays below the general edge-claim threshold; Previous/Next retains the existing deliberate swipe threshold.
- Assigning Track control automatically owns all three segments. The separate **Center play / pause** settings row/switch is removed.
- The serialized `TrackCenterPlayPauseEnabled` field remains readable for old settings but is derived from the Track binding at runtime, so it is compatibility data rather than a second product feature state.
- `TouchpadVisualizer` remains the only zone rendering/selection owner; no new overlay, input worker or recognizer is introduced.

### Fan calibration and diagnostic UX

- Calibration is an attention/task surface only while required or running.
- Once `FanCalibrationUiState.Ready` is true and no calibration is running, the top calibration card disappears; a completed mapping does not permanently occupy the page.
- Manual percentage output is presented as **Temporary fan test**, not ordinary persistent control.
- Manual percentage and raw provider states use the existing 30-second automatic restore contract and explicit **End test** behavior.
- **Raw EC diagnostics** remain available only when the active provider explicitly exposes the discrete-EC semantic contract; they are not a generic laptop option.
- Temporary test UI is hidden when no verified writable provider is active.

### X9 Lenovo Other Mode physical rejection

Physical alpha.38 testing on the reference X9 produced the same failure modes that the earlier target-RPM development plan explicitly defined as rejection criteria:

- a fixed ThinkControl fan target repeatedly speeds up/slows down rather than settling smoothly;
- the audible behavior reproduces the prior wave/re-kick concern;
- nominal ThinkControl 100% remains below naturally hot Lenovo firmware Auto;
- `FanSupervisor` does not continuously rewrite a manual target while it is active, so the observed pulsing is not explained by the normal supervision loop repeatedly issuing the same command.

Alpha.39 therefore changes the product authorization state instead of cosmetically relabelling or overdriving that path:

- Lenovo Other Mode `fanX_input` remains usable as native dual-fan telemetry evidence;
- `fanX_target` product writes are held read-only behind an explicit physical-acceptance gate;
- metadata such as VALID+GET+SET plus Fan Test min/max values is not enough to re-enable the writer after physical rejection;
- target `0` remains available for cleanup/reassertion of firmware Auto after previously owned alpha.38 state;
- once native OEM fan telemetry is confirmed, the existing service-lifetime safety latch prevents silent fallback to the known-inferior discrete EC writer;
- no larger guessed RPM, maintenance IOCTL, `0x40` EC override or other speculative writer is substituted.

A future X9 writer can be promoted only after two real channels, smooth fixed-target settling, useful high-cooling range comparable with naturally hot Auto and repeated clean Auto handoff are all physically demonstrated again.

### Preserved alpha.38 baseline

- mirrored Touchpad top-corner geometry and single-owner corner/edge model remain intact;
- Compact ↔ Advanced shell-transition ownership stays canonical;
- minimized/hidden Advanced recovery and `TargetParameterCountException` guards remain intact;
- generic Fan calibration and Keyboard Effects remain provider-capability-driven;
- Home/Updates still share one Last-checked owner;
- EnergyDrv remains read-only until a reviewed writer contract exists;
- firmware/OEM Auto handoff, explicit provider ownership and unknown-device fail-closed behavior remain unchanged;
- no current-client compatibility endpoint is removed merely because it is not exposed in the modern UI.

## Validation ownership

### CI owns

- repository hygiene;
- solution restore/build;
- Core/source regression tests;
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

Do not recreate a third full installer workflow. CI and Package are the required PR gates. Superseded PR runs may cancel; immutable/tag release packaging does not.

## Alpha.39 release gate

- [x] Started from immutable alpha.38 / current `main` at `7fa4f8507d5118e94e851c02787cabf7938b8ff9`.
- [x] Kept the follow-up on one branch/PR (#75).
- [x] Reworked Track rendering into one continuous lane with Previous/Play-Pause/Next inside the same edge band.
- [x] Increased the center hit segment from 12% to 20% while keeping tap movement below the general edge claim threshold.
- [x] Removed the separate Center play/pause settings row/switch without breaking old serialized settings.
- [x] Kept `TouchpadVisualizer`, the existing recognizer and the existing router as the only owners; no duplicate overlay/input path was added.
- [x] Changed completed fan calibration from a permanent top card to non-attention provider state.
- [x] Reframed manual percentage/raw EC controls as bounded temporary tests and kept EC diagnostics capability-gated.
- [x] Converted the physically rejected X9 Other Mode writer to read-only product state while preserving native telemetry and Auto cleanup/reassertion.
- [x] Preserved the native OEM telemetry latch so rejected native writes cannot silently re-enable the inferior EC fallback.
- [x] Updated README/Product/Architecture/Device Support/Alpha Testing contracts to describe alpha.39 rather than claiming the rejected writer is preferred.
- [x] `version.json` identifies `0.1.0-alpha.39` with `releaseReady=false` during implementation/QA.
- [ ] Exact implementation head passes CI: repository hygiene, Release build, all Core/source tests, ShellSmoke and WPF snapshot rendering.
- [ ] Manually inspect at least `advanced-touchpad.png`, `advanced-touchpad-wide.png`, `advanced-touchpad-light.png`, corner selected/live fixtures, `advanced-fans*.png` and `advanced-fans-manual-test.png` from that exact CI artifact.
- [ ] Confirm the wide Touchpad fixture shows Previous/Play-Pause/Next inside one continuous lane with no floating pill/icons, and that the center segment remains legible in light/dark themes.
- [ ] Confirm Fans calibration-required fixture is still truthful while ordinary ready/unavailable/manual-test fixtures do not leave a stale completed-calibration card at the top.
- [ ] Exact implementation head passes Package ThinkControl including UI/service publish, installer/service/IPC/update/uninstall smoke and immutable alpha.14.1 → alpha.39 updater regression.
- [ ] Review PR changed files/comments and confirm no speculative low-level fan writer or accidental second Touchpad owner entered the diff.
- [ ] Freeze implementation. Set `version.json.releaseReady=true` and update this checklist with exact CI/Package run IDs + visual artifact evidence in a docs/version-only final commit.
- [ ] Require CI + Package to pass again on that exact frozen head.
- [ ] Mark PR #75 ready, review checks/comments and merge with the exact expected head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.39 commit and immutable alpha.38 remains unchanged.
- [ ] Verify `Promote release-ready main` creates `v0.1.0-alpha.39` at the merged commit.
- [ ] Verify alpha.39 is immutable with exactly Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png` and valid published checksums.

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove these. The X9 is the current reference device, not the product boundary.

### Confirmed physical evidence from alpha.38

- [x] Lenovo Other Mode exposed plausible native dual-fan behavior sufficient to investigate the target-RPM writer.
- [x] Fixed ThinkControl target reproduced repeated speed cycling/wave/re-kick instead of stable settling.
- [x] Nominal ThinkControl 100% remained below naturally hot Lenovo Auto.
- [x] Those observations fail the writer's earlier explicit physical acceptance criteria; alpha.39 therefore holds it read-only rather than force-writing beyond Lenovo metadata.

### Alpha.39 real-device checks

- [ ] Install alpha.39 on machine type `21Q6`/`21Q7` and record the Fans provider/detail line.
- [ ] Confirm real Fan 1/Fan 2 native telemetry remains visible where Other Mode/EnergyDrv supplies it.
- [ ] Confirm no normal percentage/curve/manual fan controls are enabled merely because Other Mode metadata is write-capable.
- [ ] Confirm Raw EC diagnostics do not silently reappear on the X9 after the native writer is rejected.
- [ ] From any stale alpha.38-owned target, return/reassert firmware Auto and confirm both channels settle back under Lenovo ownership.
- [ ] Verify the calibration task appears only if an actually active provider advertises calibration and disappears once that provider is ready.
- [ ] Verify temporary manual test copy/countdown/restore on hardware where a verified writable provider actually exists.
- [ ] Verify Bottom Track Previous/Play-Pause/Next feel like one lane on the real haptic pad; specifically test center hit reliability and accidental skip rate.
- [ ] Verify top-corner idle/selected/live symmetry and reverse-close accidental-trigger rate remain unchanged.
- [ ] Verify Keyboard Effects become available only when the active provider advertises them and do not produce the Lenovo brightness pop-up.
- [ ] Verify manual update checks refresh Last checked immediately on Home and Updates.
- [ ] Continue issue #60 field observation for `TargetParameterCountException`; source regression is guarded but issue closure needs real-world evidence.
- [ ] Export a support bundle after physical testing so bounded provider/fan evidence can be compared with observations.

## Release workflow principles

For future releases:

- start from current `main` and inspect branches/PRs/releases first;
- keep one coherent release branch/PR;
- preserve capability boundaries and existing owners rather than stacking duplicate providers/timers/overlays;
- treat provider metadata and physical write acceptance as separate gates when hardware behavior requires it;
- keep generic pages vendor/model-neutral and consume explicit semantic capabilities;
- keep model-specific implementation and safety evidence inside the provider/hardware layer;
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
- [x] Raw EC controls require explicit provider/model validation rather than appearing as a generic laptop feature.
- [x] Setup distinguishes registration metadata from real provider/device readiness.
- [x] X9 fan semantics distinguish native telemetry, physically accepted writers and discrete provider fallbacks.
- [x] Fan calibration and Keyboard Effects are exposed to generic UI as semantic provider capabilities.
- [x] A physically rejected native writer can remain telemetry-only without falling back to a known-inferior writer.
- [ ] Continue replacing residual device-name assumptions outside narrowly justified recovery/safety paths.
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
