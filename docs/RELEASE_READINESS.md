# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease before this candidate:

- `v0.1.0-alpha.40`;
- immutable tag/release SHA: `448ac549433f6bdc367519d67086eb469c9ec87f`;
- published as an immutable prerelease with exactly four managed assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- promotion re-downloaded Setup/Payload and verified their published SHA-256 sums.

Current alpha.41 candidate:

- branch: `fix/alpha41-track-button-fan-truth`;
- PR: #77, **Make Track center button-like and recover true X9 max cooling**;
- version: `v0.1.0-alpha.41`;
- base: immutable alpha.40 / `main` at `448ac549433f6bdc367519d67086eb469c9ec87f`;
- implementation head validated before release freeze: `cae15ef2bcb70a4f90d6378b4fcb9716a9fa1830`;
- `version.json.releaseReady=false` while the implementation evidence was collected; set it true only for the final frozen docs/version head after this handoff records that evidence.

Alpha.41 is a physical-use follow-up to alpha.40. Real X9 use showed three things hosted tests could not establish by themselves: center Track Play/Pause still felt like a timing challenge, enabled edge gestures at Windows login felt slower to become ready than lightweight helpers such as G-Helper, and Performance-policy-only Max cooling still felt materially weaker than naturally hot Lenovo Auto even while RPM telemetry looked high.

## Alpha.41 product delta

### Track center becomes a button

Alpha.40 improved the old 460 ms / 1.9 mm center tap gate to 700 ms / 4.5 mm, but real use still required the user to think about how long to press and could leave a practical no-op region before the existing 9 mm Previous/Next threshold.

Alpha.41 changes the semantic model instead of adding another timing tweak:

- the visible Track lane remains **Previous | Play/Pause | Next** with one visualizer/recognizer/router owner;
- the center start segment remains 40%..60% of the selected edge lane;
- there is **no maximum hold duration** for the center button;
- a center-start contact remains eligible for Play/Pause while radial travel stays within **8.75 mm**;
- the deliberate Previous/Next threshold remains **9.0 mm**;
- once the recognizer claims a deliberate Track swipe, Play/Pause cannot fire on release;
- the old two-double compatibility overload was removed because it was ambiguous with the new `(travel, position)` API and could silently bypass the center-zone check through overload resolution;
- user-facing help now explicitly says to press the center and release, with no special short/long-press timing.

Standalone Play/Pause remains absent from the edge menu. Legacy serialized PlayPause values still sanitize into Track control. Occupied edge actions still swap while physical-edge sensitivity/inversion stay with their edges. Popup semantics remain resulting **Playing → pause bars** and **Paused → play triangle**.

### Earlier Windows-start gesture readiness

The startup path was compared with G-Helper's lightweight helper structure. ThinkControl does **not** copy G-Helper's single-process privilege model; the normal-user WPF app and privileged hardware service remain separate. The useful discipline adopted is **input/tray first, rich discovery later**.

- `Start with Windows` remains the existing per-user HKCU Run entry with `--tray`;
- the earliest WPF `Application.Startup` hook reads only cheap firmware identity from the Windows BIOS registry path;
- if gestures are configured, Raw Input is started directly from that Startup hook during tray-only launch instead of waiting behind normal WPF shell/diagnostics/service discovery work;
- visible launches still preserve first-paint priority and can defer the HID probe to `ContextIdle`;
- rich CPU/GPU/RAM/BIOS inventory remains on the background WMI refresh path;
- `Application.Activated` is recovery only, not first registration ownership;
- no machine-wide Explorer startup-delay registry hack or second startup owner was introduced.

Hosted CI proves the app-side ordering only. Actual Windows logon process-scheduling latency remains a separate real-session check.

### X9 Max cooling: narrow full-speed semantic

Physical alpha.40 evidence showed that Lenovo Performance policy alone was still not equivalent to the strongest naturally hot Auto cooling state even when reported tachometer RPM looked high. Alpha.41 therefore keeps Quiet/Balanced as firmware-policy profiles and adds a **separate exact-X9 global full-speed semantic** for Max cooling.

Independent Lenovo tooling identifies Other Mode feature `0x04020000` as `FanFullSpeed`; ThinkControl still requires the X9 itself to prove the contract before writing. `LenovoOtherModeFullSpeedService` is restricted to verified `21Q6/21Q7` and:

