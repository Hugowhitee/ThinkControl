# Visual QA

ThinkControl renders the real WPF interface into deterministic screenshots so layout changes can be reviewed at fixed viewports instead of relying on memory or one developer machine.

## Local review

From the repository root on Windows:

```powershell
.\tools\visual-qa.ps1
```

Use `-NoBuild` when the solution is already built and `-NoOpen` when only the files are needed. Output is generated under `artifacts/` and is ignored by Git.

## CI matrix

The snapshot renderer covers:

- Compact at its production fixed size, including battery/charging and Audio Safety states;
- every Advanced page at minimum, normal and wide viewports;
- selected light-theme states, including Compact, Advanced Home and Settings mode surfaces;
- important provider unavailable/offline states;
- hardware setup and diagnostics states;
- temporary manual fan-test safety UI;
- Touchpad normal, Track-center and active corner-launch states;
- startup/loading, the first-seen update decision popup and gesture OSD surfaces.

Snapshot telemetry is deterministic fixture data. It proves rendering/state composition only and must never be described as physical hardware evidence.

## Review contract

Generating PNG files is not enough. Material UI work requires visual inspection of the Actions `ThinkControl-Visual-QA` artifact.

Check at minimum:

1. no clipping or horizontal escape at the minimum viewport;
2. shared Advanced left rail and spacing remain consistent at normal/wide widths;
3. cards and typography do not stretch or compress awkwardly;
4. controls, icons, selected/disabled states and hit areas match the shared design language;
5. unsupported/provider-unavailable states are understandable and cannot look writable;
6. Compact stays clear and dense without hiding important state;
7. Touchpad visible gesture zones match the recognizer's real physical hit geometry;
8. dark/light contrast and hierarchy remain usable;
9. startup and Compact ↔ Advanced transitions always provide an immediately painted surface;
10. overlays/popups intended to block interaction appear above Compact as well as Advanced.
11. Compact/Advanced launch-mode labels use one vocabulary, and Audio Safety Normal / Gesture lock / Silent remain legible without footer clipping in both themes.
12. Advanced Home's Audio Safety card, fan Auto row and More-profiles affordance remain aligned and understandable at minimum, normal and light-theme states; Auto must visibly disable competing fan presets; Battery health-trend explanation must not collide with the chart/status copy.
13. Battery Preservation must use one clean current-level fill with two precisely aligned threshold markers. Fill color may react to charging/limit state, but permanent green/amber/red bands, lightning/pause glyphs, ruler ticks and lock icons must not return. The comparative wear-cycle sentence must stay secondary, readable and unclipped in dark/light themes.
14. Theme changes must repaint shell-mode labels and native Advanced caption colors immediately without minimize/reopen.
15. The first-seen update popup must show the full transition/copy and both **Install now** / **Later** actions without clipping or implying an automatic install.

## Artifact ownership

Generated screenshots and galleries are CI artifacts, not repository source. Do not commit visual-QA PNG dumps under `docs/`, create screenshot-only branches, or preserve old release screenshots as permanent documentation.

The public release contains one composed `ui-overview.png`; the full QA matrix stays attached to the workflow run that produced it.
