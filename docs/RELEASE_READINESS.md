# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease:

- `v0.1.0-alpha.42`
- immutable tag/release SHA: `2d40dffebeb6f8327cd06403e076936c40d60497`
- release is immutable and contains exactly four managed assets: Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png`
- alpha.41 remains separately immutable at `6088955eeab54d1af6506780fa7707df17fe11c3`
- alpha.42 is the known-good baseline for deliberate Track hold/release, widened reverse-close ownership, X9 cooling-profile runtime truth/persistence, early Raw Input startup, rejected per-fan target writer safety, installer/updater and prior shell/crash fixes

Current candidate:

- version `v0.1.0-alpha.43`
- branch `feat/alpha43-audio-safety`
- PR #80, **Add session Audio Safety modes**
- base `main` / alpha.42 at `2d40dffebeb6f8327cd06403e076936c40d60497`
- `version.json.releaseReady=false`
- issue #79 is the product request for Audio Safety
- the alpha.43 scope is Windows-generic Audio Safety plus the directly related Track-local Play/Pause option; no fan/hardware writer expansion is part of this candidate

## Alpha.43 product delta

### Audio Safety

Alpha.43 adds one canonical **session-level** policy owner with three modes:

- **Normal** — current ThinkControl media/output behavior is unchanged;
- **Media lock** — blocks ThinkControl Touchpad Volume, Media scrub, Previous/Next and integrated Play/Pause while leaving deliberate Windows/app audio available;
- **Silent** — includes Media lock, requires the active Windows render endpoint to be muted and blocks ThinkControl output-volume/unmute writes.

Architecture constraints:

- `AudioSafetyService` is the canonical user-session owner;
- `AudioSafetyRuntimeState` is a process-local output-write gate and has one writer;
- microphone/capture input stays independent;
- entering Media lock/Silent cancels an in-flight Touchpad audio action;
- blocked Touchpad actions receive bounded explanatory feedback rather than silently doing nothing;
- Silent remembers prior mute state once per output endpoint encountered and restores only those owned/recorded states on exit/orderly app shutdown;
- a default-output change reuses the application's existing status cadence for mute convergence; there is no second permanent audio polling loop;
- alpha.43 deliberately does **not persist** Audio Safety across process restart because a new process cannot truthfully inherit the old process's mute ownership;
- this is not a general phone-style Focus Modes/preset framework and Silent does not implicitly change fan/cooling state.

### Track Play/Pause option

Track control stays one edge action and one recognizer/router owner. Standalone current Play/Pause remains absent from the edge action list.

Alpha.43 adds one Track-local **Play / Pause** switch:

- the option is visible only when the selected edge uses Track control;
- existing alpha.42 configurations default to Play/Pause enabled for backward compatibility;
- enabled behavior remains the alpha.42 28% center region (`0.36..0.64`), >=450 ms hold, <=3 mm maximum radial movement and commit on release;
- release remains the final intent confirmation: reaching the hold duration while still touching does not auto-start media;
- Previous/Next keeps the unchanged 9 mm deliberate swipe threshold;
- disabling Play/Pause keeps Track assigned but removes the center recognition and visual together: no center fill, separators or Play/Pause glyph, leaving Previous/Next only;
- the explicit opt-out survives moving/removing/reassigning Track;
- no second center recognizer, overlay or standalone action was introduced.

### Hardware boundary

Alpha.43 adds no new low-level hardware writer.

- alpha.38 `fanX_target` remains physically rejected/read-only;
- the native Lenovo fan telemetry latch remains intact;
- exact-X9 full-speed `0x04020000` remains separately boolean/live/readback-gated;
- alpha.42 Quiet/Balanced/Max restore/reassert lifecycle is unchanged;
- classic EC/EnergyDrv fallbacks are not reauthorized;
- Audio Safety uses Windows semantic Core Audio APIs only.

## Current implementation evidence

The Audio Safety implementation and Track-local option are on PR #80. Before the documentation refresh, branch head `63b4cdeccfd1c45d1b8d71abd24b3c1104722aa1` produced these pipeline results:

### Package #1475

Run `34143979197` completed successfully on exact head `63b4cdeccfd1c45d1b8d71abd24b3c1104722aa1`:

- version resolution and canonical branding passed;
- UI publish passed, proving the WPF/UI changes compile;
- hardware-service publish passed;
- compact managed-payload checks passed;
- release payload and web bootstrap installer built;
- deep installer/service/IPC reliability smoke passed;
- oldest-supported alpha.14.1 updater compatibility passed;
- checksums and development artifact were produced.

This is useful implementation evidence but **not** release evidence because the same head did not complete CI/visual QA.

### CI #1762

Run `34143979130` stopped at repository hygiene before restore/build/tests because the branch had already moved `version.json` to alpha.43 while README and required release docs still named alpha.42. The concrete failures were only:

- `README.md` missing `v0.1.0-alpha.43`;
- `docs/ALPHA-TESTING.md` missing `v0.1.0-alpha.43`;
- `docs/ARCHITECTURE.md` missing `v0.1.0-alpha.43`;
- `docs/DEVICE-SUPPORT.md` missing `v0.1.0-alpha.43`;
- `docs/PRODUCT.md` missing `v0.1.0-alpha.43`.

Those docs are now advanced as part of this handoff. The failed run did **not** execute build/tests/WPF visual QA and must not be represented as a code failure or as validation evidence.

## Source review notes before next gate

The current design deliberately keeps existing owners rather than stacking helpers:

- Touchpad policy is checked at the existing `GestureActionRouter` boundary and the async media fallbacks re-check the policy before committing;
- Windows render writes fail closed through the process-local Audio Safety gate even if a UI control is stale for a moment;
- capture/microphone writes remain allowed because the gate is render-output specific;
- the Track option only controls canonical configuration; `TouchpadVisualizer` and the existing recognizer/router consume the same state, so turning Play/Pause off cannot leave a separate hidden center visual owner;
- backward compatibility uses a new explicit opt-out (`TrackCenterPlayPauseDisabled=false` by default) while retaining the legacy/runtime `TrackCenterPlayPauseEnabled` member;
- release-to-commit is retained because a timer-based auto-fire would allow a resting finger to start playback before the user can cancel by moving/lifting.

Before freeze, continue focused review for lifecycle races, event subscriptions and stale user-facing copy. In particular, Audio Safety must never restore an endpoint it did not record and the Compact/Settings state must remain one canonical session value.

## Alpha.43 implementation gate

Completed scope work so far:

- [x] Started from immutable alpha.42
- [x] Reused the single active alpha.43 branch/PR (#80)
- [x] Added canonical Normal / Media lock / Silent policy model
- [x] Media lock blocks ThinkControl Touchpad Volume/Track/seek while external audio remains available
- [x] Silent adds semantic Windows output mute + fail-closed ThinkControl output-write gate
- [x] Microphone remains independent
- [x] Silent tracks prior mute state per encountered endpoint and restores only recorded ownership
- [x] Default-output convergence reuses existing status cadence instead of a new polling loop
- [x] Audio Safety is session-only for alpha.43
- [x] Added Compact quick selector and Settings detailed state
- [x] Added Track-local Play/Pause on/off option
- [x] Kept alpha.42 450 ms + <=3 mm + release safety when Play/Pause is enabled
- [x] Disabled Track center removes center behavior while preserving Previous/Next
- [x] Added configuration/source regression tests for Track option
- [x] Package #1475 passed on the pre-doc implementation head
- [x] Advanced README/product/architecture/device/testing docs to alpha.43

Required before release freeze:

- [ ] Run CI on the new exact documentation/code head and require repository hygiene, zero-warning Release build, all tests, ShellSmoke and WPF visual QA
- [ ] Run Package ThinkControl on that same exact head
- [ ] Download and manually inspect the exact-head visual artifact, especially Compact Audio Safety, Settings Audio Safety and `advanced-touchpad-wide.png`
- [ ] Confirm the new Track Play/Pause editor row fits minimum/normal/wide layouts and does not make Track feel like a second overlay system
- [ ] Prefer a deterministic Play/Pause-off visual fixture if needed to prove the center disappears rather than relying only on source assertions
- [ ] Review stale Touchpad copy so enabled-center instructions say hold + release, not quick tap
- [ ] Finish focused Audio Safety lifecycle/event review and add tests for any concrete fix
- [ ] Update this handoff with exact run IDs, test count, snapshot count, artifact IDs/digests and manual visual findings

Release freeze/promotion steps after implementation evidence is complete:

- [ ] Set `version.json.releaseReady=true` only after the implementation-head evidence above is complete
- [ ] Require **CI + Package ThinkControl on the exact frozen head**
- [ ] Inspect frozen-head WPF artifact and confirm no UI regression
- [ ] Review complete PR changed-file list, comments, reviews and review threads
- [ ] Mark PR #80 ready and merge using exact expected-head SHA
- [ ] Verify post-merge `main`
- [ ] Verify promotion creates immutable `v0.1.0-alpha.43` at the merged commit
- [ ] Verify exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`
- [ ] Verify published Setup/Payload SHA-256 checksums
- [ ] Confirm immutable alpha.42 and alpha.41 tags/releases were not moved

