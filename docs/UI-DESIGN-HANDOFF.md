# ThinkControl UI design handoff

## Runtime follow-up - alpha.67

The subsequent owner request targets WPF: Mode replaces Keyboard in Compact defaults; Keyboard/Automation remain configurable; Compact is 420x501. Direct mode-off, actual-tile drag feedback, editor-local Save/Cancel, 20-unit Fluent remove glyphs and selected Custom preservation use existing resources. Advanced battery flow has On/Off/System because this desktop disables Windows animation; Compact remains static. These runtime compositions have not been synchronized to earlier Figma frames; alpha.66 Figma evidence below is historical, not alpha.67 parity proof.

## Targeted owner corrections — alpha.66

Only the designated canonical Figma instances changed: Mode card 6:124 now uses its existing Fluent icon, simple Mode title and shared Automation switch 325:6497; normal status prose is removed. Manual selection pauses triggering and displays Off; explicit Off stays paused until Resume without changing saved rule enablement. Rule editor switch 318:7032 moves to the title rail; the other fields remain. Start with Windows 11:578 aligns with its label and the bounded preference rail. Automation rows 318:6277 / 318:6286 / 318:6295 replace status labels with variable-bound dots 325:6500 / 325:6502 / 325:6504. Idle is Muted, matching/evaluating is subtly pulsing Accent, confirmed active is Success, failure is Error. Motion is visibility-gated and respects Windows reduced motion. A screenshot was inspected after each Figma correction. No other Figma cards or system controls changed in this follow-up.

## WPF consistency implementation — published alpha.66 (2026-10-07)

The owner subsequently authorized implementation. The active local branch now implements the selected direction with the existing IBM Plex/Fluent assets and shared WPF section, segmented, switch, inline-action and disclosure styles. This implementation is published as alpha.66; the local installed build remains alpha.65 and installed acceptance is separate.

- Overview keeps Mode in the lower quick-control row, separate from Keyboard. Its normal content is only the existing mode icon/title, picker and compact Automation switch; errors remain local. Normal-size renders keep the full row visible; minimum-size content scrolls.
- Saved modes has explicit Apply actions and **Use regular settings**, including repeated No mode. The coordinator restores owned facets before manual pause. Resume still re-evaluates enabled rules with the existing dwell/context-change policy; saved rules are never silently disabled.
- Rule switches commit immediately through the existing settings store and automation owner. Failed persistence restores the displayed switch and shows local feedback. Rule/condition editor switches stay draft until Save; a failed Save retains the draft. Disabled conditions retain their values; no enabled conditions cannot match. Rule enabled, Match/Priority and condition/value groups use readable bounded rails.
- Confirmed ownership, requested transition, failed target and incomplete recovery have separate state. Failures appear at the requested mode/rule with Details and Retry. Incomplete rollback suppresses success indicators; a subsequent sparse/no-op activation cannot clear uncertainty about untouched facets. This is conservative provider acknowledgement, not a new hardware-readback guarantee.
- Confirmed automatic activation reuses the existing passive notification owner for one short mode/rule notice. Same-mode rule arbitration does not repeat it; it cannot replace a visible actionable warning.
- Modes ↔ Automation preserves both existing editor drafts. Leaving the Modes primary section resets drafts; primary scroll/disclosure reset remains. No second coordinator, polling loop or settings owner was introduced.
- Theme and App icon opens share segmented styles, label roles and aligned control rails. Device support keeps label/value groups together. Recorded sessions remain below integrated Battery details, with local time, signed/unavailable Wh, Fluent disclosure, exact-date/offset tooltips and separate History settings. Wear calculations remain optional illustrative detail, distinct from measured firmware cycles.

Validation and publication status live in RELEASE_READINESS.md. Relevant Figma node IDs and interaction contracts below remain the design references. Pixel-perfect live parity, physical input/DPI, installed animation, curve hover and SolidWorks fan acceptance remain **UNVERIFIED**.

## Post-alpha.65 Figma consistency reconciliation (2026-10-07)

