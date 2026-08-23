# Editing the ThinkControl UI visually

ThinkControl uses **WPF + XAML**. The UI should stay designer-friendly so layout and visual polish can be changed without touching the hardware backend.

## Recommended editor

Use **Blend for Visual Studio** when you want the closest thing to a visual UI editor.

Install Visual Studio with the **.NET desktop development** workload. Blend is included. Open `ThinkControl.slnx`, then either:

- open a `.xaml` file in the Visual Studio XAML Designer; or
- use **View → Design in Blend…** for the richer visual designer.

The visual surface and XAML source represent the same UI. Moving or resizing supported controls updates the XAML, and XAML edits update the preview.

## Main files to edit

- `src/ThinkControl.UI/MainWindow.xaml` — compact tray popup
- `src/ThinkControl.UI/AdvancedWindow.xaml` — full advanced window
- `src/ThinkControl.UI/App.xaml` — shared colors, buttons, segments, sliders and other styles

Avoid changing hardware or service projects for visual-only work.

## Safe visual changes

These are generally UI-only:

- margins and padding
- widths and heights
- Grid row/column sizes
- font size and weight
- corner radius
- colors through the `Tc.*` resources
- icon size and placement
- card spacing
- text alignment
- layout grouping

## Be careful with

Do not rename or remove these without updating code-behind as well:

- elements with `x:Name`
- `Click`, `Checked`, `ValueChanged`, or other event handlers
- `Tag` values used for navigation or hardware actions
- `{Binding ...}` expressions
- `GroupName` values on related radio buttons

Those are the links between the visual XAML and ThinkControl behavior.

## Design rule for ThinkControl

New UI should be declared in XAML whenever practical. C# code-behind should control behavior, not construct normal layout dynamically. This keeps the application editable in Blend and makes visual changes reviewable as XAML diffs.

Hardware safety does **not** belong in XAML. Fan ranges, device verification, privileged service rules and capability gates remain enforced in the hardware/service layers even if the UI is modified.

## Suggested workflow

1. Create a Git branch for the UI change.
2. Open the XAML in Blend.
3. Adjust the layout visually.
4. Run ThinkControl and check the compact and advanced windows at 100%, 125% and 150% Windows scaling.
5. Commit only the intended XAML/style changes.
6. Let CI build the complete solution before merging.

This makes it possible to experiment with the visual design without weakening the X9 hardware protections.
