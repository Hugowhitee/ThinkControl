#!/usr/bin/env python3
"""Build a reproducible preview/export pack from the approved ThinkControl v3 assets.

This does not change the runtime icon mappings or publish a new brand identity.
Run: python tools/brand/build_pack.py --out artifacts/brandpack
Requires: Pillow; and either CairoSVG, Inkscape or rsvg-convert for SVG rendering.
"""
from __future__ import annotations
import argparse
import hashlib
import io
import json
from pathlib import Path
import shutil
import struct
import subprocess
from tempfile import TemporaryDirectory
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile

from PIL import Image, ImageChops

SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
NS = "{http://www.w3.org/2000/svg}"
ET.register_namespace("", "http://www.w3.org/2000/svg")


def render(svg: Path, size: int) -> Image.Image:
    """Rasterize the actual SVG, never upscale a raster as the vector master."""
    try:
        import cairosvg
        node=ET.parse(svg).getroot()
        x,y,w,h=(float(v) for v in node.attrib["viewBox"].replace(",", " ").split())
        height=max(1,round(size*h/w))
        data=cairosvg.svg2png(url=str(svg), output_width=size, output_height=height)
        return Image.open(io.BytesIO(data)).convert("RGBA")
    except (ImportError, OSError) as exc:
        for exe, args in (
            ("inkscape", lambda f: ["--export-type=png", f"--export-width={size}", f"--export-filename={f}", str(svg)]),
            ("rsvg-convert", lambda f: ["-w", str(size), "-o", f, str(svg)]),
        ):
            if shutil.which(exe):
                with TemporaryDirectory() as t:
                    out = str(Path(t) / "render.png")
                    subprocess.run([exe, *args(out)], check=True, capture_output=True)
                    return Image.open(out).convert("RGBA")
        raise RuntimeError("SVG rasterizer unavailable: install CairoSVG or Inkscape") from exc


def filehash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ico_frames(path: Path) -> dict[int, bytes]:
    data = path.read_bytes()
    if len(data) < 6 or data[:4] != b"\0\0\1\0":
        raise ValueError(f"Invalid ICO: {path}")
    count = struct.unpack_from("<H", data, 4)[0]
    result = {}
    for n in range(count):
        w, h, colors, reserved, planes, bpp, length, offset = struct.unpack_from("<BBBBHHII", data, 6 + n * 16)
        if w not in (0, h) or reserved or offset + length > len(data):
            raise ValueError(f"Broken ICO directory: {path}")
        pixels = 256 if w == 0 else w
        blob = data[offset:offset+length]
        im = Image.open(io.BytesIO(blob))
        if im.size != (pixels, pixels):
            raise ValueError(f"ICO frame size mismatch: {path}")
        result[pixels] = blob
    return result


def write_ico(path: Path, frames: dict[int, bytes]):
    order = sorted(frames)
    header = struct.pack("<HHH", 0, 1, len(order))
    offset = 6 + 16 * len(order)
    directory, payload = [], []
    for size in order:
        if size > 256 or size < 1:
            raise ValueError("ICO size outside 1–256")
        blob = frames[size]
        directory.append(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(blob), offset))
        payload.append(blob)
        offset += len(blob)
    path.write_bytes(header + b"".join(directory + payload))


def png_bytes(im: Image.Image) -> bytes:
    buf = io.BytesIO()
    im.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def with_black_tile(im: Image.Image) -> Image.Image:
    out = Image.new("RGBA", im.size, (0, 0, 0, 255))
    out.alpha_composite(im)
    return out


def make_variants(source: Path, out: Path) -> tuple[Path, Path, Path]:
    originals = out / "vector"
    originals.mkdir(parents=True, exist_ok=True)
    source_mark = source / "master" / "ThinkControl_TC_mark.svg"
    mark = originals / "mark-original.svg"
    shutil.copyfile(source_mark, mark)
    root = ET.parse(source_mark).getroot()
    marks = root.findall(f"{NS}path")
    dots = root.findall(f"{NS}circle")
    if len(marks) != 1 or len(dots) != 1 or marks[0].get("fill", "").upper() != "#FFFFFF":
        raise ValueError("Unexpected v3 geometry: do not auto-guess recolouring")
    marks[0].set("fill", "#16181C")
    light = originals / "mark-on-light.svg"
    ET.ElementTree(root).write(light, encoding="utf-8", xml_declaration=True)
    for key in ("dark", "light"):
        shutil.copyfile(source / "wordmark" / f"ThinkControl_wordmark_{key}.svg",
                        originals / f"wordmark-{key}.svg")
    # Black tiled version matches the approved Windows application icon exactly.
    tile_root = ET.parse(source_mark).getroot()
    tile_root.insert(1, ET.Element(f"{NS}rect", {"x": "0", "y": "0", "width": "1536", "height": "1536", "fill": "#000000"}))
    tile = originals / "mark-on-black-tile.svg"
    ET.ElementTree(tile_root).write(tile, encoding="utf-8", xml_declaration=True)
    mask_root = ET.parse(source_mark).getroot()
    mask_root.insert(1, ET.Element(f"{NS}rect", {"x":"0","y":"0","width":"1536","height":"1536","fill":"#000000"}))
    g = ET.Element(f"{NS}g", {"transform":"translate(230.4 230.4) scale(0.70)"})
    for el in list(mask_root):
        if el.tag not in (f"{NS}title", f"{NS}rect"):
            mask_root.remove(el)
            g.append(el)
    mask_root.append(g)
    mask = originals / "mark-maskable.svg"
    ET.ElementTree(mask_root).write(mask, encoding="utf-8", xml_declaration=True)
    return mark, tile, mask


