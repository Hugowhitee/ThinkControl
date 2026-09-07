# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease:

- `v0.1.0-alpha.41`
- immutable tag/release SHA `6088955eeab54d1af6506780fa7707df17fe11c3`
- exactly four managed assets: Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png`
- preserved baseline for early tray/Raw-Input startup, X9 full-speed safety, rejected per-fan target writer, installer/updater and prior shell/crash fixes

Current candidate:

- version `v0.1.0-alpha.42`
- branch `fix/alpha42-touchpad-input-reliability`
- PR #78, **Make Track Play/Pause and reverse close easier to trigger**
- base `main` / alpha.41 at `6088955eeab54d1af6506780fa7707df17fe11c3`
- validated implementation head `388a676cfbc0e66b3a95bac1f7b7b7b062970ecf`
- first frozen release-ready head `1cc66681944c690096889aa33f157423ae0f62fd`
- `version.json.releaseReady=true`
- CI #1754 and Package #1468 passed on `1cc66681944c690096889aa33f157423ae0f62fd`; this documentation-only handoff update must receive one final exact-head CI + Package pass before merge

Alpha.42 was reopened twice before release for valid physical evidence: first because quick/automatic center Play/Pause was too easy to trigger accidentally, then because a saved X9 cooling profile such as Quiet could remain selected in UI while the physical machine had effectively returned to Auto/base policy after restart/lifecycle transitions. Neither superseded freeze is release evidence for the final candidate.

## Alpha.42 product delta

### Track center Play/Pause

Track remains one continuous **Previous | Play/Pause | Next** lane with one recognizer/router owner.

- center start region is 28% of the selected Track edge (`0.36..0.64`)
- quick center taps intentionally do nothing
- Play/Pause requires at least 450 ms hold time
- maximum eligible radial excursion is 3 mm
- maximum excursion is preserved, so moving away and returning cannot re-arm the hold
- Play/Pause commits only on release
- after leaving the hold envelope, normal Track recognition resumes
- Previous/Next threshold remains 9 mm
- a claimed Track swipe cannot also become Play/Pause on release

### Reverse close

- visible mirrored guard → diagonal lane → rounded end-cap geometry is unchanged
- reverse-close ownership may start anywhere in the inner half of the already-visible diagonal lane
- outer guard remains inward launch
- no hidden hit area was added
- rejected corner ownership remains locked until lift

### X9 firmware-profile runtime truth and persistence

Physical pre-release testing found that a saved profile such as Quiet could appear selected after restart even though the fan audibly behaved like firmware Auto/base policy. Alpha.42 fixes the lifecycle without adding a new low-level writer.

- Fans initializes from service/runtime telemetry, not the persisted preference pretending to be applied state
- saved firmware profiles are actively restored after capability discovery
- one bounded 7-second startup-settle reassert handles a later Lenovo-service policy overwrite without creating a permanent polling fight
- AC/DC, resume and Windows power-baseline transitions reassert the currently owned Quiet/Balanced/Max profile through the same reviewed source-specific LITSSvc path
- closing/restarting only the WPF UI no longer releases a service-owned firmware profile to Auto
- direct/manual fan ownership keeps the existing Auto-on-UI-exit/timeout safety class
- service shutdown remains the final owner of firmware/full-speed/direct cleanup
- rejected per-fan `fanX_target` remains read-only
- Max still uses only the existing exact-X9 `0x04020000` boolean full-speed contract when its live/readback gates pass
- no silent classic-EC or EnergyDrv writer fallback was introduced

## Implementation evidence

Exact implementation head `388a676cfbc0e66b3a95bac1f7b7b7b062970ecf` passed both required PR pipelines after the cooling-persistence fix.

### CI #1751

Run `34135415794` completed successfully on that exact head:

- repository hygiene passed
- Release build: **0 warnings, 0 errors**
- Core/source tests: **177 passed, 0 failed, 0 skipped**
- Compact/Advanced real WPF ShellSmoke passed
- **85** deterministic WPF visual-QA snapshots rendered
- visual artifact `10023821813`, `ThinkControl-Visual-QA`
- artifact digest `cbc0a051cb1a5128b3d30436a0115a0e7505dee909e4a0ac28ac67826bb690d9`

The artifact was downloaded and manually inspected. `advanced-touchpad-wide.png` keeps Previous / Play-Pause / Next inside one continuous bottom band, the 28% center remains integrated rather than a separate pill, and the active Next treatment stays local. Top-left and top-right selected fixtures remain visually mirrored. Fans normal/manual/unavailable fixtures remain aligned; direct/manual controls still read as temporary/provider-specific rather than generic X9 controls. No screenshot regression was found from the persistence changes, which intentionally alter lifecycle/runtime truth rather than static Fans layout.

### Package #1465

Run `34135415785` completed successfully on the same exact implementation head:

- UI publish passed
- hardware-service publish passed
- compact managed-payload checks passed
- payload archive and web bootstrap installer built
- deep installer/service/IPC reliability smoke passed
- custom-location lifecycle passed
- oldest-supported immutable alpha.14.1 updater compatibility passed
- checksums generated
- development artifact uploaded
- package artifact `10023809253`, `ThinkControl-0.1.0-alpha.42-dev.1465`
- artifact digest `1cf1de4e06113db6d4daf17e5a5c759f172af626ec4c0ffad25ccf1bb5a4d038`

## Frozen release-head evidence

The release-ready freeze at `1cc66681944c690096889aa33f157423ae0f62fd` also passed the exact-head gates.

### CI #1754

Run `34136818024` completed successfully:

- repository hygiene passed
- Release restore/build passed
- all Core/source tests passed
- Compact/Advanced ShellSmoke passed
- all **85** WPF visual-QA snapshots rendered and uploaded
- frozen-head visual artifact `10024353664`, `ThinkControl-Visual-QA`
- artifact digest `205ac684ad4c78c000c17814d3726a34058ae50ea66d645598fc13e626bdbca5`

The frozen-head artifact was downloaded and inspected. Representative Touchpad and Fans snapshots remained visually unchanged from the already-reviewed implementation artifact. The only compared PNG with a different binary hash was the top-right live-corner fixture; direct visual comparison showed the same mirrored geometry and state, consistent with nondeterministic WPF raster/compression detail rather than a product change.

### Package #1468

Run `34136818032` completed successfully on the same frozen head:

- UI/service publish passed
- compact managed-payload verification passed
- payload/bootstrap construction passed
- deep installer/service/IPC reliability smoke passed
- oldest-supported alpha.14.1 updater compatibility passed
- checksums generated
- development artifact `10024350248`, `ThinkControl-0.1.0-alpha.42-dev.1468`
- artifact digest `5bdf73d5f53a4e6c0fd5c10ed5ab86d7faec94d991b2c1b953943df8a96b7ca7`

This handoff update changes documentation only. Because exact-head discipline applies to the actual merge SHA rather than a previous almost-identical head, CI + Package must pass once more on the resulting final PR head before merge.

## Source/safety review

Focused review after the physical cooling report confirms:

- `LenovoCoolingPolicyCoordinator.SetBasePowerMode` reasserts an active semantic profile through `SetBuiltInProfile`, so AC/DC/source-specific Lenovo commands converge without a second writer
- `App.Cooling` owns one bounded startup-settle retry and has no `DispatcherTimer`/permanent enforcement loop
- UI exit skips `ReturnFanToAuto` only for service-owned firmware policy; direct/manual paths retain cleanup
- Fans selector initialization derives its current id from runtime profile state, not `UserSettings.Current.CoolingProfile`
- four new source regression tests cover source/baseline reassertion, UI-exit ownership class, bounded startup settle and runtime selector truth
- hardware safety docs, cooling design, architecture, device support and X9 research reflect the new lifecycle

The actual root cause of any Lenovo-side overwrite is not claimed. The product fix only responds to the observed lifecycle failure with reviewed semantic reassertion.

## Alpha.42 release gate

Completed implementation and validation work:

- [x] Started from immutable alpha.41
- [x] Kept one active branch/PR
- [x] Final Track interaction uses 28% center + 450 ms hold + release + <=3 mm maximum excursion
- [x] Previous/Next remains 9 mm and cannot overlap Play/Pause
- [x] Reverse-close target widened only inside visible lane geometry
- [x] Cooling selector/runtime truth no longer confuses persisted preference with applied state
- [x] Saved firmware profiles actively restore after startup capability discovery
- [x] Added one bounded 7-second startup-settle reassert
- [x] Active firmware profile reasserts across AC/DC/resume/power-baseline changes
- [x] WPF UI exit preserves service-owned firmware profile but direct/manual cleanup remains intact
- [x] No rejected direct writer/EC fallback was reauthorized
- [x] Implementation head passed CI + Package
- [x] Implementation-head visual artifact downloaded and inspected
- [x] Alpha testing guide updated for the new physical persistence regression
- [x] Frozen `version.json.releaseReady=true`
- [x] CI #1754 + Package #1468 passed on the first exact frozen head
- [x] Frozen-head WPF artifact downloaded and inspected
- [x] Complete PR changed-file list reviewed; scope is Touchpad, cooling lifecycle, tests/docs/version only
- [x] PR conversation and inline review threads checked; no outstanding comments/threads at that point

Remaining release steps:

- [ ] Require CI + Package to pass on the **final documentation handoff head**
- [ ] Mark PR #78 ready and merge with exact expected-head SHA
- [ ] Verify post-merge `main`
- [ ] Verify `Promote release-ready main` creates immutable `v0.1.0-alpha.42` at the merged commit
- [ ] Verify exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`
- [ ] Verify published Setup/Payload SHA-256 checksums
- [ ] Confirm immutable alpha.41 tag/release was not moved

