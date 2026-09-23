# ThinkControl release-readiness roadmap

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Current release state

Last immutable published prerelease:

- `v0.1.0-alpha.46`
- immutable tag/release SHA: `ccca29ed696d422b21f96589b972fbee5884b291`
- published 2026-09-20 at 18:39:17 UTC as an immutable prerelease
- release contains exactly four managed assets: Setup, Payload, `SHA256SUMS.txt`, `ui-overview.png`
- GitHub asset digests:
  - Setup: `sha256:4baa95fa1575d9d995ecad1b9c51dbdc53b73f4b2bd7a17c9f158aaf6231a45a`
  - Payload: `sha256:e7a2d5e0756b900a58fcf101c5146d15b75071ecd7afae1b26f4d4acdd668153`
  - `SHA256SUMS.txt`: `sha256:126a78c0920f512ff28f3656f98dba00e7b40ac4fdd69a8cd3fe84f3bc1a001a`
  - `ui-overview.png`: `sha256:ad829a8cde69279f8f2443c838c6d83b7f6a761c2024a76950ef9ecfe4c4a651`
- alpha.45 remains separately immutable at `310d505b66e7be90ae97ed30ae16e39e6ddd72c8`
- alpha.44 remains separately immutable at `17abe5458a1f6f43f66383827d463bd1094498c2`
- alpha.43 remains separately immutable at `ba13fab6d5b47cf127f4b627976662678f2ec491`

Alpha.46 completion:

- PR #90, **Prepare ThinkControl 0.1.0-alpha.46 shell regression repair**, merged with frozen head `1b3cd07722259ed05ca43942248c6d8c86df6fcf`
- frozen-head CI run `35529489572`: success
- frozen-head Package ThinkControl run `35529489569`: success
- merge commit / immutable release tag target: `ccca29ed696d422b21f96589b972fbee5884b291`
- post-merge main CI run `35529618038`: success
- promotion/checksum verification run `35529618037`: success
- complete immutable release run `35529626686`: success
- branch hygiene run `35529620904`: success
- promotion re-downloaded the four managed public assets and completed checksum verification before succeeding
- post-merge visual-QA artifact `10610971981`, digest `sha256:fa474fe1f627d013de86fd0df7248e7ab8483a46006dc315037e10e03e77f50c`, rendered 87 deterministic screenshots

## Alpha.49 candidate — Battery Preservation visual semantics

Alpha.49 is a narrow UI follow-up on the alpha.48 release line. It does not change Lenovo charge-threshold writes or any low-level hardware contract.

Scope:

- remove the ambiguous lock glyph and generic 10% ruler ticks from Battery Preservation;
- show three semantic threshold zones using existing theme colors: charge-resume range, hysteresis/hold band and stop/high-charge range;
- mark the start threshold with a lightning/charge symbol and the stop threshold with a pause/stop symbol;
- retain one high-contrast live battery-position marker;
- order the concise helper copy left-to-right with the visual: resume below start, stop at upper threshold;
- verify the preservation card in dark and light WPF snapshots before promotion.

Release gate:

- [x] alpha.49 isolated from the immutable alpha.48 tag line
- [x] semantic preservation gauge implemented without changing threshold hardware behavior
- [x] source regression coverage updated for colors/icons and removal of generic ticks/lock
- [ ] exact implementation-head CI + Package green
- [ ] dark/light preservation snapshots manually inspected at full resolution
- [ ] release-ready metadata freeze
- [ ] frozen-head CI + Package green
- [ ] expected-head merge and immutable alpha.49 GitHub release verification

## Alpha.48 release-ready — UX clarity and live state

Alpha.48 is the active candidate on immutable alpha.47. It does **not** add a new low-level hardware command surface.

Scope:

