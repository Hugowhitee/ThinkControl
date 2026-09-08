# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease:

- `v0.1.0-alpha.42`
- immutable tag/release SHA: `2d40dffebeb6f8327cd06403e076936c40d60497`
- release is immutable and contains exactly four managed assets: Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png`
- alpha.41 remains separately immutable at `6088955eeab54d1af6506780fa7707df17fe11c3`

Current candidate:

- version `v0.1.0-alpha.43`
- branch `feat/alpha43-audio-safety`
- PR #80, **Finish alpha.43 Audio Safety, Track control and Battery preservation**
- base `main` / alpha.42 at `2d40dffebeb6f8327cd06403e076936c40d60497`
- implementation-review head: `5501e0d0b9f211636f2106a0fe5eae418fc5caae`
- `version.json.releaseReady=false` until the final freeze commit

## Alpha.43 product delta

### Audio Safety

Alpha.43 adds one canonical **session-level** policy owner:

- **Normal** — existing ThinkControl media/output behavior;
- **Media lock** — blocks ThinkControl Touchpad Volume, Media scrub, Previous/Next and integrated Play/Pause while deliberate Windows/app audio remains available;
- **Silent** — includes Media lock, mutes the Windows render endpoint and blocks ThinkControl output-volume/unmute writes.

The microphone stays independent. Silent records the previous mute state only for endpoints it actually encounters, restores those recorded states on exit/orderly shutdown, and reuses the existing runtime status cadence for default-output convergence. The state deliberately does not persist across process restart in alpha.43 because a new process cannot truthfully inherit the old process's mute ownership.

### Track control

Track remains one edge action and one recognizer/router owner. A Track-local **Play / Pause** switch now controls the center behavior and visual together.

- enabled center: 28% lane region (`0.36..0.64`), at least 450 ms hold, at most 3 mm maximum radial movement, commit on release;
- release is the final confirmation, so merely resting on the center cannot auto-start media when the timer elapses;
- Previous/Next retains the deliberate 9 mm swipe threshold;
- disabling Play/Pause removes the center hit target, fill/separators and Play/Pause glyph while leaving Previous/Next intact;
- existing alpha.42 configurations default the center on for compatibility;
- occupied edge assignments swap actions rather than clearing the previous edge;
- reverse-close remains owned by the visible inner half of the corner lane.

### Battery preservation

Alpha.43 adds a separate exact-X9 charge-threshold provider around Lenovo's installed Windows Power Manager stack. It does **not** guess an EC register, reuse the rejected fan writer or expose raw IOCTLs to the desktop client.

Provider gate:

- exact verified X9 identity (`21Q6/21Q7`);
- real PWRMGRV battery configuration under `HKLM\SOFTWARE\WOW6432Node\Lenovo\PWRMGRV\ConfKeys\Data`;
- live privileged `\\.\IBMPmDrv` access;
- semantic threshold pairs only: start `40..90`, stop `45..95`, 5% steps, `start < stop`;
- fixed reviewed Lenovo PM Device operations only;
- Lenovo rejection handling plus PWRMGRV readback after a transition;
- best-effort restore of the exact previous Lenovo state if a transition fails;
- disabling preservation clears both threshold latches before selecting automatic/full charging.

The Battery page exposes a small preset list rather than raw values:

- **Daily · 75–85% (recommended)**;
- **Desk · 55–80%**;
- **Maximum care · 40–60%**;
- **Full charge · 100%**.

Existing non-preset Lenovo thresholds are shown truthfully as `Custom · start–stop%` and are not overwritten until the user deliberately chooses a preset. If the Lenovo PM Device is unavailable, the surface stays read-only and offers the Lenovo battery-settings fallback.

The UI explicitly communicates the real benefit: limiting time at very high state of charge **reduces high-charge battery wear/stress** compared with routinely remaining near 100%. Stronger limits are described as greater qualitative wear reduction. ThinkControl deliberately does **not** claim a fixed `x fewer cycles` or battery-life multiplier because actual lifetime improvement also depends on temperature, chemistry, calendar time and usage/depth of discharge.

### Battery history

Battery history no longer presents an ever-growing raw list as the normal experience.

- seven days shown by default;
- `Show older` expands to fourteen days without changing storage policy;
- days remain compact summaries; opening a day reveals charge/discharge sessions, and a session opens its graph/statistics;
- detailed graph retention can be 7 / 14 / 30 days;
- older detailed samples compact automatically while summaries remain for one year;
- destructive `Reset all history…` lives behind **Manage history** and warns that local summaries, graphs, health trend and learned estimates are reset while current firmware health, cycle count and charge-protection state are untouched.

## Carry-forward hardware boundary

Alpha.43 does not weaken the alpha.42 fan safety model.

- alpha.38 Lenovo Other Mode `fanX_target` remains physically rejected/read-only;
- native Lenovo fan telemetry remains available where real channels exist;
- the native telemetry latch still prevents silent fallback to the known-inferior EC writer;
- Quiet/Balanced firmware-policy persistence and bounded startup/AC/DC/resume reassertion remain intact;
- exact-X9 full speed `0x04020000` remains a separate boolean/live/readback-gated semantic for Max cooling;
- custom curves/manual percentages remain direct-writer capabilities and are not faked through firmware policy;
- the new battery provider is a separate semantic writer and does not authorize arbitrary Lenovo PM Device, EC, ACPI or Other Mode commands.

## Alpha.43 implementation-head evidence

Implementation-review head: `5501e0d0b9f211636f2106a0fe5eae418fc5caae`.

### CI #1808

Run `34262191646` completed successfully on that exact head:

- repository hygiene passed;
- Release restore/build passed with **0 warnings and 0 errors**;
- Core tests: **195 passed, 0 failed, 0 skipped**;
- real Compact ↔ Advanced ShellSmoke passed;
- WPF visual QA rendered **85 snapshots** successfully;
- visual artifact: `ThinkControl-Visual-QA`, artifact id `10070394833`, digest `sha256:a208c8995fc2b11541f0b0c52d2909595b358c6eba6fe15fcfeaff1069acb528`.

A preceding CI run correctly caught a 32 px Battery preservation selector against the shared 38 px selector contract. After fixing both new Battery ComboBoxes to the shared `TcComboBox` style and 38 px minimum height, CI #1808 passed.

Manual inspection of the **exact-head** artifact completed for:

- `advanced-battery.png`, `advanced-battery-min.png`, `advanced-battery-wide.png` and `advanced-battery-day-expanded.png`;
- `compact-dark.png` and `compact-light.png`;
- `advanced-settings.png`;
- `advanced-touchpad-wide.png` and `advanced-touchpad-light.png`.

Findings:

- Battery preservation now uses the shared dark selector treatment; the selected 75–85% preset is legible and aligned;
- normal/minimum/wide Battery layouts remain scroll-safe with no horizontal escape;
- grouped Battery history and expanded session rows remain readable and the destructive management surface is not part of routine history interaction;
- Compact Audio Safety fits the existing 390×500 surface in both themes;
- Settings Audio Safety and battery-history retention remain aligned with the shared control grammar;
- Track control remains one continuous lane with the center Play/Pause option represented as one Track-local switch; no second overlay/pill returned.

### Package #1521

Run `34262191737` completed successfully on the same exact head:

- canonical branding/version checks passed;
- UI and hardware-service publish passed;
- compact managed payload checks passed;
- release payload and web bootstrap installer built;
- deep installer/service/IPC reliability smoke passed;
- oldest-supported alpha.14.1 updater fixture verification and upgrade compatibility passed;
- checksums and development artifact were produced.

This implementation head is therefore suitable for the release freeze. It is not the final release evidence until `releaseReady=true` is committed and both workflows pass again on that exact frozen head.

## Final alpha.43 release gate

Implementation scope:

- [x] Audio Safety Normal / Media lock / Silent implemented with one canonical session owner
- [x] Silent endpoint ownership/restore and late-convergence race reviewed and serialized
- [x] microphone remains independent
- [x] Compact + Settings Audio Safety UI implemented
- [x] Track-local Play/Pause switch implemented
- [x] deliberate 450 ms / 3 mm / release center contract preserved
- [x] center-off removes center behavior and visual together
- [x] occupied edge assignment swaps rather than clearing another edge
- [x] exact-X9 battery threshold provider implemented behind PWRMGRV + `IBMPmDrv` gates
- [x] semantic 5% start/stop range, readback and rollback architecture covered by source tests
- [x] Battery preset UI and qualitative wear-reduction explanation implemented without fake cycle multiplier
- [x] Battery history grouped/compacted with bounded retention and destructive reset behind Manage history
- [x] implementation-head CI #1808 passed
- [x] implementation-head Package #1521 passed
- [x] exact-head WPF artifact manually inspected

Freeze/promotion:

- [ ] set `version.json.releaseReady=true`
- [ ] require **CI + Package ThinkControl on the exact frozen head**
- [ ] inspect the frozen-head WPF artifact and confirm the implementation visuals did not regress
- [ ] review complete PR changed-file list, comments, reviews and review threads
- [ ] mark PR #80 ready and merge using exact expected-head SHA
- [ ] verify post-merge `main`
- [ ] verify promotion creates immutable `v0.1.0-alpha.43` at the merged commit
- [ ] verify exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`
- [ ] verify published Setup/Payload SHA-256 checksums
- [ ] confirm immutable alpha.42 and alpha.41 tags/releases were not moved