- uses only exact feature `0x04020000`;
- accepts only boolean values `0` and `1`;
- requires an active `LENOVO_OTHER_METHOD`;
- requires a live boolean read;
- respects an explicitly present capability row and its VALID/GET/SET contract;
- permits the exact known-feature fallback only when the capability row is omitted and the exact X9 live-read proves a boolean immediately around the transition;
- verifies every write by reading the same feature back;
- best-effort releases to `0` after a failed enable verification;
- never re-enables `1` as rollback after a failed disable;
- records ownership only when ThinkControl itself actually changes and verifies the state.

Built-ins now map as:

```text
Auto         -> explicit verified full-speed release when needed; restore latest Lenovo power-policy baseline
Quiet        -> release ThinkControl-owned full speed; Lenovo Quiet policy
Balanced     -> release ThinkControl-owned full speed; Lenovo Balanced policy
Max cooling  -> Lenovo Performance policy + verified 0x04020000 full speed when safely exposed
```

Routine lower-profile/disposal cleanup releases only ThinkControl-owned full-speed state so it does not silently fight another utility. Explicit user Auto is intentionally the wider recovery operation and can clear the exact known boolean after a service restart lost the previous in-memory ownership marker.

The rejected alpha.38 per-fan `fanX_target` writer remains read-only. Alpha.41 does not raise Fan Test ranges, guess another EC register, enable blocked `0x40`, invoke unverified EnergyDrv ChangeFanSpeed, substitute dust/maintenance commands, or expose arbitrary Lenovo feature IDs through IPC/UI.

Focused implementation evidence is also preserved in [`research/x9-alpha41-full-speed.md`](research/x9-alpha41-full-speed.md).

## Validation ownership

### CI owns

- repository hygiene;
- solution restore/build;
- Core/source tests for Track center semantics, startup ordering, X9 full-speed gates/ownership and preserved regression contracts;
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

## Alpha.41 implementation evidence

Exact implementation head `cae15ef2bcb70a4f90d6378b4fcb9716a9fa1830` passed both required PR pipelines before the final docs/version freeze:

- **CI #1708 / run `34060868886`**: repository hygiene passed; Release build succeeded with **0 warnings / 0 errors**; **170/170** Core/source tests passed; Compact/Advanced ShellSmoke passed; **85** WPF visual-QA snapshots rendered successfully.
- **WPF artifact `9997440086`** (`ThinkControl-Visual-QA`) has SHA-256 digest `8b54d84ba8815b2352e4e518638ec299d96217f5ea2e102fab3dcd186b6f04e4` and was downloaded and manually inspected.
- Visual inspection covered Touchpad normal/minimum/wide/light and mirrored corner selected/live fixtures. The wide Bottom Track fixture remains one continuous Previous | Play/Pause | Next lane; the center is still integrated rather than becoming a pill/overlay; normal/min/light remain aligned and unclipped; left/right corner geometry remains mirrored. The first implementation artifact exposed stale “quick tap” help copy; that copy was corrected before this exact `cae15ef...` artifact, which now explicitly describes center press/release with no short/long timing. A representative Fans fixture was also checked for layout regression.
- **Package #1423 / run `34060868861`** passed UI/service publish, payload/bootstrap construction, deep installer/service/IPC lifecycle, custom-location preservation, clean uninstall, checksum creation and immutable alpha.14.1 → alpha.41 updater compatibility.
- **Package artifact `9997437606`** (`ThinkControl-0.1.0-alpha.41-dev.1423`) has SHA-256 digest `38bc987b11fadd3aee012fa7df27af05e7a9b7fac099c4d760123894c0d15d1e`.

Software evidence is complete for the implementation head. Physical fan airflow/acoustics, physical Track feel and real Windows-logon scheduling are intentionally not marked proven by hosted runners.

## Alpha.41 release gate