This section records the preceding **design-only** pass. The WPF candidate status above supersedes its implementation-pending notes; installed acceptance is still separate.

### Follow-up: explicit No mode, trigger switches and failure locality

- Canonical Automation `11:256` now has aligned two-line rule identities/outcomes, a semantic status dot (replacing the preceding Active/Waiting/Off labels), priority, an enabled switch and Edit. Rule rows `318:6277`, `318:6286`, `318:6295` use shared switch instances `318:6284`, `318:6293`, `318:6302` from `245:4643`; IBM Plex text styles and semantic Text/Muted/Success bindings were inspected. Accent on a switch means enabled configuration, never successful mode application.
- Rule editor `29:835` uses the same background composition and separate **condition enabled** `318:7035` and **rule enabled** `318:7032` instances. The condition switch has a 56 × 41 logical-unit wrapper. Off preserves the condition and its values; editor changes take effect with Save. The rule-list switch commits immediately, requests normal arbitration, and needs local pending/failure feedback. Any/All evaluates only enabled conditions; zero enabled conditions never matches. Disabling the winning rule may expose another winner, so it must not promise No mode.
- Saved-mode references `11:85`, `130:2940`, `130:3055`, `130:3170` offer **Use regular settings** directly on No mode. Successful restoration is demonstrated by `318:7077` (**Modes / No mode · automation paused**). It keeps Resume automation visible and has no success-green dot. This is one additional state case inside the existing state section, not another canonical screen.
- **Use regular settings** must be a command even when the selector already contains No mode: a SelectionChanged event alone cannot express that intent. Run the coordinator restoration path, preserve manual facet overrides, then pause automation on success using the existing winning-context contract. Do not silently disable stored rules. Show which context can end the pause. Resume explicitly re-evaluates enabled rules, preserving the five-second entry/exit stability period; it does not promise a particular mode. If no rule wins, regular settings remain.
- Failed Performance activation `130:3170` retains a separate confirmed Battery saver status and puts the error, Details and Retry inside Performance (`318:6310`). This case assumes confirmed recovery. **Incomplete recovery is different:** shared Error `253:6343` now says Settings need checking; never show a green confirmed mode solely because `ActiveModeId` retained its prior value. Error details must expose the failing facet and recovery result on demand. The current coordinator's error string distinguishes requested rollback from incomplete recovery; implementation needs explicit confirmation evidence, not string cosmetics.
- Expanded sessions `66:1133` have aligned date/time, direction/percentage, duration and signed Wh plus 40 × 40 disclosure targets (`319:7065`, `319:7068`, `319:7071`). Opening a day/session is separate from History settings. Relative dates need the exact local date/time on demand, including zone/offset where repeated daylight-saving times are ambiguous. An unavailable energy value is unavailable, never zero. Native disclosure and graph-range behavior remain UNVERIFIED.
- Battery wear text was reconciled in affected dark and Light references: an illustrative model is not measured firmware cycles or proven battery-life gain. Keep actual firmware cycle history distinct. Do not restore the old green −89% summary as an unqualified measured benefit; model assumptions and illustrative calculation belong in optional detail.
- Audio `10:574` no longer overlaps the Dolby management status with Open Dolby Access. Updates `12:103` has a visible adjacent Fluent check, without covering the version or claiming package signatures that the checksum contract does not prove.

Reviewed in this run: representative Automation and failed activation first, then affected editors, regular-settings pause, canonical Overview/Power/Battery/Display/Keyboard/Touchpad/Audio/System/Updates/Diagnostics, Compact/paused Compact and passive/actionable notifications. Changed mode compositions were also rendered under Light. This is bounded Figma visual/source review, not complete native interaction, accessibility, DPI or hardware acceptance. Switch prototype reactions were read back; they demonstrate presentation only, not arbitration or persistence. Touchpad geometry was preserved, not re-certified.

