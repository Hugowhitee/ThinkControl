# Editing the UI

ThinkControl uses WPF and XAML. Layout and visual work should remain separate from hardware-provider code whenever practical.

## Recommended tools

Use Visual Studio with the `.NET desktop development` workload. Blend for Visual Studio is included and provides the most useful visual editor for WPF.

Open `ThinkControl.slnx`, then open the relevant `.xaml` file in the XAML Designer or use `View > Design in Blend`.

## Main UI files

- `src/ThinkControl.UI/MainWindow.xaml`: compact tray window
- `src/ThinkControl.UI/AdvancedWindow.xaml`: Advanced window and navigation
- `src/ThinkControl.UI/Controls/KeyboardEffectsPanel.xaml`: keyboard effects
- `src/ThinkControl.UI/Controls/BatteryTelemetryPanel.xaml`: battery telemetry
- `src/ThinkControl.UI/Controls/DiagnosticsPanel.xaml`: compatibility and diagnostics settings
- `src/ThinkControl.UI/App.xaml`: shared colors, styles and controls

The larger panels are separate `UserControl` files so they can be edited independently.

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

## UI snapshots

CI builds the real WPF application and runs `tools/ThinkControl.Snapshots`. The snapshot job renders the compiled XAML with design-time telemetry and uploads the `ThinkControl-UI-Snapshots` artifact.

The snapshots cover the compact window and major Advanced pages in the supported themes. They are used to catch clipping, spacing regressions, missing icons and theme problems.

Snapshot telemetry is sample data only and must not be described as data captured from a real laptop.

## Recommended workflow

1. Create a branch for the UI change.
2. Edit the XAML in Visual Studio or Blend.
3. Run the application locally.
4. Check compact and Advanced layouts at common Windows scaling levels.
5. Inspect the CI snapshots.
6. Keep the change limited to the intended XAML and style files where possible.
7. Merge only after the full solution and installer checks pass.

See [Design System](DESIGN.md) for layout and visual rules.
