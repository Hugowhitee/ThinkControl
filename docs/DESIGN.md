# UI design rules

ThinkControl should feel like a precise Windows utility, not a gaming dashboard and not an AI-generated collection of cards.

The current visual reference is the dark compact ThinkControl mockup supplied during the v0.1 design phase: strong hierarchy, telemetry in the header, one temperature graph, segmented hardware controls and a utility-style footer. Keep that structure, but make the production UI flatter, calmer and less textured than the reference.

## Visual language

Use:

- Segoe UI / system typography
- Microsoft Fluent System Icons or another single vetted Windows-friendly icon set
- system accent color, with one restrained ThinkControl accent treatment
- 1 px borders and separators
- roughly 2-4 px corner radii
- compact spacing
- clear selected states
- system/light/dark themes
- keyboard navigation and visible focus
- subtle elevation only where it explains window/surface hierarchy
- flat or very lightly tinted surfaces rather than simulated metal/plastic textures

Avoid:

- gradients used as decoration
- brushed-metal / carbon / heavy noise textures
- giant rounded cards
- card-inside-card layouts
- decorative gauges/speedometers
- oversized headings and empty padding
- emoji as product icons
- random icon styles
- many accent colors
- fake precision
- glow-heavy red gaming UI
- skeuomorphic switches or knobs that do not behave like native controls

## Icon policy

Icons are functional, not decoration.

Use icons for:

- Advanced section navigation
- update/attention state
- AC/battery state
- hardware-access state
- tray menu actions where recognition improves scanning
- window/dock actions

Do not put a different icon in every segmented button. `Quiet`, `Balanced`, `Performance`, `60 Hz` and `120 Hz` are clearer as text-first controls.

The icon pack must be consistent across tray, compact popup, Advanced and installer. Do not hand-generate lookalike icons.

## Compact popup

Starts from the tray and opens near the bottom-right of the active taskbar work area.

The supplied mockup is a good hierarchy reference, but the production compact popup should use a shorter graph and less vertical chrome so it feels like a real quick-control panel rather than a full dashboard.

Target hierarchy:

```text
ThinkControl                         CPU 44 C   Fan 2050 RPM
ThinkPad X9-15 Gen 1 · Quiet         [battery/AC when useful]

thin 60-second temperature sparkline

Power mode     [ Quiet ] [ Balanced ] [ Performance ]
Display        [ Auto ]  [ 60 Hz ]    [ 120 Hz ]
Brightness     ---------------- 70%
Adaptive       On
Keyboard       [ Off ] [ Low ] [ High ] [ Auto ]

[ Advanced ] [ Settings ]                 v0.1
```

### Header telemetry

The header should contain:

- detected model/family;
- CPU temperature when trustworthy;
- actual fan RPM when trustworthy;
- a subtle AC/battery indicator when it adds context;
- a small readiness/attention icon only when hardware access is limited or needs attention.

Do not permanently consume header space with green `OK` badges. Healthy state should be quiet.

Temperature and RPM values must carry exact source metadata on hover/details.

Examples:

```text
CPU temperature
CPU Package
LibreHardwareMonitor / PawnIO
```

```text
Fan speed
ThinkPad EC tachometer 0x84/0x85
Fan 1
```

If the source is approximate, label it as approximate instead of silently changing the sensor meaning.

### Sparkline

In compact mode the graph is a 60-second context line, not the focus of the application.

- target height roughly 64-90 logical pixels;
- minimal axes/grid;
- no filled area gradient;
- no red segment just because a sample is recent;
- use warning/accent color only when a real threshold/state requires attention;
- Advanced may expose a larger 5-10 minute temperature + fan graph later.

### Power mode row

`Quiet / Balanced / Performance` is the primary control.

A secondary status line may state the actual coordinated policy, for example:

```text
Best efficiency · Lenovo Intelligent Cooling
```

Do not show `15 W`, `25 W` or another power value unless ThinkControl actually configured or measured that exact quantity and can explain its source.

### Display and keyboard

The quick controls in the mockup are appropriate when capabilities exist:

- refresh: Auto / 60 / panel maximum;
- brightness slider;
- adaptive brightness only when Windows/platform support is verified;
- keyboard backlight: Off / Low / High / Auto when verified.

Unsupported items are omitted or replaced by a concise unavailable explanation in Advanced. They never remain as dead controls.

### Footer

Keep the compact footer quiet:

- Advanced;
- Settings;
- version/update indicator;
- optional attention icon when a dependency needs action.

`ThinkControl is running in the background` is useful as a first-run hint, but should not permanently occupy a full status strip after the user understands tray behavior.

## Hardware-readiness surface

Do not turn dependency management into setup clutter on every launch.

Healthy machine:

```text
(no badge)
```

Limited low-level access:

```text
Hardware access limited   [ Fix ]
```

Missing/unhealthy expected OEM component:

```text
Device software needs attention   [ Review ]
```

`Fix` opens a small readiness flyout or Settings > Device, not an unexplained browser page.

## Features deliberately kept out of the compact popup

Keep these in Advanced unless future testing proves they deserve a daily quick control:

- custom fan table editor;
- charge thresholds / conservation settings;
- fan conflict diagnostics;
- Lenovo software list;
- raw sensor/provider details;
- hardware validation reports;
- update channel;
- startup/service settings;
- profile import/export;
- experimental keyboard effects.

## Advanced window

Undocked, resizable normal window with:

- Performance
- Fans
- Display
- Keyboard
- Battery
- Lenovo Software
- Device
- Settings

Use one consistent left navigation icon set. Advanced may use more whitespace than the compact popup but should still feel dense enough to be a system utility.

When the compact surface is undocked, provide a clear thin grab area and remember window placement.

## Unsupported features

Do not render a clickable control that does nothing. Prefer:

- omit the control; or
- show a concise unavailable reason when the missing capability itself matters.

Examples:

```text
Fan RPM
Requires hardware access
[ Install ]
```

or:

```text
Keyboard backlight
Lenovo Power Management not detected
[ Open Lenovo Drivers ]
```

## Fan curve editor

Use discrete state labels (`Auto`, `1` ... `7`). If a graph is present, it visualizes a table and snaps to the real supported states. Never present `47.3% fan` for the X9 EC backend.

## Interaction polish

Production polish should come from behavior, not decorative texture:

- 120-180 ms subtle state transitions;
- immediate selected-state feedback;
- skeleton/progress state only for genuinely asynchronous hardware queries;
- no spinner for sub-100 ms actions;
- optimistic UI only where rollback is safe;
- accessible tooltips for source/provider details;
- no button should appear enabled while its backend is unavailable.
