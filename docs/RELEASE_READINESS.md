# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.35`
- immutable tag/release SHA: `b6d2fb7a0a19d65dbac070f1c3f54fb9a662c6eb`
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`

Current `main` before the alpha.36 merge:

- `aa697799319b8b942e58834649f7fed25bc33b85`
- includes PR #70 touchpad corner reliability/reverse-close work

Current release candidate:

- branch: `fix/x9-dual-fan-control-stability`
- PR: #71 — **Harden X9 dual-fan control with Lenovo OEM target-RPM provider**
- version: `v0.1.0-alpha.36`
- release scope: post-alpha.35 touchpad corner completion + X9 dual-fan/native-provider hardening + bounded diagnostics/research support
- alpha.36 remains a prerelease; physical X9 behavior is not inferred from hosted runners

The release branch can ship because risky fan behavior fails closed: a direct Lenovo target-RPM writer is exposed only when exact-X9 identity, capability data, constraints and live channel reads all agree; EnergyDrv remains read-only until its write encoding is actually recovered; and the known-inferior EC writer does not silently reappear after native two-fan evidence has been proven during the hardware-service lifetime.

## Alpha.36 product delta

### Touchpad

- unified Top-left / Top-right with the same six-zone editor used by Top/Bottom/Left/Right;
- exact mirrored **guard → diagonal lane → rounded end-cap** geometry;
- enabled corner guard is the recognizer's real first-frame priority area;
- rejected corner candidates remain locked out until lift and cannot fall through into the nearby edge gesture;
- center divider replaced by a directional arrow; rounded end-cap replaces the old flat/blob treatment;
- enabled corner actions show Compact/Advanced semantic icon + text;
- optional per-corner **Reverse swipe closes ThinkControl** uses the same recognizer/ownership path rather than a second overlay/worker;
- live corner state must not cause editor reflow.

### X9 fans

Physical evidence before alpha.36 established:

- the X9 has two physical fan channels and ThinkControl can expose both through reviewed telemetry paths;
- the old one-fan product model was incomplete;
- `dev.1191` still used `Fallback · verified X9 discrete EC telemetry/control`;
- EC Max Cooling remained softer than naturally hot Lenovo Auto and could have a faint electronic/buzzy/wavy character;
- therefore EC step 7 is **not** accepted as the finished X9 product maximum.

Alpha.36 architecture:

1. **Lenovo Other Mode target-RPM** is the preferred writer when the exact X9 exposes at least two independently live VALID+GET+SET fan channels with sane Lenovo Fan Test constraints.
2. Fan attributes use Lenovo's documented `0x04030001` onward IDs. `GetFeatureValue` reads current RPM; `SetFeatureValue` writes the target; target `0` returns the owned channel to Lenovo Auto; effective targets use 100-RPM granularity.
3. Extra/phantom capability records do not make the whole provider all-or-nothing: two real live constrained writable channels are sufficient.
4. ThinkControl records the exact channels it actually writes and returns only owned channels to Auto. Provider refresh preserves ownership evidence if an Auto handoff fails so cleanup can be retried.
5. **Lenovo EnergyDrv** `QueryFanSpeed 0x83102570` is read-only native telemetry. The separate `ChangeFanSpeed 0x8310257C` writer remains blocked until the exact X9 `dwFanCtrlCmd` encoding and rollback/Auto semantics are recovered.
6. Once two native Lenovo fan channels are proven during a service lifetime, transient native telemetry loss cannot silently re-authorize the EC writer.
7. The classic seven-step ThinkPad EC path remains an exact-model fallback/investigation provider only. The ambiguous `0x40` full-speed/disengaged family remains blocked after exact-X9 testing echoed `0x47` while producing 0 RPM.
8. Fan-state changes invalidate stale RPM before a settled replacement is presented. Visible Fans-page refresh advances the canonical status pipeline without creating a high-rate EC polling loop.
9. Bounded support diagnostics preserve active provider, control temperature, applied state and up to two fan RPM values without starting a duplicate hardware polling worker.

### Research-only Lenovo evidence

Do not turn these into production writes merely because the symbols exist:

- EnergyDrv `0x8310257C ChangeFanSpeed` — one `dwFanCtrlCmd` DWORD in / one action-status DWORD out, but X9 command encoding still unknown;
- EnergyDrv `0x831020C0` dust/temporary high-speed family — maintenance behavior, not a smooth target-RPM contract;
- family-specific `0x8310213C` ITS/Geek full-speed overlay — not generalized to X9;
- ThinkSmartSense/LITSSvc AC/DC Cool `500/501` and `505/506`;
- Improved Cooling Efficiency `510/511`;
- Balanced/Performance LCM `31..34`.

`tools/research/Capture-LenovoAuto.ps1` remains observational. Its Lenovo Other Mode path invokes **GetFeatureValue only**, its EnergyDrv calls are read/query contracts only, and optional OEM binaries remain local until explicitly shared. `tools/research/Analyze-LenovoOemFanBinaries.ps1` is static/offline analysis only and never opens a driver or executes captured OEM binaries.

## Validation ownership

### CI owns

- repository hygiene;
- solution restore/build;
- Core tests;
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

## Alpha.36 release gate

- [x] Work remained on one active branch / one PR.
- [x] AGENTS, architecture, product, device support, testing and release handoff were recovered before release preparation.
- [x] Known-good alpha.33 shell/touchpad lifecycle protections were preserved rather than rewritten casually.
- [x] Fan provider ownership is single-owner through `FanSupervisor` / `LenovoHardwareController`; no duplicate fan worker was introduced.
- [x] Other Mode direct-RPM writes are capability- and live-read-gated.
- [x] EnergyDrv writer remains disabled; no `GENERIC_WRITE`/brute-force fan command was added.
- [x] Research scripts are parser-checked in CI and are read-only/static by contract.
- [x] `version.json`, README and active version docs are frozen at `v0.1.0-alpha.36`.
- [x] Representative Fans and Touchpad visual-QA states have been manually inspected during release preparation.
- [x] Final changed-file scope contains only release docs/version plus the intended fan provider/service/UI/diagnostic/research/test work.
- [ ] Immediately before merge, require **CI + Package ThinkControl green on the exact same final PR head**. Record the concrete head/run IDs in the PR body so this handoff does not need another evidence-only commit that would invalidate the head.
- [ ] Squash-merge with that exact expected head SHA.
- [ ] Verify post-merge `main`.
- [ ] Verify `Promote release-ready main` creates `v0.1.0-alpha.36` at the merged commit and does not move alpha.35.
- [ ] Verify the new prerelease has exactly Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png` and valid checksums.

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove these. Alpha.36 may be published with these still open because unsupported/unproven writers fail closed, but the results must remain documented honestly.

- [ ] Install alpha.36 on machine type `21Q6`/`21Q7` and record the Fans provider/detail line before changing fan state.
- [ ] If **Lenovo Other Mode direct target-RPM** activates, verify two plausible live fan channels plus manual 25/50/75/100% settling.
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
