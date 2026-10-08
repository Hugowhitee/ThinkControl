# ThinkControl release-readiness roadmap

## Published v0.1.0-alpha.71 — October 8, 2026

The reported screenshot exposes an actual route mismatch: SetCoolingCurve treated builtin:max specially and discarded its points in favor of exact full speed, while capability reporting still described the direct provider and the UI showed an unused 40% curve plus Auto. Calibrated direct Max now reaches FanSupervisor like the other curves. Fixed full-speed ownership reports its active capability/state and hides percentage curves. Without calibration, explicit Max may still use verified fixed full speed; it is not presented as a percentage curve. Hardware allowlists/readback and safe Auto fallback are unchanged.

A simulated production-supervisor regression checks that Max at 60 C writes its 40% curve target, then returns to Auto. Source contracts cover the service routing and full-speed telemetry. Warning-free local Release build, 323 Core tests and focused fan recovery smoke passed. Native dark/light fixtures at 980/1200/1600 were inspected for direct Max curve versus fixed full speed, Overview dropdown, moved preference rows and slider/text rails. Physical RPM acceptance on the updated installed version remains UNVERIFIED.

Overview Cooling uses the existing profile catalog, shared dropdown style and apply coordinator directly, with calibration/capability/busy gates, failed-selection restoration and a Fan settings link. Its state/calibration subscriptions are removed when the window closes.

Battery history detail moves into General preferences with the shared 160 px label rail. Compact Customize uses the same control rail, with descriptive text following it. Gesture popup text/value align with the shared slider's 13 px track inset.


Frozen source head 903ab62c1ba197fd908a1e191527cc28d8aba6a7 passed CI 37813498250 (323 Core tests, full native WPF lifecycle/motion smoke) and Package 37813498329 (installer/service/IPC and oldest-supported immutable upgrade). Visual artifact 11566017422 was downloaded, digest f4926483ec93f82db9a97cdc223d1f6dd402aaceaa43ccbbf107eba0fe89dc25 verified and target screenshots inspected. PR #141 merged to main ab803b4dce9b1f62649ee7aa269f08a3f8cda7ed; immutable v0.1.0-alpha.71 points there. Post-merge CI 37813891649, tagged complete package 37813915099 and promotion verifier 37813891653 passed.

All four public assets were independently downloaded and API digests verified: Setup, Payload, SHA256SUMS.txt and ui-overview.png. Setup/Payload also matched SHA256SUMS.txt. Setup: 2,412,230 bytes, SHA256 6dccd80b76c4d0e1f414eff729447f2d30aa07b8720fd8a87ef49580a26a61f7. Payload: 17,540,683 bytes, SHA256 8276876bcb7e4b250a8d3d1fc1f1d7598022a226be416e0fd78aa284e154c763. [Download alpha.71](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.71). No local installer was executed; updated installed RPM behavior and pointer/DPI/accessibility acceptance remain UNVERIFIED.


## Published v0.1.0-alpha.70 — October 8, 2026

Charging keeps subtle moving diagonal lines inside the level-colored battery, with a smaller pulsing green Fluent lightning next to the percentage. Discharge stays static; reduced motion and Compact stay static. Startup uses the shared preference control-start rail. Preservation puts its switch on the header rail and Edit beside the limit value. Expanded hardware details stretch to the System content width. Touchpad corner arrows and edge controls have separate rails; its popup has more room for icon, label, value and slider.

A bounded production-service request reproduced Max cooling rejection because no Windows power baseline was stored. Auto recovery was confirmed afterwards. Verified exact full-speed ownership does not depend on this baseline; its readback-gated path now runs before the guard. Thermal-policy profiles retain the baseline guard and hardware allowlists are unchanged. Updated installed hardware acceptance, sustained fan reproduction, pointer/DPI acceptance remain UNVERIFIED. Local Release build passed with no warnings, all 323 Core tests passed and repository hygiene passed. Native renders were inspected in dark/light at minimum, normal and wide widths; actual charging frames show the diagonal flow and adjacent lightning.


Frozen source head 79a44c526e2b26735aad0fb55b215e86f9b50f2e passed CI 37810650179 (323 Core tests, native WPF lifecycle/motion smoke) and Package 37810650166 (installer/service/IPC and oldest-supported immutable upgrade). Visual artifact 11564208918 was downloaded, digest ef55a967a5d00bc34fc80d08a6640e0d9e57a0ddba663fbe880a5fc9a4147b38 verified and target screenshots inspected. PR #139 merged to main 339b502d9be4b2322b411a5bcaf766ed5e6be5a8; immutable v0.1.0-alpha.70 points there. Post-merge CI 37811128192, tagged complete package 37811151103 and promotion verifier 37811128045 passed.

All four public assets were independently downloaded and API digests verified: Setup, Payload, SHA256SUMS.txt and ui-overview.png. Installer/Payload also matched SHA256SUMS.txt. Setup: 2,412,226 bytes, SHA256 6b5281936c4d9bb6f89e10ce9d37cca0317819bf6473e0667080279632a0bdfb. Payload: 17,539,731 bytes, SHA256 3a419a506e6907bb489424253e8225a2e338fd9b36de60e9fad1716208adc18c. [Download alpha.70](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.70). No local installer was executed; updated installed hardware behavior, sustained fan reproduction and pointer/DPI acceptance remain UNVERIFIED.


## Published v0.1.0-alpha.69 - October 8, 2026

Advanced charging uses a clear Fluent lightning symbol with a gentle brightness pulse. Battery level colors remain visible; discharge is static, low-charge warnings remain semantic, and Compact stays static. Direct fan profile errors expose the explicit Auto recovery action even when Auto is already selected. Edited built-in curves retain their points on measured direct controllers; firmware-only routing and Max cooling are preserved. 0% means the lowest measured running state, not fan off; firmware Auto can stop the fans.

A bounded alpha.68 production-service probe accepted Quiet and then confirmed Auto. This does not reproduce the sustained RPM complaint or verify the alpha.69 installed fix. New physical fan-off writes are not introduced. Build passed without warnings; all 322 Core tests and repository hygiene passed. Inspected the final Fluent charging symbol and static green/low-charge discharge renders in dark/light, plus failed-profile Auto recovery at minimum/normal/wide widths. Frozen head fbe465a354f9e627792d954d180589cd2b4bfb09 passed CI 37796749290 (322 Core tests and native WPF smoke) and Package 37796749367 (installer/service/IPC and oldest-supported immutable upgrade). CI visual artifact 11559376023 was downloaded, SHA256 verified and battery dark/light and fan-unavailable states inspected. The standalone local motion fixture received no composition frames; the updated hosted Windows smoke passed independently.

Publication: PR #137 merged to main 2990f30cd4cc61745e6d206254e1dc215236bab0; immutable v0.1.0-alpha.69 points there. Post-merge CI 37797278238, tagged package 37797296645 and promotion verifier 37797278076 passed. Exactly four public assets were independently downloaded and their API digests verified: Setup, Payload, SHA256SUMS.txt and ui-overview.png. Setup and Payload also matched SHA256SUMS.txt. Setup SHA256: 9b803e8bb1d12a3fbe6704f3c88575ccf424b58d41ebe19e8dfed8cbc80d1948 (2,412,216 bytes). Payload SHA256: e61bc75902f735cf0175221e4c7588bcad519ec7ea685b15a8831dedf6658385 (17,539,222 bytes). [Download alpha.69](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.69). No local installer was executed; sustained physical fan reproduction and installed acceptance remain UNVERIFIED.


## Published alpha.68 - October 8, 2026

Reported full-speed output on a low curve is not physically reproduced. The production loop previously confirmed EC state/ownership but did not compare sustained RPM against calibration. It now allows 30 seconds to settle, then hands back to Auto after three ticks above the measured state by more than 1000 RPM or 30%. A failed tick no longer silently retains its last output, and unconfirmed Auto recovery is labelled honestly. No new hardware write contract or unsupported lower state is enabled. Stored reference calibration starts at about 3500 RPM against 9400 RPM maximum: an 8% graph target resolves to that minimum, not literal 8% PWM. Physical reproduction and post-install fan acceptance remain UNVERIFIED.

Compact CPU/Sensors go to Diagnostics; all eight status-tile destinations have routed WPF checks. Mode selection is separate from the Saved modes management list. System battery history retention (already functional), device links and lid actions are visible without More preferences. Cooling replaces its provider-only Advanced fan controls expander with an inline measured-output hint; temporary tests remain capability-gated. Frozen head 29a8b1d1eb4cc7f0b4a85f7c51328712b43a3258 passed CI 37738288190 and Package 37738288157, including full routed WPF lifecycle/all eight Compact destinations, installer/service/IPC and oldest-supported upgrade. Local Release build and 322 Core tests passed; simulated production-supervisor tests cover settling, repeated excessive RPM and failed Auto confirmation. The local full shell run stalled after automation; hosted full shell passed independently. 72 canonical and eight targeted local renders plus relevant CI screenshots were inspected. CI visual artifact 11532692865 was downloaded and SHA256 verified.

