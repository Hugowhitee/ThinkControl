# ThinkControl exact asset pack v3

Canonical production branding imported from the approved `ThinkControl_TC_Exact_Asset_Pack_v3` package.

The TC geometry is vector-traced from the approved reference. Do not redraw, simplify or replace the C/dot geometry with a generated approximation.

This repository keeps only the production source subset ThinkControl currently needs:

- `master/ThinkControl_TC_mark.svg` — traced TC master used for WPF geometry verification;
- `wordmark/ThinkControl_wordmark_dark.svg` and `ThinkControl_wordmark_light.svg` — canonical repository/app wordmarks;
- `windows/ThinkControl.ico` — canonical multi-resolution Windows icon;
- `windows/ThinkControl_mark.ico` — Windows tray compatibility alias, intentionally byte-identical to `ThinkControl.ico` in alpha.4;
- `status/status-colors.json` — approved status-dot palette reserved for future state-aware assets;
- `verification.json` — production mapping metadata.

Alpha.4 intentionally uses the same proven multi-resolution icon on every Windows shell surface. The earlier tray-only raster produced a visibly broken 16 px Notification Area result, so it is no longer a separate production artwork.

Production mappings:

```text
assets/brand/v3/windows/ThinkControl.ico
  -> src/ThinkControl.UI/Assets/ThinkControl.ico

assets/brand/v3/windows/ThinkControl_mark.ico
  -> src/ThinkControl.UI/Assets/tray.ico
```

Both `.ico` files above are byte-identical in alpha.4. Packaging CI verifies these mappings and also rejects the former hand-drawn 64 × 64 WPF TC geometry; `BrandMark.xaml` must retain the exact 1536 × 1536 traced master geometry.