## Physical follow-up — separate evidence class

Hosted CI cannot prove finger feel, audible silence, real default-endpoint transitions or Lenovo firmware acoustics. After installing the published alpha.43, record these separately.

Touchpad:

- [ ] with Play/Pause enabled, quick center tap does nothing
- [ ] roughly half-second center hold toggles once **on release**
- [ ] nothing auto-fires while still held
- [ ] <=3 mm natural jitter remains usable
- [ ] >3 mm movement permanently disarms the hold for that contact
- [ ] 9 mm Previous/Next remains reliable without Play/Pause overlap
- [ ] disable Track Play/Pause: center visual disappears and center never toggles media
- [ ] with center disabled, Previous/Next still work normally
- [ ] re-enable center: visual and behavior return together
- [ ] reverse close remains reliable across the inner half of both mirrored lanes
- [ ] outer guard still launches inward

Audio Safety:

- [ ] Media lock blocks ThinkControl Touchpad Volume/Track/seek but deliberate app/Windows audio still works
- [ ] entering Media lock/Silent during an active audio gesture stops further ThinkControl writes
- [ ] Silent mutes the current default output and ThinkControl cannot unmute/change output while active
- [ ] microphone remains independently controllable
- [ ] switching default output while Silent causes the new output to become muted without rapid polling behavior
- [ ] leaving Silent restores each encountered endpoint to its prior mute state
- [ ] an endpoint already muted before Silent stays muted afterwards
- [ ] orderly app exit from Silent restores owned states
- [ ] restart begins at Normal as designed for alpha.43