- make Home fan Auto visually and behaviorally exclusive by disabling competing presets while firmware/OEM Auto owns cooling;
- rename the user-facing Audio Safety middle state from **Media lock** to **Gesture lock** without changing its internal serialized/session enum, so keyboard and Windows/app audio behavior is explicit;
- reassert Silent from CoreAudio endpoint notifications after keyboard/app unmute attempts while retaining the existing bounded status cadence for endpoint convergence;
- tighten Compact Audio Safety width, label alignment and explanatory copy;
- replace Battery Preservation's text-heavy explanation with a compact threshold view and current-position marker plus one concise state sentence;
- keep shell-mode and native Advanced chrome colors live across light/dark theme switches;
- add deterministic dark/light visual coverage for fan Auto and retain existing safety/preservation snapshots.

Release gate:

- [x] alpha.47 immutable release remains the implementation base
- [x] implementation branch isolates alpha.48 from immutable alpha.47
- [x] fan Auto competing controls disable from canonical cooling state
- [x] Gesture lock naming/copy matches actual touchpad-only boundary
- [x] Silent external-volume event enforcement implemented without a polling timer
- [x] Compact Audio Safety geometry tightened
- [x] Battery Preservation threshold ruler implemented and verbose normal-state copy removed
- [x] live theme resource/chrome refresh repaired for the reported light → dark artifacts
- [x] source regression coverage updated for the changed contracts
- [x] exact implementation-head CI green · run `35845736667` / CI #2017
- [x] Package ThinkControl green · run `35845736662` / Package #1717
- [x] full-resolution WPF visual artifact manually inspected, including fan Auto dark/light, Compact Audio Safety, Silent light and Battery Preservation · artifact `10742907974`, digest `sha256:2c17e0f8c36d52554b6baacf19070ddf58ed18eab9b09673872a49a8974937d0`
- [ ] post-release physical X9 follow-up: confirm Gesture lock keyboard semantics and Silent keyboard/app re-mute behavior on the reference machine; this remains real-device evidence and is not inferred from hosted CI
- [x] final review/release-ready metadata freeze
- [ ] frozen-head CI + Package green
- [ ] expected-head merge, immutable alpha.48 release and public checksum verification

## Alpha.47 published release

Alpha.47 is the immutable interface-consistency, feedback and clarity release on alpha.46. It does **not** add a new low-level hardware command surface.

Published state:

