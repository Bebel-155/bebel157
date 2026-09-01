from pathlib import Path
import json, re, sys

ROOT = Path(__file__).resolve().parents[1]

required = [
    ROOT/'Drivers'/'DriverModels.cs',
    ROOT/'Drivers'/'UsbDriverDiscovery.cs',
    ROOT/'Drivers'/'DriverResolver.cs',
    ROOT/'Drivers'/'DriverPackageManager.cs',
    ROOT/'Drivers'/'DriverInstaller.cs',
    ROOT/'Drivers'/'DriverService.cs',
    ROOT/'Drivers'/'drivers-manifest.json',
    ROOT/'scripts'/'validate_driver_packages.py',
    ROOT/'scripts'/'validate_driver_signatures.ps1',
    ROOT/'tests'/'DriverTests.cs',
    ROOT/'tests'/'run-driver-tests.ps1',
]
missing = [str(p.relative_to(ROOT)) for p in required if not p.exists()]
if missing:
    print('FAIL missing driver subsystem files:', ', '.join(missing))
    sys.exit(1)

models = (ROOT/'Drivers'/'DriverModels.cs').read_text(encoding='utf-8-sig')
for token in ['DRIVER_MISSING','DRIVER_INCORRECT','ADB_UNAUTHORIZED','ADB_OFFLINE','ADB_READY','USB_ONLY','UNKNOWN']:
    if token not in models:
        print('FAIL missing state token:', token); sys.exit(1)

manifest = json.loads((ROOT/'Drivers'/'drivers-manifest.json').read_text(encoding='utf-8-sig'))
vendors = {p.get('manufacturer', p.get('Manufacturer','')).lower() for p in manifest.get('packages', manifest.get('Packages',[]))}
for vendor in ['samsung','motorola','xiaomi','oneplus','oppo','realme','vivo','huawei','honor','sony','asus','hmd','nothing','zte','tcl','lg','google','generic']:
    if vendor not in vendors:
        print('FAIL missing vendor manifest coverage:', vendor); sys.exit(1)


sig = (ROOT/'scripts'/'validate_driver_signatures.ps1').read_text(encoding='utf-8-sig')
if not sig.lstrip().startswith('param('):
    print('FAIL signature validator param block must be first'); sys.exit(1)

source = (ROOT/'Bebel155_v5_2_0.cs').read_text(encoding='utf-8-sig')
if 'Drivers USB / ADB' not in source:
    print('FAIL Drivers USB / ADB page missing'); sys.exit(1)

print('PASS driver subsystem structure')
