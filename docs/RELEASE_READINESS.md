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
- PR: #75;
- version: `v0.1.0-alpha.39`;
- base: immutable alpha.38 / `main` at `7fa4f8507d5118e94e851c02787cabf7938b8ff9`;
- implementation head validated before freeze: `31af3cc646e04bb5c194ac3f2cbc7c8e0887532b`;
- `version.json.releaseReady=true` after implementation CI + Package passed and the exact WPF artifact was manually inspected;
- no new guessed EC register, RPM ceiling, IOCTL or vendor write contract is authorized by this candidate.

Alpha.39 has three evidence-backed stabilization goals after alpha.38 shipped: finish the Touchpad Track lane, replace the physically rejected X9 fixed-target fan path without disabling normal cooling profiles, and remove slow/dormant behavior from silent Windows startup so configured edge gestures do not wait for a ThinkControl window activation.

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
- Manual percentage and raw provider states use the existing 30-second automatic restore contract and explicit **End test** behavior where a direct provider supports them.
- **Raw EC diagnostics** remain available only when the active provider explicitly exposes the discrete-EC semantic contract; they are not a generic laptop option.
- Direct-test/curve UI is hidden on a firmware-policy backend and whenever no physically accepted direct writer is active.

### X9 cooling backend after alpha.38 physical rejection

Physical alpha.38 testing on the reference X9 produced the same failure modes that the earlier target-RPM development plan explicitly defined as rejection criteria:

- a fixed ThinkControl fan target repeatedly speeds up/slows down rather than settling smoothly;
- the audible behavior reproduces the prior wave/re-kick concern;
- nominal ThinkControl 100% remains below naturally hot Lenovo firmware Auto;
- `FanSupervisor` does not continuously rewrite a manual target while it is active, so the observed pulsing is not explained by the normal supervision loop repeatedly issuing the same command.

Alpha.39 keeps that failed direct writer read-only instead of cosmetically relabelling or overdriving it:

- Lenovo Other Mode `fanX_input` remains usable as native dual-fan telemetry evidence;
- `fanX_target` product writes are held read-only behind the explicit physical-acceptance gate;
- metadata such as VALID+GET+SET plus Fan Test min/max values is not enough to re-enable the writer after physical rejection;
- target `0` remains available for cleanup/reassertion of firmware Auto after previously owned alpha.38 state;
- once native OEM fan telemetry is confirmed, the service-lifetime safety latch prevents silent fallback to the known-inferior discrete EC writer;
- no larger guessed RPM, maintenance IOCTL, `0x40` EC override or other speculative writer is substituted.

Normal user-facing cooling remains available through a **different semantic capability**: the already reviewed exact-X9 Lenovo LITSSvc thermal-policy backend. `LenovoCoolingPolicyCoordinator` maps:

```text
Quiet        -> Lenovo Quiet policy
Balanced     -> Lenovo Balanced policy
Max cooling  -> Lenovo Performance cooling policy
Auto         -> clear cooling override and restore current power-policy baseline
```

This firmware-policy backend keeps Lenovo in the closed-loop fan controller. It is exposed separately as `FanControlKinds.FirmwarePolicy`; it does not pretend to provide direct RPM/PWM percentages. Home, Compact and Fans retain Auto/Quiet/Balanced/Max cooling, while custom curves/manual percentages remain direct-writer features.

The coordinator also resolves the previous product conflict between Performance and Fans. Before a built-in cooling profile becomes active, the current Windows/Lenovo performance preference is stored as its restore baseline. A later Performance change updates that baseline without silently cancelling the cooling profile; Auto clears the override and restores the newest baseline.

A future X9 direct writer can be promoted only after two real channels, smooth fixed-target settling, useful high-cooling range comparable with naturally hot Auto and repeated clean Auto handoff are all physically demonstrated again.

### Windows startup and gesture readiness

The existing installer/Settings contract still starts the UI executable per-user through the Windows Run entry with `--tray`; alpha.39 does not add a second startup owner or modify machine-wide Explorer startup-delay policy.

The application-side startup path changes materially:

