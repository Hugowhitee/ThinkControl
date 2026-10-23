# ThinkControl exact asset pack v3

Canonical production branding imported from the approved `ThinkControl_TC_Exact_Asset_Pack_v3` package.

The TC geometry is vector-traced from the approved reference. Do not redraw, simplify or replace the C/dot geometry with a generated approximation.

This repository keeps only the production source subset ThinkControl currently needs:

- `master/ThinkControl_TC_mark.svg` — traced TC master used for WPF geometry verification;
- `wordmark/ThinkControl_wordmark_dark.svg` and `ThinkControl_wordmark_light.svg` — canonical repository/app wordmarks;
- `windows/ThinkControl.ico` — approved 256 px single-frame Windows source icon (not yet multi-resolution);
- `windows/ThinkControl_mark.ico` — Windows tray compatibility alias, intentionally byte-identical to `ThinkControl.ico` in alpha.4;
- `status/status-colors.json` — approved status-dot palette reserved for future state-aware assets;
- `verification.json` — production mapping metadata.

The v3 production mappings intentionally use the same approved 256 px artwork. Windows scales that frame for smaller shell sizes; an earlier separate tray-only raster rendered incorrectly, so that artwork was retired. The approved source ICO itself still has only one 256 px frame.

Production mappings:

```text
assets/brand/v3/windows/ThinkControl.ico
  -> src/ThinkControl.UI/Assets/ThinkControl.ico

assets/brand/v3/windows/ThinkControl_mark.ico
  -> src/ThinkControl.UI/Assets/tray.ico
```

Both `.ico` files above are byte-identical in alpha.4. Packaging CI verifies these mappings and also rejects the former hand-drawn 64 × 64 WPF TC geometry; `BrandMark.xaml` must retain the exact 1536 × 1536 traced master geometry.

## Reproducible derived exports

`tools/brand/build_pack.py` generates optional vector, raster, Windows multi-frame and web/favicon/PWA exports under ignored `artifacts/brandpack/`. It validates SHA-256 outputs and keeps the approved 256 px frame byte-identical. See `tools/brand/README.md`. No live UI, tray, installer or browser asset mapping is changed pending Windows runtime acceptance. Product-family naming remains open in #119.
