# Design system

ThinkControl is a compact Windows hardware utility. The interface should feel like a precise functional instrument: restrained, systematic and desktop-native, with enough tactile/technical character to make state and interaction clear without decoration for its own sake.

## Visual language

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
- Display
- Audio
- Keyboard
- Battery
- Touchpad
- System
- Updates
- Settings

Detailed Sensors opens from System rather than becoming a second permanent navigation hierarchy.

## Responsive layout

Every Advanced page uses the same left anchor/readable maximum width and must survive the documented minimum, normal and wide snapshots without horizontal escape or clipped labels.

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

`PackIconLucide` is a historical type name; its production language is the curated local Material Symbols geometry plus a few ThinkControl-specific glyphs such as Compact/Advanced and touchpad/battery shapes.

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