Cooling carry-forward:

- [ ] select Quiet and verify physical/runtime Quiet state
- [ ] close/reopen only the UI while service remains running; Quiet remains active
- [ ] reboot with Quiet saved; runtime UI remains truthful during restore and Quiet converges after startup
- [ ] unplug/replug AC and sleep/resume while a non-Auto profile is active; policy remains/reasserts correctly
- [ ] repeat lifecycle with Balanced and Max only where existing safety gates pass
- [ ] no alpha.38 target-RPM wave/re-kick behavior returns

## Release workflow principles

For future releases:

- recover current state from `main`, version, releases, active PR and this handoff
- stabilize related regressions before expanding scope
- improve existing owners instead of stacking helpers/timers/providers/overlays
- keep generic UI capability-first and hardware writes provider-gated
- separate hosted validation from physical evidence
- inspect UI artifacts manually
- freeze docs/version before exact final gates
- merge with expected-head guard
- verify promotion, immutable tag, assets and checksums
- never move an existing immutable release tag

The reusable version-agnostic bootstrap is [`CHAT_STARTER.md`](CHAT_STARTER.md).

## Commercial/public release program

Do not mix commercial backend/licensing work into alpha hardware stabilization.

### Installer, updater and signing

- [x] Preserve custom install location across supported in-place update.
- [x] Exercise install, service start/IPC, updater compatibility and uninstall in Package.
- [ ] Test that a failed staged update cannot destroy the last working payload.
- [ ] Define explicit uninstall policy for ThinkControl-owned local/runtime data.
- [ ] Sign binaries/installer and document/test SmartScreen reputation strategy.
- [ ] Keep legacy updater compatibility until the installed-client floor is deliberately advanced.

