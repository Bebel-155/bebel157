#!/usr/bin/env python3
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

BINARY_EXTENSIONS = {".exe", ".msi", ".inf", ".cat", ".sys"}
ALLOWED_METADATA_NAMES = {"drivers-manifest.json", "LICENSE.txt", "SOURCE.txt", "README.txt"}

def fail(message):
    raise ValueError(message)

def safe_join(root: Path, relative: str) -> Path:
    if not relative:
        fail("caminho vazio")
    root = root.resolve()
    candidate = (root / relative).resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        fail(f"caminho fora de Drivers: {relative}")
    return candidate

def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()

def load_manifest(root: Path):
    manifest_path = root / "drivers-manifest.json"
    if not manifest_path.is_file():
        fail("drivers-manifest.json ausente")
    try:
        data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        fail(f"manifesto JSON invalido: {exc}")
    if data.get("SchemaVersion") != 1:
        fail("SchemaVersion deve ser 1")
    packages = data.get("Packages")
    if not isinstance(packages, list):
        fail("Packages deve ser uma lista")
    return data, packages

def validate(root: Path):
    root = root.resolve()
    data, packages = load_manifest(root)

    ids = set()
    declared_files = set()
    allowed_count = 0
    metadata_count = 0

    for p in packages:
        if not isinstance(p, dict):
            fail("entrada de pacote nao e objeto")
        pid = str(p.get("Id", "")).strip()
        if not pid:
            fail("pacote sem Id")
        key = pid.lower()
        if key in ids:
            fail(f"Id duplicado: {pid}")
        ids.add(key)

        manufacturer = str(p.get("Manufacturer", "")).strip()
        if not manufacturer:
            fail(f"Manufacturer ausente: {pid}")

        status = str(p.get("RedistributionStatus", "")).strip()
        if status not in {"Allowed", "NotAllowed", "Unknown"}:
            fail(f"RedistributionStatus invalido em {pid}: {status}")

        relative = str(p.get("RelativePath", "") or "").strip()
        signature_relative = str(p.get("SignatureRelativePath", "") or "").strip()
        license_relative = str(p.get("LicenseFile", "") or "").strip()

        if relative:
            package_path = safe_join(root, relative)
            declared_files.add(package_path)
        else:
            package_path = None

        if signature_relative:
            declared_files.add(safe_join(root, signature_relative))

        if license_relative:
            declared_files.add(safe_join(root, license_relative))

        if status != "Allowed":
            metadata_count += 1
            # Unknown/NotAllowed entries are catalog knowledge only. They must not ship binaries.
            for path in [package_path, safe_join(root, signature_relative) if signature_relative else None]:
                if path is not None and path.exists() and path.suffix.lower() in BINARY_EXTENSIONS:
                    fail(f"binario presente para pacote nao redistribuivel {pid}: {path.relative_to(root)}")
            continue

        allowed_count += 1
        if package_path is None:
            fail(f"pacote Allowed sem RelativePath: {pid}")
        if not package_path.is_file():
            fail(f"arquivo do pacote ausente {pid}: {relative}")

        sha = str(p.get("Sha256", "")).strip().lower()
        if not re.fullmatch(r"[0-9a-f]{64}", sha):
            fail(f"SHA-256 invalido: {pid}")
        actual = sha256_file(package_path)
        if actual != sha:
            fail(f"SHA-256 divergente {pid}: esperado {sha}, atual {actual}")

        source_url = str(p.get("SourceUrl", "")).strip()
        if not source_url.startswith(("https://", "http://")):
            fail(f"SourceUrl invalida/ausente: {pid}")

        publisher = str(p.get("SignaturePublisher", "")).strip()
        if not publisher:
            fail(f"SignaturePublisher ausente: {pid}")

        if not license_relative:
            fail(f"LicenseFile ausente: {pid}")
        license_path = safe_join(root, license_relative)
        if not license_path.is_file():
            fail(f"arquivo de licenca/origem ausente {pid}: {license_relative}")

        if signature_relative:
            sig_path = safe_join(root, signature_relative)
            if not sig_path.is_file():
                fail(f"arquivo de assinatura/catalogo ausente {pid}: {signature_relative}")

    # No undeclared driver binary may enter the Setup tree.
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        if path.name in ALLOWED_METADATA_NAMES:
            continue
        if path.suffix.lower() in BINARY_EXTENSIONS and path.resolve() not in declared_files:
            fail(f"binario de driver nao declarado: {path.relative_to(root)}")

    return {
        "packages": len(packages),
        "allowed": allowed_count,
        "metadata_only": metadata_count,
    }

def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default="Drivers")
    args = parser.parse_args(argv)
    try:
        result = validate(Path(args.root))
    except Exception as exc:
        print("DRIVER VALIDATION FAILED:", exc)
        return 1
    print("DRIVER VALIDATION PASSED packages={packages} allowed={allowed} metadata_only={metadata_only}".format(**result))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
