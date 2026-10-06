# ThinkControl UI design handoff

This is the existing durable design owner. Recover live Git state and read AGENTS.md, CHAT_STARTER.md, DESIGN.md and PRODUCT.md. Figma owns the selected visual target; the repository owns behavior, capabilities and verification. Do not recover requirements by replaying the old chat.

## Selected source and migration

- Approved file: https://www.figma.com/design/U3tJrxFyixV1ubC7ZTlSGM
- Pages: Advanced `0:1`; Compact/feedback/Light QA `2:2`; design system `2:3`.
- Selected typography: **IBM Plex Sans Regular and SemiBold**. Selected icon language: **Microsoft Fluent System Icons**, plus purposeful ThinkControl brand/gesture glyphs.
- The older Fans-only file `Dcl5mMTiWUYwMcwiB0oVdy` is superseded as a visual target. Its Segoe/Inter fallback decision does not apply to this direction.
- Installed alpha.61 remains Segoe/Material. Replace shared WPF resources and the semantic icon adapter coherently; do not mix systems per page. Package font assets/licenses, retain the wordmark, and compare actual native font metrics.
- Keep the user/service privilege boundary, IPC, settings schemas, capabilities, session ownership, rollback and data. No flattened screenshot implementation or duplicate mock application.
- The implementation request now covers the whole real WPF application, near-1:1 visual fidelity and functional integration. Readiness evidence belongs in RELEASE_READINESS.md. Figma completion is not installed-app completion.

## Canonical screens

Latest owner corrections (2026-10-06): Overview has separate Mode and Keyboard light cards. Canonical Mode remains `6:124`; Keyboard light is `266:6134`, with preserved Auto choice `266:6155`. Light counterparts are `266:6851` and `266:6855`. This overrides the former combined card. Footer actions align with the main navigation icon/text rails. Status copy wraps instead of truncating ordinary battery information. Dark semantic Success/Warning/Error are now `#3ed486`, `#ffb545`, `#ff645c`, bound through the existing Figma variables and WPF theme owner. Charging flow remains tied to real charging; a discharge animation is still UNVERIFIED/unimplemented and must not imply fabricated percentage movement.

Manual mode selection and rules share the production coordinator. Manual selection pauses automation until the winning context changes or Resume is requested; the selector does not constitute a second competing owner. Overview must show that pause explicitly. Modes uses the shared Saved modes/Automation tabs and has no additional duplicate Automation header link.

Latest navigation preference: keep Compact view in the footer with Notifications and Preferences; raise primary destinations 14px (first destination y=110 at the canonical size). Align every action's text/icon rail with the primary destinations. This supersedes the briefly explored placement beneath the wordmark.

Scrollbar override: square thumb corners and a 6px gap between content and the scrollbar, shared across scrollable surfaces. Native scroll interaction and orientation-specific minimum thumb sizes remain intact.

Preservation benefit: green qualitative copy, "Helps reduce battery wear" in WPF / "Helps reduce wear" in the Figma badge `9:358`. Show it only with verified enabled preservation. Do not display invented cycles saved or an exponential improvement estimate. Charge cycles accumulate 100% capacity use across sessions; charge level, temperature, depth of discharge and age all affect wear. Context: https://pcsupport.lenovo.com/lc/en/solutions/ht509084/ and https://www.apple.com/batteries/why-lithium-ion/. Custom start/stop pairs must retain their actual readback instead of selecting a misleading fixed preset.

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

**Compact customization:** drag directly in the preview to swap slots. Status and controls are separate rows, shown side-by-side in the editor at the same vertical level. Available lists contain only unused eligible items. Persist changes through the existing settings owner; cancellation, lost capture and a later drag must work. Never duplicate the same item into a second slot.

**Modes and Automation:** modes contain removable included settings only. New mode starts with Name and Add setting, no fixed full-settings form; saving does not activate. Supported facets and exclusions are defined in PRODUCT.md. Rules link to modes independently, use Any/All, highest priority then visible list order and five-second stable entry/exit. Identical triggers are valid and modes do not stack. Manual selection pauses until the winning context changes; Resume automation explicitly ends pause. Manual facet overrides survive restoration. Preserve unavailable included settings and report the failing facet. The runtime-status component includes Paused/Uncertain/Error/Restoring examples.

**Cooling:** canonical9:35 is a firmware Auto/Max example; it does not promise calibration will create a writer. The exact alpha.62 21Q6/N4CET45W provider can expose measured states4/5/6/7/0x40 after accepted calibration. Show requested percentage separately from effective step/RPM. 0% keeps the lowest accepted state running, 99% maps to Max; no continuous PWM or fabricated second sensor. Only show calibration when supported. Return to Auto is shown for actual ownership/recovery need, not forever after confirmed Auto. Source/hardware evidence lives in COOLING-DESIGN.md and the X9 research owner.

**Battery:** retain the functional gauge, charge-limit thresholds/custom pairs, ETA, temperature capability, Wh, health/cycles and actual history axes/time gaps. Percentage is separate like Home. Lower charge limits reduce high-state-of-charge stress; heat, cycles and age also matter. No unsupported precise wear prediction or implementation narration.

**Audio/Keyboard:** Audio Safety includes Normal/Gesture lock/Silent and one session owner. Dolby direct writes, Access fallback and unavailable states differ; fallback cannot show enabled local profile writes or an unverified selected profile. Effects remain experimental and fully dim when disabled, sliders included. Temporary Lenovo OSD suppression must release on cancellation, shutdown and return to normal; do not permanently disable vendor notifications.

**Touchpad geometry:** source is TouchpadCornerZonePolicy and TrackCenterGesturePolicy. Default reference pad135×80mm maps423.5625×251px (3.1375px/mm); edge5mm→15.6875px, quarter-disc guard10mm→31.375px, diagonal lane24mm long with4mm halfwidth and rounded cap. Mirror the right corner. Rendering, clicking and recognition share geometry and corner priority; parameter changes must recompute it. Overlapping lower-edge candidates are not rectangular guesses. Click masks are neutral near-transparent; only the canonical component draws selection.

Corners support inward launch and outward close when enabled, with reverse start along≥12mm. Plus/minus stay inside active continuous-control bands; Track/off do not retain volume glyphs. LeftTrack variant `253:6277` is an action-state illustration, not a new Core zone enum. Hold target spans36–64% of the physical edge; hold≥450ms, max3mm movement, commit on release. Previous/next requires12mm travel. Bottom and side assignments use the same vocabulary. Live trails break on lift/jump; feedback is bounded, local and replaced immediately. Haptic capability is distinct from touchpad presence.

## Evidence and remaining gates

Independent assessment A visually reviewed all twelve canonical screens, Compact, editors and selected QA; isolated assessment B audited the whole hierarchy and compared exact Touchpad paths with Core. Both used current Figma/Drive/Impeccable guidance. The Impeccable CLI detector is unavailable, and Figma has no HTML DOM to validate; no detector/browser-overlay claim is made.

Post-repair Figma rendering/structural review is still required before treating all corrected states as approved. **UNVERIFIED:** native redesign parity; all minimum/reference/wide layouts; actual DPI/text scaling and font fallback; mouse/keyboard/focus/popup/drag behavior; installed candidate lifecycle and publication; broad device sleep/resume/AC/DC and independent fan sensors.

Use existing WPF fixtures and shell smoke, inspect real renders against corresponding canonical nodes, and exercise actual interactions and persistence. A green build is not pixel or hardware acceptance. Keep current release state and open implementation work in RELEASE_READINESS.md rather than creating another project dossier.
