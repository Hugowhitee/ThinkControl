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

- Compact tray surface at its fixed 410 × 640 size.
- Advanced pages at 1160 × 760.
- Critical Advanced pages at the minimum supported 980 × 650 viewport.
- Selected pages at 1720 × 980 to catch over-stretching on wide windows.
- Charging and on-battery Compact states.
- Hardware-ready and hardware-service-offline states.
- Dark and selected light-theme states.

The renderer uses a deterministic demo `AppState`. It must never be mistaken for physical hardware validation; its purpose is layout, hierarchy, clipping and state-visibility review.

## Pull requests

CI builds/tests the solution and uploads a `ThinkControl-Visual-QA` artifact containing:

- every PNG snapshot;
- `gallery.html` for a single-page visual review;
- `README.md` as a GitHub-friendly image gallery;
- `manifest.json` with surface, state and viewport metadata.

When a UI-related PR changes, review the artifact before merge. This is the preferred way to catch regressions such as content extending past the right edge, controls becoming too wide, misaligned cards, clipped text or inconsistent empty states.

## Main preview

After a green change reaches `main`, the `Publish Visual Preview` workflow renders the same matrix and replaces the generated `visual-main` branch. The repository README embeds selected images from that branch, so the public preview follows the current shipped UI without manually copying screenshots into source control.

The `visual-main` branch is generated output only. Do not hand-edit it.

## Review checklist

For every material UI change, check:

1. no horizontal clipping at 980 × 650;
2. cards do not become unnecessarily stretched on wide windows;
3. titles, icons and action rows align to the same visual grid;
4. disabled/unavailable hardware states remain understandable;
5. Compact communicates the current battery/power/hardware state without looking busy;
6. interactive charts reserve readable axis and hover-label space;
7. dark and light surfaces retain contrast and hierarchy;
8. navigation and branding remain visually consistent between Compact and Advanced.