- **Overview `6:2`:** Mode stays in the lower quick-control row at `6:124`; Keyboard light remains a separate sibling card. The normal state is visually neutral. A confirmed automatic activation may also produce one brief passive ThinkControl notification; the existing notification family `38:814` / passive `38:802` is the visual owner, with the current example in `198:5007`. Do not restore the large permanent alpha.65 active-mode card above the quick controls.
- **Mode state:** `253:6354` is the shared compact runtime-status family for confirmed active, manual-pause, uncertain, error and restoring states. Green Success is reserved for a mode/rule that the coordinator has actually confirmed. A dropdown selection, requested facet or saved preference is not confirmation. A failed facet stays attached to the failed action while the prior confirmed mode remains visually active; examples are `130:2940`, `130:3055` and `130:3170`.
- **Modes / Automation:** canonical `11:85` and `11:256` now share the same current-mode/status presentation. Active rows use a quiet surface plus a small semantic indicator instead of repeated red ACTIVE text. The mode and rule editors `29:548` and `29:835` were reconciled to these same backgrounds.
- **Editor continuity:** switching between the Saved modes and Automation context tabs must preserve the current editor and unsaved transient values. Navigating to another primary destination and later returning to Modes may reset the transient editor and restore the normal Saved modes entry state. The current alpha.65 WPF reset path does not yet satisfy this distinction and remains an implementation item.
- **Battery `9:205`:** Battery details is integrated in-page. Recorded sessions live under Battery details; `66:1133` is the expanded-session reference. History settings is a separate storage/retention action, and `66:1311` remains the destructive reset confirmation. The obsolete separate details inspector was reconciled into `65:1089` rather than retained as a competing layout.
- **Battery motion:** the Figma battery gauge shows the intended subtle diagonal flow for charging; runtime source already defines opposite-direction discharge flow and idle fade. This is a static appearance reference only. Installed charging/discharging motion remains **UNVERIFIED** after the owner reported no visible motion in alpha.65.
- **System General `11:422`:** App icon opens uses the same shared segmented-choice grammar as Theme, rather than a second button style. Device & support uses compact label-value groups instead of pushing values to the far right.
- **Updates `12:103`:** the current static reference is alpha.65 and uses the adjacent confirmed-check treatment instead of a distant green status badge.
- **Light QA:** affected Overview `34:1054` and Battery `67:1707` were rebuilt from the same updated canonical compositions under the Light variable mode rather than maintained as drifting copies.

The selected IBM Plex Sans / Fluent direction, semantic variables, shared page-header rail and existing capability/state owners remain unchanged. These corrections supersede older placement and red-active examples where they conflict.

Alpha.65 runtime flow: Overview's existing mode selector/status moves above quick controls. One presentation of active settings and manual/rule source is shared with Modes and Automation; applied rules and pending matches differ. Curve editing is directly in Cooling, and read-only hover shows temperature/target. Battery details and sessions are visible; History settings separately reveals retention/reset in view. App-icon opening preference sits with Theme in General preferences. These supersede earlier hidden disclosure placements. Capability gates and arbitration remain unchanged. Evidence and **UNVERIFIED** items live in RELEASE_READINESS.md; equivalent live Figma reconciliation is **UNVERIFIED**. Retain IBM Plex Sans/Fluent.

Alpha.64 runtime correction: Battery details precedes Recorded sessions; capacity and temperature use two columns, learned charging power and Windows usage are grouped below. Session kind columns share size; time/duration and percentage/energy use two wrapped lines. Minimum/normal/wide dark/light runtime evidence is recorded in RELEASE_READINESS.md. Equivalent live Figma reconciliation is **UNVERIFIED**; retain the selected IBM Plex Sans/Fluent foundation.

This is the existing durable design owner. Recover live Git state and read AGENTS.md, CHAT_STARTER.md, DESIGN.md and PRODUCT.md. Figma owns the selected visual target; the repository owns behavior, capabilities and verification. Do not recover requirements by replaying the old chat.

## Selected source and migration

