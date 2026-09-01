import errno
import importlib.util
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / 'scripts' / 'update_device_catalogs.py'
FIXTURES = ROOT / 'tests' / 'fixtures'

spec = importlib.util.spec_from_file_location('update_device_catalogs', SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


class CrossVolumePublishTest(unittest.TestCase):
    def test_fixture_generation_survives_cross_volume_replace(self):
        real_replace = os.replace

        def windows_like_replace(src, dst):
            src_parent = Path(src).resolve().parent
            dst_parent = Path(dst).resolve().parent
            if src_parent != dst_parent:
                raise OSError(errno.EXDEV, 'The system cannot move the file to a different disk drive')
            return real_replace(src, dst)

        with tempfile.TemporaryDirectory(prefix='bebel_test_out_') as td:
            outdir = Path(td) / 'Catalogs'
            argv = [
                str(SCRIPT),
                '--google-input', str(FIXTURES / 'google_supported_devices_sample.html'),
                '--apple-input', str(FIXTURES / 'ipsw_devices_sample.json'),
                '--output-dir', str(outdir),
            ]
            with patch.object(mod.os, 'replace', side_effect=windows_like_replace), \
                 patch.object(sys, 'argv', argv):
                rc = mod.main()

            self.assertEqual(0, rc)
            self.assertTrue((outdir / 'android_devices.json').is_file())
            self.assertTrue((outdir / 'apple_devices.json').is_file())
            self.assertTrue((outdir / 'catalog-manifest.json').is_file())
            self.assertEqual((2, 2), mod.validate_dir(outdir))


if __name__ == '__main__':
    unittest.main()
