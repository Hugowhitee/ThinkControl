# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release: alpha.33 touchpad, shell and crash regression hardening

`v0.1.0-alpha.32` is the immutable production baseline. The active single release PR is #63 and targets **`v0.1.0-alpha.33`**.

The product-code head `7d54051c2447b298c09d05da574b551e9093c3f0` passed CI, Package ThinkControl and Installer reliability after the recurring WPF dispatcher crash root cause was fixed and a source regression guard was added. Release metadata was then frozen to alpha.33. The release-ready head `a8340287bd821c020a348aed6ec6cf3b1aa9c9db` subsequently passed CI, Package ThinkControl and Installer reliability on the same SHA, including repository hygiene, Core tests, the real WPF shell smoke, installer/service lifecycle and updater compatibility. Its generated alpha.33 visual-QA artifact was manually inspected: minimum/normal/wide Touchpad layouts are stable, idle corner guides are mirrored, the live-corner fixture keeps the settings geometry in place, and the corrected QA fixture shows both idle corner selectors as `Off` rather than blank.

This handoff update itself changes only documentation. No more branch content changes are planned. Before squash merge, CI + Package + Installer reliability must also be green on the exact PR head that contains this handoff commit; GitHub workflow/release history is the immutable record of that final gate and promotion.

### Implemented in alpha.33

- [x] Live Touchpad responsive layout is anchored to the stable Advanced page rail instead of a grid whose own layout changes its measured width.
- [x] Full-rate raw HID frames remain available to gesture recognition while WPF live visualization coalesces updates to roughly display-refresh cadence; all-up/release frames remain immediate.
- [x] Raw-input/HID startup is deferred until after Advanced/Touchpad has painted so device probing cannot decide whether the window becomes visible.
- [x] Runtime corner Candidate/Claimed/Active state no longer collapses/re-expands the edge editor; live ownership dims/disables it in place without changing page geometry.
- [x] Touchpad corner/gesture UI listeners attach only while the Touchpad page is visible and detach when the page is hidden, preventing continued high-rate dispatcher work elsewhere in Advanced.
- [x] Left/right idle corner guides use exact mirrored final-pixel geometry and identical idle treatment; hover changes pointer affordance without promoting one corner to a different visual state.
- [x] Alpha.32 corner/edge recognition exclusivity, lift-required lockout and deliberate diagonal launch thresholds remain unchanged.
- [x] Successful-update passive confirmation is click-dismissable.
- [x] A deliberate Compact → Advanced transition removes the passive successful-update confirmation so it cannot remain topmost over Full.
- [x] Advanced-preferred startup no longer redundantly reopens an already visible Advanced window after update handoff, so a successful-update confirmation is still shown once when the updater restarts directly into Advanced.
- [x] Advanced is restored/shown and receives a render pass before a heavy requested page such as Touchpad is activated.
- [x] Primary-surface verification treats an expected Advanced window that is still minimized as a shell failure.
- [x] Real WPF shell smoke covers passive-update → Advanced dismissal, minimized Advanced → Touchpad recovery, bounded pre-HID open latency and leaving Touchpad after deferred input work.
- [x] Recurring issue #60 / signature `016E9126228907A1` was traced to a real WPF overload bug: `App.Notifications` passed a zero-argument `Action` first and `DispatcherPriority` second, allowing WPF to bind the priority into `params object[]` and later throw `TargetParameterCountException` through `DynamicInvoke`.
- [x] Notification-center dispatcher calls now use the strongly typed priority-first overload.
- [x] Core CI includes a source regression test that rejects the dangerous method/delegate-first `Dispatcher.BeginInvoke(..., DispatcherPriority...)` pattern across `ThinkControl.UI`.
- [x] No fan/PawnIO/Lenovo-Auto safety contract from alpha.32 is weakened by this regression release.

### Automated release evidence

Passed before this final handoff-only commit:

- [x] Repository hygiene and alpha.33 version/document consistency.
- [x] Release restore/build and Core tests, including the dispatcher scheduling source guard.
- [x] Real Compact ↔ Advanced WPF shell smoke with the new update-toast/minimized-Touchpad regression paths.
- [x] WPF visual-QA matrix render.
- [x] Final alpha.33 Touchpad minimum/normal/wide and live-corner screenshots manually inspected.
- [x] Snapshot-only corner selector synchronization fixed so visual QA reflects the real runtime `Off` state.
- [x] Package ThinkControl workflow.
- [x] Installer reliability workflow, including deep install/service/IPC and legacy updater compatibility smoke.
- [x] Package workflow bootstrap installer/service lifecycle smoke.
- [x] Crash issue #60 remains open for physical confirmation rather than being closed merely because a source root cause and automated guard exist.

### Promotion gate

- [x] Freeze README/Product/release-readiness documentation to alpha.33.
- [x] Set `version.json` to `0.1.0-alpha.33` with `releaseReady=true`.
- [x] Run CI + Package + Installer reliability on the release-ready alpha.33 candidate.
- [x] Inspect the generated alpha.33 UI artifact for version/header drift and representative Touchpad stability.
- [ ] Require CI + Package + Installer reliability green on the exact PR head containing this final handoff commit; make no further branch changes afterward.
- [ ] Squash-merge PR #63 to `main` using that exact expected head SHA.
- [ ] Verify `main` points to the returned squash SHA.
- [ ] Verify the `Promote release-ready main` workflow succeeds for the squash SHA.
- [ ] Verify tag `v0.1.0-alpha.33` points to that exact `main` SHA.
- [ ] Verify the immutable GitHub prerelease exists and contains exactly `ThinkControl-Setup-0.1.0-alpha.33.exe`, `ThinkControl-Payload-0.1.0-alpha.33.zip`, `SHA256SUMS.txt` and `ui-overview.png`.
- [ ] Verify the published release checksums succeed and assets are non-empty/downloadable.
- [ ] Remove the merged alpha.33 branch after immutable release verification.

### Physical X9 follow-up — not an automated release claim

Hosted CI cannot prove the following and this document must not mark them complete without a real 21Q6/21Q7 test:

- [ ] Live Touchpad no longer oscillates in size on the physical X9, including when Advanced is fullscreen.
- [ ] Opening/using/leaving Touchpad no longer makes the rest of Advanced sluggish during a real high-rate touch stream.
- [ ] Opening Touchpad from a minimized/hidden state no longer leaves ThinkControl invisible in normal installed use.
- [ ] Left/right corner launch guides and real corner interaction feel symmetric and deliberate on the physical haptic pad.
- [ ] Issue #60 `TargetParameterCountException` does not recur during normal shell/notification use on alpha.33; keep the issue open until field evidence supports closure.
- [ ] Clean PawnIO reinstall/repair after stale/missing kernel-service state.
- [ ] Restart/UAC path and provider refresh after PawnIO repair.
- [ ] Fan RPM/control recovery on the verified X9 EC path after repair.
- [ ] Lenovo OEM Auto, Fn+Space and readback stay in agreement through normal use/restart.

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