- Approved file: https://www.figma.com/design/U3tJrxFyixV1ubC7ZTlSGM
- Pages: Advanced `0:1`; Compact/feedback/Light QA `2:2`; design system `2:3`.
- Selected typography: **IBM Plex Sans Regular and SemiBold**. Selected icon language: **Microsoft Fluent System Icons**, plus purposeful ThinkControl brand/gesture glyphs.
- The older Fans-only file `Dcl5mMTiWUYwMcwiB0oVdy` is superseded as a visual target. Its Segoe/Inter fallback decision does not apply to this direction.
- Alpha.65 source and the current native fixtures use IBM Plex/Fluent. The older installed alpha.61 Segoe/Material description is historical. Retain the packaged font assets/licenses and shared semantic icon adapter; do not mix systems per page. Actual installed font metrics and DPI/text scaling remain acceptance items.
- Keep the user/service privilege boundary, IPC, settings schemas, capabilities, session ownership, rollback and data. No flattened screenshot implementation or duplicate mock application.
- The current round refines the existing product family and records implementation-ready contracts. The later request to publish extends beyond this design checkpoint; implementation and release must satisfy the normal repository gates. Readiness evidence belongs in RELEASE_READINESS.md. Figma completion is not installed-app completion.

## Canonical screens

Latest owner corrections (2026-10-06): Overview has separate Mode and Keyboard light cards. Canonical Mode remains `6:124`; Keyboard light is `266:6134`, with preserved Auto choice `266:6155`. Light counterparts are `266:6851` and `266:6855`. This overrides the former combined card. Footer actions align with the main navigation icon/text rails. Status copy wraps instead of truncating ordinary battery information. Dark semantic Success/Warning/Error are now `#3ed486`, `#ffb545`, `#ff645c`, bound through the existing Figma variables and WPF theme owner. Charging/discharging flow is implemented in the shared BatteryGauge and tied to real state; installed motion remains UNVERIFIED and must not imply fabricated percentage movement.

Manual mode selection and rules share the production coordinator. Manual selection pauses automation until the winning context changes or Resume is requested; the selector does not constitute a second competing owner. Overview must show that pause explicitly. Modes uses the shared Saved modes/Automation tabs and has no additional duplicate Automation header link.

Latest navigation preference: keep Compact view in the footer with Notifications and Preferences; raise primary destinations 14px (first destination y=110 at the canonical size). Align every action's text/icon rail with the primary destinations. This supersedes the briefly explored placement beneath the wordmark.

Scrollbar override: square thumb corners and a 6px gap between content and the scrollbar, shared across scrollable surfaces. Native scroll interaction and orientation-specific minimum thumb sizes remain intact.

Preservation benefit (latest owner decision, 2026-10-07): show a minimal green **estimated charging-wear reduction**, comparing one generic 0–selected-limit charge against 0–100%. Example at 85%: approximately 89% lower modeled charging wear. Always show the compared ranges and label the result Estimated. This is an illustrative generic Li-ion model, not measured capacity loss, saved firmware cycles or a prediction of longer lifespan. Use BatteryPreservationImpactModel, round the displayed reduction to whole percentages, and keep its chemistry/temperature/voltage/time-at-high-charge limits in the tooltip. Primary cards omit top-up wear estimates. The 85% preset resumes below 80%; actual custom pairs must retain their real readback without selecting a misleading preset. Figma benefit: 9:358 / Light 67:1761. Methodology: https://accubattery.zendesk.com/hc/en-us/articles/210224725-Charging-research-and-methodology

Advanced canonical section `190:4791` contains the twelve reference screens. Reference size is1200×780; native minimum980×650 must reflow rather than scale the canvas.

| Screen | Frame | Existing behavioral owner |
|---|---|---|
| Overview | `6:2` | AdvancedWindow Home dashboard/quick controls |
| Power & cooling | `9:35` | Performance panel, Fans panel, App.Cooling, service |
| Battery | `9:205` | BatteryTelemetryPanel, preservation and history |
| Display & input: Display | `10:61` | Display panel |
| Display & input: Keyboard | `10:221` | Keyboard panel/effects/OSD owner |
| Display & input: Touchpad | `10:394` | TouchpadPanel and Core recognizer |
| Audio | `10:574` | Audio panel and AudioSafety coordinator |
| Modes: Saved presets | `11:85` | ModesPanel, sparse mode definitions |
| Modes: Automation | `11:256` | Automation rules and ModesCoordinator |
| System: General | `11:422` | System and general settings |
| System: Updates | `12:103` | Update service/presentation |
| System: Diagnostics | `12:220` | Diagnostics/recovery and sensor telemetry |