- [x] Started from immutable alpha.40 / `main` at `448ac549433f6bdc367519d67086eb469c9ec87f`.
- [x] Kept the follow-up on one branch/PR (#77).
- [x] Removed Track center hold-duration semantics and preserved the deliberate 9 mm skip threshold.
- [x] Removed the practical alpha.40 center no-op range without lowering Previous/Next intent.
- [x] Preserved one Track visualizer/recognizer/router and legacy settings migration.
- [x] Preserved edge action swapping and corrected media popup semantics.
- [x] Moved configured tray-start Raw Input registration ahead of normal shell/discovery work without adding another startup owner.
- [x] Kept rich WMI inventory off the synchronous startup critical path.
- [x] Added exact-X9 `0x04020000` boolean full-speed provider with live-read/capability/readback gates.
- [x] Kept rejected per-fan target-RPM, EC fallback and unverified EnergyDrv/IOCTL paths blocked.
- [x] Added ownership-aware lower-profile/disposal release plus wider explicit-Auto stale-state recovery.
- [x] Updated Product/Architecture/Device Support/Hardware Safety/Cooling Design/Alpha Testing and focused X9 research.
- [x] Exact implementation head passed CI and Package.
- [x] Downloaded and manually inspected exact implementation-head WPF QA.
- [x] Reviewed/fixed the stale Track “quick tap” help copy found during visual inspection.
- [ ] Freeze docs/version with `version.json.releaseReady=true`.
- [ ] Require CI + Package to pass again on that exact frozen head.
- [ ] Mark PR #77 ready; review final checks/comments/diff and merge with the exact expected head SHA.
- [ ] Verify post-merge `main` equals the merged alpha.41 commit and immutable alpha.40 remains unchanged.
- [ ] Verify `Promote release-ready main` creates immutable `v0.1.0-alpha.41` at the merged commit.
- [ ] Verify exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png` are published and published Setup/Payload checksums validate.

## Physical X9 / Windows-session follow-up — separate evidence class

### Confirmed evidence retained

- [x] Alpha.38 fixed per-fan target reproduced repeated speed cycling/wave/re-kick instead of stable settling.
- [x] Nominal per-fan target 100% remained physically below naturally hot Lenovo Auto.
- [x] Alpha.39/40 firmware policy improved smoothness and telemetry, but alpha.40 Max/Performance still felt materially weaker than naturally hot Auto even with high-looking RPM.
- [x] Alpha.40 Track center remained difficult enough that the user still had to think about press duration/placement.
- [x] G-Helper felt quicker/stabler at login, motivating an app-side critical-path comparison rather than a copy of its privilege architecture.

### Alpha.41 real-device/session checks

These are important post-install evidence, but they are not prerequisites that hosted CI can fabricate:

- [ ] Repeated center press/release reliably stops/starts media without learning a short/long press duration.
- [ ] Holding center well over one second still toggles once on release if it never becomes a deliberate swipe.
- [ ] Natural center drift in the former 4.5–8 mm range no longer disappears into a no-op.
- [ ] Deliberate ~9 mm+ Previous/Next swipes still win and never also fire Play/Pause.
- [ ] Popup reports Playing + pause bars / Paused + play triangle on a state-reporting media session.
- [ ] With Start with Windows + an obvious edge gesture enabled, reboot/sign in and confirm the gesture works without opening ThinkControl; record real time from desktop/process start separately from app-side readiness.
- [ ] Quiet and Balanced remain smooth Lenovo-managed profiles.
- [ ] Max cooling engages materially stronger Lenovo-style airflow than alpha.40 when the exact full-speed feature is exposed.
- [ ] Max remains steady without the alpha.38 repeated re-kick/wave behavior.
- [ ] Max → Balanced/Quiet releases ThinkControl-owned full speed promptly.
- [ ] Explicit Auto restores firmware ownership/latest power-policy baseline, including after a service restart where stale full-speed ownership memory was lost.
- [ ] If `0x04020000` is unavailable/rejected/non-boolean, Max fails explicitly rather than silently enabling another low-level writer.
- [ ] Native Fan 1/Fan 2 telemetry remains truthful, while RPM is not treated as proof of airflow intensity.

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
- [x] X9 fan semantics distinguish native telemetry, firmware policy, narrow global full speed, physically accepted direct writers and discrete fallbacks.
- [x] Fan calibration and Keyboard Effects are exposed to generic UI as semantic provider capabilities.
- [x] A physically rejected native direct writer can remain telemetry-only without falling back to a known-inferior writer.
- [x] A semantic firmware-policy backend can preserve useful built-in profiles without falsely advertising direct RPM/PWM control.
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
