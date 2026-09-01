from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
cs = (root / 'Bebel155_v5_2_0.cs').read_text(encoding='utf-8-sig')
bat = (root / 'CRIAR_EXE_V5_2_0.bat').read_text(encoding='ascii', errors='ignore')

legacy = ['infoType', 'infoOs', 'infoDriver', 'infoRam', 'infoStorage', 'infoBattery']
found = [name for name in legacy if re.search(r'\b' + re.escape(name) + r'\b', cs)]
assert not found, 'legacy UI identifiers still present: ' + ', '.join(found)

assert '-SourceDir "%~dp0."' in bat, 'Roslyn SourceDir must not end with a raw trailing backslash'
assert '-SourceDir "%~dp0"' not in bat, 'unsafe trailing-backslash SourceDir invocation remains'

for source in [
    'Drivers\\DriverModels.cs',
    'Drivers\\UsbDriverDiscovery.cs',
    'Drivers\\DriverResolver.cs',
    'Drivers\\DriverPackageManager.cs',
    'Drivers\\DriverInstaller.cs',
    'Drivers\\DriverService.cs',
]:
    assert source.lower() in bat.lower(), 'local build missing driver source: ' + source

print('PASS compile regression checks')