These six navigation groups replace presentation of thirteen old destinations; they do not delete those capabilities. Preferences, detailed sensors and feature-specific editors remain reachable. State examples are not separate application pages.

Canonical Compact composition: `12:345` on page2:2. Prototype entry `27:574` is its linked entry/example. Focus `12:407`, charging paused `12:469`, Light `34:1219` are state references, not independent layouts. Compact customization `23:385` and drag examples section `191:5338` describe swaps and unused-item assignment.

## Shared design system

Use live component properties and styles instead of copying primitives. Existing IDs are retained where possible.

| Canonical family | ID |
|---|---|
| Semantic colors, Dark/Light | `VariableCollectionId:2:4` |
| Button, three styles × seven interaction states | `241:4625` |
| Select, seven interaction states | `241:4675` |
| Navigation item, label and icon properties | `245:4618` |
| Switch, value and interaction state | `245:4643` |
| Page header | `245:4644` |
| Context tabs | `19:338` |
| Segmented choice | `30:639` |
| Inline slider | `13:192` |
| Mode included-settings editor | `183:4907` |
| Touchpad zone visualizer | `83:1438` |
| Touchpad bottom assignments | `140:3651` |
| Compact three-metric status | `55:1112` |
| Compact unused-item palette | `171:4249` |
| Notifications, actionable/passive | `38:814` |
| Automation runtime status | `253:6354` |

The reconciliation replaced more than800 manual controls with instances and bound more than2200 standalone texts to shared styles, including canonical component text. Exact counts are run evidence, not a permanent completion claim. Do not detach instances during implementation extraction. Purposeful fixed physical geometry is an exception to flexible layout.

Typography/rails are specified in DESIGN.md. The semantic Action background is distinct from Accent so small white action labels have adequate contrast. Faint Light was corrected to `#5b686e`. Disabled presentation dims the entire control, including sliders/thumbs. Preserve visible keyboard focus and selected state separately from hover. Loading blocks duplicate actions and keeps last confirmed hardware state; errors appear next to the rejected action with recovery, rather than turning missing telemetry into a permanent alarm.

## Secondary screens and state coverage

- Editors/capability section `191:5335`: Mode edit `29:548`, new Mode `29:699`, Rule `29:835`, new Rule `30:644`, thresholds `31:833`, cooling details `31:1067`.
- Cooling examples: calibrated editor `57:1182`, read-only library `57:1371`, calibrated discrete state `57:1470`, custom dropdown `58:878`, custom edit `58:1531`. These are capability/state examples; static values are not device evidence.
- Inbox `25:448`; notification and gesture feedback section `198:5007`.
- Battery history/active-mode examples `191:5336`: discharge `63:990`, health `63:1140`, details `65:1089`, sessions `66:1133`, reset confirmation `66:1311`, active modes `130:2940/3055/3170`.
- Touchpad QA section `191:5337` groups zone selection, edge assignments, corner launches, bottom actions and live/unavailable states. Preserve these linked tests.
- Light/contrast section `198:5008`: Overview `34:1054`, source-accurate Touchpad `103:2645`, Battery `67:1707`, Power `67:1814`, curve editor `67:1916`.
- Page menus `191:5339` are contextual-action references, not navigation destinations. Common Windows links stay direct; Defaults remains a quiet direct page action.
- Proven obsolete Light Touchpad `67:2002` was removed after checking all three pages for incoming prototype references. It lacked canonical geometry; `103:2645` remains.