## Physical X9 follow-up — separate evidence class

Hosted CI cannot prove finger feel or Lenovo firmware acoustics. After installing the published alpha.42, record these separately.

Touchpad:

- [ ] quick center tap does nothing
- [ ] roughly half-second center hold toggles once on release
- [ ] nothing auto-fires while still held
- [ ] <=3 mm natural jitter remains usable
- [ ] >3 mm movement permanently disarms the hold for that contact
- [ ] 9 mm Previous/Next remains reliable without Play/Pause overlap
- [ ] reverse close works across multiple points in the inner half of both mirrored lanes
- [ ] outer guard still launches inward

Cooling persistence:

- [ ] select Quiet and verify physical/runtime Quiet state
- [ ] close/reopen only the UI while service remains running; Quiet remains active
- [ ] reboot with Quiet saved; runtime UI remains truthful during restore and Quiet converges after startup
- [ ] listen through the post-login settle window; no later silent return to Auto/base policy
- [ ] unplug/replug AC while Quiet is active; Quiet remains active
- [ ] sleep/resume while Quiet is active; Quiet remains active
- [ ] change Windows performance preference while Quiet is active; Quiet remains override and Auto later restores the new baseline
- [ ] repeat lifecycle with Balanced
- [ ] repeat Max only if the existing exact full-speed safety gates pass
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

A green compiler is not release readiness. Promotion requires exact-head build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review and immutable release verification. Physical hardware behavior remains a separate evidence class and must never be invented from hosted CI.