### Capability-driven hardware architecture

- [x] Windows-generic UI is vendor-neutral.
- [x] Raw EC controls require explicit provider/model validation.
- [x] Setup distinguishes registration metadata from real provider/device readiness.
- [x] X9 fan semantics distinguish telemetry, firmware policy, narrow global full speed, direct writers and discrete fallbacks.
- [x] Fan calibration and Keyboard Effects are semantic capabilities.
- [x] A physically rejected writer can remain telemetry-only without falling back to a known-inferior writer.
- [x] Audio Safety is Windows-generic and does not mutate hardware capability boundaries.
- [ ] Continue replacing residual device-name assumptions outside narrowly justified recovery/safety paths.
- [ ] Never show EC/PWM/vendor wording unless the active provider exposes that exact semantic contract.
- [ ] Unknown hardware remains read-only/safe until a reviewed write provider is verified.

### Privacy-safe diagnostics and device learning

Diagnostics consent and licensing remain separate. Never upload usernames, hostnames, serial numbers, personal files/paths/content, browser content, keystrokes, raw touch coordinates/trails, memory dumps or arbitrary raw logs.

- [ ] Shared redaction/schema layer powers preview and upload.
- [ ] Durable local crash journal remains source of truth; mark Reported only after acknowledgement.
- [ ] Upload/retry is asynchronous/bounded and never blocks startup.
- [ ] Unknown-device learning uses passive normal-app evidence; no experimental writes merely for telemetry.
- [ ] Confidence states: `Observed → Candidate → Verified → Regression watch`.
- [ ] Conflicting evidence blocks automatic promotion.
- [ ] Any remote profile manifest is signed/versioned and cannot inject arbitrary hardware-write instructions.

### Accounts, licensing and backend

- [ ] Define tiers, activation limits and offline grace before enforcement code.
- [ ] Use OAuth/OIDC Authorization Code + PKCE through the system browser.
- [ ] Store refresh/session secrets only in OS-protected storage.
- [ ] Purchases create server-side entitlements; desktop receives short-lived signed entitlement state.
- [ ] License/network failure never disables safety-critical firmware Auto/restore behavior.
- [ ] Device activation/deactivation is self-service.
- [ ] Payment/signing secrets never ship in the desktop client.
- [ ] Payment-provider webhooks are authoritative for purchase/refund/subscription state.
- [ ] Add audit logging, rate limiting, retention and deletion/export flows.

### Source/release transition

Do not make source private while updater/build distribution still depends on public GitHub release URLs.

- [ ] Decide public versus private surfaces.
- [ ] Move release assets/update manifest to a paid-user-compatible endpoint before privatizing source.
- [ ] Rotate credentials/tokens that were ever exposed.
- [ ] Add commercial license/EULA/privacy policy before accepting payment.

## Release principle

A green compiler is not release readiness. Promotion requires exact-head build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review and immutable release verification. Physical hardware/audio behavior remains a separate evidence class and must never be invented from hosted CI.