Not every real state is drawn. Existing update checkmark/Up to date, download/install/cancel/failure, empty rules/modes, pending/rejected writes, unsupported settings, long names, disconnected telemetry and session overrides remain mandatory. Test them in the real app; never remove functionality because a reference screen omits it.

## Interaction and product contracts

**Navigation and shell:** preserve Windows caption, dragging, resize/Snap, focus, tray ownership and Compact↔Advanced lifecycle. Re-clicking a destination returns to its default list/subview, top scroll and collapsed disclosures consistently. Sidebar bottom fade appears only with remaining content and does not cover/block the scrollbar. Headers share one fixed title/action rail; narrow widths consolidate secondary actions.

**Compact customization:** the native editor now shares metric content and keeps the two persisted layout families in `compact-layout.json` schema 2, migrating the old status array. Live control previews share the actual selectors' items/selection and style; native drag acceptance remains UNVERIFIED. Drag directly in the preview to swap slots. Status and controls are separate rows, shown side-by-side in the editor at the same vertical level. Available lists contain only unused eligible items. Persist changes through the existing settings owner; cancellation, lost capture and a later drag must work. Never duplicate the same item into a second slot.

**Modes and Automation:** modes contain removable included settings only. New mode starts with Name and Add setting, no fixed full-settings form; saving does not activate. Supported facets and exclusions are defined in PRODUCT.md. Rules link to modes independently, use Any/All, highest priority then visible list order and five-second stable entry/exit. Identical triggers are valid and modes do not stack. Manual selection pauses until the winning context changes; Resume automation explicitly ends pause. Manual facet overrides survive restoration. Preserve unavailable included settings and report the failing facet. The runtime-status component includes Paused/Uncertain/Error/Restoring examples.

**Cooling:** canonical9:35 is a firmware Auto/Max example; it does not promise calibration will create a writer. The exact alpha.62 21Q6/N4CET45W provider can expose measured states4/5/6/7/0x40 after accepted calibration. Show requested percentage separately from effective step/RPM. 0% keeps the lowest accepted state running, 99% maps to Max; no continuous PWM or fabricated second sensor. Only show calibration when supported. Return to Auto is shown for actual ownership/recovery need, not forever after confirmed Auto. Source/hardware evidence lives in COOLING-DESIGN.md and the X9 research owner.

**Battery:** retain the functional gauge, charge-limit thresholds/custom pairs, ETA, temperature capability, Wh, health/cycles and actual history axes/time gaps. Percentage is separate like Home. Lower charge limits reduce high-state-of-charge stress; heat, cycles and age also matter. No unsupported precise wear prediction or implementation narration.

Battery history now includes **Cycles** alongside Charge, Discharge and Health. Store actual firmware cycle counts only, once per UTC day or on counter change, bounded by the existing one-year summary retention and 400 samples. Unknown readings do not become zero. A decreasing count remains visible and reports a possible replacement/reset; do not derive negative wear. Show the observed counter increase and weekly rate only after at least one day. The runtime reads firmware at most every 30 minutes; schema 6 adds samples without discarding schema 5 history. The model percentage and firmware cycle history are separate quantities.

Chart hover uses the shared `Chart tooltip / Nearest observation` component `276:6279`: local time above value/unit. WPF selects the nearest actual sample, clamps the tooltip inside the plot, and hides it on pointer exit. Keyboard focus supports Left/Right and Home/End. Axis labels and grids use the exact numeric tick positions, not rounded labels at fractional coordinates. Figma is a static example of the appearance; dynamic data selection belongs to TimeSeriesChart. Native hover/keyboard acceptance is **UNVERIFIED** until exercised in the running candidate.

Battery details add remaining/full energy capacity, temperature and locally learned charge time; do not repeat the primary percentage/health/cycles/power row. Charging and discharging use the same BatteryGauge: subtle diagonal flow in opposite directions, a short fade when paused, no movement while plugged-in but not charging, and no rendering callback while hidden/unloaded/idle. Respect Windows reduced motion. Do not add a competing decorative border animation.

