# Editing the UI

ThinkControl uses WPF and XAML. Layout and visual work should remain separate from hardware-provider code whenever practical.

## Recommended tools

Use Visual Studio with the `.NET desktop development` workload. Blend for Visual Studio is included and provides the most useful visual editor for WPF.

Open `ThinkControl.slnx`, then open the relevant `.xaml` file in the XAML Designer or use `View > Design in Blend`.

## Main UI files

- `src/ThinkControl.UI/Controls/CompactDashboard.xaml`: the actual 410 × 640 tray flyout UI;
- `src/ThinkControl.UI/MainWindow.xaml`: minimal non-taskbar host for `CompactDashboard`;
- `src/ThinkControl.UI/AdvancedWindow.xaml`: Advanced window shell and static pages;
- `src/ThinkControl.UI/Controls/FansPanel.xaml`: cooling, fan telemetry and advanced fan controls;
- `src/ThinkControl.UI/Controls/KeyboardEffectsPanel.xaml`: keyboard effects;
- `src/ThinkControl.UI/Controls/BatteryTelemetryPanel.xaml`: battery telemetry;
- `src/ThinkControl.UI/Controls/TouchpadPanel.xaml`: Precision Touchpad gestures and haptics;
- `src/ThinkControl.UI/Controls/DiagnosticsPanel.xaml`: compatibility and diagnostics settings;
- `src/ThinkControl.UI/App.xaml`: shared colors, styles and controls.

The larger panels are separate `UserControl` files so they can be edited independently. Do not recreate a second compact dashboard inside `MainWindow.xaml`; alpha.4 intentionally removed that legacy duplicate.

## Visual-only changes

Typical UI-only changes include:

- margin and padding;
- width and height;
- Grid row and column sizing;
- font size and weight;
- corner radius;
- colors through shared resources;
- icon size and placement;
- spacing and alignment;
- layout grouping.

## Elements tied to behavior

Take care when changing:

- elements with `x:Name`;
- event handlers such as `Click`, `Checked` and `ValueChanged`;
- `Tag` values used for navigation or actions;
- binding expressions;
- `GroupName` values on related controls.

Renaming or removing these may require a corresponding code change.

## XAML ownership

Normal layout should be declared in XAML. Code-behind should manage behavior rather than construct ordinary cards, labels and controls dynamically unless there is a clear runtime reason.

Hardware safety does not belong in XAML. Device gates, fan ranges, privileged operations and provider validation remain enforced in the service and hardware layers even when the UI is modified.

## Alignment rules

Advanced pages share one literal left content rail. Do not give individual pages their own centered `MaxWidth` behavior; wide-screen unused space belongs on the right. Long pages use the shared themed scrollbar and reserve a small right gutter so header actions cannot disappear underneath it.

Compact is a fixed 410 × 640 surface. Changes to its runtime dimensions must also update the snapshot renderer in the same change.

## UI snapshots

CI builds the real WPF application and runs `tools/ThinkControl.Snapshots`. The job uploads the `ThinkControl-Visual-QA` artifact.

The current matrix covers Compact plus every Advanced page at 1160 × 760, minimum 980 × 650 and wide 1720 × 980, with selected light/offline states. Use it to catch clipping, spacing regressions, missing icons, page recentering and theme problems.

Snapshot telemetry is deterministic sample data only and must not be described as data captured from a real laptop. Generated screenshots stay in CI artifacts rather than a permanent repository branch.

## Recommended workflow

1. Create a short-lived branch for the UI change.
2. Edit the relevant XAML in Visual Studio or Blend.
3. Run the application locally.
4. Check Compact and Advanced layouts at common Windows scaling levels.
5. Inspect the CI visual-QA artifact at normal, minimum and wide sizes.
6. Keep the change limited to the intended UI/style files where practical.
7. Merge only after the full solution and installer checks pass; merged branches are then removed automatically.

See [Design System](DESIGN.md) and [Visual QA](VISUAL-QA.md) for the shared rules.
