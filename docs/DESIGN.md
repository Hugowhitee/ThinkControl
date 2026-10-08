# Design system

ThinkControl is a compact Windows hardware utility. The interface should feel like a precise functional instrument: restrained, systematic and desktop-native, with enough tactile/technical character to make state and interaction clear without decoration for its own sake.

## Visual language

### Selected redesign and migration boundary

The approved whole-application visual target is [ThinkControl — Full UI Redesign · Concept 01](https://www.figma.com/design/U3tJrxFyixV1ubC7ZTlSGM). `UI-DESIGN-HANDOFF.md` owns its canonical frame/component IDs and interaction requirements. **IBM Plex Sans Regular/SemiBold and Microsoft Fluent System Icons** supersede the following Segoe/Material presentation rules for the redesign. These are intentional choices, not unavailable-font fallbacks.

The installed alpha.61 and pre-migration WPF still use Segoe UI/Material Symbols. Preserve those as the existing runtime baseline until replacing the shared resources and semantic icon adapter coherently. Do not select fonts/icons independently per page or retain a permanent mixed system. Package the used IBM Plex assets and their license; use the exact approved Fluent geometry through one local semantic icon owner. Keep the ThinkControl wordmark and purposeful product-specific glyphs. Product behavior, provider boundaries and session ownership remain authoritative in PRODUCT/ARCHITECTURE/COOLING-DESIGN.

Figma reference is 1200×780 Advanced with a 222 px navigation rail, x29 content inset, an 85 px shared header at y24 and separator at y108. Minimum runtime remains 980×650; narrower layouts reflow without scaling the whole canvas. Shared typography roles: Title27/37, Heading19/26, Section17/24, Body14/19, Body-small and Control-small13/18, Caption12/16, Micro11/15, Metric25/34. These are logical units before Windows DPI/text scaling. Dark/Light semantic tokens, same-role controls, focus and disabled presentation must come from shared resources. Filled actions use the contrast-safe Action token; Accent remains selection/brand emphasis.

### Existing runtime baseline

Use:

- Segoe UI with normal Windows text rendering;
- the curated local Material Symbols Outlined geometry set plus the small purpose-built ThinkControl glyphs where a generic symbol is ambiguous;
- one restrained ThinkControl accent plus semantic warning/error/state colors;
- thin borders and separators;
- compact control radii and spacing;
- strong alignment and repeatable geometry;
- clear selected, hover, disabled and keyboard-focus states;
- System, Light and Dark themes;
- subtle elevation only when it clarifies hierarchy or a floating surface.

Avoid:

- decorative gradients, glass effects or textures that do not communicate state;
- large rounded cards without structural purpose;
- nested cards for simple settings rows;
- generic SaaS dashboard decoration;
- oversized headings/excessive padding;
- emoji as interface icons;
- mixed icon languages;
- glow-heavy effects;
- fabricated precision or telemetry;
- enabled-looking controls when the backend/capability is unavailable;
- release-specific runtime visual-tree patches when a shared XAML/style/layout owner can express the rule.

## Shared control and state consistency

Same-role settings use the same component grammar. A preference such as **Theme** and **App icon opens** may differ in options, but not in control height, selected treatment, spacing rail or button vocabulary without a product reason. Fix the shared owner or reuse the shared component instead of creating a visually similar one-off.

Color carries state deliberately:

- Accent marks navigation, selected controls and deliberate primary actions.
- Success green means an outcome or effective state was **confirmed by its authoritative owner/readback**. Saved preference, requested selection, pending application and availability are not success.
- Warning and Error stay local to the uncertain or rejected action. A failed mode facet does not turn the previous confirmed active mode into an error.
- Healthy ordinary readiness remains neutral unless showing confirmation materially helps the current decision.

Modes has one status grammar across Overview, Saved modes and Automation. The persistent Overview control stays compact in the lower quick-control row; a successful rule-triggered activation may additionally use one brief passive notification, not a large permanent announcement. Manual pause exposes **Resume automation** only where it changes state.

Context tabs are not primary navigation. When Saved modes and Automation are two views of the same Modes task, changing between them preserves an in-progress editor and unsaved transient values. Leaving Modes for another primary destination may reset that transient state on the next entry. Primary-destination re-entry still follows the normal top-scroll/reset rule.

Rule enabled state, condition enabled state, pending activation and confirmed active ownership are separate roles. Reuse `Control / Switch` for the first two, the shared runtime-status family for ownership, and local action feedback for failures. A rule-list switch writes immediately; an editor switch remains draft until Save. Use an accessible name identifying the rule/condition and at least a 40-unit hit area. Never use a dropdown's selected item as confirmation or a retained coordinator ID as proof of recovered hardware state.

Recorded sessions use fixed semantic columns for local date/time, direction/percentage, duration and signed energy, with a separate disclosure target. A session/day disclosure inspects history; History settings changes retention/storage. At narrower widths, wrap within those roles or stack metadata beneath its session, preserving reading and focus order. Do not push values to the far end of an unbounded container. Battery wear models use quiet illustrative language; measured firmware cycles retain their separate role.

The post-alpha.65 WPF candidate reuses `TcSection`, `TcSegment`, `TcSwitch`, `TcInlineButton`, `TcQuietExpander`, the packaged IBM Plex font and the existing Fluent adapter. Confirmed indicators come from the coordinator/automation owners; failed-target identity and uncertain rollback are explicit state rather than parsed message text. General preference rails align, device label/value groups stay adjacent, and rule Match/Priority/condition rails remain bounded at wide widths.

## Information hierarchy

ThinkControl has two interface densities.

### Compact

Compact is a fixed utility surface for frequently checked state and frequently changed settings. It should remain useful without becoming a miniature copy of every Advanced page.

Current hierarchy:

1. product/device identity and shell actions;
2. three configurable live metrics (default Battery, CPU, Fans);
3. battery power preference;
4. fan profile;
5. display refresh;
6. keyboard backlight;
7. brightness and volume;
8. direct utility/page links.

Compact stays visible when focus moves to another normal application. It hides only through explicit close/tray behavior or a deliberate Compact/Advanced transition.

### Advanced

Advanced is a normal resizable Windows application window with one shared content rail and native Windows caption/Snap behavior.

Primary navigation:

- Home
- Performance
- Fans
- Battery
- Display
- Audio
- Keyboard
- Touchpad
- Modes
- System
- Updates
- Settings

Detailed Sensors opens from System rather than becoming a second permanent navigation hierarchy.

## Page headers and interface copy

Every sibling Advanced destination uses `AdvancedPageHeader`. The shared primitive owns the title anchor, 38 px first-row geometry, optional supporting-text row and right-side action rail. Pages may supply zero, one or a small set of true page-level actions; ordinary feature state belongs in the body. Secondary actions consolidate behind an existing menu instead of wrapping the title rail at narrow widths.

A missing subtitle is allowed. Do not invent prose to preserve vertical geometry.

Visible copy describes the user's current choice, state, consequence or recovery path. It must not narrate requirements, implementation intent or UI construction. In particular:

- do not repeat visible button labels in explanatory prose;
- do not explain that a future dialog will contain specific buttons when the dialog itself is clear;
- do not write design commentary such as “kept in one place”, “instead of separate cards” or “keeps the daily surface compact”;
- keep provider/API/fallback detail out of permanent UI unless it materially changes safety, capability or troubleshooting.

Home and Compact prefer controls plus live state over explanatory paragraphs. Deeper technical evidence belongs in Diagnostics or documentation.

## Responsive layout

Every Advanced page uses the same left anchor/readable maximum width and must survive the documented minimum, normal and wide snapshots without horizontal escape or clipped labels.

The page header is also one shared rail. The title occupies a 38 px title row; true page actions align on that row, ordered consistently as contextual actions, external Windows links and Defaults. Subtitle/help text sits below it. Feature state switches (for example, Touchpad edge gestures) belong next to the relevant feature in the page body, not between unrelated page actions. Changing destinations must not make the top action jump because one page centered it against a two-line title block while another centered it against the title alone.

- Prefer wrapping concise helper copy over ellipsizing a sentence that changes the meaning of a setting.
- Values/telemetry may use ellipsis only where the complete value can genuinely exceed the available semantic slot.
- Do not make one page invent a different content rail or card width because its contents are awkward.
- Wide windows keep readable content bounded instead of stretching controls across the entire monitor.
- Reopening a normal scrollable page starts at the top unless preserving position is explicitly part of the interaction.

## Typography

Typography is shared; do not locally shrink text to make a layout bug disappear.

| Role | Intent |
| --- | --- |
| Page title | clear page identity, not marketing hero text |
| Section title | local hierarchy inside a page |
| Body/control | default readable interaction text |
| Secondary | supporting state/detail |
| Caption/metadata | source, provider and low-priority technical detail |
| Value | numeric/state values that need faster scanning |

Use the shared `TypographyScale`/text styles. Large numeric telemetry may use a lighter weight. Labels should remain quieter than the values they describe.

## Icons

`PackIconLucide` is a compatibility type name. The active redesign uses packaged Microsoft Fluent System Icons through one semantic SVG adapter. The installed alpha.61 baseline uses Material Symbols; those resources are superseded in the redesign. Physical touchpad geometry and battery drawings remain native, driven by their existing owners.

Icons support recognition and navigation, not decoration. Text-first segmented choices such as Efficiency / Balanced / Performance or Auto / 60 Hz / Max do not need individual icons.

Do not use an unrelated icon simply because it is available. A new icon should match the existing stroke/weight/optical scale and be reviewed at its actual 12–20 px product size.

## Power terminology

User-facing power terminology is consistently:

- **Efficiency**
- **Balanced**
- **Performance**

Compact and Home expose the **battery preference** as the quick control. The full Performance page exposes both battery and plugged-in preferences independently. Internal enum/provider names may retain historical terminology but must not leak into visible copy.

## Home telemetry

Home telemetry is a compact scan line, not a collection of mini dashboards.

- Battery, CPU, Fans, Power and Sensors share the same label/value/detail rhythm and left inset.
- Battery may include a contextual gauge, but the text still aligns with the neighboring metrics and remains readable at minimum width.
- Fan value prioritizes the selected profile/owner; real RPM is supporting telemetry underneath.
- Healthy state stays visually neutral; accent/error color is for meaningful active/attention state, not decoration.
- Clicking a metric navigates to its deeper page without an obstructive permanent tooltip layer.

## Capability states

Unavailable hardware should not look like a normal working control.

Use either:

- a disabled control with a short explanation when preserving location helps discoverability; or
- a concise compatibility/provider message in place of the action.

Healthy state should remain visually quiet. Avoid permanent green badges for ordinary readiness.

Capability copy must describe the missing layer accurately. A reachable service with an unavailable sensor provider is not “offline”, and missing haptic-setting support does not mean the Precision Touchpad itself is absent.

## Fans

Fan UI describes the semantics the active provider really exposes.

For the verified X9 discrete-EC provider:

- firmware **Auto** is the safe ownership state;
- raw diagnostics use EC steps 1–7;
- supervised/user-facing curves may show a percentage **target** only when it is mapped to verified/calibrated discrete output states;
- the UI must never imply continuous PWM where the backend does not expose it;
- manual tests are visibly temporary and expose a clear restore/end state;
- X9 calibration/raw controls disappear unless that exact provider plus required capabilities are active.

Fan RPM and current output/profile should appear near each other because they describe the same subsystem, but measured RPM must never be invented to make the panel look complete.

## Touchpad

The Touchpad visualizer is an explanation of the real recognizer, not a decorative diagram.

- Edge bands correspond to the configurable precision edge actions.
- Top-corner launch lanes use the exact same millimetre geometry for rendering, clicking and Core recognition.
- The optional Track-center Play/Pause region is a visible bounded target; there is no hidden center hot zone.
- Contact trails represent continuous physical contact only. Lift/re-touch and implausible jumps break the segment.
- Active/released gesture feedback is bounded, local to the relevant edge/action and replaced by new input immediately.
- Haptic controls reflect the actual Windows/provider capability state and use the same direction/level semantics as the underlying setting.

## Battery

Compact/Home prioritize percentage, state, live power and ETA. Advanced Battery can add Wh, health, cycle count, history and provider detail.

Time estimates are approximate and should be formatted as human-readable duration rather than fake precision. Battery temperature appears only when a credible battery-specific provider/sensor identifies it.

## Motion and loading

Animation is short and functional.

- ordinary state transitions: roughly 100–180 ms;
- no animation that delays a hardware command;
- no whole-window opacity trick that can expose an unpainted native WPF frame;
- respect Windows animation accessibility settings;
- expensive cold startup/view construction uses an already-painted loading/transition surface rather than an apparently dead click.

## Floating surfaces

Attention/update/hardware popups belong to ThinkControl and should remain above their visible owner without stealing unrelated focus. Dismissal must not accidentally minimize/hide the owner. Completed-update confirmation is passive; decision buttons are reserved for states that genuinely require a decision.

## Validation

UI-affecting work is not complete because XAML compiles. Inspect deterministic screenshots at minimum/normal/wide widths plus light/dark and relevant unavailable/error/active states. Compact/Advanced lifecycle changes additionally require real shell smoke because static screenshots cannot prove window ownership, activation or transition behavior.


## Figma and redesign work

Targeted Figma/redesign work follows `docs/UI-DESIGN-HANDOFF.md`. That file defines which surfaces are currently weak enough to redesign, which validated visual mechanisms must be preserved, and how selected Figma changes return to the actual WPF source and screenshot gates.


Cooling/preservation cohesion (v0.1.0-alpha.72): Overview quick-action cards share the icon/title, supporting line, control row and caption rhythm. Cooling adds no one-off dividers or uppercase mini-columns. Full-width preference/cooling rows share `TcSettingLabelColumn` (160 DIP); the choice and associated action sit together with a 12 DIP gap. Preservation keeps title/switch on the header rail and limit/state/Edit together on the value rail. Its 18 DIP track contains threshold marks, a patterned resume/stop window and a neutral region above the stop limit. The actual charge marker is retained above the limit; zones are described on hover rather than relying solely on color. Charging phase reuses BatteryGauge and respects the same motion preference. Paused/discharging views remain still.