## Physical follow-up — separate evidence class

Hosted CI cannot prove finger feel, audible silence, Lenovo fan acoustics or real battery charging behavior. These checks remain honest post-build physical evidence rather than hosted claims.

Touchpad:

- [ ] quick Track-center tap does nothing
- [ ] roughly half-second hold toggles once on release
- [ ] nothing auto-fires while still held
- [ ] >3 mm movement disarms the center hold for that contact
- [ ] Previous/Next remains reliable at the 9 mm swipe threshold
- [ ] disabling Track Play/Pause removes the center visual and behavior while Previous/Next still work
- [ ] reverse-close remains reliable across the inner half of both mirrored lanes

Audio Safety:

- [ ] Media lock blocks ThinkControl Touchpad media/volume actions while deliberate app/Windows audio remains available
- [ ] Silent mutes current output and ThinkControl cannot unmute/change output while active
- [ ] microphone stays independent
- [ ] switching default output during Silent converges the new output without rapid polling
- [ ] leaving Silent/orderly app exit restores each recorded endpoint to its prior mute state
- [ ] restart begins at Normal as designed

Battery preservation:

- [ ] on the reference X9, select Daily 75–85% and confirm the runtime provider reports the same pair
- [ ] on AC, confirm charging stops around the configured upper threshold rather than continuing toward 100%
- [ ] after discharge, confirm charging does not resume until below the lower threshold
- [ ] choose Full charge and confirm the threshold latches are released and normal charging can continue toward 100%
- [ ] verify Vantage/Lenovo settings agree with the state ThinkControl reports
- [ ] verify an unsupported/missing PM Device stays read-only instead of attempting another low-level path

Cooling carry-forward:

- [ ] saved Quiet remains physically active after UI close/reopen and converges after reboot
- [ ] AC/DC and resume reassert the selected non-Auto firmware profile
- [ ] Balanced/Max retain expected behavior under the existing gates
- [ ] no alpha.38 per-fan target wave/re-kick behavior returns

## Release workflow principles

For future releases:

- recover current state from `main`, version, releases, active PR and this handoff;
- stabilize related regressions before expanding scope;
- improve existing owners instead of stacking helpers/timers/providers/overlays;
- keep generic UI capability-first and hardware writes provider-gated;
- separate hosted validation from physical evidence;
- inspect UI artifacts manually;
- freeze docs/version before exact final gates;
- merge with expected-head guard;
- verify promotion, immutable tag, assets and checksums;
- never move an existing immutable release tag.

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
- [x] Battery preservation is a separate semantic capability and does not expose arbitrary driver commands.
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

A green compiler is not release readiness. Promotion requires exact-head build/test gates, real WPF lifecycle smoke, **inspected** visual QA, package/installer/updater verification, capability-safety review and immutable release verification. Physical hardware/audio/battery behavior remains a separate evidence class and must never be invented from hosted CI.
