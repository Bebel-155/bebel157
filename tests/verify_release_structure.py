#!/usr/bin/env python3
from pathlib import Path
import re, sys, json, hashlib

ROOT = Path(__file__).resolve().parents[1]
errors=[]

def require(cond,msg):
    if not cond: errors.append(msg)

version=(ROOT/'VERSION').read_text(encoding='utf-8').strip()
require(version=='5.2.0', 'VERSION must be 5.2.0')

production=[ROOT/'Bebel155_v5_2_0.cs']
for folder in ('Core','Devices','Catalogs','Market'):
    production += sorted((ROOT/folder).glob('*.cs'))

bat=(ROOT/'CRIAR_EXE_V5_2_0.bat').read_text(encoding='ascii', errors='ignore')
for p in production:
    if p.parent==ROOT:
        needle=p.name
    else:
        needle=str(p.relative_to(ROOT)).replace('/','\\')
    require(needle.lower() in bat.lower(), 'local build missing source: '+needle)

wf=(ROOT/'.github/workflows/release.yml').read_text(encoding='utf-8')
require('run-all-tests.ps1' in wf, 'release workflow does not run all tests')
require('Atualizar catalogos da Release' in wf and 'python scripts/update_device_catalogs.py --output-dir Catalogs' in wf, 'release workflow does not generate current catalogs before packaging')
require('actions/setup-python@v6' in wf, 'release workflow must pin Python setup action')
require(wf.find('actions/setup-python@v6') < wf.find('python tests/validate-source.py'), 'Python setup must run before Python validation')
for trigger_path in ('VERSION','Bebel155_v5_2_0.cs','Core/**','Devices/**','Catalogs/*.cs','Market/**','.github/workflows/release.yml'):
    require(trigger_path in wf, 'release push trigger missing '+trigger_path)
require('Get-Content "VERSION"' in wf, 'release workflow does not read VERSION')
require('Bebel155_v5_2_0.cs' in wf, 'release workflow missing v5.2.0 source')
require('softprops/action-gh-release@v3' in wf, 'release workflow release action unexpected')
require('Gerar manifest compativel' in wf and 'Atualizacao\\manifest.json' in wf, 'release workflow must generate compatibility manifest')
require('Atualizacao/manifest.json' in wf, 'release must publish compatibility manifest asset')
bridge=ROOT/'ATIVAR_UPDATE_PAINEL_V5_1_9.bat'
bridge_ps1=ROOT/'ATIVAR_UPDATE_PAINEL_V5_1_9.ps1'
require(bridge.exists() and bridge_ps1.exists(), 'v5.1.9 update bridge missing')
if bridge.exists() and bridge_ps1.exists():
    bt=bridge.read_text(encoding='ascii',errors='ignore')
    pt=bridge_ps1.read_text(encoding='utf-8-sig',errors='ignore')
    require('ATIVAR_UPDATE_PAINEL_V5_1_9.ps1' in bt, 'bridge BAT does not invoke bridge PS1')
    require('releases/latest/download/manifest.json' in pt, 'bridge does not use stable latest manifest URL')
    require('update_source' in pt and 'manifest' in pt, 'bridge does not set manifest update source')
require('--force' not in wf, 'release workflow must not force push')
publisher=(ROOT/'PUBLICAR_NO_GITHUB.bat').read_text(encoding='ascii',errors='ignore')
require('ATIVAR_UPDATE_PAINEL_V5_1_9.ps1' in publisher, 'publisher must configure v5.1.9 compatibility manifest after push')
require('git push origin HEAD:main' in publisher and '--force' not in publisher, 'publisher push policy invalid')

runall=(ROOT/'tests/run-all-tests.ps1').read_text(encoding='utf-8-sig')
require('verify_release_structure.py' in runall, 'run-all does not execute release structure guard')
for script in ('run-device-recognition-tests.ps1','run-catalog-tests.ps1','run-market-tests.ps1','run-update-tests.ps1'):
    require(script in runall, 'run-all missing '+script)
    require((ROOT/'tests'/script).exists(), 'test runner missing '+script)

main=(ROOT/'Bebel155_v5_2_0.cs').read_text(encoding='utf-8-sig')
require('AutoRefreshCatalogsStartup' in main and 'ThreadPool.QueueUserWorkItem(delegate { AutoRefreshCatalogsStartup(); });' in main, 'app does not auto-refresh device catalogs on startup')

iss=(ROOT/'Bebel155_v5_2_0.iss').read_text(encoding='utf-8-sig')
m=re.search(r'(?ms)^\[Icons\]\s*(.*?)(?=^\[|\Z)',iss)
require(bool(m),'Inno [Icons] missing')
if m: require('skipifsourcedoesntexist' not in m.group(1).lower(),'invalid Inno flag in [Icons]')
for filename in ('android_devices.json','apple_devices.json','catalog-manifest.json'):
    require(filename in iss,'Setup missing '+filename)

manifest=json.loads((ROOT/'Catalogs/catalog-manifest.json').read_text(encoding='utf-8'))
for key,name in (('androidSha256','android_devices.json'),('appleSha256','apple_devices.json')):
    actual=hashlib.sha256((ROOT/'Catalogs'/name).read_bytes()).hexdigest()
    require(actual==manifest.get(key),'catalog hash mismatch '+name)

if errors:
    for e in errors: print('FAIL',e)
    sys.exit(1)
print('PASS release structure: %d production C# sources tracked' % len(production))