Publication: PR #135 merged to main f3f50abd9f76811ccb44b975a60b0574756e0dc0; immutable v0.1.0-alpha.68 points there. Post-merge CI 37772797997, tagged package 37772817687 and promotion verifier 37772798056 passed. Exactly four public assets were verified: Setup, Payload, SHA256SUMS.txt and ui-overview.png. An independent public installer download matched SHA256 e74b273f15d745ea52c671c25bbe47ad1fd0386f838a8018bfc226cbc34f310d (2,412,208 bytes). Payload SHA256: ce15871df37f68d80556c536b3204a16f1d240ed824405ed99504ddb6397484e (17,538,758 bytes). [Download alpha.68](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.68). No local installer was executed; physical fan reproduction remains UNVERIFIED.

## Published alpha.67 - October 8, 2026

Owner requested a downloadable follow-up: direct mode-off, shorter Compact with Mode among configurable controls, actual-tile drag feedback, editor-local actions, larger remove icons, clearer Custom preservation and visible Advanced charge/discharge flow. Windows ClientAreaAnimation is disabled on the reference desktop, explaining absent alpha.66 flow. Alpha.67 adds On/Off/System without changing Windows; On is the requested app default, Compact stays static.

Bounded local evidence: 317 Core tests; 72 canonical plus compact/editor/custom/100% renders. Review caught clipped close glyphs and Custom text; corrected 40-unit targets and compact Custom segment were re-rendered and inspected. Frozen head f185caecdfd1cd88e228bbccb642d6c856c02b26 passed CI 37688137436 and Package 37688137604, including installer/service/IPC reliability and oldest-supported alpha.14.1 upgrade. Routed WPF lifecycle smoke passed. CI visual artifact 11512555271 was downloaded, SHA256 verified and target screenshots inspected. No physical mode/battery-threshold writes were issued in this UI pass. Supplied No mode failure lacks expanded provider detail: effective-power baseline and transaction restoration are corrected, not every possible provider failure. Native pointer drag and installed-device acceptance remain separate from routed WPF fixtures.

Publication: PR #133 merged to main 9d040063d9e50b08ec044105cd817534712e860d; immutable tag v0.1.0-alpha.67 points there. Post-merge CI 37729557198, complete tagged package 37729566801 and promotion verifier 37729557229 passed. Exactly four public assets are present: Setup, Payload, SHA256SUMS.txt and ui-overview.png. Promotion downloaded and verified the assets. The public installer was independently downloaded locally and matched SHA256 7202513e23c04bf03338df01a0ac59ebc5faedbc6cb8dd2377144cca28ec4c22 (2,412,213 bytes). Payload SHA256: d8ac4c36986d9348c25274794d0b434dcf459b00a939c0f2dcccf0c1f5badbbb (17,538,231 bytes). [Download alpha.67](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.67). No local installer was executed; installed-device acceptance remains separate.

## Published alpha.66 — October 7, 2026

Merged PR #130 carries the existing WPF consistency candidate plus the four targeted owner corrections. Simple Mode icon/title and Automation switch, editor rule switch on the title rail, bounded Windows-startup row and semantic rule dots are implemented. Explicit Automation Off survives context changes until Resume; manual selection uses the existing pause-until-context-change policy. Rule enablement is preserved. No hardware provider or write permission changed.

Local Release build passed with zero warnings/errors; 315 Core tests and real WPF shell lifecycle smoke passed, including explicit pause/resume, manual override, rule/condition draft/persistence, editor continuity and shared switch hit area. Native renders: 72 canonical dark/light minimum/normal/wide and 18 mode/editor/session state cases. Visually inspected Home dark normal/light minimum, Automation dark minimum, System dark normal, rule editor dark minimum/light wide, mode failure dark normal and expanded sessions light normal. Figma screenshots were inspected after each targeted edit; shared component and semantic variable bindings were read back. Frozen head 7d42b97503b7cd03e6b9a4bcabdb74778151dbdf passed CI 37683349915 and Package 37683350049, including installer/service/IPC and alpha.14.1 upgrade compatibility. CI visual artifact 11510496911 was downloaded, digest verified and target screenshots inspected. Installed pointer/DPI/animation acceptance and SolidWorks fan reproduction remain UNVERIFIED. Previous design-only implementation-pending statements below describe the preceding pass and are superseded by this candidate.

Publication: immutable tag v0.1.0-alpha.66 points to merged main 8c3b67d0c66659b055c42dc25a2761691f5d7ddc. Post-merge CI 37683736071, complete tagged package 37683763076 and promotion verifier 37683736048 passed. Exactly four assets are public: Setup, Payload, SHA256SUMS.txt and ui-overview.png. The promotion downloaded and verified all assets; the public installer was independently downloaded locally and matched the checksum. Setup SHA256: b4fb1531e2fc91be5f42da636758792698d9d1b5438d489ff0bc0100f6ffd2d1 (2,412,236 bytes). Payload SHA256: f00f8278c5056a01966f32ca1c28e118f7ea4799a368cea79b5b38ced68f6432 (17,535,879 bytes). [Download alpha.66](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.66). Installed UI remains alpha.65; no local installer or hardware-state changes were performed during publication.

## Published alpha.65 — October 7, 2026

## Post-alpha.65 Figma consistency handoff — design only (2026-10-07)

The selected Figma file was reconciled against current `main` / published alpha.65 and the current mode/power state owners. **No WPF implementation or release was started in this pass.**

Follow-up context recovery on October 7 verified public `main` at `d8e115ffa69dbdab8674c80633d6997c647284a8`, latest release `v0.1.0-alpha.65`, and open PRs #130 (this design handoff) and #120 (brand exports). The running installed UI executable reports `0.1.0-alpha.65+2ee3737efae8ab6f4972e0501c6158d32ea99baa`. This is build identity, not installed acceptance. GitHub CLI authentication is unavailable in this session; public API and Git fetch provided the read-only repository evidence.

Additional Figma corrections: canonical Automation rule grouping/switches, condition/rule editor switches, explicit No mode command and paused result `318:7077`, local failed-Performance feedback `318:6310`, honest incomplete-recovery component semantics, session disclosure targets, Light wear-estimate copy, Dolby status overlap and adjacent version check. Exact node IDs, commit timing and interaction contracts belong in UI-DESIGN-HANDOFF.md / PRODUCT.md.

Source findings relevant to implementation:

- `ModesPanel.CreateModeRow` renders display rows without activation handlers; only `ModeSelector_SelectionChanged` activates, and it returns early for an already-active unmodified mode. A repeat No mode command needs an explicit action path.
- `ThinkControlModeCoordinator.ActivateAsync` must restore owned facets before No mode can succeed; restoration can fail. Manual pause is notified only after success. `ActiveModeId`, `VisibleModeId`, saved power preference and actual OS/provider readback are different contracts.
- `ThinkControlModeAutomationService` evaluates only enabled conditions, treats zero enabled conditions as no match and releases manual pause when the observed winner changes. The serialized Enabled fields already exist; visible condition switches and direct list rule switches are still missing.
- `AdvancedWindow.ResetTransientPageUi` calls `ModesPanel.ResetNavigationView` on page entry. Saved modes and Automation currently use separate panel surfaces; preserve their drafts deliberately without adding another coordinator or a duplicate settings owner.
- `BatteryGauge` already binds charging/discharging, gates motion on visibility and Windows ClientAreaAnimation, reverses diagonal flow and fades when idle. Installed missing motion is not yet diagnosed; do not claim a new animation implementation or a proven repair.

Current-run validation: the existing native WPF snapshot harness generated the discoverability matrix for Home, Modes, Automation, Fans, Battery and Settings in dark/light at 980/1200/1600 widths. Home-Dark-1200, Modes-Dark-980, Battery-Light-980 and Settings-Dark-1200 were visually inspected as runtime-fixture evidence; they demonstrate remaining design/runtime drift. Figma representative Automation and failed activation were inspected before expansion, then affected states/editors and sibling surfaces were inspected; changed mode compositions were also inspected in Light. Shared switch/component/style/variable bindings and prototype reactions were read back. No native installed UI input was automated: the required Computer Use node session is not exposed here. Actual installed chart hover, trigger behavior, editor continuity, animation, DPI/text scaling, Touchpad hit geometry and SolidWorks fan acceptance remain **UNVERIFIED**. No build/tests/installer result from an earlier run is promoted to user acceptance.

Design-ready references now cover Overview `6:2`, System General `11:422`, Battery `9:205`, Saved modes `11:85`, Automation `11:256`, shared mode status `253:6354`, editors `29:548` / `29:835`, mode states `130:2940` / `130:3055` / `130:3170`, integrated/expanded Battery states `65:1089` / `66:1133` / `66:1311`, passive mode notification in `198:5007`, Updates `12:103`, and the reconciled Light QA frames Overview `34:1054` / Battery `67:1707`.

Implementation / acceptance still required:

