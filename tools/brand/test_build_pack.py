"""Offline brand export invariants: run via python -m unittest discover tools/brand."""
from pathlib import Path
import tempfile
import unittest
import json
from PIL import Image
from build_pack import build, verify, ico_frames, SIZES, filehash

SOURCE = Path(__file__).resolve().parents[2] / "assets" / "brand" / "v3"


@unittest.skipUnless(SOURCE.exists(), "Canonical ThinkControl v3 source not present")
class ApprovedBrandExportTests(unittest.TestCase):
    def test_export_and_verification(self):
        with tempfile.TemporaryDirectory() as t:
            out=Path(t)/"pack"
            archive=build(SOURCE,out)
            self.assertTrue(archive.is_file())
            self.assertTrue(verify(SOURCE,out))
            self.assertEqual(set(ico_frames(out/"windows"/"ThinkControl-app-multisize.ico")),set(SIZES))
            for n in SIZES:
                with Image.open(out/"windows"/"ThinkControl-app-multisize.ico") as im:
                    im.size=(n,n)
                    self.assertEqual(im.copy().size,(n,n))
            self.assertEqual(ico_frames(out/"windows"/"ThinkControl-app-multisize.ico")[256],
                             ico_frames(SOURCE/"windows"/"ThinkControl.ico")[256])
            self.assertEqual(set(ico_frames(out/"web"/"favicon.ico")),{16,32,48})
            for name in ["wordmark-dark-2048.png","wordmark-light-2048.png"]:
                with Image.open(out/"raster"/name) as im:
                    self.assertEqual(im.width,2048)
                    self.assertLess(im.height,2048)
                    self.assertEqual(im.mode,"RGBA")
            self.assertTrue((out/"vector"/"mark-on-light.svg").is_file())
            web=json.loads((out/"web"/"site.webmanifest").read_text())
            self.assertEqual(len(web["icons"]),3)

    def test_checksum_detects_manual_drift(self):
        with tempfile.TemporaryDirectory() as t:
            out=Path(t)/"pack"
            build(SOURCE,out)
            f=out/"vector"/"mark-original.svg"
            f.write_text(f.read_text()+"<!--tamper-->")
            with self.assertRaisesRegex(ValueError,"Export drift"):
                verify(SOURCE,out)

if __name__ == "__main__":
    unittest.main()