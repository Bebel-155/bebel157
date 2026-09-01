import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "scripts" / "validate_driver_packages.py"
spec = importlib.util.spec_from_file_location("driver_validator", MODULE_PATH)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

def base_manifest(package):
    return {"SchemaVersion": 1, "GeneratedUtc": "2026-09-01T00:00:00Z", "Packages": [package]}

def write_manifest(root, data):
    (root / "drivers-manifest.json").write_text(json.dumps(data), encoding="utf-8")

def allowed_package(root):
    vendor = root / "Samsung"
    vendor.mkdir()
    binary = vendor / "driver.exe"
    binary.write_bytes(b"official-fixture")
    license_file = vendor / "LICENSE.txt"
    license_file.write_text("fixture license", encoding="utf-8")
    sha = hashlib.sha256(binary.read_bytes()).hexdigest()
    return {
        "Id": "samsung",
        "Manufacturer": "Samsung",
        "DisplayName": "Samsung USB Driver",
        "Version": "1",
        "PackageType": "Exe",
        "RelativePath": "Samsung/driver.exe",
        "SignatureRelativePath": "",
        "SilentInstallArguments": "",
        "RequiresElevation": True,
        "UsbVendorIds": ["04E8"],
        "HardwareIdPatterns": ["USB\\\\VID_04E8*"],
        "Sha256": sha,
        "SignaturePublisher": "Samsung",
        "SourceUrl": "https://developer.samsung.com/android-usb-driver",
        "RedistributionStatus": "Allowed",
        "LicenseFile": "Samsung/LICENSE.txt",
        "Priority": 10
    }

class DriverManifestValidationTests(unittest.TestCase):
    def test_valid_allowed_package(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            p = allowed_package(root)
            write_manifest(root, base_manifest(p))
            result = mod.validate(root)
            self.assertEqual(1, result["allowed"])

    def test_wrong_sha_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            p = allowed_package(root)
            p["Sha256"] = "0" * 64
            write_manifest(root, base_manifest(p))
            with self.assertRaisesRegex(ValueError, "SHA-256 divergente"):
                mod.validate(root)

    def test_duplicate_id_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            p = allowed_package(root)
            write_manifest(root, {"SchemaVersion": 1, "Packages": [p, dict(p)]})
            with self.assertRaisesRegex(ValueError, "Id duplicado"):
                mod.validate(root)

    def test_path_traversal_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            p = allowed_package(root)
            p["RelativePath"] = "../evil.exe"
            write_manifest(root, base_manifest(p))
            with self.assertRaisesRegex(ValueError, "fora de Drivers"):
                mod.validate(root)

    def test_unknown_binary_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            vendor = root / "Samsung"
            vendor.mkdir()
            binary = vendor / "driver.exe"
            binary.write_bytes(b"x")
            p = {
                "Id": "samsung",
                "Manufacturer": "Samsung",
                "DisplayName": "Samsung USB Driver",
                "PackageType": "Exe",
                "RelativePath": "Samsung/driver.exe",
                "RedistributionStatus": "Unknown"
            }
            write_manifest(root, base_manifest(p))
            with self.assertRaisesRegex(ValueError, "nao redistribuivel"):
                mod.validate(root)

    def test_undeclared_binary_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            (root / "rogue.exe").write_bytes(b"x")
            write_manifest(root, {"SchemaVersion": 1, "Packages": []})
            with self.assertRaisesRegex(ValueError, "nao declarado"):
                mod.validate(root)

if __name__ == "__main__":
    unittest.main()