- make System General use one shared segmented-control treatment for Theme and App icon opens, and retain compact label/value grouping;
- make Overview keep Mode low and separate from Keyboard while showing only confirmed mode/source state;
- make Saved modes and Automation share the compact confirmed/manual/rule/error status grammar; a failed Performance facet must leave the previous confirmed mode active instead of making requested selection look successful;
- preserve an in-progress Modes editor when switching Saved modes ↔ Automation, but reset it after leaving the Modes primary destination and returning;
- surface a passive mode-enabled notification only after confirmed automatic activation;
- implement the revised in-page Battery details / Recorded sessions / History settings hierarchy without adding another disclosure layer;
- verify the existing BatteryGauge charging/discharging diagonal-flow animation in the installed app. Source binding and ShellSmoke coverage exist, but the owner reported no visible motion in alpha.65, so installed behavior is **UNVERIFIED**;
- repeat live Figma↔WPF comparison, minimum/reference/wide light/dark visual QA, actual DPI/text scaling, keyboard/focus/error/unavailable acceptance and installed alpha.65/next-candidate lifecycle;
- repeat the fan-curve hover acceptance that was interrupted with Escape, and reproduce/resolve the intermittent fan behavior under SolidWorks load before claiming that evidence.

This section is the implementation handoff for the approved Figma corrections; it is not release evidence.

Curve inspection shows temperature and interpolated target in read-only previews and the editor without editing. Edit curves is directly in Cooling, preserving capability/calibration gates. Overview's existing mode selector moves above quick controls. Overview, Saved modes and Automation share the actual active mode and its manual/rule source, transition, modification or failure. Rules distinguish the applied rule from matching candidates during dwell/manual pause; active-rule changes notify even when wording stays equal. Resume has one location per surface.

Battery details and Recorded sessions are visible sections; day/session disclosure remains. History settings is a separate secondary action, reveals existing retention/reset and brings it into view. Reopening Battery closes management. The app-icon Compact/Advanced preference sits with Theme in General preferences; the tray shortcut remains Compact.

Validation: Release build, 315 Core tests and WPF dispatcher smoke passed before final freeze. New smoke covers read-only curve hit-testing/interpolation/no edit and shared active-rule/manual-pause presentation. Current-run dark/light minimum/normal/wide renders cover Overview, Modes, Automation, Cooling, Battery, Settings and expanded history. Live isolated WPF inspection reached Overview, Modes, Automation, Battery, sessions and history settings; it exposed an off-screen management panel, corrected by bringing it into view. Physical Escape interrupted final native fan-hover acceptance; no further computer input was issued. **UNVERIFIED:** final manual pointer hover, installed alpha.65 acceptance, actual DPI/text scaling and live Figma reconciliation. Fixtures establish layout, not physical fan output or SolidWorks reliability.

Final PR head `c9c153872538fb13b90b34ba1e381032f28e0862` passed CI `37613195551` and Package `37613195565`, including deep installer/service IPC and the alpha.14.1 upgrade fixture. PR #128 merged at `2ee3737efae8ab6f4972e0501c6158d32ea99baa`; immutable tag `v0.1.0-alpha.65` points to that source. Merged-main CI `37613506078`, complete tagged packaging `37613521650` and public verification `37613506066` passed. The verifier downloaded exactly four managed assets on GitHub and confirmed Setup/Payload checksums. Independent public installer HEAD returned HTTP 200; the public checksum text agrees with asset digests. Setup is 2,412,220 bytes, SHA256 `2ab7914409645155159220384fd5b6974918ceb457517268fb523296d47409bd`; Payload is 17,532,031 bytes, SHA256 `d7bb21fd9c6ebda4b26604103cac3af55366a89e03318a206dd1390b3803221a`. The other assets are SHA256SUMS.txt and ui-overview.png. [Download alpha.65](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.65). Local installation and new hardware tests were not performed in this publication pass.

## Published alpha.64 — October 7, 2026

Owner feedback on installed alpha.63: Modes cannot apply under High performance; profiles sometimes fall back, possibly under SolidWorks load; recorded sessions clip and precede awkward details. Live service observation showed Quiet/EC state 4 at approximately 55°C with no safety override, so heat alone is not established as the cause of the intermittent failure.

Modes explicitly prepares Balanced for a performance facet, verifies the effective overlay and restores the original plan on mode exit/rollback/app exit; externally selected plans supersede the temporary lease. Background AC/DC preferences do not silently switch plans. Native Windows proof: High performance → confirmed Efficiency under Balanced → original High performance restored. This is effective API readback, not a workload-performance benchmark.

The existing fan supervisor retains a curve through missing sensors, confirms Auto, avoids repeating an already-confirmed handoff, requires two consecutive valid samples below the recovery threshold, and preserves the provider boundary. A curve selected at ≥94°C is queued under firmware cooling and resumes only below 90°C. Manual requests, provider changes, lost ownership and rejected writes retain their explicit Auto fallback. Bounded service logs now record handoff reasons; UI errors expose the underlying detail in a tooltip. No new fan register/state or forced ownership loop is introduced.

Battery details groups capacity/sensor measurements and learned charging power with the Windows usage link. Recorded sessions follows it, with shared row columns and two-line time/energy summaries. Existing day aggregation, session detail graphs, retention and destructive reset remain owned by the existing history service.

Local validation: Release build (zero warnings/errors), 315 Core tests, full WPF shell smoke and native plan proof passed. Supervisor runtime simulation passed warm input/output, sensor loss/exception recovery, failed Auto confirmation, hot selection, consecutive-safe samples, provider changes and rejected-write fallback. Six dark/light minimum/normal/wide expanded history renders were visually inspected. Final PR head `9b5590d5511d9186df95d471c43e85d8d8151e09` passed CI `37601105793` and Package `37601105796`, including deep installer/IPC lifecycle and the alpha.14.1 upgrade fixture. PR #126 merged at `8f3e0ed76668ca50cbb7085c6cb49f28b6b40483`; immutable tag `v0.1.0-alpha.64` points to that merge. Merged-main CI `37602449802`, complete tagged packaging `37602467540` and public-asset verification `37602449693` passed. The immutable prerelease is public with exactly four managed assets. The verifier downloaded all four assets on GitHub and checked Setup/Payload hashes; the public installer endpoint also returned HTTP 200 locally. Setup SHA256: `c41b68e82b4db5a065006f4ca0c8f0f09afe752ba9810eed9d9a9042ac602c78`; Payload SHA256: `be9094f2374b9c347a482ea6f42d1b12589b48d7589d75cc0ee715cbcedf0132`. [Download alpha.64](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.64). SolidWorks-load reproduction, new service behavior on actual hot hardware and installed alpha.64 acceptance are **UNVERIFIED**. Do not claim this release fixes every intermittent fan failure.


## Published alpha.63 — October 7, 2026

Alpha.62 owner feedback identified an Audio readback race/rounding jump, retained navigation-tab selection, excess Compact header chrome and distant update status. The existing Audio probe generation now invalidates pre-interaction/pre-write reads; confirmed rounded readback preserves the continuous thumb coordinate while real external changes still update it. Context tabs isolate their radio group and rebuild destination-owned selection on click/revisit. Compact retains the original SVG geometry, shifts its wrapper left and uses an inline notification action. Updates shows the green check circle beside the version. Preservation uses a slim green current-level track and displays the generic normalized wear-cycle basis (0–100% = 1.00), separately from actual firmware cycle history.

Local validation: Release build, WPF regression for navigation/revisit and audio readback/active-drag behavior, 72 canonical and six Compact dark/light renders. Changed surfaces were visually inspected at minimum/reference sizes. All 312 Core tests pass. PR #124 is merged at `8ae9af4fca3039b41f9074a59edf445ff31a7e15`; final PR head `69cd9d40daa9612a78bef014beeaec2a7aaa3f21` passed CI `37584822256` and Package `37584822255`, including clean install, service IPC and the real alpha.14.1 upgrade. Immutable tag `v0.1.0-alpha.63` points to the merge. Merged-main CI `37585510366`, complete immutable packaging `37585522411` and full-asset promotion verification `37585510165` passed. The promotion downloaded all four public assets and verified the Setup/Payload checksums. Installed pointer drag/release and an artificially delayed real endpoint read remain UNVERIFIED; no hardware or user-volume writes were used for these isolated tests. Existing fan calibration and alpha limitations are unchanged. [Download alpha.63](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.63), published without a dev suffix. Setup is 2,412,226 bytes, SHA-256 `a6a775cbb41a685a27d1a41a50464b22a41df01246ff3e5fa1f7559598125585`; Payload is 17,528,040 bytes, SHA-256 `7ac07cc44f9a794ec0f936cbd059f2e0c2cdce5c81badab33fd8f579ee8906ae`. The other two assets are SHA256SUMS.txt and ui-overview.png. Tag/source SHA and public checksum text were independently checked; large payloads were not redundantly downloaded over tethering. Installing this release and manual pointer/keyboard acceptance remain UNVERIFIED; no local install or hardware-state changes were performed in this publication pass.

