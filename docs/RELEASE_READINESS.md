# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.39`;
- immutable tag/release SHA: `ac3434805ff54e24fbee7178ec36e3e4d7829c4c`;
- published 2026-09-06 as an immutable prerelease;
- exactly four managed public assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- promotion workflow re-downloaded Setup/Payload and verified the published SHA-256 sums.

Current alpha.40 candidate:

- branch: `fix/alpha40-track-control-polish`;
- PR: #76;
- version: `v0.1.0-alpha.40`;
- base: immutable alpha.39 / `main` at `ac3434805ff54e24fbee7178ec36e3e4d7829c4c`;
- implementation head validated before release freeze: `591707b56d72b16f409f9b9669662a3b74a97533`;
- `version.json.releaseReady=true` after implementation CI + Package passed and the exact WPF artifact was manually inspected;
- no hardware-provider, fan-writer, startup-owner or low-level write contract is changed by this candidate.

Alpha.40 is a narrow Touchpad interaction follow-up. Alpha.39 got the Track lane visually correct, but real-pad use showed that the center Play/Pause tap could still be difficult to trigger quickly. Alpha.40 fixes the recognition dead zone, removes the now-duplicate standalone Play/Pause menu action, makes occupied edge reassignment swap actions, and corrects the media popup glyph semantics.

## Alpha.40 product delta

### Center Play/Pause reliability

Alpha.39 used a 20% center start segment with a 460 ms / 1.9 mm tap gate. The general edge recognizer begins resolving intent at roughly the configured 2 mm activation distance. That meant normal finger wobble—especially movement perpendicular to a horizontal Track lane—could leave the tap path, enter generic direction classification and get rejected before lift.

Alpha.40 fixes the recognizer ownership instead of simply lowering swipe thresholds:

- the center start segment remains 40%..60% of the selected edge lane;
- `TrackCenterGesturePolicy.MaximumTapMs` becomes **700 ms**;
- `TrackCenterGesturePolicy.MovementToleranceMm` becomes **4.5 mm** radial physical travel;
- a one-finger Track contact that starts inside the center segment remains a Candidate while it stays within that 4.5 mm envelope, regardless of small off-axis drift;
- leaving that envelope resumes the existing normal edge direction/claim logic;
- the action router still requires the existing **9 mm** deliberate Track swipe threshold for Previous/Next;
- Play/Pause still commits only from the reserved candidate path on lift, so a claimed swipe cannot be reinterpreted as a tap because sensitivity-scaled axis travel happened to look small.

This ordering targets the real failure mode while preserving skip intent and the single recognizer/router owner.

### One Track action, no duplicate Play/Pause menu item

- Current Touchpad edge choices no longer include standalone **Play / pause**.
- Track control remains the one product affordance: **Previous | Play/Pause | Next**.
- `GestureActionKind.PlayPause` remains in the enum only for numeric/settings compatibility.
- Old standalone PlayPause bindings sanitize to `PreviousNextTrack`, so an upgrade does not silently lose the user's media assignment.
- The old serialized `TrackCenterPlayPauseEnabled` compatibility field remains derived from the presence of Track control rather than becoming a second UI switch again.

### Edge action swapping

Alpha.39 enforced one edge per non-Off action, but assigning an already-used action moved it and left the old edge Off. Alpha.40 makes rearrangement reversible and less destructive:

- when the selected edge has an action and the requested action is already on another edge, the two **action kinds swap**;
- sensitivity and inversion remain with their physical edges;
- if the selected edge is Off, moving an occupied action there naturally leaves Off behind because there is no second active action to exchange;
- configuration sanitization still provides a deterministic compatibility fallback for duplicate actions loaded from old/external settings.

### Playback popup glyph semantics

The Track popup now follows familiar media-player affordance semantics:

- resulting state **Playing** → show **pause bars**;
- resulting state **Paused** → show the **play triangle**;
- virtual-key fallback with unknown post-command state → keep `Playback toggled` without inventing a play/pause result.

The label describes current/resulting state; the glyph describes the action available next.

### Preserved alpha.39 baseline