- `Application.Startup` marks exactly one `SystemStatusService.Read()` as a fast startup preflight;
- that preflight reads cheap firmware identity from `HKLM\HARDWARE\DESCRIPTION\System\BIOS` plus Windows power state, not the full WMI CPU/GPU/BIOS inventory;
- the existing initial `RefreshStatusAsync` immediately performs the rich cached WMI inventory on a worker via `Task.Run`;
- enabled Touchpad gestures are explicitly queued during startup after the handler yields;
- silent `--tray` startup gives raw-input registration background priority because there is no visible destination window whose first paint needs protection;
- `Application.Activated` remains a recovery path but is no longer the only path that starts edge gestures.

This fixes a real architecture bug: a tray-only launch can remain completely unactivated, so the previous activation-only gesture startup could leave edge gestures dormant until the user opened ThinkControl. Hosted source/build tests can prove the startup ownership change, but actual Windows logon timing remains a system-session validation item.

### Preserved alpha.38 baseline

- mirrored Touchpad top-corner geometry and single-owner corner/edge model remain intact;
- Compact ↔ Advanced shell-transition ownership stays canonical;
- minimized/hidden Advanced recovery and `TargetParameterCountException` guards remain intact;
- generic Fan calibration and Keyboard Effects remain provider-capability-driven;
- Home/Updates still share one Last-checked owner;
- EnergyDrv remains read-only until a reviewed direct writer contract exists;
- firmware/OEM Auto handoff, explicit provider ownership and unknown-device fail-closed behavior remain unchanged;
- the installer still owns one per-user Start-with-Windows mechanism rather than adding a duplicate Task Scheduler/startup path;
- supported installed-client compatibility endpoints remain until the explicit updater floor advances.

## Validation ownership

### CI owns

- repository hygiene;
- solution restore/build;
- Core/source regression tests, including startup critical-path and tray-gesture ownership guards;
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
- the existing `startwithwindows` HKCU Run-entry install/update/uninstall contract;
- checksums and development artifact.

Do not recreate a third full installer workflow. CI and Package are the required PR gates. Superseded PR runs may cancel; immutable/tag release packaging does not.

### Validated implementation evidence

Exact implementation head `31af3cc646e04bb5c194ac3f2cbc7c8e0887532b` passed both required PR pipelines before release freeze:

- CI run `34044922761` / #1662: Release build succeeded with 0 warnings and 0 errors; all **152/152** Core/source tests passed; Compact/Advanced ShellSmoke passed; **85** WPF snapshots rendered successfully.
- WPF artifact `9992815021` (`ThinkControl-Visual-QA`), SHA-256 `7932b4adad64b3d375bc0ca94ac147e2df9d9254a1ebc322cc0b631a9209eb21`, was downloaded and manually inspected.
- Touchpad inspection covered normal/minimum/wide/light plus top-left/top-right selected/live fixtures. The wide Bottom Track fixture shows Previous, Play/Pause and Next inside one continuous edge lane, with the center segment integrated into the band and no separate floating pill or duplicate skip glyph system. Selected/live corner fixtures remain visually mirrored with matching geometry/state grammar.
- Fans inspection covered calibration-required, ready/active-curve, unavailable and temporary-manual-test states. Required calibration remains an attention card, the ready state no longer carries a stale completed-calibration card, unavailable state stays read-only, and the temporary direct-output test is explicitly bounded/restore-oriented rather than presented as ordinary persistent control.
- Package run `34044922757` / #1379 passed UI/service publish, payload budgets, bootstrap build, installer/service/IPC lifecycle, custom-location persistence, uninstall cleanup and real alpha.14.1 → alpha.39 updater compatibility. The development package artifact is `9992815901`, SHA-256 `c300522fe60fb223ea1c26b8a303de2b57afacb3a7c4a00dd10e22f77b0b0485`.
- PR #75 changed-file review found one startup owner, one Touchpad recognizer/visual owner, the rejected Other Mode direct writer still physically gated read-only, and no speculative replacement EC/IOCTL/RPM ceiling. PR review/comment threads were empty at freeze time.

## Alpha.39 release gate