- source version: `v0.1.0-alpha.47`
- `version.json.releaseReady=true`
- immutable base: `v0.1.0-alpha.46` at `ccca29ed696d422b21f96589b972fbee5884b291`
- release PR: #91, merged with exact expected head `34576eec7599a00eee4ab5bff2f9da977cc7859b`
- merge commit / immutable tag target: `f00a11ca789e0d360051bae9358e4312316cde59`
- immutable tag: `v0.1.0-alpha.47`
- public prerelease contains exactly four managed assets: Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`
- the implementation/release scope is closed; future changes belong to a later version

Scope:

- use one user-facing shell vocabulary: `Compact` / `Advanced`;
- keep Audio Safety as one session owner, expose it where it is operationally useful (Advanced Home, Compact media controls and Audio) and remove the duplicate Settings editor;
- place Compact `Media safety` inside the Brightness/Volume control cluster instead of the footer; the footer returns to version + Audio + Settings;
- expose both **Battery** and **Plugged in** Windows power preferences directly on Advanced Home while keeping the full Performance page;
- make update discovery reliable for long-lived tray sessions: startup plus stale-gated activation/resume checks (minimum four hours apart, no polling timer), followed by a persistent first-seen **Install now** / **Later** prompt once a ThinkControl window is visible;
- replace the Home fan `More…` sentinel with a real saved-profile menu, keep manual state truthful, and expose firmware/OEM `Auto` beside the presets;
- make Battery Preservation visibly trustworthy: mirror the live threshold state into AppState, replace the stale disabled Battery placeholder, confirm applied/disabled thresholds, and notify when charging actually pauses at the stop threshold or resumes below the start threshold;
- sample battery health from firmware full-charge/design capacity independently of completing a charge session and carry design capacity into long-lived tray sampling, so an 80–90% preservation cap does not stop the health trend learning;
- label Keyboard Effects **EXPERIMENTAL** and add a session-only, explicit-warning fallback opt-in when static backlight control exists but the provider does not advertise native effect capability; keep the existing deduplication/rate limit and existing Off/Low/High command surface;
- expand deterministic visual QA for the actual first-seen update popup, Compact modes, Home power/fan/Audio Safety states, Battery Preservation paused state, Experimental keyboard fallback and minimum/light layouts;
- preserve all alpha.46 low-level fan/battery/provider/startup/installer safety boundaries.

Release gate:

- [x] alpha.46 immutable release state reconciled in the persistent handoff
- [x] strict alpha.46 full-resolution visual pass completed before starting this candidate
- [x] Compact / Advanced visible naming unified
- [x] automatic update discovery made stale-aware on startup/activation/resume without a permanent poller
- [x] first-seen update decision made persistent with Install now / Later
- [x] Home update availability made directly actionable
- [x] duplicate Audio Safety editor removed from Settings; one canonical owner remains
- [x] Compact Media safety moved out of the footer and into the Volume/control cluster
- [x] Advanced Home exposes independent Battery and Plugged-in power preferences
- [x] Home fan Auto switch and real More profiles menu implemented without a second fan-state owner
- [x] Battery Preservation live state + applied/disabled + pause/resume feedback implemented
- [x] battery-health trend decoupled from full-charge completion and long-lived runtime keeps design-capacity context
- [x] Keyboard Effects marked Experimental with session-only warned fallback opt-in
- [x] deterministic QA fixtures and source regression coverage expanded
- [x] no new low-level hardware command or provider capability introduced
- [x] exact final implementation-head CI + Package green
- [x] exact-head WPF artifact manually inspected at full resolution, including minimum and light layouts
- [x] review gate reconciled: zero review threads; Codex review requests were blocked by the configured usage limit, so approval was not inferred
- [x] release-ready metadata freeze completed
- [x] frozen-head CI + Package green
- [x] PR #91 merged with exact expected-head SHA
- [x] immutable `v0.1.0-alpha.47` published with exactly four managed assets
- [x] published assets re-downloaded and SHA-256 verified
- [x] post-merge main CI, release promotion and branch hygiene green
- [x] post-release documentation records the immutable tag SHA and final workflow evidence

### Alpha.47 final evidence

Implementation head before the release-ready metadata freeze: `a1bd7f136aaa50bb15b2fac30157aa24778abba5`.

- implementation CI `35788158995`: success; repository hygiene, Release build, **213 tests**, real Compact ↔ Advanced shell smoke and WPF renderer passed;
- implementation Package `35788159070`: success; payload, bootstrap installer, deep installer/IPC smoke, oldest-supported alpha.14.1 updater compatibility and checksums passed;
- exact-head visual artifact `10720992192`, digest `sha256:4c09d4e8a2e852774acc121b0f03af22ee4d08a5101d05f6bb0a83739f2978b7`: **99 deterministic screenshots**, manually inspected at full resolution;
- frozen candidate head `34576eec7599a00eee4ab5bff2f9da977cc7859b`;
- frozen-head CI `35788527824`: success;
- frozen-head Package `35788527725`: success;
- exact expected-head merge commit / tag target `f00a11ca789e0d360051bae9358e4312316cde59`;
- post-merge main CI `35799444416`: success;
- branch hygiene `35799443578`: success;
- complete immutable tagged release `35799456825`: success;
- promotion / public re-download / checksum verification `35799444386`: success;
- public release `v0.1.0-alpha.47` published on 2026-09-22 UTC with exactly four managed assets.

## Alpha.44 stabilization release

Alpha.44 is intentionally narrow: cold-boot cooling convergence plus safer high-rate Touchpad controls. It does not broaden any low-level hardware writer.

### Cold-boot cooling convergence

Root cause on silent Windows startup: the UI can issue its first service status request before the auto-start hardware service/provider is ready. Hidden tray runtime intentionally avoids frequent hardware status polling, so a failed first request could leave the saved cooling profile unapplied until a later activation or resume.

The release fixes this with one bounded lifecycle-owned convergence path:

- the ordinary `HardwareServiceClient` offline backoff remains the default;
- cold-start convergence may explicitly bypass that backoff only during its finite login window;
- probes stop after success, cancellation, cooling-selection generation change or the bounded retry sequence;
- successful status still routes through the canonical `TryRestoreCoolingPreferenceAsync` owner;
- the existing seven-second firmware settle reassert remains the later one-shot convergence step after a successful restore;
- normal tray runtime stays sparse; there is no permanent fast hardware poller.

### Touchpad edge-control safety

Continuous Volume/Brightness now separates recognition from writing:

- an edge claim acts as a clutch and does not immediately change the setting;
- an extra 1.5 mm post-claim dead zone must be crossed before continuous writes contribute;
- accelerated contribution is capped to 6 percentage points per input frame;
- gesture volume intent is limited to 8 points ahead of the last confirmed CoreAudio value;
- CoreAudio writes are read back before becoming the next confirmation point;
- brightness uses the same ownership model with a 10-point lead limit;
- release/cancel clears pending gesture intent, preventing delayed catch-up after the finger leaves the pad.

Track Previous/Next is also safer:

- skip threshold increases from 9 mm to 12 mm;
- crossing the threshold while the finger is still down does not skip;
- one skip may commit on release after the deliberate threshold;
- Track-center Play/Pause remains the existing 450 ms / ≤3 mm / release contract.

### Alpha.44 implementation gate

- [x] cold-start race traced through initial status → client offline backoff → tray-only sparse runtime → cooling restore
- [x] bounded cold-start convergence implemented without adding a permanent polling loop
- [x] cooling generation/cancellation guards preserved
- [x] Volume/Brightness claim no longer performs an immediate write
- [x] continuous post-claim dead zone and per-frame contribution cap implemented
- [x] volume writes use CoreAudio readback and bounded confirmed-state lead
- [x] release/cancel drops pending continuous gesture intent
- [x] Track skip raised to 12 mm and moved to release-to-commit
- [x] low-level fan/battery/provider safety boundaries unchanged
- [x] implementation-head CI green
- [x] implementation-head Package ThinkControl green
- [x] complete implementation diff reviewed and all substantive Codex review threads addressed/resolved
- [x] final review feedback addressed/resolved; exact implementation head has zero unresolved review threads
- [x] freeze `version.json.releaseReady=true`
- [x] frozen-head CI + Package green
- [x] merge with exact expected-head SHA
- [x] immutable `v0.1.0-alpha.44` published and assets/checksums verified
- [x] post-merge main CI/promotion/branch hygiene green

### Alpha.44 implementation-head evidence

Final implementation-review head before release-handoff-only edits: `d029b947fdeb546b737310dfc59d82a97279da41`.

CI run `35152197755` completed successfully on that exact head:

- repository hygiene passed with 342 tracked paths / 27 Markdown files;
- Release solution build succeeded;
- Core tests: **202 passed, 0 failed, 0 skipped**;
- real Compact ↔ Advanced ShellSmoke passed;
- WPF visual QA rendered **85 snapshots**, including the corrected Compact footer;
- visual artifact `ThinkControl-Visual-QA`: artifact id `10469288845`, digest `sha256:d00f2f5b74f1fba7b06951e2e3acbf099d52c8b9b0dbf84d1e878a844b9d1f2b`.

Package ThinkControl run `35152197813` (#1591) completed successfully on the same exact head:

- version and canonical branding checks passed;
- release payload and web bootstrap installer built;
- deep installer/service/IPC reliability smoke passed;
- oldest-supported alpha.14.1 updater fixture verification and upgrade compatibility passed;
- checksums were produced;
- development artifact `ThinkControl-0.1.0-alpha.44-dev.1591`: artifact id `10469159099`, digest `sha256:2d6ba18c5c05cbde54c264be6727b3cb253495c7e611f19027c7645dbf9bd5ec`.

Final implementation hardening after review and visual QA:

- non-Auto firmware cooling restore now waits until the real current Windows power mode has been read; the default UI `Balanced` value is never used as a cold-start hardware baseline;
- startup restore, manual/direct fan writes, delayed firmware reassert and explicit shutdown handoff share the serialized cooling writer; explicit Quit cancels restore work, awaits the gate and hands a direct writer back to Lenovo Auto before WPF shutdown;
- unknown CoreAudio volume remains unknown: no live endpoint + no valid cache returns `null`, the Touchpad editor renders `—`, and blocked Audio Safety OSD uses status-only feedback rather than fabricating `0%`;
- the Compact Audio Safety selector was confirmed genuinely clipped by visual QA: the runtime card-sizing path forced an incompatible geometry. It now has a dedicated footer, **44 px footer clearance** and a normal **40 px ComboBox**, with no card top-offset. The central UI layout contract and full WPF renderer both pass;
- all three final review threads were replied to with the implemented behavior and resolved only after exact-head validation passed.

Review hardening after the first green candidate:

- CoreAudio failure no longer fabricates a 50% starting volume; Volume fails closed without a real endpoint baseline.
- the 1.5 mm clutch is measured in unscaled physical travel and sensitivity is applied only after the clutch;
- staying inside the clutch performs no Volume/Brightness OS write;
- Brightness starts from one live WMI baseline per gesture, fails closed when no live baseline exists, and advances confirmation only from post-write WMI readback; it never treats the early/default AppState brightness as hardware truth;
- Track preserves a signed physical peak so crossing 12 mm and retracing still commits once on release;
- cold-start convergence rechecks the captured cooling-selection generation after the service-status await before restore can write;
- continuous Volume/Brightness work carries gesture generations and uses per-control write gates, so release/cancel revokes old queued/dequeued work before a stale generation can touch the device after release completes;
- startup restore, delayed firmware settle reassert and deliberate profile/curve selections share one serialized cooling-write gate. Manual selections increment the generation before waiting, guaranteeing that an older startup write cannot become the final physical state after a newer user choice.

Multiple Codex review passes produced the actionable inline threads recorded on PR #83. Every substantive thread was addressed, replied to with the implemented behavior/evidence and resolved. Later code-review requests also hit the configured Codex review usage limit; that is recorded as a tooling constraint, not treated as implicit approval. Final source was manually diff-reviewed again and the exact implementation head passed CI + Package with zero unresolved review threads.

The earlier green implementation head `ab5065630886aea26b189a82940070f67d2fc876` was deliberately superseded after an additional manual exact-head review found two remaining safety opportunities: stale/default Brightness baseline use before the first display refresh, and a physical write-order race between startup cooling restoration and a newer manual profile choice. Neither earlier green run is used as final release evidence.

Alpha.44 now includes one small Compact-dashboard XAML/layout correction prompted by physical screenshot feedback. The full WPF renderer is green on the final implementation head, including Compact dark/light snapshots. Hardware changes remain limited to existing semantic service/status/cooling interfaces; no new register, IOCTL, EnergyDrv, Other Mode or EC write path is introduced.

### Alpha.44 release completion evidence

Frozen PR head: `7cd9de0a4c9ad85e0f83d9f68bad6aac29c92b98`.

- frozen-head CI run `35154300313`: success;
- frozen-head Package ThinkControl run `35154300283`: success;
- exact-head merge commit: `17abe5458a1f6f43f66383827d463bd1094498c2`;
- tag `v0.1.0-alpha.44` points exactly to that merge commit;
- complete immutable release run `35156064317`: success;
- post-merge main CI run `35156052891`: success;
- promotion and public-asset checksum verifier run `35156052870`: success;
- branch hygiene run `35156051821`: success;
- published release is `prerelease=true`, `immutable=true`, with exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`.