## Previous published alpha.62 — October 7, 2026

[Download v0.1.0-alpha.62](https://github.com/Hugowhitee/ThinkControl/releases/tag/v0.1.0-alpha.62). PR #122 is merged at immutable tag/source `65b1686777b190d5cad48080db318f0037ef2157`. This is the versioned alpha release, with no dev suffix. The source checkout is on current main; original local recovery material is preserved and QA artifacts remain ignored.

Frozen PR head `c40a848816d1188b620b67b7429c319fa1f8b48f` passed CI `37573675715` and Package `37573675714`. Merged main CI `37573866354`, complete immutable packaging `37573877284` and full-asset promotion verification `37573866355` all passed. The latter downloaded the release assets, verified Setup/Payload checksums and exactly four managed public assets. Local Release build had zero warnings/errors; 312 tests and WPF lifecycle/motion/migration smoke passed. The visual checks include 72 canonical renders, 179 secondary-state fixtures, battery history and minimum/reference Compact editor renders. Independent review found no remaining blocker in the reviewed editor surfaces.

Setup: `ThinkControl-Setup-0.1.0-alpha.62.exe`, SHA-256 `01a8238c9393b0d5c952aa0496a2e54b5dab33bdfc09c0b819c64e11a972bb99` (2,412,226 bytes). Payload: `ThinkControl-Payload-0.1.0-alpha.62.zip`, SHA-256 `3b2f2f85216b0593a5a47ee91146fdd1672e16230337d478919e2cdab2961cf3` (17,527,844 bytes). The other assets are `SHA256SUMS.txt` and `ui-overview.png`. The public checksum text and tag SHA were independently read after publication; large payloads were not redundantly downloaded over tethering.

**Installed state:** alpha.61 remains the last verified local UI/service installation. The service is Running and the Windows power plan is High performance. This UI/release pass did not issue hardware writes or replace the installed service. Installing alpha.62 and accepting its native pointer/keyboard/drag, DPI and lifecycle behavior remain UNVERIFIED. Physical calibration/custom-curve/Auto evidence below applies to the production controller tested earlier; it does not certify every installed UI scenario. Keep the disclosed alpha limitations and follow-up gates below.

## Alpha.62 experimental release freeze (2026-10-07, historical checkpoint)

PR #122, branch `fix/calibrated-curves-and-navigation`, contains the native IBM Plex Sans/Fluent redesign and the exact-device calibrated custom-curve controller. Publication is authorized as an experimental alpha after frozen-head CI/package pass; `releaseReady=true` is a publication flag, not a claim that every hardware or manual-input scenario is verified. Alpha.61 is still the installed baseline until the owner installs the release. Do not install a dev build or silently change the original battery thresholds/power plan.

Bounded evidence: full solution build, 312 Core tests and real WPF dispatcher smoke; 72 canonical dark/light minimum/reference/wide renders; battery/cycle history renders; Compact and its owned editor in both themes, including 780×500. Independent review found no blocker in the editor; its mixed-case legacy layout finding was corrected and added to migration smoke. The production-service physical fan calibration/custom-curve/Auto evidence below is retained. GitHub source `f33ca071dfa891d2ec617ca737b555170644cb79` passed CI `37573156785` and Package `37573156794`, including installer/service IPC and oldest-supported upgrade; final freeze changes must pass new exact-head runs before merge.

The visual QA host is isolated from tray, repair, polling, power writes, automation and user preference files. Actual native pointer/keyboard acceptance was interrupted by physical Escape. **UNVERIFIED alpha follow-up:** manual chart hover/keyboard and editor drag/cancel/lost-capture; installed redesigned UI/service lifecycle; actual DPI/text scaling; broad reboot/wake/AC/DC; independent fan channels. These are disclosed in the immutable release notes and stay tracked. No bug-free or continuous-PWM claim is made. Hosted installer acceptance does not establish physical hardware acceptance.

This is the **single persistent handoff/checklist** for unfinished release and commercial-readiness work. Keep it current; do not create parallel release checklists. Executable gates live in `.github/workflows/`, `tools/` and tests.

## Battery and Compact verification checkpoint (2026-10-07)

The current candidate compares estimated charging wear for 0→selected limit with 0→100, using a whole percentage and explicit ranges. Top-up wear numbers were removed from the primary cards. Firmware cycle history is a separate fourth chart tab, persisted additively in schema 6; unknown counts and counter resets are preserved truthfully. Details now add energy capacity, temperature and learned charge time rather than repeating charge, health, power and cycles.

Compact now uses the selected 420×565 composition, shared metric content and numeric units beside the value. Its small battery silhouette renders at the correct size. A separate owned layout editor offers status replacement/swap and four quick-control swaps; schema 2 migrates the legacy status array and preserves both families. Visual QA does not read/write the user's compact layout. Charging/discharging flow follows real state, respects Windows reduced motion, fades on pause and releases its rendering callback after pause/unload.

Verification: solution build succeeded with zero warnings/errors; all 312 Core tests passed; real WPF dispatcher smoke passed, including legacy layout migration, family preservation, duplicate rejection and actual composition-clock battery movement/pause/unload. Twelve battery/history renders and six Compact renders passed layout/typography guards; four editor renders passed in dark/light and minimum/reference dimensions. Static snapshots do not prove animation or native drag input. A final native acceptance attempt was stopped by physical Escape; its isolated process was closed.

The later editor control previews use the live controls' items/selection and shared ComboBox style; details/preservation rerender passed after the threshold gauge move. Native chart input, editor drag/cancel/lost-capture and installed alpha.62 lifecycle remain **UNVERIFIED alpha follow-up**. Frozen-head CI/package and four-asset checksum publication verification remain delivery gates. Publication status must be updated only after observing the actual release.

## Current release state

### Alpha.62 acceptance — X9 21Q6, October 6, 2026

Alpha.61 is published and installed: PR #121 merged to `09e6aaecc1e927e4e88c3ce6a80d1f7b679196a9`. Main CI `37419408264`, immutable release `37419420114` and full-asset verification `37419408270` succeeded. The public release has exactly four managed assets; Setup SHA-256 is `5c145ca610d3c75da25dea945a870a48cd581ff16084c78f80ae97a8a9fed62d`. Both installed UI and service report alpha.61 plus that commit. The older dirty checkout remains preserved.

The active `fix/calibrated-curves-and-navigation` branch prepares alpha.62. It extends the existing exact-device ownership session rather than adding a competing fan worker. The native MHQT/MHGT/MHAT SCRM contract is restricted to **21Q6/N4CET45W** and reviewed running EC states **4, 5, 6, 7, 0x40**. Unknown firmware, off/states 1–3, selectors, continuous PWM and guessed target-RPM writes remain unavailable. The per-fan writer rejected in alpha.38 remains disabled.

Physical production-service acceptance completed through actual named-pipe IPC on October 6. Calibration measured shared tachometer medians **3500 / 3700 / 4000 / 4400 / 9400 RPM**. Manual 0%, 45% and 99% settled at **3500 / 4500 / 9244 RPM** in the final repeat. A valid custom curve requesting 40% held **4000 RPM**, with the curve ID, applied state 6 and native ownership confirmed. It then returned to Lenovo Auto at 4500 RPM; the original installed service was restored to Running. CPU temperature stayed below the probe guard. An earlier Max-to-lower transition probe independently verified the Auto bridge in both directions. The owner accepts slight variation in lower states; these results do not establish smooth continuous speed control or independent fan sensors.

One repeat was affected by the WPF QA client's ability to connect to the live service. Visual-QA application clients are now explicitly disconnected, including shutdown and control requests. Repeating the same production-service sequence alongside the isolated UI tests passed. An earlier test curve was rejected correctly because its final point was not 100%; the test input was corrected without relaxing curve validation. Private evidence is retained under `artifacts/local-recovery`, including failed runs.

The shared UI pass standardizes field padding, icon drawings and hit targets, disabled sliders, action-error typography, Home title/control gaps and compact Mode navigation. Every Advanced destination resets scrolling/disclosures through one navigation owner; Modes and Automation also return from their editors to the list. Fan availability sits beneath the reading. System's live hardware details follow the shortcuts, aligned right, with provider diagnostics collapsed separately. Battery-aging text explains stress, heat, cycles and age without a numerical wear prediction.

Lenovo's Fn+Space popup was physically restored by restarting its own TPHKLOAD loader, which supplied the required private pipe to tposd. The owner confirmed it works again. No persistent Lenovo setting was changed. The effect suppression lease now releases only windows hidden by that session at burst expiry; a real WPF regression checks restoration and untouched pre-existing popups.

This is the sole deduplicated scope checklist. **Verified** means the stated bounded behavior has direct evidence; it does not certify the entire subsystem. **Partial** means implemented behavior with remaining acceptance. Hosted tests are distinct from physical device evidence.

| Area | Status | Evidence and remaining acceptance |
|---|---|---|
| Recovery, history, data and GitHub | Verified | Alpha.61 merged/published/installed; original source and user data preserved. Alpha.62 publication and installed upgrade remain pending below. |
| Fans: Auto/Max, lower speeds and custom curves | Partial | Five-state calibration, manual mapping, valid custom curve, native ownership, actual RPM and Auto/service restoration physically verified through the production service. Final installed alpha.62 UI/quit acceptance pending. No continuous PWM or independent two-fan reading claim. |
| Fan lifecycle and external ownership | Partial | Existing single owner, 45-second client lease, Max downshift bridge, readback loss and failed-recovery retention covered; pending cleanup reopens EC, and suspend now releases regulated ownership. Broad AC/DC, reboot and physical wake remain unverified. |
| Modes application and manual ownership | Partial | Sparse apply, rollback/backoff, modified-session restoration and facet release pass real WPF dispatcher tests. Installed touchpad-only mode and No mode restoration passed earlier. Physical all-facet combinations remain unverified. |
| Automation and school Wi-Fi | Partial | Separate persisted rules; priority then visible saved order; equal-priority reorder, five-second dwell, pending-match overrides and handoff pass dispatcher tests. Installed current-network activation, pause/resume and exit restoration passed. Real school network and wake remain unverified. |
| Shared headers, Defaults, links and navigation | Partial | Common header/action styles and dark/light width matrix retained; all 13 destinations pass actual WPF navigation reset/reselect checks, including editors and disclosures. Final visual/installed checks recorded below. |
| Home, Compact, status and typography | Partial | Shared padding/icons/disabled treatment; Home gaps and Mode width corrected; one availability grammar and red action errors. Native shell focus/routing tests pass. Final installed verification pending. |
| Battery thresholds and physical charging | Partial | Threshold write/readback, physical stop and resume already verified; latest 75–80 pair unchanged during fan acceptance. Preserve actual current pair at upgrade. Natural hysteresis, external changes/reboot/wake remain unverified. |
| Battery ETA, cycles, explanation and history | Partial | Stop-target ETA, missing/stale-data rejection, gap/DST aggregation, retention and firmware/session cycle distinction tested. No precise wear prediction. Long measured-session calibration and physical wake remain unverified. |
| Audio/media/touchpad | Partial | Installed volume write/readback and bounded gesture/slider owners tested. Physical edge gestures, reverse-close and haptics remain unverified. |
| Keyboard and Lenovo popup | Partial | Static levels verified; owner confirms restored Fn+Space popup. Latest-selection, delayed-write/disposal and popup lease regressions pass. Reactive/Audio physical behavior and rapid installed effect acceptance remain unverified. |
| Startup, tray and responsiveness | Partial | Single primary surface, rapid opening/focus and dispatcher navigation pass real shell tests. Startup preference preserved. Cold boot/background/wake and long-duration resource measurement remain unverified. |
| Updates, install/service and diagnostics | Partial | Alpha.61 installed hashes/version confirmed. Dev ordering, cancellation and existing-install upgrade covered by repository gates. Alpha.62 local upgrade and frozen-head hosted package/promotion are pending. Alternate credentials/uninstall are hosted-only acceptance. |

Current local gates: solution build has zero warnings/errors, **312/312** Core tests pass, and the real WPF shell smoke passes. The visual matrix includes measured-curve fixtures at minimum, normal and wide sizes in both themes. Later source edits must rerun affected gates. Frozen-head CI/package, immutable release and published checksum verification are required before download delivery. Installed alpha.62 acceptance remains a separate UNVERIFIED follow-up while alpha.61 is installed.

### Prior recovery evidence

Power-policy research observed matching selected/actual overlays but an effective Efficiency overlay, even on AC with energy saver off. Original High performance plan and AC/DC preferences were restored. No physical performance change is inferred from an accepted selection. Historical evidence remains in the research documents and ignored local artifacts.

Battery stop/resume evidence records 38.975 W charging, three zero-rate samples after a temporary 40–45 window, restored thresholds and positive charging resumption. Modes/rules were validated with temporary non-hardware-heavy modes while preserving the existing three modes and one rule. Earlier development installers and screenshots are historical evidence, not the current installed version.

Previous immutable alpha.60: `bda4894040d5b658201613df954430bb27733e36`, published October 4, 2026. Its fan-profile limitations are superseded only by the bounded exact-firmware evidence above.
Previous fully recorded release evidence (alpha.58):

- `v0.1.0-alpha.58`
- immutable tag/release SHA: `8f27003f63fc80142b5d4cb16a11659a0a83c19a`
- published 2026-10-02 at 17:17:40 UTC as an immutable prerelease
- canonical PR: **#109 — Prepare alpha.58 fan capability, Modes and Battery stabilization**
- reviewed frozen branch head: `2198084013e706a82933c6ffdf9b55c0d042d2d7`
- frozen-head CI `37039150096` / #2222: success
- frozen-head Package ThinkControl `37039150038` / #1905: success
- squash-merge commit: `8f27003f63fc80142b5d4cb16a11659a0a83c19a`
- complete immutable release run `37039432658` / #47: success
- promotion/checksum verification `37039410023` / #79: success
- post-merge main CI `37039408945` / #2223: success
- branch hygiene `37039409906` / #93: success
- release contains exactly four managed assets:
  - Setup: `sha256:04b5f610b53bf66f4401d2310f106f47e93c6224cab99cab68073f4ba4e1ca70`
  - Payload: `sha256:4e219dae4ecb7919c4e4b8b9534e5dd2de57165336903cf351f674cc9ce89ada`
  - `SHA256SUMS.txt`: `sha256:de67f3114b78a019633cadbed9bc9662a0ee481ca936f9fae752c124d1e7b151`
  - `ui-overview.png`: `sha256:f5653726dcb1286f2519dfdd74e6d5f7aef2b72287a90e7c134387fd1e3ef317`
- alpha.58 restores service-owned fan-controller capability truth, recomposes Modes as editable saved modes, restores direct contextual Windows Settings actions, and clarifies Battery Preservation with 80 / 85 / 90 / 95% limits plus a stable 0%→limit modeled-wear comparison
- physical X9 Quiet/Balanced/Max and battery-threshold behavior remain a separate real-device evidence class; hosted CI does not invent that evidence

## Alpha.59 published experimental prerelease

- Tracking: issue #113 and merged implementation PR #114 (`main` commit `0154163840ec5f37be086ac20561b68268b75eef`). The published immutable release is `v0.1.0-alpha.59`. This is an explicitly experimental alpha update with outstanding OEM hardware validation, not a claim that physical fan switching was repaired.
- Implementation: one Modes selector, Save & apply, explicit failed facet/rollback feedback, bounded automation retries, Lenovo firmware acknowledgment caveats, capability-gated custom battery thresholds, clearer battery-aging explanation, and consistent Defaults/Windows links.
- Existing evidence: final source PR head CI `37222379239`, Package `37222379242` and post-merge main CI `37222491468` all passed. Detailed 261-test source CI `37221596098`, packaging `37221596095`, and visual gallery artifact `11310905138` also passed or were inspected.
- **UNVERIFIED, still tracked in issue #113:** real X9 21Q6/21Q7 AC/DC Quiet/Balanced/Max policy changes; boot/resume and Windows/F8/Vantage interactions; and real battery charge stop/resume with custom thresholds. The LITSSvc pipe acknowledgment is not proof of active firmware state. Users must not interpret this prerelease as a physically verified fan fix.
- Safety constraints stay unchanged: rejected direct EC and per-fan target-RPM writes remain disabled; unknown hardware stays read-only; charge-threshold writing remains provider-, identity- and range-gated. `releaseReady=true` here authorizes **publication of an experimental prerelease only**, not claiming completed hardware acceptance.
- GitHub Releases exposes the four immutable alpha.59 assets at tag SHA `94133c0d4c4b4cf8eeaa45a875800923bdf131c5`. Physical X9 fan behavior remains unverified; this was an experimental release.

## Alpha.60 published experimental stabilization — October 4, 2026

- Tracking: issue #113 remains open for real-device acceptance. PR #116 was squash-merged to `main` at `bda4894040d5b658201613df954430bb27733e36` after frozen source head `6a5958a76a658f061362d609769a470b3e1811d3` passed CI/Package; the feature branch was deleted. Published source version is `v0.1.0-alpha.60`. This work remains **UNVERIFIED on physical X9** until the owner runs real recovery/AC/DC/resume/fan-noise checks.
- Cooling: identify non-ThinkControl Lenovo full-speed owner explicitly; stop automatic persisted Quiet/Balanced retries for that permanent conflict; offer a deliberate verified-feature `Return to Lenovo Auto` recovery action. Never clear another utility's live ownership silently or re-enable physically rejected per-fan/EC writers.
- Modes: surface the Windows power-overlay failure reason, avoid rollback of untouched facets and silent auto-handoffs, keep one dropdown as selection, save without auto-activating, restore pre-automation manual mode, apply 5-second context dwell, resolve overlaps by user priority plus deterministic trigger-type specificity, support Any/All and a small locally saved Wi-Fi suggestion list.
- UI: one shared page-header style and order, Touchpad switch beside Touchpad function, consolidated flat Settings rows with advanced diagnostics/support/reset behind disclosure, restrained copy.
- Keyboard: release scoped Lenovo tposd window hiding when an experimental effect ends so subsequent Fn+Space feedback remains visible.
- [x] interim source head `036780bc2a4c1f85f423fe8cfefce5ce22fa3205` passed CI `37230128783` and Package `37230128739`; preceding compilation error in the Touchpad field was corrected.
- [x] alpha.60 implementation head `de90d52eab1f6758df30811497ee0bec6ab1b17a` passed CI `37232421497` and Package `37232421493` (Windows build, source tests, shell lifecycle, installer, IPC, oldest-supported updater).
- [x] inspected actual dark/light WPF snapshots in normal/minimum layouts: Modes list/editor, Settings, Fans external-owner/recovery and other affected surfaces; exact-head artifact `11314471676` includes the full width/theme matrix.
- [x] frozen `releaseReady=true` PR head `6a5958a76a658f061362d609769a470b3e1811d3` passed CI `37232679413` and Package `37232679271` before guarded squash merge.
- [x] published immutable `v0.1.0-alpha.60` matches the PR merge and exactly four nonempty expected public assets, with GitHub SHA-256 digests recorded above. Production installer is accessible through the normal release/updater channel.
- [x] merged feature branch removed; only `main` remains.
- [ ] physically test X9 explicit Auto recovery, Quiet/Balanced/Max on AC/DC after reboot and wake, and verify other utility ownership semantics
- [ ] physically test automatic school-Wi-Fi entry/exit, overlapping conditions, manual selection and keyboard OSD after disabling experimental effects
- [ ] physical-device acceptance remains separate from experimental prerelease publication. Release notes must disclose it; never convert hosted success into a physical-fan-fix claim.

## Alpha.58 published release — fan capability truth, Modes and Battery clarity

Completion evidence:

- candidate runtime/UI head `997e2f56eda9a934e0f11c675d3df472038c0a10`
- candidate CI `37017069892` / #2220: success
- candidate Package `37017069512` / #1903: success
- candidate visual artifact `11230297804`, digest `sha256:700064a7d0041bacfab7f0fac2aad088782df8608ac2b526c2b5f5e40b9b4cf9`
- direct user review replaced the easy-to-misread live current→target Battery wear sentence with a stable 0%→selected-limit comparison while preserving incremental start→end wear internally
- 90% preservation screenshots were re-inspected after the change; 85%/90% threshold labels do not collide in dark or light
- frozen head `2198084013e706a82933c6ffdf9b55c0d042d2d7` passed CI #2222 and Package #1905
- PR #109 merged with exact-head guard; immutable tag/release points to the merge commit
- release/promotion/post-merge CI/branch hygiene all completed successfully
- no PR discussion/review backlog remained at merge

Published scope:

- copy `Capabilities.FanControlKind` into canonical app state on the normal runtime refresh path and clear it explicitly when service state is unavailable;
- remove client-side X9/DriverStatus string inference for firmware-policy fan ownership; the service capability snapshot is authoritative;
- keep Quiet / Balanced / Max available when the service advertises firmware-policy control, while custom curves/manual percentages remain direct-writer-only;
- seed Focus, Battery saver and Performance once as ordinary editable saved modes; retain optional templates only under New mode;
- simplify the Modes editor/list and remove repetitive admin-style separator/remove rows;
- replace generic Windows-settings dropdowns with direct contextual links and keep page Defaults as a quiet direct action;
- present Battery Preservation by 80 / 85 / 90 / 95% charge limit, with explicit resume threshold and a stable 0%→limit modeled-wear comparison;
- preserve existing valid Lenovo custom battery threshold pairs without silently rewriting them.

Published gate:

- [x] exact-head build/test/package gates
- [x] dark/light + minimum/normal/wide visual QA inspected for affected surfaces
- [x] frozen-head CI + Package
- [x] squash merge
- [x] immutable tag/release
- [x] exactly four public assets + checksum verification
- [x] post-merge main CI
- [x] merged branch cleanup
- [ ] physical X9 fan/battery confirmation remains separate evidence

## Alpha.57 published release — Fans composition and design handoff

Published implementation state:

- canonical PR: #108
- immutable tag/release SHA: `c8dc44a2ddc36590e769852baeba5d87807b08a8`
- reviewed frozen branch head: `4c14ea436abddd685ca247071faacab35b5157b6`
- frozen-head CI `36996062771` / #2204: success
- frozen-head Package ThinkControl `36996062763` / #1888: success
- promotion and immutable release completed successfully
- later live feedback is tracked in alpha.58 instead of mutating the immutable release

## Alpha.56 published release — fan protocol, Mode selection and updater stabilization

Active implementation state:

- branch: `alpha56-fan-modes-updater`
- source target: `v0.1.0-alpha.56`
- immutable/public baseline: `v0.1.0-alpha.55`
- canonical PR: **#106 — Prepare alpha.56 fan protocol, Modes and updater stabilization**
- candidate evidence head: `9c714e9ab956d904af6080eb53602532e3d46ac1`
- candidate CI `36777932595` / #2194: success — hygiene, build, tests, Compact↔Advanced shell smoke and WPF visual QA
- candidate Package ThinkControl `36777932472` / #1882: success — payload, installer, deep IPC reliability and oldest-supported upgrade compatibility
- candidate visual artifact `11126641312`, digest `sha256:abee8bf4eca8d9dccd29cc4cc87e7528fb40c32bf1db3e161277747c973c9fde`
- Modes starter/list interaction, Home Keyboard alignment, Fans firmware fallback and Updates inspected on the exact candidate gallery; direct row selection no longer exposes a separate Select/Activate action
- no open PR review threads/comments on the candidate head
- `version.json.releaseReady=true`; this commit creates the frozen release candidate

Scope:

- accept the exact-X9 LITSSvc clean-close/no-legacy-reply policy variant after a complete allowlisted UInt32 write;
- keep partial replies, access failures and true timeouts fail-closed;
- prevent the baseline policy command from blocking the requested Quiet/Balanced/Max command only because a newer LITSSvc omits the legacy reply;
- replace separate Mode Activate/Use buttons with direct row selection;
- expose Focus, Battery saver and Performance starter presets; Focus disables ThinkControl Touchpad gestures/edge gestures;
- align Home Keyboard segmented controls with the neighboring Display control;
- remove the permanent startup `Checking automatically…` seed state;
- bound release checks to 20 seconds and guarantee every automatic check exits the Checking state.

Current gate:

- [x] alpha.56 isolated from immutable alpha.55
- [x] clean-close LITSSvc compatibility implemented only on the reviewed X9 policy path
- [x] direct Mode row selection and three starter presets implemented
- [x] Home Keyboard control alignment corrected
- [x] updater idle/check timeout state corrected
- [x] open canonical PR and run exact-head CI + Package
- [x] inspect Modes/Home/Fans/Updates screenshots in dark/light and minimum/normal widths
- [x] resolve review backlog on exact candidate head
- [x] freeze `releaseReady=true`
- [x] frozen-head CI + Package
- [x] squash merge, immutable promotion and public asset verification
- [ ] physical X9 confirmation: Quiet/Balanced/Max now reach and change Lenovo policy instead of failing on missing legacy reply

## Alpha.55 published release — context Modes and X9 fan ownership

Active implementation state:

- branch: `alpha55-context-modes`
- source target: `v0.1.0-alpha.55`
- immutable/public baseline: `v0.1.0-alpha.54`
- canonical PR: **#105 — Prepare alpha.55 context Modes and X9 fan control recovery**
- candidate evidence head: `71610a14a2d381a305e9fae58f2505455dfc82e3`
- candidate CI `36772955585` / #2189: success — hygiene, build, tests, Compact↔Advanced shell smoke and WPF visual QA
- candidate Package ThinkControl `36772955889` / #1878: success — payload, installer, deep IPC reliability and oldest-supported upgrade compatibility
- candidate visual artifact `11123664179`, digest `sha256:ef21320bdd8147882d06271f8c773c465e3fd28ed42a56d295ece9508579fe9a`
- Modes context list/editor inspected in dark/light; refresh selection stays visible and automation rows fit the normal composition
- verified-X9 fallback inspected in dark/light with `FanControl=false`, `FanControlKind=None`, `CoolingProfile=Quiet`; built-in selector remains enabled while direct curve editing remains unavailable
- no open PR review threads/comments on the candidate head
- `version.json.releaseReady=true`; this commit creates the frozen release candidate

Scope:

- replace visible Normal / Gesture lock / Silent built-ins with **No mode + user modes**;
- allow sparse temporary ownership of Performance, Cooling, Refresh rate, Audio Safety, Touchpad gestures and non-experimental Keyboard light;
- add optional automatic triggers for Wi-Fi, running app/process, power source, battery threshold and local schedule;
- keep only one active mode; manual selection suppresses automation until context changes;
- keep active mode/baselines session-only while definitions persist;
- move Modes below the direct laptop-control pages in Advanced navigation;
- keep verified-X9 Lenovo Auto / Quiet / Balanced / Max firmware profiles selectable through transient provider/capability misses;
- retain direct custom curves/manual percentages behind the existing physically accepted writer gate;
- retry transient LITSSvc policy-pipe acquisition within one bounded user action instead of immediately falling back to Auto.

Current gate:

- [x] alpha.55 isolated from immutable alpha.54
- [x] expanded sparse Mode data model remains backward-compatible with alpha.54 custom definitions
- [x] transient Performance / Cooling / Refresh ownership paths implemented without rewriting ordinary preferences
- [x] user-session trigger engine implemented outside the privileged service
- [x] manual-wins-until-context-change conflict rule implemented
- [x] Modes editor redesigned around Settings + Turn on automatically
- [x] Modes moved below Touchpad in Advanced navigation
- [x] verified-X9 firmware profile fallback added to Home / Compact / Fans / cooling orchestration
- [x] bounded Lenovo LITSSvc reacquire/retry added without reauthorizing rejected direct fan writers
- [x] open canonical PR and run exact-head CI + Package
- [x] inspect Modes list/editor/trigger states and verified-X9 fan fallback in dark/light
- [x] resolve review backlog on exact candidate head
- [x] freeze `releaseReady=true`
- [ ] frozen-head CI + Package
- [ ] squash merge, immutable promotion and public asset verification
- [ ] real X9 smoke: Quiet / Balanced / Max profile changes physically change behavior and do not snap back to Auto

## Alpha.54 published release — Mode feedback and firmware fan recovery

Active implementation state:

- branch: `alpha54-mode-fan-recovery`
- source target: `v0.1.0-alpha.54`
- immutable/public baseline: `v0.1.0-alpha.53`
- canonical PR: **#104 — Prepare alpha.54 Mode feedback and firmware fan recovery**
- candidate evidence head: `c2529ca856a1ad07ba0401b8b57cb8ab2c6c06fe`
- candidate CI `36323404715` / #2183: success — hygiene, build, tests, Compact↔Advanced shell smoke and WPF visual QA
- candidate Package ThinkControl `36323404729` / #1873: success — payload, installer, deep IPC reliability and oldest-supported upgrade compatibility
- candidate visual artifact `10932848239`, digest `sha256:cd76459548282828ab9792da86b8b1690a79e1e7c05a593b3ee41102fc6fe0ec`
- Modes editor inspected in dark/light; native menu gutter replaced by the shared app ContextMenu template
- firmware-policy recovery inspected in dark/light: built-in profile selector remains enabled while direct curves remain unavailable without a direct writer
- no open PR review threads/comments on the candidate head
- `version.json.releaseReady=true`; this commit creates the frozen release candidate

Scope:

- remove the native WPF ContextMenu gutter that produced the white column in the custom-mode editor;
- compact the custom-mode Name field and remove empty filler copy;
- publish a pending Mode target immediately as `Applying…` while preserving confirmed Active state until all subsystem writes succeed;
- keep Home and Compact selectors synchronized to the pending target instead of snapping back to the previous mode during slow transitions;
- preserve verified X9 Auto / Quiet / Balanced / Max firmware-policy control when direct fan-provider discovery or telemetry is unavailable;
- route built-in X9 cooling policies independently of the rejected direct fan writer;
- keep custom curves/manual percentages gated behind a physically accepted direct writer.

Current gate:

- [x] alpha.54 isolated from immutable alpha.53
- [x] shared ContextMenu template removes the Windows-native gutter
- [x] Mode coordinator exposes pending/visible state before slow subsystem writes
- [x] Home/Compact follow pending mode state
- [x] firmware-policy fan capability remains advertised during provider discovery
- [x] built-in cooling profiles no longer depend on a successful direct-provider status read
- [x] rejected X9 direct writer remains blocked
- [x] open canonical PR and run exact-head CI + Package
- [x] inspect Modes editor/list plus fan provider-unavailable screenshots in dark/light
- [x] resolve review backlog on exact candidate head
- [x] freeze `releaseReady=true`
- [ ] frozen-head CI + Package
- [ ] squash merge, immutable promotion and public asset verification

## Alpha.53 published release — Modes and shared Advanced layout

Active implementation state:

- branch: `alpha53-modes-header`
- source target: `v0.1.0-alpha.53`
- immutable/public baseline: `v0.1.0-alpha.52`
- canonical PR: **#103 — Prepare ThinkControl 0.1.0-alpha.53 Modes and shared Advanced layout**
- candidate evidence head: `17d517936fb4659f72e47143c4efe8065f3bffbd`
- candidate CI `36250998205` / #2178: success — hygiene, build, 251 tests, Compact↔Advanced shell smoke and WPF visual QA
- candidate Package ThinkControl `36250998063` / #1869: success — payload, installer, deep IPC reliability and oldest-supported upgrade compatibility
- candidate visual artifact `10909096643`, digest `sha256:eec3ea47b19c60098853972e3fc905bb4da2cfb4b1f26655d715149ad388fb33`
- baseline Advanced matrix inspected at minimum / normal / wide in dark and light, plus Modes editor snapshots
- no open PR review threads/comments at the candidate evidence head
- `version.json.releaseReady=true`; this commit creates the frozen release candidate

Scope:

- promote Modes to a first-class Advanced destination without turning it into a generic settings preset engine;
- retain Normal / Gesture lock / Silent as deliberately narrow built-ins;
- support sparse custom modes for Audio Safety, Touchpad gesture enablement and non-experimental keyboard-light state only;
- keep custom definitions persistent while active mode ownership/baselines remain session-only;
- make direct subsystem changes release only that mode facet and mark the active mode Modified;
- keep cooling, Windows performance, Battery Preservation, microphone, display policy and experimental keyboard effects independent;
- replace Home/Compact Audio Safety duplication with the active Mode selector while leaving the Audio subsystem control available;
- replace page-local Advanced title rows with one `AdvancedPageHeader` primitive and one shared content rail;
- remove requirements-as-copy and unnecessary permanent provider/implementation prose from routine UI;
- expand deterministic WPF QA so every Advanced destination renders at minimum / normal / wide in dark and light.

Current gate:

- [x] implementation is isolated from immutable alpha.52 on `alpha53-modes-header`
- [x] power-state naming is disambiguated before introducing product-level Modes
- [x] sparse mode model and serialized coordinator implemented
- [x] transient Audio Safety, Touchpad and Keyboard apply/rollback paths implemented
- [x] Touchpad temporary On/Off ownership is isolated from persisted gesture configuration
- [x] direct manual changes release only their owned facet and preserve the active mode as Modified
- [x] Home and Compact expose Mode; dedicated Modes page/custom editor implemented
- [x] Advanced pages use the shared header primitive; runtime Home/Battery header reconstruction removed
- [x] source regressions cover mode boundaries, transient ownership, reset behavior and shared header usage
- [x] full dark/light × minimum/normal/wide Advanced visual matrix is required by CI
- [x] alpha.53 product/architecture/design/testing documentation updated
- [x] open canonical PR and run exact-head CI + Package
- [x] inspect every baseline Advanced screenshot and Modes editor snapshots at full resolution
- [x] resolve review backlog on the exact candidate head
- [x] freeze `releaseReady=true` only after implementation-head gates pass
- [ ] frozen-head CI + Package
- [ ] expected-head squash merge and immutable alpha.53 promotion
- [ ] verify public Setup/Payload/checksum/overview assets and digests

## Alpha.52 published release — interaction and X9 safety

Alpha.52 is immutable at `52380cbd0b3d508976f63420a4bd19bfa302650b`. It shipped the direct-feeling Audio sliders, Battery Preservation target ETA, one-button temporary fan test, Home Sensors navigation, shared-switch Touchpad reverse-close, the first title/action rail alignment pass and the narrowed X9 EC writer boundary. Exact-head hosted build/test/ShellSmoke/WPF and Package gates passed before promotion. The published release contains exactly the four managed assets recorded in Current release state.

Post-release physical follow-up remains separate evidence:

- [ ] Audio output/microphone slider feel and endpoint convergence on the reference X9
- [ ] preservation switch, stop-target ETA and physical threshold behavior
- [ ] built-in Auto / Quiet / Balanced / Max behavior remains correct after direct-EC capability removal

## Alpha.51 published release — Compact dropdown dismiss state

Alpha.51 is a narrow user-session UI hotfix on immutable alpha.50. No hardware provider, Windows service, updater, installer, fan, battery, audio-safety or keyboard-effect contract changes.

Scope:

- fix the Compact-only stale ComboBox highlight reported after a dropdown is dismissed by clicking elsewhere;
- keep the shared `TcComboBox` template unchanged so Advanced and all other selectors retain one visual contract;
- on Compact only, release stale popup mouse capture after `DropDownClosed`, clear lingering ComboBox keyboard focus and resynchronize WPF pointer hover state;
- apply the same dismiss path to Performance, Fan mode, Refresh rate, Keyboard and Audio safety;
- add source regression coverage so future Compact selectors cannot silently bypass the cleanup path.

Release gate:

- [x] alpha.51 isolated from immutable alpha.50
- [x] Compact-only dismiss cleanup implemented without changing the shared ComboBox template
- [x] all five Compact selectors share the same `DropDownClosed` path
- [x] source regression covers mouse-capture release, keyboard-focus clear and pointer resynchronization
- [x] exact implementation-head CI + Package green · head `500c7649a5fdd1048a323fb17a6543bfe2480147` · CI `35942349214` / #2120 · Package `35942349175` / #1815
- [x] Compact dark/light visual artifact reviewed for unchanged layout/styling · artifact `10785183982`, digest `sha256:a6c8d78b98c7c035e4a2f46e92647c308fad360d2fbe82bb87d91b4f0030caac`
- [x] release-ready metadata freeze
- [x] frozen-head CI + Package green · CI `35942610435` / #2121 · Package `35942610387` / #1816
- [x] expected-head merge and immutable alpha.51 GitHub release/update verification · merge/tag `46bd09bb8556966b49413f7d7115d581c1b4c107` · release/promotion runs `35942852253` / `35942842366`

## Alpha.50 published release — runtime reliability

Alpha.50 is a Windows-generic audio-safety and Battery Preservation explanation follow-up on immutable alpha.49. It does **not** change Lenovo threshold writes, fan providers or any privileged hardware command surface.

Scope:

- make Silent authoritative against physical Windows volume keys by swallowing only `VK_VOLUME_MUTE / DOWN / UP` with a session-scoped low-level keyboard hook;
- retain CoreAudio endpoint-volume notifications for app/SndVol changes while ensuring callbacks arriving during an existing re-mute pass are not lost;
- subscribe to default-device notifications so Silent follows a changed render endpoint immediately instead of relying on the normal multi-second status cadence;
- allow only a short bounded retry burst when a newly announced default endpoint is temporarily not ready; do not add a permanent polling timer;
- preserve per-endpoint mute ownership/restore and microphone independence;
- replace the alpha.49 three-zone Battery Preservation diagram with one state-reactive current-level fill and two aligned threshold markers;
- calculate an AccuBattery-style comparative wear-cycle estimate from the current battery level to the selected stop threshold, with full 0→100% normalized to a 1.00 baseline and clearly labeled as a generic Li-ion model rather than measured pack wear;
- document the Windows CoreAudio/keyboard-hook contracts and lithium-ion aging evidence used for these choices;
- prevent fan Auto from bouncing through stale Max/Quiet telemetry by keeping the user's pending intent authoritative until the serialized write finishes;
- prioritize release of an active firmware/full-speed cooling override before wider stale direct-provider Auto recovery;
- suppress only Lenovo `tposd.exe` backlight windows created during automatic experimental-effect writes;
- repair keyboard Audio mode for WASAPI `WAVE_FORMAT_EXTENSIBLE` output, adaptive loopback levels and bounded capture restart.

Release gate:

- [x] alpha.50 isolated from immutable alpha.49
- [x] Silent standard volume-key guard implemented on the WPF dispatcher thread
- [x] endpoint-volume enforcement uses a pending pass so repeated/held-key races cannot be coalesced away
- [x] default render endpoint changes are event-driven through `IMMNotificationClient`
- [x] transient endpoint replacement uses only bounded 40/120/350 ms retry delays
- [x] existing per-endpoint mute restore and microphone independence preserved
- [x] comparative Battery Preservation wear-cycle model implemented in Core with a 1.00 full-charge baseline
- [x] wear estimate remains explicitly comparative and documents generic SOC/voltage assumptions instead of claiming measured pack wear
- [x] preservation visual simplified to one reactive fill, two aligned threshold markers and no permanent color zones/icons
- [x] same-base public release is ordered above `alpha.N-dev.BUILD` so dev testers still receive the canonical update
- [x] alpha.50 research note and regression/unit tests added
- [x] fan pending-intent masking, short post-success telemetry-confirmation lease and firmware-first Auto release implemented
- [x] automatic keyboard effect OSD suppression is scoped to new `tposd.exe` windows during effect-write bursts
- [x] keyboard Audio mode handles extensible float/PCM loopback, adaptive level context and bounded unexpected-stop restart
- [x] exact implementation-head CI + Package green · head `d4e1de924e02b130f80144b1e3325c3548882202` · CI `35930413152` / #2115 · Package `35930412970` / #1812
- [x] full-resolution Silent + Battery Preservation + fan Auto + experimental keyboard-effect visual review · artifact `10780372906`, digest `sha256:492f5585e592be233cdba0534a96e9257c849b9c3a06ec70eaf4bd27793dd6ab`
- [x] exact implementation-head development installer/payload checksums re-verified after download · Package artifact `10780387836`, digest `sha256:6537d00a9aef2df99355868d4ddfc046648ca20a7d110e8cc423168317532296`
- [x] user-reported physical baseline recorded honestly: settled Silent stayed silent and the Fans-page Auto path worked; the activation-edge and Home-switch bounce failures were reproduced by the user's prior candidate and directly drive the current fixes
- [x] post-report Silent activation fix is source-regressed: only key-down/repeat is swallowed, key-up always passes, the hook is installed before the final mute write, and CoreAudio remains authoritative
- [x] post-report Home fan fix is source-regressed and visually reviewed: in-flight busy ownership plus a four-second expected-state lease prevents crossed stale telemetry from repainting Max/Quiet over Auto
- [x] keyboard OSD suppression and Audio response remain explicitly Experimental; alpha.50 is the physical feedback vehicle rather than pretending hosted CI proves Lenovo popup/audio behavior
- [x] release-ready metadata freeze
- [x] frozen-head CI + Package green · CI `35930818160` / #2116 · Package `35930818064` / #1813
- [x] expected-head merge and immutable alpha.50 GitHub release/update verification · merge/tag `a56df08ec756261472fc4362e7473e9482ffd7fe` · release/promotion runs `35931073957` / `35931058527`

## Alpha.49 published release — Battery Preservation visual semantics

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
- [x] exact implementation-head CI + Package green · CI `35849670151` / #2034 · Package `35849670147` / #1733
- [x] dark/light preservation snapshots manually inspected at full resolution · artifact `10745092844`, digest `sha256:dc903e31387248ffe901ae16fe8c3c2296f9ccb58c6d88bf13cc0e63982e7dd0`
- [x] release-ready metadata freeze
- [x] frozen-head CI + Package green · CI #2036 / Package #1735
- [x] expected-head merge and immutable alpha.49 GitHub release verification

## Alpha.48 published release — UX clarity and live state

Alpha.48 is the immutable UX-clarity release on alpha.47. It does **not** add a new low-level hardware command surface.

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
- [x] frozen-head CI + Package green · CI #2022 / Package #1722
- [x] expected-head merge, immutable alpha.48 release and public checksum verification

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

Local candidate delivery: draft PR #121 contains the recovery changes. GitHub CI 37286007648 and Package 37286007757 passed at source 7785e99. Development version 0.1.0-alpha.60-dev.2026100502 was upgraded in place through the checksum-verified local payload; installed UI/service versions match that commit and the service is running. All 13 pre-upgrade user files remain present. Settings differences were limited to attention acknowledgement/prompt bookkeeping; startup and diagnostics consent were preserved. New battery feedback puts status and ETA below the percentage on one left rail, with wrapping; dark/light minimum long-text and normal charging renders were inspected. Lower fan control and broader mode acceptance remain open.

Installed Auto UI acceptance: the actual Fans recovery button was clicked in dev.2026100502. Its result changed to 'Last recovery: Lenovo Auto confirmed.' and survived a subsequent status refresh; unsupported profiles stayed disabled. Routine Fans text now summarizes availability, with technical provider detail in its tooltip.

Auto action correction: availability is separate from need. The recovery button now disappears after confirmed Auto and returns on a later non-Auto observation; routine polling preserves the last action result. Real-dispatcher smoke covers both transitions. This UI behavior does not add lower-speed control or repair an unsupported provider.

Modes/rules implementation checkpoint: full solution build has zero warnings/errors; 277 existing tests pass. The real WPF dispatcher exercises manual mode application, five-second entry/exit dwell, brief Wi-Fi loss, manual selection during dwell, overlapping rules for the same mode, preservation of manually released facets, return to the previous Modified session, unsupported-facet failure/backoff, disabled legacy migration, multiple rules per mode and JSON round-trip. These are controlled-context tests through the production coordinator, not a claim of physical school-Wi-Fi validation. The gallery adds two native rule-editor renders (161 total); affected System/Modes/rule layouts are reviewed before installed acceptance. Rendering no longer writes to the user's preferences.
