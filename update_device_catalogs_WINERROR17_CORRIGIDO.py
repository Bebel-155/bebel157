#!/usr/bin/env python3
import argparse, csv, hashlib, html, json, os, re, shutil, sys, tempfile, urllib.request
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path

GOOGLE_URL = "https://storage.googleapis.com/play_public/supported_devices.html"
APPLE_URL = "https://api.ipsw.me/v4/devices"
RAW_BASE = "https://raw.githubusercontent.com/Bebel-155/bebel157/main/Catalogs"

class TableParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.rows=[]; self.row=None; self.cell=None; self.in_cell=False
    def handle_starttag(self, tag, attrs):
        tag=tag.lower()
        if tag=='tr': self.row=[]
        elif tag in ('td','th') and self.row is not None:
            self.cell=[]; self.in_cell=True
    def handle_data(self, data):
        if self.in_cell and self.cell is not None: self.cell.append(data)
    def handle_endtag(self, tag):
        tag=tag.lower()
        if tag in ('td','th') and self.in_cell:
            self.row.append(html.unescape(''.join(self.cell)).strip()); self.cell=None; self.in_cell=False
        elif tag=='tr' and self.row is not None:
            if any(x.strip() for x in self.row): self.rows.append(self.row)
            self.row=None

def norm(s): return re.sub(r'\s+',' ',(s or '').strip())
def keynorm(s): return norm(s).lower()

def fetch_text(url):
    req=urllib.request.Request(url,headers={'User-Agent':'BebelEquipe155-CatalogUpdater/1.0'})
    with urllib.request.urlopen(req,timeout=45) as r:
        return r.read().decode('utf-8','replace')

def parse_google(text):
    # Current public file is HTML; support CSV/TSV fallback to survive source presentation changes.
    rows=[]
    if '<table' in text.lower() or '<tr' in text.lower():
        p=TableParser(); p.feed(text); rows=p.rows
    else:
        sample=text[:4096]
        delim='\t' if sample.count('\t') > sample.count(',') else ','
        rows=list(csv.reader(text.splitlines(),delimiter=delim))
    if not rows: raise ValueError('Google device list has no rows')
    header=[keynorm(x) for x in rows[0]]
    aliases={
        'brand':['retail branding','retail brand','brand','manufacturer','fabricante'],
        'marketing':['marketing name','marketingname','model name','nome do modelo'],
        'device':['device','device code'],
        'model':['model','model code','código do modelo','codigo do modelo']
    }
    def find(names):
        for n in names:
            if n in header: return header.index(n)
        return -1
    idx={k:find(v) for k,v in aliases.items()}
    if idx['brand']<0 or idx['device']<0: raise ValueError('Google header missing brand/device: '+repr(rows[0][:12]))
    out={}
    for row in rows[1:]:
        def g(i): return norm(row[i]) if i>=0 and i<len(row) else ''
        brand=g(idx['brand']); device=g(idx['device']); model=g(idx['model']); marketing=g(idx['marketing'])
        if not brand or not device: continue
        k=(brand.lower(),device.lower(),model.lower())
        entry={
            'manufacturer': brand, 'brand': brand, 'device': device, 'modelCode': model,
            'commercialName': marketing or model or device, 'ramBytes': None, 'soc':'', 'gpu':'',
            'abis':[], 'storageVariants':[]
        }
        if k not in out or len(entry['commercialName'])>len(out[k]['commercialName']): out[k]=entry
    return sorted(out.values(), key=lambda e:(keynorm(e['manufacturer']),keynorm(e['device']),keynorm(e['modelCode'])))

def parse_apple(text):
    data=json.loads(text)
    if isinstance(data,dict): data=data.get('devices') or data.get('results') or []
    out={}
    for d in data:
        ident=norm(d.get('identifier') or d.get('productType'))
        name=norm(d.get('name') or d.get('commercialName'))
        if not ident or not (ident.startswith('iPhone') or ident.startswith('iPad') or ident.startswith('iPod')): continue
        boards=d.get('boards') or []
        if not boards:
            boards=[{'boardconfig':d.get('boardconfig') or d.get('boardConfig') or d.get('hardwareModel') or '', 'platform':d.get('platform') or ''}]
        for b in boards:
            board=norm(b.get('boardconfig') or b.get('boardConfig') or '')
            platform=norm(b.get('platform') or '')
            k=(ident.lower(),board.lower())
            out[k]={
                'productType':ident,'hardwareModel':board,'commercialName':name or ident,
                'platform':platform or ('iPad' if ident.startswith('iPad') else 'iPod' if ident.startswith('iPod') else 'iPhone'),
                'boardConfig':board,'ramBytes':None,'storageVariants':[]
            }
    return sorted(out.values(), key=lambda e:(keynorm(e['productType']),keynorm(e['hardwareModel'])))

