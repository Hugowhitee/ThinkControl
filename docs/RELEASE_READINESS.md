# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release: alpha.35 maintenance cleanup

`v0.1.0-alpha.34` is the immutable production baseline. The active single release PR is #66 on `cleanup/alpha35-maintenance` and targets **`v0.1.0-alpha.35`**.

The cleanup implementation head `3211e540b1359cdb684200f1f998e9389b853ce0` passed all three normal release workflows on the same SHA before the metadata freeze:

- CI 1402 / run `33221051212` — repository hygiene, Release build, **0 compiler warnings**, **107/107 tests**, real Compact ↔ Advanced shell smoke, 85-snapshot WPF visual-QA render and artifact upload.
- Package ThinkControl 1132 / run `33221051181` — release overview, UI/service publish, payload-size validation, bootstrap build, installer/service lifecycle and development artifact creation.
- Installer reliability 431 / run `33221051268` — restore/build, compact payload, non-elevated UI manifest gate, bootstrap install, deep installer/service/IPC smoke and exact immutable `v0.1.0-alpha.14.1` updater-compatibility smoke.

The WPF artifact from CI 1402 (`ThinkControl-Visual-QA`, artifact `9705190628`, digest `sha256:29d88953acc12aa0e27bb24d30d2bf65e2c18a60b71e7cd99bb76604dfc8a39b`) was inspected after the implementation stabilized. Audio normal/minimum/wide retained the existing layout, Home and Touchpad remained visually unchanged, the notification sheet still rendered correctly after the obsolete color shim was removed, and the alpha.34 Touchpad corner fixtures remained present.

Release metadata is frozen to `0.1.0-alpha.35` with `releaseReady=true`. README, Product, Architecture, Device Support and Alpha Testing describe the cleanup behavior. This handoff update is the **last planned branch content change**. CI + Package + Installer reliability must now all be green on the exact PR head containing this commit. If the head moves, repeat the exact-head gate.

### Implemented in alpha.35

- [x] Audio page lifecycle clears both output and microphone debounce timers when hidden/unloaded.
- [x] Audio page lifecycle clears transient output/microphone drag flags so navigating away mid-drag cannot suppress a later refresh.
- [x] Source regression coverage protects the Audio hide/unload lifecycle behavior.
- [x] Removed obsolete current-client cooling compatibility wrappers from `App.Cooling` and `HardwareServiceClient`.
- [x] Preserved service-side legacy cooling IPC required by the supported installed-client/updater compatibility floor.
- [x] Added source guards so removed current-client cooling APIs do not silently return while the service compatibility handlers remain intentional.
- [x] Fan percentage and graph-curve writes now classify as fan-control diagnostics instead of generic `hardware.operation` events.
- [x] Removed the obsolete `AdvancedWindow.ColorCompat.cs` shim and replaced its only WPF usage with an explicit `System.Windows.Media.Color` reference, avoiding global drawing/WPF type ambiguity.
- [x] Updated active Architecture, Device Support and Alpha Testing docs that still described alpha.31/alpha.34-era contracts as current.
- [x] Updated GitHub Actions to current official majors used by the hosted runner (`actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`) and eliminated the previous Node-20 deprecation warnings.
- [x] Added PR/ref concurrency cancellation to CI, Package ThinkControl and Installer reliability so superseded branch heads stop consuming Windows runners.
- [x] Kept immutable/tag release packaging outside the PR/ref cancellation behavior.
- [x] Strengthened repository hygiene against removed alpha-era helper files, stale action majors and current-version documentation drift.
- [x] No hardware write scope, X9 EC safety gate, Touchpad recognizer behavior, Keyboard Auto contract, updater safety boundary or release asset model was widened by this cleanup.

### Audit findings deliberately retained

Not every old-looking path is dead code. The cleanup intentionally retains:

- service-side `SetCoolingProfile`, `SetCustomCoolingCurve` and related legacy handlers required by installed-client compatibility;
- the immutable alpha.14.1 updater fixture used by Installer reliability;
- conservative X9/PawnIO/provider gates whose duplication is tied to process/provider safety rather than presentation convenience;
- physical-device validation items that hosted CI cannot prove.

Future cleanup must distinguish **current-client dead code** from **server/updater compatibility debt** before deleting compatibility endpoints.

### Promotion gate

- [x] Audit current `main`, open PR/branch state and alpha.34 baseline before changes.
- [x] Keep one coherent release branch and one PR (#66).
- [x] Pass implementation-head CI + Package + Installer reliability on `3211e540b1359cdb684200f1f998e9389b853ce0`.
- [x] Inspect the implementation-head WPF artifact.
- [x] Freeze `version.json` to `0.1.0-alpha.35` with `releaseReady=true`.
- [x] Freeze README/Product/Architecture/Device Support/Alpha Testing to alpha.35.
- [x] Update this single persistent release-readiness handoff for alpha.35.
- [ ] Require CI + Package + Installer reliability green on the **exact final PR #66 head** containing this handoff commit; make no further branch changes afterward.
- [ ] Confirm PR #66 has no unexpected changed files and is still based on immutable alpha.34 `main` (`e09e3eeb6145d5dda8b6351ebb0c3c0bf7292796`).
- [ ] Squash-merge PR #66 to `main` using that exact expected head SHA.
- [ ] Verify `main` points to the returned squash SHA.
- [ ] Verify post-merge main CI succeeds for that squash SHA.
- [ ] Verify `Promote release-ready main` succeeds for the squash SHA.
- [ ] Verify tag `v0.1.0-alpha.35` points to that exact `main` SHA.
- [ ] Verify the immutable GitHub prerelease exists and contains exactly `ThinkControl-Setup-0.1.0-alpha.35.exe`, `ThinkControl-Payload-0.1.0-alpha.35.zip`, `SHA256SUMS.txt` and `ui-overview.png`.
- [ ] Verify published release checksums succeed and assets are non-empty/downloadable.
- [ ] Confirm the merged cleanup branch is removed by branch hygiene or remove it after immutable release verification.

### Physical X9 follow-up — not an automated release claim

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
- [ ] Fan RPM/control recovery on the verified X9 EC path after repair.

## Commercial/public release program

Do **not** mix this backend/licensing program into an alpha stabilization/hardware-hardening PR. Implement it in bounded phases with a threat model and migration plan first.

### Installer, updater and signing

- [x] Preserve custom install location across the currently supported in-place update path.
- [x] Exercise clean install, service start/IPC, update compatibility and uninstall cleanup in CI.
- [ ] Failed staged update cannot destroy the last working payload; rollback stays tested.
- [ ] Define explicit uninstall policy for ThinkControl-owned runtime/local data before a commercial release.
- [ ] Sign binaries/installer and document/test SmartScreen reputation strategy.
- [ ] Keep intentional legacy-updater compatibility until the supported installed-client floor no longer needs it.

### Capability-driven hardware architecture

- [x] Windows-generic UI is vendor-neutral.
- [x] Raw X9 EC controls require the verified X9 provider path.
- [x] Setup distinguishes installation metadata from real provider readiness.
- [ ] Continue replacing residual device-name assumptions with explicit capabilities.
- [ ] Keep fan semantics distinct: OEM thermal policy, read-only telemetry, discrete EC states, continuous percentage/PWM.
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

A green compiler is not release readiness. Promotion requires the exact-head repository/build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review, and only then release publication. Physical hardware behavior remains a separate evidence class and must never be inferred from hosted CI.