def build(source: Path, out: Path):
    if not (source / "master" / "ThinkControl_TC_mark.svg").is_file():
        raise ValueError("Pass the canonical assets/brand/v3 directory")
    out.mkdir(parents=True, exist_ok=True)
    svg, tiled, masked = make_variants(source, out)
    raster, win, web = (out / p for p in ("raster", "windows", "web"))
    for p in (raster, win, web): p.mkdir(exist_ok=True)
    for name, vector, widths in (
        ("mark-transparent", svg, (256, 512, 1024, 2048)),
        ("mark-on-light", out / "vector" / "mark-on-light.svg", (512,1024)),
        ("mark-on-black", tiled, (256,512,1024,2048)),
        ("wordmark-dark", out / "vector" / "wordmark-dark.svg", (1024,2048)),
        ("wordmark-light", out / "vector" / "wordmark-light.svg", (1024,2048)),
    ):
        for width in widths:
            im = render(vector,width)
            # Wordmarks keep the SVG's original 455:144 aspect ratio.
            im.save(raster / f"{name}-{width}.png", optimize=True)

    approved = source / "windows" / "ThinkControl.ico"
    approved_frame = ico_frames(approved)
    if set(approved_frame) != {256}:
        raise ValueError("Approved v3 source ICO changed: inspect before migration")
    if png_bytes(with_black_tile(render(svg,256))) != approved_frame[256]:
        # Different PNG encoding is acceptable; exact decoded pixels are NOT negotiable.
        a = Image.open(io.BytesIO(approved_frame[256])).convert("RGBA")
        b = with_black_tile(render(svg,256))
        if ImageChops.difference(a,b).getbbox():
            raise ValueError("256 px source icon no longer matches approved SVG geometry")
    icons = {}
    for n in SIZES:
        icons[n] = approved_frame[256] if n == 256 else png_bytes(with_black_tile(render(svg,n)))
    write_ico(win / "ThinkControl-app-multisize.ico", icons)
    write_ico(win / "ThinkControl-tray-multisize.ico", icons)
    shutil.copyfile(source / "windows" / "ThinkControl_setup.ico", win / "ThinkControl-setup-legacy-validated.ico")

    shutil.copyfile(tiled, web / "favicon.svg")
    for s in (16,32,48):
        (web / f"favicon-{s}.png").write_bytes(icons[s])
    write_ico(web / "favicon.ico", {s:icons[s] for s in (16,32,48)})
    for s in (180,192,512):
        render(tiled,s).save(web / ("apple-touch-icon.png" if s==180 else f"icon-{s}.png"),optimize=True)
    render(masked,512).save(web / "icon-512-maskable.png", optimize=True)
    (web / "site.webmanifest").write_text(json.dumps({
        "name":"ThinkControl", "short_name":"ThinkControl", "display":"standalone",
        "background_color":"#000000", "theme_color":"#000000",
        "icons":[
            {"src":"icon-192.png","sizes":"192x192","type":"image/png"},
            {"src":"icon-512.png","sizes":"512x512","type":"image/png"},
            {"src":"icon-512-maskable.png","sizes":"512x512","type":"image/png","purpose":"maskable"},
        ]},indent=2) + "\n",encoding="utf-8")
    metadata = {"identity":"ThinkControl v3 (existing legacy public name; rename decision open)",
        "origin":"assets/brand/v3", "approved_source_svg_sha256":filehash(source / "master" / "ThinkControl_TC_mark.svg"),
        "approved_256_icon_sha256":hashlib.sha256(approved_frame[256]).hexdigest(),
        "windows_sizes":list(SIZES),
        "web_only":"Web files are optional, not mapped to the current Windows runtime",
        "files":{str(p.relative_to(out)):filehash(p) for p in sorted(out.rglob('*')) if p.is_file() and p.name!='manifest.json'}}
    (out/"manifest.json").write_text(json.dumps(metadata,indent=2)+"\n",encoding="utf-8")
    verify(source,out)
    archive=out.parent/(out.name+".zip")
    with ZipFile(archive,"w",ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file(): z.write(p,arcname=p.relative_to(out))
    return archive


def verify(source: Path, out: Path):
    meta=json.loads((out/"manifest.json").read_text(encoding="utf-8"))
    if meta["approved_source_svg_sha256"]!=filehash(source/"master"/"ThinkControl_TC_mark.svg"):
        raise ValueError("Canonical master drift")
    for relative,digest in meta["files"].items():
        p=out/relative
        if not p.is_file() or filehash(p)!=digest: raise ValueError(f"Export drift: {relative}")
    frames=ico_frames(out/"windows"/"ThinkControl-app-multisize.ico")
    if sorted(frames)!=list(SIZES): raise ValueError("Missing Windows frames")
    approved=ico_frames(source/"windows"/"ThinkControl.ico")[256]
    if frames[256]!=approved: raise ValueError("256px frame is not exact approved pixel source")
    if sorted(ico_frames(out/"web"/"favicon.ico"))!=[16,32,48]:raise ValueError("Web favicon frames")
    for name in ["mark-transparent-2048.png","wordmark-light-2048.png","mark-on-black-2048.png"]:
        with Image.open(out/"raster"/name) as im:
            if im.width!=2048:raise ValueError(f"Unexpected PNG width: {name}")
    return True


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    root=Path(__file__).resolve().parents[2]
    parser.add_argument("--source",type=Path,default=root/"assets/brand/v3")
    parser.add_argument("--out",type=Path,default=root/"artifacts/brandpack")
    parser.add_argument("--verify-only",action="store_true")
    args=parser.parse_args()
    archive = None if args.verify_only else build(args.source,args.out)
    if args.verify_only:verify(args.source,args.out)
    print(f"Verified brandpack: {args.out}")
    if archive:print(f"ZIP: {archive}")