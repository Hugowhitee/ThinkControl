# Visual QA

ThinkControl renders its real WPF interface into deterministic screenshots so UI changes can be reviewed at the same viewport sizes every time.

## Local review

From the repository root on Windows:

```powershell
.\tools\visual-qa.ps1
```

The command restores/builds ThinkControl, renders the snapshot matrix into `artifacts/visual-qa`, and opens `gallery.html`.

Use `-NoBuild` when the solution is already built and `-NoOpen` when only the files are needed.

## Snapshot matrix

Visual QA intentionally covers more than one comfortable desktop size:

- Compact tray surface at its fixed 410 × 640 runtime size.
- Every Advanced page at 1160 × 760.
- Every Advanced page at the minimum supported 980 × 650 viewport.
- Every Advanced page at 1720 × 980 to catch wide-screen recentering or over-stretching.
- Charging and on-battery Compact states.
- Hardware-ready and hardware-service-offline states.
- Dark and selected light-theme states.

The renderer uses a deterministic demo `AppState`. It must never be mistaken for physical hardware validation; its purpose is layout, hierarchy, clipping and state-visibility review.

## Pull requests and main

CI builds/tests the solution and uploads a `ThinkControl-Visual-QA` artifact containing:

- every PNG snapshot;
- `gallery.html` for a single-page visual review;
- `README.md` as a GitHub-friendly image gallery;
- `manifest.json` with surface, state and viewport metadata.

Review the artifact before merging material UI changes. This is the preferred way to catch regressions such as content extending past the right edge, controls becoming too wide, misaligned cards, clipped text or inconsistent empty states.

Generated screenshots are intentionally **not committed to a permanent branch**. The former `visual-main` publishing workflow was removed in alpha.4; source branches now contain source only, while visual output stays attached to the CI run that produced it.

## Review checklist

For every material UI change, check:

1. no horizontal clipping at 980 × 650;
2. every page keeps the same left content rail at 1160 and 1720 widths;
3. cards do not become unnecessarily stretched on wide windows;
4. titles, icons and action rows align to the same visual grid;
5. disabled/unavailable hardware states remain understandable;
6. Compact communicates battery, cooling and hardware state without looking busy;
7. interactive charts reserve readable axis and hover-label space;
8. dark and light surfaces retain contrast and hierarchy;
9. navigation and branding remain visually consistent between Compact and Advanced.
