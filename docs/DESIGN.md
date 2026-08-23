# UI design rules

ThinkControl should feel like a precise Windows utility, not a gaming dashboard and not an AI-generated collection of cards.

## Visual language

Use:

- Segoe UI / system typography
- Lucide-style consistent line icons
- system accent color
- 1 px borders and separators
- roughly 2-4 px corner radii
- compact spacing
- clear selected states
- system/light/dark themes
- keyboard navigation and visible focus

Avoid:

- gradients
- giant rounded cards
- card-inside-card layouts
- decorative gauges/speedometers
- oversized headings and empty padding
- emoji as product icons
- random icon styles
- many accent colors
- fake precision

## Compact popup

Starts from the tray and opens near the bottom-right of the active taskbar area.

Target hierarchy:

```text
ThinkControl                    44 C   2050 RPM
ThinkPad X9-15 Gen 1 · Quiet

[ Quiet ] [ Balanced ] [ Performance ]

thin 60-second temperature sparkline

Display        Auto / 60 / max
Brightness     slider
Keyboard       Auto
Battery        78%

Advanced
```

Temperature and RPM values must carry exact source metadata on hover/details.

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

When the compact surface is undocked, provide a clear thin grab area and remember window placement.

## Unsupported features

Do not render a clickable control that does nothing. Prefer:

- omit the control; or
- show a concise unavailable reason when the missing capability itself matters.

## Fan curve editor

Use discrete state labels (`Auto`, `1` ... `7`). If a graph is present, it visualizes a table and snaps to the real supported states. Never present `47.3% fan` for the X9 EC backend.