- Track remains one continuous visible lane with Previous/Play-Pause/Next inside the same band;
- mirrored Touchpad corner geometry and single-owner edge/corner model remain intact;
- silent `--tray` startup still keeps rich WMI off the synchronous critical path and explicitly starts enabled background gestures;
- X9 Auto/Quiet/Balanced/Max cooling still uses the reviewed Lenovo firmware-policy backend;
- the physically rejected Other Mode target-RPM writer remains read-only;
- the native fan telemetry latch, target-0 Auto cleanup and no-silent-EC-fallback boundary remain unchanged;
- fan calibration/manual diagnostics remain capability-driven and bounded;
- Compact ↔ Advanced shell ownership, hidden/minimized recovery and the `TargetParameterCountException` regression guards remain unchanged;
- no second Touchpad recognizer, overlay, input worker or Windows-startup mechanism is introduced.

## Validation ownership

### CI owns

- repository hygiene;
- solution restore/build;
- Core/source regression tests, including the new center drift reservation, legacy PlayPause migration, swap semantics and OSD mapping guards;
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
- existing `startwithwindows` HKCU Run-entry install/update/uninstall contract;
- checksums and development artifact.

Do not recreate a third full installer workflow. CI and Package are the required PR gates. Superseded PR runs may cancel; immutable/tag release packaging does not.

## Alpha.40 release gate