The release pipeline re-downloaded the published Setup/Payload/checksum/overview assets and completed `sha256sum --check SHA256SUMS.txt` successfully. Physical X9 behavior remains a separate real-device validation layer and is not inferred from hosted CI.

### Alpha.44 physical follow-up

- [ ] full Windows restart with Start with Windows enabled and Quiet/Balanced saved converges without opening a ThinkControl window
- [ ] delaying/restarting the hardware service during login still allows bounded convergence once it becomes ready
- [ ] tray-only operation returns to normal sparse cadence after startup convergence
- [ ] touching/claiming Volume or Brightness and immediately releasing does not alter the setting
- [ ] small post-claim movement remains inactive; deliberate movement stays responsive
- [ ] under a lagging/changing audio endpoint, extra movement cannot later catch up into a large volume jump
- [ ] release/cancel produces no delayed Volume/Brightness write
- [ ] Track movement below 12 mm never skips
- [ ] Track crossing 12 mm skips once on release and never while still held
- [ ] Track-center hold behavior remains deliberate and unchanged

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

This implementation head was used for the final visual review. The frozen head `5f0c3af64f3fc94028567d9e057b4b4844c2a602` then changed only release documentation and `version.json.releaseReady`; no UI/source file changed after the inspected implementation head. Frozen-head CI and Package both passed before merge.

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