- [x] Started from immutable alpha.38 / current `main` at `7fa4f8507d5118e94e851c02787cabf7938b8ff9`.
- [x] Kept the follow-up on one branch/PR (#75).
- [x] Reworked Track rendering into one continuous lane with Previous/Play-Pause/Next inside the same edge band.
- [x] Increased the center hit segment from 12% to 20% while keeping tap movement below the general edge claim threshold.
- [x] Removed the separate Center play/pause settings row/switch without breaking old serialized settings.
- [x] Kept `TouchpadVisualizer`, the existing recognizer and the existing router as the only owners; no duplicate overlay/input path was added.
- [x] Changed completed fan calibration from a permanent top card to non-attention provider state.
- [x] Reframed manual percentage/raw EC controls as bounded direct-provider tests and kept EC diagnostics capability-gated.
- [x] Converted the physically rejected X9 Other Mode writer to read-only product state while preserving native telemetry and Auto cleanup/reassertion.
- [x] Preserved the native OEM telemetry latch so rejected native writes cannot silently re-enable the inferior EC fallback.
- [x] Added a distinct X9 firmware-policy backend so **Auto / Quiet / Balanced / Max cooling stay functional** without re-authorizing the rejected fixed-target writer.
- [x] Coordinated firmware cooling overrides with the current Windows performance baseline so Performance and Fans no longer repeatedly overwrite each other.
- [x] Removed rich WMI discovery from the synchronous app-startup critical path with a one-shot registry identity preflight and existing asynchronous refresh.
- [x] Made enabled edge gestures explicitly start during `--tray` startup instead of depending on WPF `Activated`.
- [x] Added source guards for startup critical-path and silent tray gesture startup ownership.
- [x] Updated README/Product/Architecture/Device Support/Hardware Safety/Cooling Design/Alpha Testing contracts for the alpha.39 architecture; release-readiness remains the mutable handoff.
- [x] Exact implementation head passed CI: repository hygiene, Release build, 152/152 Core/source tests, ShellSmoke and 85-snapshot WPF rendering.
- [x] Manually inspected the required Touchpad/Fans screenshots from exact artifact `9992815021`.
- [x] Confirmed the wide Touchpad fixture shows Previous/Play-Pause/Next inside one continuous lane with no floating pill/icons and the center segment remains legible in dark/light coverage.
- [x] Confirmed Fans required/ready/unavailable/manual-test states do not leave a stale completed-calibration card and do not fake direct percentage/RPM ownership on the firmware-policy path.
- [x] Exact implementation head passed Package ThinkControl including UI/service publish, installer/service/IPC/update/uninstall smoke, Start-with-Windows Run-entry contract and immutable alpha.14.1 → alpha.39 updater regression.
- [x] Reviewed PR changed files/comments and confirmed no speculative low-level fan writer, accidental second Touchpad owner or duplicate startup mechanism entered the diff.
- [x] Froze implementation and set `version.json.releaseReady=true`; this document records the pre-freeze evidence.
- [ ] Require CI + Package to pass again on the exact frozen docs/version head.
- [ ] Mark PR #75 ready, review final checks/comments and merge with the exact expected head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.39 commit and immutable alpha.38 remains unchanged.
- [ ] Verify `Promote release-ready main` creates `v0.1.0-alpha.39` at the merged commit.
- [ ] Verify alpha.39 is immutable with exactly Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png` and valid published checksums.

## Physical X9 / Windows-session follow-up — separate evidence class

Hosted CI cannot prove these. The X9 is the current reference device, not the product boundary.

### Confirmed physical evidence from alpha.38

- [x] Lenovo Other Mode exposed plausible native dual-fan behavior sufficient to investigate the target-RPM writer.
- [x] Fixed ThinkControl target reproduced repeated speed cycling/wave/re-kick instead of stable settling.
- [x] Nominal ThinkControl 100% remained below naturally hot Lenovo Auto.
- [x] Those observations fail the writer's earlier explicit physical acceptance criteria; alpha.39 therefore holds it read-only rather than force-writing beyond Lenovo metadata.

### Alpha.39 real-device/session checks

- [ ] Install alpha.39 on machine type `21Q6`/`21Q7` and record the Fans provider/detail line.
- [ ] Confirm real Fan 1/Fan 2 native telemetry remains visible where Other Mode/EnergyDrv supplies it.
- [ ] Confirm Fans/Home/Compact offer **Auto / Quiet / Balanced / Max cooling** through the firmware-policy backend.
- [ ] Under comparable load, confirm Quiet is the least aggressive/smooth Lenovo-managed profile, Balanced is stable normal behavior and Max cooling reaches useful high-cooling Lenovo behavior without the alpha.38 fixed-target re-kick cycle.
- [ ] While a non-Auto cooling profile is active, change Windows performance preference and confirm the cooling profile remains active while the new preference becomes the Auto restore baseline.
- [ ] Select Auto and confirm the latest Windows/Lenovo power-policy baseline returns.
- [ ] Confirm no normal percentage/custom-curve/manual direct controls are enabled merely because Other Mode metadata is write-capable.
- [ ] Confirm Raw EC diagnostics do not silently reappear on the X9 after the native writer is rejected.
- [ ] From any stale alpha.38-owned target, return/reassert firmware Auto and confirm both channels settle back under Lenovo ownership.
- [ ] Verify the calibration task appears only if an actually active direct provider advertises calibration and disappears once that provider is ready.
- [ ] Verify temporary manual test copy/countdown/restore on hardware where a verified direct provider actually exists.
- [ ] With **Start with Windows** and an obvious edge gesture enabled, sign out/in or reboot; without opening ThinkControl, confirm the gesture works once the tray process has started and record approximate desktop → first-success timing.
- [ ] Repeat the startup gesture check after a cold reboot and after sign-out/sign-in so Windows Run timing is not inferred from one session.
- [ ] Verify Bottom Track Previous/Play-Pause/Next feel like one lane on the real haptic pad; specifically test center hit reliability and accidental skip rate.
- [ ] Verify top-corner idle/selected/live symmetry and reverse-close accidental-trigger rate remain unchanged.
- [ ] Verify Keyboard Effects become available only when the active provider advertises them and do not produce the Lenovo brightness pop-up.
- [ ] Verify manual update checks refresh Last checked immediately on Home and Updates.
- [ ] Continue issue #60 field observation for `TargetParameterCountException`; source regression is guarded but issue closure needs real-world evidence.
- [ ] Export a support bundle after physical testing so bounded provider/fan evidence can be compared with observations.

If application-side startup is fast but the ThinkControl process itself still appears materially late after sign-in, treat that as separate Windows Run-launch timing evidence. Do not modify a machine-wide Explorer `StartupDelayInMSec` policy as a product workaround. A future move from HKCU Run to a per-user scheduled logon task would be an installer/startup-contract change requiring its own install/update/uninstall smoke and must not be stacked into alpha.39 without evidence that the remaining delay is actually Windows launch scheduling.

## Release workflow principles

For future releases:

- start from current `main` and inspect branches/PRs/releases first;
- keep one coherent release branch/PR;
- preserve capability boundaries and existing owners rather than stacking duplicate providers/timers/overlays;
- treat provider metadata and physical write acceptance as separate gates when hardware behavior requires it;
- keep generic pages vendor/model-neutral and consume explicit semantic capabilities;
- keep model-specific implementation and safety evidence inside the provider/hardware layer;
- keep startup/navigation independent of slow hardware discovery and avoid duplicate background/startup owners;
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
- [x] X9 fan semantics distinguish native telemetry, firmware policy, physically accepted direct writers and discrete provider fallbacks.
- [x] Fan calibration and Keyboard Effects are exposed to generic UI as semantic provider capabilities.
- [x] A physically rejected native direct writer can remain telemetry-only without falling back to a known-inferior writer.
- [x] A semantic firmware-policy backend can preserve useful built-in fan profiles without falsely advertising direct RPM/PWM control.
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

A green compiler is not release readiness. Promotion requires exact-head build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review and immutable release verification. Physical hardware behavior and actual Windows-logon startup timing remain separate evidence classes and must never be invented from hosted CI.
