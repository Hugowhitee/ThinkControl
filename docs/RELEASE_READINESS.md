# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.37`;
- immutable tag/release SHA: `752ede6365a3f2423fb2e1507af0d7cc589c803a`;
- published 2026-09-05 as a prerelease;
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`.

Current alpha.38 candidate:

- branch: `fix/alpha38-touchpad-fan-ux`;
- PR: #74 — **Finish touchpad visuals, update freshness and fan calibration UX**;
- version: `v0.1.0-alpha.38`;
- base: immutable alpha.37 / `main` at `752ede6365a3f2423fb2e1507af0d7cc589c803a`;
- no new low-level write contract is authorized by this candidate.

Alpha.38 exists because physical UI review after alpha.37 still showed Touchpad corner-zone visual inconsistency, the optional Track-center media action still behaved like a hidden hold gesture, update-check freshness had two owners, and generic fan/keyboard UI was making provider decisions that should be explicit capabilities.

## Alpha.38 product delta

### Touchpad visual and interaction contract

- Top-left and top-right launch regions use one canonical corner shape and the right region is an exact horizontal mirror transform of the left.
- Edge-band fill/boundary drawing is clipped around corner regions instead of visually stacking a square edge overlay beneath the corner shape.
- Edges and corners share the same idle/hover/selected/candidate/live fill and boundary grammar.
- CI asserts corner guide/fill symmetry and renders left/right selected + live fixtures.
- Optional Track-center Play/Pause is a **visible bounded tap target** inside `TouchpadVisualizer`.
- The old hidden hold-and-release policy/copy is removed; Previous/Next remains the surrounding swipe gesture.

### Provider-driven fan calibration

Generic Fans UI must not decide calibration from a laptop model name.

- service capability snapshot exposes `FanCalibrationSupported` and `FanCalibrationRequired`;
- `App.Cooling` owns the resulting generic `FanCalibrationUiState`;
- the calibration card/Inbox attention/control lock respond to that capability state;
- generic Fans copy no longer says the X9 is the product rule;
- raw provider-specific EC controls appear only when the active provider exposes the discrete-EC semantic contract;
- the current X9 discrete EC implementation remains a physically reviewed provider implementation, not the architecture boundary;
- future Lenovo/ASUS/Dell/HP/Acer/MSI/etc. providers may advertise their own calibration contract—or no calibration at all—without model-specific Fans-page branches.

### Provider-driven keyboard effects

- capability snapshot exposes `KeyboardEffects`;
- Keyboard Effects UI consumes that bit rather than parsing `Lenovo`, `Vantage` or other backend labels;
- saved Breathing/Reactive/Audio state is restored only after provider capability is known;
- current repeated-write safety rules remain unchanged: an OEM fallback that cannot safely support rapid user-session changes does not advertise effects.

### Update-check freshness

- Home and Updates publish one update result;
- one in-memory/persisted Last-checked timestamp owner records a completed manual check;
- the visible Last checked value updates immediately rather than waiting for page reconstruction.

### Preserved alpha.37 safety/lifecycle baseline

- Compact ↔ Advanced shell-transition ownership stays canonical;
- minimized/hidden Advanced recovery and the `TargetParameterCountException` regression guards remain intact;
- fan writes remain behind existing exact-device/provider/readback gates;
- Lenovo Other Mode target-RPM remains the preferred reviewed X9 writer when its gates pass;
- EnergyDrv remains read-only until a reviewed writer contract exists;
- the discrete EC fallback keeps its existing blocked ambiguous override states;
- firmware/OEM Auto handoff, provider ownership and unknown-device fail-closed behavior remain unchanged;
- no Touchpad second recognizer/worker/overlay owner is introduced.

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

## Alpha.38 release gate

- [x] Started from immutable alpha.37 / current `main` and preserved it as the regression baseline.
- [x] Kept all alpha.38 work on one branch/PR (#74).
- [x] Reworked Touchpad corner rendering into a single canonical mirrored geometry and shared zone grammar.
- [x] Replaced hidden Track-center hold behavior with a visible bounded tap target and updated policy tests/copy.
- [x] Added provider-advertised fan calibration capability state and removed X9/model rules from generic Fans UI.
- [x] Added explicit Keyboard Effects capability state and delayed saved-effect restoration until provider support is known.
- [x] Unified manual update-check Last-checked state.
- [x] Preserved existing low-level write authorization/safety boundaries.
- [x] `version.json`, README and product docs identify `v0.1.0-alpha.38` as this candidate.
- [ ] Final changed-file review confirms no accidental feature/provider expansion.
- [ ] Final exact PR-head CI passes build, all tests, ShellSmoke and visual-QA matrix.
- [ ] Inspect final `advanced-touchpad.png`, wide Touchpad, left/right selected and left/right live corner screenshots; verify symmetry, identical idle grammar, no edge overlap and visible center tap target.
- [ ] Inspect final Fans screenshots; verify calibration wording is provider-neutral and unavailable/manual states remain truthful.
- [ ] Inspect final Keyboard screenshots; verify Effects availability follows fixture capability and layout remains clean.
- [ ] Inspect final Updates screenshot; verify Last checked is current in the deterministic fixture.
- [ ] Final exact PR-head Package ThinkControl passes publish/installer/service/IPC/update/uninstall/alpha.14.1 regression checks.
- [ ] Review PR checks/comments and merge #74 with the exact expected final head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.38 commit and alpha.37 remains unchanged.
- [ ] Verify `Promote release-ready main` creates `v0.1.0-alpha.38` at the merged commit.
- [ ] Verify alpha.38 is an immutable prerelease with exactly Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png` and valid published checksums.

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove these. The X9 is the current reference device, not the product boundary.