def stable_bytes(obj):
    return json.dumps(obj,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode('utf-8')
def sha(b): return hashlib.sha256(b).hexdigest()

def install_staged_file(src, dst):
    """Install a validated staged file atomically even when staging is on another volume."""
    src=Path(src); dst=Path(dst)
    dst.parent.mkdir(parents=True,exist_ok=True)
    fd, local_name=tempfile.mkstemp(prefix='.'+dst.name+'.',suffix='.tmp',dir=str(dst.parent))
    os.close(fd)
    local=Path(local_name)
    try:
        shutil.copyfile(str(src),str(local))
        os.replace(str(local),str(dst))
    finally:
        try: local.unlink()
        except FileNotFoundError: pass

def validate_dir(outdir):
    outdir=Path(outdir)
    a=(outdir/'android_devices.json').read_bytes(); p=(outdir/'apple_devices.json').read_bytes()
    m=json.loads((outdir/'catalog-manifest.json').read_text(encoding='utf-8'))
    aa=json.loads(a.decode()); pp=json.loads(p.decode())
    if not aa: raise ValueError('Android catalog empty')
    if not pp: raise ValueError('Apple catalog empty')
    if sha(a)!=m.get('androidSha256'): raise ValueError('Android SHA mismatch')
    if sha(p)!=m.get('appleSha256'): raise ValueError('Apple SHA mismatch')
    if any(not (x.get('brand') or x.get('manufacturer')) or not x.get('device') for x in aa): raise ValueError('Android primary key incomplete')
    if any(not x.get('productType') for x in pp): raise ValueError('Apple ProductType missing')
    return len(aa),len(pp)

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--google-input'); ap.add_argument('--apple-input'); ap.add_argument('--output-dir',default='Catalogs')
    ap.add_argument('--validate-only',action='store_true')
    args=ap.parse_args()
    outdir=Path(args.output_dir)
    if args.validate_only:
        a,p=validate_dir(outdir); print('valid android=%d apple=%d'%(a,p)); return 0
    google=Path(args.google_input).read_text(encoding='utf-8') if args.google_input else fetch_text(GOOGLE_URL)
    apple=Path(args.apple_input).read_text(encoding='utf-8') if args.apple_input else fetch_text(APPLE_URL)
    android=parse_google(google); apple_rows=parse_apple(apple)
    # Public live source sanity floors. Fixture mode intentionally bypasses these floors.
    if not args.google_input and len(android)<1000: raise ValueError('Android live catalog unexpectedly small: %d'%len(android))
    if not args.apple_input and len(apple_rows)<50: raise ValueError('Apple live catalog unexpectedly small: %d'%len(apple_rows))
    ab=stable_bytes(android); pb=stable_bytes(apple_rows)
    day=datetime.now(timezone.utc).strftime('%Y-%m-%d')
    manifest={
        'schemaVersion':1,'generatedUtc':datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace('+00:00','Z'),
        'androidVersion':day,'androidSha256':sha(ab),'androidUrl':RAW_BASE+'/android_devices.json',
        'appleVersion':day,'appleSha256':sha(pb),'appleUrl':RAW_BASE+'/apple_devices.json'
    }
    mb=stable_bytes(manifest)
    outdir.parent.mkdir(parents=True,exist_ok=True)
    tmp=Path(tempfile.mkdtemp(prefix='bebel_catalog_'))
    try:
        (tmp/'android_devices.json').write_bytes(ab); (tmp/'apple_devices.json').write_bytes(pb); (tmp/'catalog-manifest.json').write_bytes(mb)
        validate_dir(tmp)
        outdir.mkdir(parents=True,exist_ok=True)
        for n in ('android_devices.json','apple_devices.json','catalog-manifest.json'):
            install_staged_file(tmp/n,outdir/n)
    finally:
        shutil.rmtree(tmp,ignore_errors=True)
    print('generated android=%d apple=%d'%(len(android),len(apple_rows)))
    return 0
if __name__=='__main__': sys.exit(main())
