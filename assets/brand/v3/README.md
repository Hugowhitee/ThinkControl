# ThinkControl exact asset pack v3

Canonical production branding imported from the approved `ThinkControl_TC_Exact_Asset_Pack_v3` package.

The TC geometry is vector-traced from the approved reference. Do not redraw, simplify or replace the C/dot geometry with a generated approximation.

This repository keeps the **production source subset** required by ThinkControl rather than duplicating every generated PNG size from the original archive:

- `master/ThinkControl_TC_mark.svg` is the exact traced TC master used for WPF geometry verification;
- `wordmark/` contains the exact outlined dark/light wordmarks used by GitHub;
- `windows/ThinkControl.ico` is the exact multi-resolution application/installer icon;
- `windows/ThinkControl_mark.ico` is the exact multi-resolution tray mark;
- `status/status-colors.json` preserves the approved status-dot palette for future state-aware tray assets;
- `verification.json` records the canonical production mappings.

Production copies are intentionally byte-identical:

```text
assets/brand/v3/windows/ThinkControl.ico
  -> src/ThinkControl.UI/Assets/ThinkControl.ico

assets/brand/v3/windows/ThinkControl_mark.ico
  -> src/ThinkControl.UI/Assets/tray.ico

assets/brand/v3/wordmark/ThinkControl_wordmark_outlined_dark.svg
  -> docs/assets/thinkcontrol-logo-dark.svg

assets/brand/v3/wordmark/ThinkControl_wordmark_outlined_light.svg
  -> docs/assets/thinkcontrol-logo-light.svg
```

Packaging CI hashes these pairs and fails on any drift. It also rejects the former hand-drawn 64×64 WPF TC geometry; `BrandMark.xaml` must retain the exact 1536×1536 traced master geometry.
