#!/usr/bin/env python3
import hashlib,json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
errors=[]

def req(cond,msg):
    if not cond: errors.append(msg)

version=(ROOT/'VERSION').read_text().strip()
req(version=='5.2.0','VERSION must be 5.2.0')
main=(ROOT/'Bebel155_v5_2_0.cs').read_text(encoding='utf-8-sig')
iss=(ROOT/'Bebel155_v5_2_0.iss').read_text(encoding='utf-8-sig')
req('readonly string AppVersion = "5.2.0";' in main,'AppVersion mismatch')
req('footer.Text = "v5.2.0 • USB / Android / iOS";' in main,'footer mismatch')
req('#define MyAppVersion "5.2.0"' in iss,'ISS version mismatch')
req('#define MyAppExeName "Bebel-155_V5_2_0.exe"' in iss,'ISS EXE mismatch')
req('OutputBaseFilename=Bebel-155_Setup_V5_2_0' in iss,'ISS setup mismatch')
icons=re.search(r'(?ms)^\[Icons\]\s*(.*?)(?=^\[|\Z)',iss)
req(bool(icons),'[Icons] missing')
if icons: req('skipifsourcedoesntexist' not in icons.group(1).lower(),'invalid [Icons] flag')
for f in ('android_devices.json','apple_devices.json','catalog-manifest.json'):
    req(f in iss,'catalog missing from ISS: '+f)
for f in ('Core/DeviceModels.cs','Core/GithubReleaseParser.cs','Devices/AndroidProbe.cs','Devices/AppleProbe.cs','Catalogs/CatalogManager.cs','Market/MarketPriceService.cs','Drivers/DriverModels.cs','Drivers/DriverService.cs'):
    req((ROOT/f).exists(),'source missing: '+f)
req('com.apple.disk_usage' in (ROOT/'Devices/AppleProbe.cs').read_text(),'Apple disk_usage query missing')
req('DeviceTargeting.AndroidPrefix(device)' in (ROOT/'Devices/AndroidProbe.cs').read_text(),'Android serial targeting missing')
req('GithubReleaseParser.Parse(json)' in main,'GitHub JSON parser integration missing')
req('Atualização rejeitada: a Release não forneceu SHA-256 do EXE.' in main,'updater SHA gate missing')
req('mercadolivre_access_token_dpapi' in main,'DPAPI market token setting missing')
req('ProtectedData.Protect' in main and 'DataProtectionScope.CurrentUser' in main,'DPAPI protection missing')
req('Drivers USB / ADB' in main,'driver page missing')
req('driverPackageManager' in main and 'RefreshDriverDiagnostics' in main,'driver subsystem integration missing')
req((ROOT/'Drivers/drivers-manifest.json').exists(),'driver manifest missing')
req((ROOT/'scripts/validate_driver_packages.py').exists(),'driver validator missing')
req((ROOT/'scripts/validate_driver_signatures.ps1').exists(),'driver signature validator missing')

# catalog hash validation
manifest=json.loads((ROOT/'Catalogs/catalog-manifest.json').read_text())
for name,key in [('android_devices.json','androidSha256'),('apple_devices.json','appleSha256')]:
    b=(ROOT/'Catalogs'/name).read_bytes()
    req(hashlib.sha256(b).hexdigest()==manifest.get(key),'catalog hash mismatch: '+name)

# Production source delimiter scanner (ignores strings/comments enough to catch accidental edit damage).
def strip_cs(s):
    out=[]; i=0; state='code'
    while i<len(s):
        c=s[i]; d=s[i+1] if i+1<len(s) else ''
        if state=='code':
            if c=='/' and d=='/': state='line'; out.extend('  '); i+=2; continue
            if c=='/' and d=='*': state='block'; out.extend('  '); i+=2; continue
            if c=='@' and d=='"': state='vstr'; out.extend('  '); i+=2; continue
            if c=='"': state='str'; out.append(' '); i+=1; continue
            if c=="'": state='char'; out.append(' '); i+=1; continue
            out.append(c); i+=1; continue
        if state=='line':
            out.append('\n' if c=='\n' else ' '); state='code' if c=='\n' else state; i+=1; continue
        if state=='block':
            if c=='*' and d=='/': state='code'; out.extend('  '); i+=2
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='str':
            if c=='\\': out.extend('  '); i+=2
            elif c=='"': state='code'; out.append(' '); i+=1
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='char':
            if c=='\\': out.extend('  '); i+=2
            elif c=="'": state='code'; out.append(' '); i+=1
            else: out.append(' '); i+=1
            continue
        if state=='vstr':
            if c=='"' and d=='"': out.extend('  '); i+=2
            elif c=='"': state='code'; out.append(' '); i+=1
            else: out.append('\n' if c=='\n' else ' '); i+=1
    return ''.join(out),state

prod=[ROOT/'Bebel155_v5_2_0.cs']+sorted((ROOT/'Core').glob('*.cs'))+sorted((ROOT/'Devices').glob('*.cs'))+sorted((ROOT/'Catalogs').glob('*.cs'))+sorted((ROOT/'Market').glob('*.cs'))+sorted((ROOT/'Drivers').glob('*.cs'))
for p in prod:
    s=p.read_text(encoding='utf-8-sig')
    stripped,state=strip_cs(s)
    req(state in ('code','line'),'unterminated lexical state: '+str(p.name))
    for op,cl in [('{','}'),('(',')'),('[',']')]:
        depth=0; min_depth=0
        for ch in stripped:
            if ch==op: depth+=1
            elif ch==cl: depth-=1; min_depth=min(min_depth,depth)
        req(depth==0 and min_depth>=0,'delimiter mismatch %s in %s'%(op+cl,p.name))

# Secret scan — patterns, not placeholders. Allow only the setting key and Authorization code expression.
secret_patterns=[re.compile(r'APP_USR-[A-Za-z0-9_-]{12,}'), re.compile(r'Bearer\s+[A-Za-z0-9_-]{24,}')]
for p in prod+list((ROOT/'.github/workflows').glob('*.yml')):
    text=p.read_text(encoding='utf-8-sig')
    for pat in secret_patterns:
        req(not pat.search(text),'possible embedded secret in '+str(p.relative_to(ROOT)))

if errors:
    print('SOURCE VALIDATION FAILED')
    for e in errors: print(' - '+e)
    sys.exit(1)
print('SOURCE VALIDATION PASSED')
print('production_cs_files=%d android_catalog=%d apple_catalog=%d'%(len(prod),len(json.loads((ROOT/'Catalogs/android_devices.json').read_text())),len(json.loads((ROOT/'Catalogs/apple_devices.json').read_text()))))
