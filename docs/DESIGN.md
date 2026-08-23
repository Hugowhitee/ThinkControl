# ThinkControl design system

ThinkControl is a compact Windows hardware utility. The interface should feel precise, quiet and native to a desktop control panel rather than like a gaming dashboard or a collection of decorative cards.

## Core visual language

Use:

- Segoe UI and normal Windows text rendering
- Google Material Symbols Outlined for functional icons
- one restrained ThinkControl accent plus system state colors
- 1 px borders and separators
- small corner radii, generally 2-4 px for controls and 4-7 px for window surfaces
- compact spacing and strong alignment
- clear selected, hover, disabled and focus states
- System, Light and Dark themes
- subtle elevation only where it clarifies window hierarchy
- flat or lightly tinted surfaces

Avoid:

- decorative gradients
- brushed-metal, carbon or heavy-noise textures
- large rounded cards
- nested card layouts without structural purpose
- speedometer-style gauges
- oversized headings and excessive empty padding
- emoji as product icons
- mixed icon families
- glow-heavy gaming effects
- fake precision
- controls that look active when their backend is unavailable

## Information hierarchy

ThinkControl has two UI densities:

### Compact

The tray popup is for settings and telemetry a user may check or change frequently. It should be readable at a glance and require little pointer travel.

Priority order:

1. device identity
2. temperature and fan state
3. performance profile
4. display controls
5. keyboard backlight
6. battery/fan quick links
7. Settings, System and Advanced access

### Advanced

The Advanced window is for deeper controls, compatibility information and less frequently used settings. It may use more whitespace, but it should still behave like a dense system utility rather than a marketing page.

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

Use a small number of levels consistently:

- window/product title: approximately 20 px
- Advanced page title: approximately 24 px
- major telemetry: 24-38 px depending on context
- normal controls/body text: 10.5-12 px
- metadata/helper text: 9-10.5 px

Large numeric telemetry may use a light font weight. Labels should remain compact and should not compete visually with the value they describe.

## Icons

ThinkControl uses a curated subset of Google Material Symbols Outlined represented as local WPF vector geometry.

Icons are used for recognition, not decoration. Appropriate uses include:

- Advanced navigation
- Settings and System actions
- update/attention states
- tray actions
- dock/expand actions

Text-first segmented controls such as `Quiet`, `Balanced`, `Performance`, `60 Hz` and `Max` do not need individual icons.

All product UI should use the same icon family unless a Windows-native window control is clearer.

## Compact popup

The compact popup is a fixed-size tray surface intended to open near the taskbar work area.

Current layout:

```text
ThinkControl
ThinkPad model

CPU temperature        Fan RPM
60-second temperature sparkline

Performance
[ Quiet ] [ Balanced ] [ Performance ]

Display
[ Auto ] [ 60 Hz ] [ Max ]
Brightness        slider
Adaptive brightness     toggle

Keyboard
[ Off ] [ Low ] [ High ] [ Auto ]

Battery status      Fan state

Settings   System           Expand
```

The popup should remain close to its current 410 × 640 logical-pixel envelope unless a feature genuinely requires more room.

### Telemetry

CPU temperature and fan RPM are displayed only when the active provider can identify their source reliably.

Source details belong in tooltips or Advanced rather than permanently occupying compact space.

Examples of valid source descriptions:

```text
CPU Package
LibreHardwareMonitor / PawnIO
```

```text
ThinkPad EC tachometer
EC 0x84/0x85
```

Approximate sources must be labeled as approximate. An ACPI thermal-zone reading must not be renamed to `CPU Package`.

### Sparkline

The compact temperature graph is context, not the focus of the application.

- approximately 60 seconds of history
- thin line
- minimal grid/axes
- no area gradient
- no decorative warning color
- warning/accent colors only for real states or thresholds

### Performance modes

The primary labels are:

- Quiet
- Balanced
- Performance

Short secondary text may describe intent, such as `Efficient`, `Everyday` or `Maximum`.

Do not show watts, dBA or other numeric characteristics unless ThinkControl has a measured/configured source for that exact value.

### Display

Refresh rate, brightness and adaptive brightness belong in one coherent Display section.

Refresh controls should use:

- Auto
- 60 Hz
- Max

`Max` resolves to the actual panel maximum rather than hardcoding a refresh rate into the layout.

### Keyboard

The compact surface exposes hardware-facing daily states:

- Off
- Low
- High
- Auto

More complex ThinkControl effects belong in the Advanced Keyboard page.

### Footer and window actions

The footer contains only high-value actions and quiet status information.

The expand button opens the Advanced window. The Advanced window provides a corresponding dock/back-to-popup action. Closing either surface hides ThinkControl to the tray rather than quitting the process.

## Advanced window

The Advanced window uses a left navigation rail and one content page at a time.

The Home page summarizes:

- CPU
- Fan
- Battery
- Performance
- Display
- Keyboard
- compatibility/readiness
- update state

Dedicated pages provide deeper controls without duplicating unrelated settings.

## Capability states

Unavailable hardware should not appear as a normal enabled control.

Preferred patterns:

- disable a control when its location is important for discoverability, with a concise reason nearby; or
- replace the action area with a short compatibility explanation.

Examples:

```text
Fan control
Hardware access unavailable
```

```text
Keyboard backlight
Lenovo Power Management provider not available
```

Compatibility wording should describe ThinkControl's confidence, not imply that the laptop itself is faulty.

## Hardware readiness

Healthy readiness should stay visually quiet.

Only surface an attention state when the user can act on it, for example:

```text
Hardware access limited
[ Review ]
```

or:

```text
Device software needs attention
[ Open Lenovo Drivers ]
```

Do not permanently show green `OK` badges throughout the interface.

## Fan controls

The X9 interface represents the real backend states:

- Lenovo Auto
- Level 1 through Level 7

Manual fan controls must not be presented as percentages. Any future curve editor uses the discrete levels as its source of truth; a graph is only a visualization of that table.

Current fan level should appear close to fan RPM because the two values describe the same physical state.

## Battery presentation

Compact battery information prioritizes:

- percentage
- live power
- ETA when available

The Advanced Battery page can additionally show:

- filtered average power
- energy remaining/full
- health
- source/provider

ETA wording should remain approximate (`~52 min`, `~1h 20m`) because it is derived from recent power use rather than a fixed promise.

## Motion

Motion is subtle and functional:

- roughly 120-180 ms for state/window transitions
- no long easing sequences
- no animation that delays a hardware action
- respect Windows Animation Effects accessibility settings

Because compact and Advanced are separate WPF windows, the expand/dock transition should create a light visual handoff rather than attempting a fragile cross-HWND morph.

## Accessibility and scaling

The interface must remain usable with:

- keyboard navigation
- visible focus
- Windows Light/Dark/System themes
- common desktop scaling levels including 100%, 125% and 150%

CI-rendered snapshots are used to catch clipping, missing icons and obvious layout regressions, but physical-device testing remains necessary for DPI, taskbar placement and interaction behavior.