- [ ] Install alpha.38 on machine type `21Q6`/`21Q7` and record the Fans provider/detail line before changing fan state.
- [ ] If Lenovo Other Mode direct target-RPM activates, verify two plausible live channels plus manual 25/50/75/100% settling.
- [ ] Confirm direct OEM targets do not reproduce the earlier repeating wave/re-kick/buzzy character.
- [ ] Compare OEM 100% with naturally hot Lenovo Auto without claiming metadata/self-test max equals the physical ceiling.
- [ ] Repeatedly return direct OEM ownership to Auto and confirm both owned channels release cleanly.
- [ ] If only EnergyDrv native telemetry appears, keep control read-only and confirm Fan 1/Fan 2 plausibly track physical sound.
- [ ] If only the discrete EC fallback remains, complete its real tachometer calibration before judging percentage profiles.
- [ ] Verify the calibration prerequisite/card appears because the active provider advertises it, not because the UI recognizes `21Q6`/`21Q7`.
- [ ] Verify Keyboard Effects become available only on the direct provider and do not produce the Lenovo brightness pop-up.
- [ ] Verify Touchpad corner idle/selected/live symmetry and accidental-trigger rate on the real haptic pad.
- [ ] Verify the visible center Play/Pause target behaves as a tap target while surrounding Previous/Next swipes remain reliable.
- [ ] Verify reverse-close still hides Compact/Advanced cleanly without false shell failure diagnostics.
- [ ] Verify manual update checks refresh Last checked immediately on Home and Updates.
- [ ] Continue issue #60 field observation for `TargetParameterCountException`; source regression is guarded but issue closure needs real-world evidence.
- [ ] Export a support bundle after physical testing so bounded provider/fan evidence can be compared with observations.

## Release workflow principles

For future releases:

- start from current `main` and inspect branches/PRs/releases first;
- keep one coherent release branch/PR;
- preserve capability boundaries and existing owners rather than stacking duplicate providers/timers/overlays;
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
- [x] Raw X9 EC controls require exact-model/provider validation.
- [x] Setup distinguishes registration metadata from real provider/device readiness.
- [x] X9 fan semantics distinguish native target-RPM, native read-only telemetry and discrete EC fallback.
- [x] Fan calibration and Keyboard Effects are exposed to generic UI as semantic provider capabilities.
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