- [x] set `version.json.releaseReady=true` on frozen head `5f0c3af64f3fc94028567d9e057b4b4844c2a602`
- [x] frozen-head CI run `34262769539` completed successfully
- [x] frozen-head Package ThinkControl run `34262769678` completed successfully
- [x] visual equivalence confirmed: after manually inspected implementation head `5501e0d0b9f211636f2106a0fe5eae418fc5caae`, commit `8b0d9865f1bf87e034e350883fd1c8f9573fc9f8` changed only this release handoff and commit `5f0c3af64f3fc94028567d9e057b4b4844c2a602` changed only `version.json`; frozen-head CI also rendered the WPF QA matrix successfully
- [x] complete PR changed-file list, comments, reviews and review threads reviewed; no review/comment backlog remained
- [x] PR #80 marked ready and merged using exact expected-head SHA
- [x] post-merge `main` verified at `ba13fab6d5b47cf127f4b627976662678f2ec491`
- [x] promotion run `35124966419` completed successfully
- [x] complete immutable release run `35124981334` completed successfully
- [x] promotion created immutable `v0.1.0-alpha.43` at the merged commit
- [x] release contains exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`
- [x] promotion re-downloaded the published assets and `sha256sum --check SHA256SUMS.txt` succeeded; GitHub also records the Setup/Payload digests above
- [x] post-merge main CI run `35124966441` completed successfully, including build, tests, ShellSmoke and WPF rendering
- [x] immutable alpha.42 and alpha.41 tag/release SHAs re-verified unchanged
- [x] merged feature branch removed by branch hygiene
- [x] release feature/regression issues #79 and #60 closed with completion evidence

## Physical follow-up — separate evidence class

Hosted CI cannot prove finger feel, audible silence, Lenovo fan acoustics or real battery charging behavior. These checks remain honest post-build physical evidence rather than hosted claims.

Touchpad:

- [ ] quick Track-center tap does nothing
- [ ] roughly half-second hold toggles once on release
- [ ] nothing auto-fires while still held
- [ ] >3 mm movement disarms the center hold for that contact
- [ ] Previous/Next remains reliable at the current 12 mm release-to-commit threshold
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