Cycle/hover QA frame `286:6149` is an illustrative top-level state example, not a new canonical screen. Tooltip instance `286:6234` reuses component `276:6279`. Prototype navigation to this new target was rejected by Figma despite a persisted top-level frame; links from Cycles tabs remain UNVERIFIED. The illustration is not measured data. Native behavior remains owned by TimeSeriesChart.

**Audio/Keyboard:** Audio Safety includes Normal/Gesture lock/Silent and one session owner. Dolby direct writes, Access fallback and unavailable states differ; fallback cannot show enabled local profile writes or an unverified selected profile. Effects remain experimental and fully dim when disabled, sliders included. Temporary Lenovo OSD suppression must release on cancellation, shutdown and return to normal; do not permanently disable vendor notifications.

**Touchpad geometry:** source is TouchpadCornerZonePolicy and TrackCenterGesturePolicy. Default reference pad135×80mm maps423.5625×251px (3.1375px/mm); edge5mm→15.6875px, quarter-disc guard10mm→31.375px, diagonal lane24mm long with4mm halfwidth and rounded cap. Mirror the right corner. Rendering, clicking and recognition share geometry and corner priority; parameter changes must recompute it. Overlapping lower-edge candidates are not rectangular guesses. Click masks are neutral near-transparent; only the canonical component draws selection.

Corners support inward launch and outward close when enabled, with reverse start along≥12mm. Plus/minus stay inside active continuous-control bands; Track/off do not retain volume glyphs. LeftTrack variant `253:6277` is an action-state illustration, not a new Core zone enum. Hold target spans36–64% of the physical edge; hold≥450ms, max3mm movement, commit on release. Previous/next requires12mm travel. Bottom and side assignments use the same vocabulary. Live trails break on lift/jump; feedback is bounded, local and replaced immediately. Haptic capability is distinct from touchpad presence.

## Evidence and remaining gates

The native redesign is published in v0.1.0-alpha.62 at source `65b1686777b190d5cad48080db318f0037ef2157`; installation on the owner's device remains UNVERIFIED. Canonical design/interaction requirements above remain authoritative, including static versus dynamic state boundaries. Exact release/checksum evidence belongs to RELEASE_READINESS.md.

Independent assessment A visually reviewed all twelve canonical screens, Compact, editors and selected QA; isolated assessment B audited the whole hierarchy and compared exact Touchpad paths with Core. Both used current Figma/Drive/Impeccable guidance. The Impeccable CLI detector is unavailable, and Figma has no HTML DOM to validate; no detector/browser-overlay claim is made.

The native alpha.62 implementation has bounded visual evidence documented in RELEASE_READINESS.md; this does not certify every Figma prototype link or secondary state. **UNVERIFIED:** native redesign parity; all minimum/reference/wide layouts; actual DPI/text scaling and font fallback; mouse/keyboard/focus/popup/drag behavior; installed candidate lifecycle and publication; broad device sleep/resume/AC/DC and independent fan sensors.

Use existing WPF fixtures and shell smoke, inspect real renders against corresponding canonical nodes, and exercise actual interactions and persistence. A green build is not pixel or hardware acceptance. Keep current release state and open implementation work in RELEASE_READINESS.md rather than creating another project dossier.

## Alpha.63 runtime corrections (2026-10-07)

Compact notification uses a transparent inline action with the existing 40px target; the original wordmark SVG stays intact and its wrapper aligns visible lettering to the left content rail. Battery preservation uses a slim green level fill with real start/stop markers. Show estimated reduction and the normalized modeled wear-cycle basis: 0-to-limit versus 0-to-100 = 1.00. This is not actual firmware cycles saved. Updates uses a green check circle 12px after the current version, not a distant text badge. Context tabs are destination-owned navigation: clicking another page must never persist a second selection on return. Audio polling must discard older reads after user adjustment and must not quantize a continuous thumb coordinate when its rounded value is already confirmed. Shared WPF owners implement these corrections; corresponding live Figma refinements are UNVERIFIED and older static examples must not override these explicit owner corrections.