- [x] Started from immutable alpha.39 / `main` at `ac3434805ff54e24fbee7178ec36e3e4d7829c4c`.
- [x] Kept the follow-up on one branch/PR (#76).
- [x] Increased the center tap time/slop envelope to 700 ms / 4.5 mm without lowering the 9 mm skip threshold.
- [x] Reserved center-start Track candidates through ordinary off-axis finger drift before generic edge direction classification.
- [x] Kept Play/Pause candidate-only on release so claimed swipes cannot be downgraded into taps.
- [x] Added runtime recognizer tests for natural center drift, drift beyond the envelope and deliberate Track swipe escape.
- [x] Removed standalone Play/Pause from current edge action choices while migrating old serialized PlayPause bindings to Track control.
- [x] Changed occupied edge reassignment from destructive move-to-Off behavior to action swapping while retaining physical-edge sensitivity/inversion.
- [x] Corrected Track popup semantics to Playing→pause-bars and Paused→play-triangle.
- [x] Added source/policy compatibility guards for the new interaction contracts.
- [x] Updated README/Product/Architecture/Device Support/Alpha Testing for alpha.40; this document remains the mutable handoff.
- [x] Exact implementation head passed CI: hygiene, Release build, all Core/source tests, ShellSmoke and WPF rendering.
- [x] Exact implementation head passed Package ThinkControl including installer/service/IPC/update/uninstall and oldest-supported updater regression.
- [x] Downloaded and manually inspected the exact-head WPF visual-QA artifact, especially Touchpad normal/minimum/wide/light plus the existing media OSD shell fixture.
- [x] Reviewed the implementation diff for duplicate/dead event handlers, duplicate action owners and accidental alpha.39 hardware/startup regressions.
- [x] Recorded exact implementation-head run IDs, test/snapshot counts and visual/package artifact IDs/digests below.
- [x] Set `version.json.releaseReady=true` only after implementation evidence was complete.
- [ ] Require CI + Package to pass again on the exact frozen docs/version head.
- [ ] Mark PR #76 ready, review final checks/comments and merge with the exact expected head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.40 commit and immutable alpha.39 remains unchanged.
- [ ] Verify `Promote release-ready main` creates `v0.1.0-alpha.40` at the merged commit.
- [ ] Verify alpha.40 is immutable with exactly Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png` and valid published checksums.

### Alpha.40 implementation evidence

Exact implementation head `591707b56d72b16f409f9b9669662a3b74a97533` passed both required PR pipelines before the final docs/version freeze:

- CI run `34056270851` / #1688: repository hygiene passed; Release build succeeded with **0 warnings / 0 errors**; **162/162** Core/source tests passed; Compact/Advanced ShellSmoke passed; **85** WPF snapshots rendered successfully.
- WPF artifact `9996065736` (`ThinkControl-Visual-QA`) has SHA-256 digest `c93c2b4d3e6460d07c03d5bec908cd5697f8ff243139a08eb2987e9f85698b43` and was downloaded and manually inspected.
- Touchpad visual inspection covered `advanced-touchpad.png`, `advanced-touchpad-min.png`, `advanced-touchpad-wide.png` and `advanced-touchpad-light.png`. The wide Bottom Track fixture still reads as one continuous Previous | Play/Pause | Next band; the 20% center segment remains integrated rather than becoming a second pill/overlay; normal/minimum/light layouts remain aligned and unclipped.
- The current 85-snapshot matrix contains the existing media popup shell/Next-track fixture rather than separate deterministic Playing/Paused state fixtures. The exact Playing→pause-bars and Paused→play-triangle mapping is therefore guarded by `TrackControlPolishSourceTests`; visual review confirmed the popup shell itself remains intact. Do not claim a physical media-session result from hosted screenshots.
- Package run `34056270849` / #1404 passed UI/service publish, payload/bootstrap build, deep installer/service/IPC lifecycle, custom-location preservation, clean uninstall, checksum creation and immutable alpha.14.1 → alpha.40 updater compatibility.
- Package development artifact `9996071168` (`ThinkControl-0.1.0-alpha.40-dev.1404`) has SHA-256 digest `62992447a75d19f09c4506fdae542e03181a564d03b9fbd1a6536e04ddb61010`.
- Final implementation diff is Touchpad/core/UI/tests/docs/version only. No hardware provider, cooling writer, startup owner or installer mechanism changed. `TouchpadPanel.OnInitialized` explicitly detaches the old move-only action-selection handler before attaching the swap handler, so there is one active assignment owner; Track recognition remains in the existing recognizer/router and the standalone PlayPause enum survives only as compatibility input.

## Physical X9 / Windows-session follow-up — separate evidence class

Hosted CI cannot prove physical finger feel, acoustic fan behavior or actual Windows sign-in scheduling. The X9 is the current reference device, not the product boundary.

### Confirmed physical evidence retained from earlier releases

- [x] Alpha.38 Lenovo Other Mode fixed target reproduced repeated speed cycling/wave/re-kick instead of stable settling.
- [x] Nominal direct target 100% remained below naturally hot Lenovo Auto.
- [x] Those observations fail the direct writer's explicit physical acceptance criteria; the writer remains read-only.
- [x] Alpha.39 Track visual structure matched the requested single Previous | Play/Pause | Next lane, but real use showed center tap reliability still needed work.

### Alpha.40 real-device/session checks

- [ ] Install alpha.40 and confirm Bottom Track still reads visually as one lane.
- [ ] Perform repeated center taps with natural finger wobble; confirm stopping/starting media is materially more reliable than alpha.39.
- [ ] Test taps with small diagonal/inward drift and confirm they no longer disappear into a wrong-direction dead zone.
- [ ] Confirm 4–5 mm movements do not accidentally Previous/Next; deliberate ~9 mm+ swipes still do.
- [ ] Alternate center tap / Previous / center tap / Next repeatedly and record any missed toggles or accidental skips.
- [ ] Confirm the popup shows **Playing + pause bars** and **Paused + play triangle** on a media session that reports state.
- [ ] Confirm standalone Play/Pause is absent from the edge menu after upgrade and any old binding migrates to Track control.
- [ ] Reassign two occupied edge actions and confirm they swap, persist and retain per-edge sensitivity/inversion.
- [ ] With Start with Windows and an obvious edge gesture enabled, sign out/in or reboot; without opening ThinkControl, confirm the gesture works once the tray process has started.
- [ ] Confirm real Fan 1/Fan 2 telemetry remains visible where native Lenovo providers supply it.
- [ ] Confirm Fans/Home/Compact still offer Auto / Quiet / Balanced / Max cooling through firmware policy.
- [ ] Under comparable load, confirm Quiet/Balanced/Max remain smooth Lenovo-managed profiles and Max does not reproduce the rejected fixed-target re-kick cycle.
- [ ] Select Auto and confirm the latest Windows/Lenovo power-policy baseline returns.
- [ ] Confirm no direct percentage/custom/raw EC controls reappear merely because rejected Other Mode metadata advertises SET.

## Release workflow principles

For future releases:

- start from current `main` and inspect branches/PRs/releases first;
- keep one coherent release branch/PR;
- preserve capability boundaries and existing owners rather than stacking duplicate providers/timers/overlays;
- treat provider metadata and physical write acceptance as separate gates when hardware behavior requires it;
- keep generic pages vendor/model-neutral and consume explicit semantic capabilities;
- keep model-specific implementation and safety evidence inside the provider/hardware layer;
- keep startup/navigation independent of slow hardware discovery and avoid duplicate background/startup owners;
- distinguish current-client dead code from intentionally retained updater/service/settings compatibility;
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
