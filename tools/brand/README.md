# ThinkControl v3 brand exports

`assets/brand/v3/` remains the approved source of truth. This tool creates a **derived pack**, not a new logo or another master. Nothing here modifies current UI, installer, tray, or documentation asset mappings; public naming may change in the future (see issue #119).

```bash
python -m pip install Pillow CairoSVG
python tools/brand/build_pack.py --out artifacts/brandpack
python tools/brand/build_pack.py --out artifacts/brandpack --verify-only
python -m unittest discover tools/brand
```

On Windows, Inkscape can be used as an SVG renderer instead of CairoSVG; Pillow is still required. All exports live in `artifacts/` and are omitted from source control. Output includes original mark, dark-background and light-background vectors, both existing wordmarks, transparent and background raster PNGs, app/tray Windows ICOs with native 16/20/24/32/40/48/64/96/128/256 px frames, unchanged legacy setup ICO, browser SVG/ICO/PNG favicons, and touch/PWA formats. `manifest.json` records source provenance, every file's SHA-256, and the byte-preserved approved 256px icon frame.

The web subdirectory is a **capability export**, not an instruction to install browser favicons into a Windows application. A final new public product name and identity must go through the separate identity approval process before production mapping.