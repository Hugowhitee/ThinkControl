# Design system

ThinkControl is a compact Windows hardware utility. The interface should feel precise, restrained and consistent with a desktop settings application.

## Visual language

Use:

- Segoe UI with normal Windows text rendering;
- Material Symbols Outlined for functional icons;
- one restrained ThinkControl accent plus system state colors;
- thin borders and separators;
- small control radii;
- compact spacing and strong alignment;
- clear selected, hover, disabled and focus states;
- System, Light and Dark themes;
- subtle elevation only when it clarifies hierarchy.

Avoid:

- decorative gradients or textures;
- large rounded cards without structural purpose;
- nested cards for simple rows;
- dashboard gauges for ordinary settings;
- oversized headings and excessive padding;
- emoji as interface icons;
- mixed icon families;
- glow-heavy effects;
- fabricated precision;
- enabled-looking controls when the backend is unavailable.

## Information hierarchy

ThinkControl has two interface densities.

### Compact window

The tray window contains frequently checked telemetry and frequently changed settings.

Typical order:

1. device identity;
2. temperature and fan state;
3. performance mode;
4. display controls;
5. keyboard backlight;
6. battery and fan shortcuts;
7. Settings, System and Advanced access.

### Advanced window

The Advanced window contains detailed controls and information without turning the compact window into a full settings application.

Navigation:

- Home
- Performance
- Fans
- Display
- Keyboard
- Battery
- System
- Updates
- Settings

## Typography

Use a small number of text levels consistently.

| Role | Typical size |
| --- | --- |
| Product or compact-window title | about 20 px |
| Advanced page title | about 24 px |
| Major telemetry | 24 to 38 px, depending on context |
| Normal control and body text | 10.5 to 12 px |
| Metadata and helper text | 9 to 10.5 px |

Large numeric telemetry may use a lighter weight. Labels should not compete visually with the value they describe.

## Icons

ThinkControl uses a curated local subset of Material Symbols Outlined represented as WPF vector geometry.

Use icons for recognition and navigation, not as decoration. Text-first segmented controls such as Quiet, Balanced, Performance, 60 Hz and Max do not need separate icons.

Normal product UI should use one icon family unless a native Windows control is clearer.

## Compact layout

The compact window should remain close to its existing size unless a feature genuinely requires more space.

```text
ThinkControl
ThinkPad model

CPU temperature        Fan RPM
Temperature history

Performance
[ Quiet ] [ Balanced ] [ Performance ]

Display
[ Auto ] [ 60 Hz ] [ Max ]
Brightness        slider
Adaptive brightness     toggle

Keyboard
[ Off ] [ Low ] [ High ] [ Auto ]

Battery status      Fan state

Settings   System   Advanced
```

### Telemetry

Temperature and RPM are shown only when the provider identifies the value reliably. Source details belong in tooltips or Advanced pages rather than occupying permanent compact-window space.

Approximate data must stay labelled as approximate. An ACPI thermal-zone reading should not be renamed to `CPU Package`.

### Temperature history

The compact temperature graph is secondary information.

- about 60 seconds of history;
- thin line;
- minimal axes and grid;
- no decorative area fill;
- attention colors only for real states or thresholds.

### Performance

Primary labels are Quiet, Balanced and Performance. Do not display wattage, dBA or other numeric claims unless ThinkControl has a real source for that exact value.

### Display

Refresh rate, brightness and adaptive brightness belong in one Display section. The refresh choices are Auto, 60 Hz and Max. Max resolves to the actual built-in panel maximum.

### Keyboard

The compact window exposes Off, Low, High and Auto. More complex effects belong in the Advanced Keyboard page.

## Advanced window

The Advanced window uses a left navigation rail and one content page at a time. Dedicated pages should contain only controls related to that subject.

The Home page summarizes the most useful current state, including CPU, fan, battery, performance, display, keyboard, compatibility and update information.

## Capability states

Unavailable hardware should not look like a normal working control.

Use either:

- a disabled control with a short explanation when preserving its location helps discoverability; or
- a concise compatibility message in place of the action.

Healthy state should remain visually quiet. Avoid filling the interface with permanent green status badges.

## Fan controls

The verified X9 fan interface uses Lenovo Auto and discrete levels 1 through 7. The UI must not convert those states into a fake percentage.

Current fan state should appear close to fan RPM because both values describe the same subsystem.

## Battery presentation

Compact battery information prioritizes percentage, live power and time estimate. The Advanced Battery page can add average power, energy, health and provider details.

Time estimates are approximate and should be formatted accordingly.

## Motion

Animation should be short and functional.

- roughly 120 to 180 ms for ordinary state transitions;
- no long easing sequences;
- no animation that delays a hardware command;
- respect Windows animation accessibility settings.

The compact and Advanced surfaces are separate WPF windows. Use a simple handoff rather than a fragile cross-window morph.

## Accessibility and scaling

The interface must remain usable with keyboard navigation, visible focus, all supported themes and common Windows scaling levels including 100, 125 and 150 percent.

CI snapshots are used to catch obvious clipping, icon and layout regressions. Real-device testing remains necessary for taskbar placement, DPI behavior and interaction quality.
