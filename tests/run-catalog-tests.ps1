$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "csc.exe nao encontrado" }
& $csc /nologo /target:exe /codepage:65001 /reference:System.Core.dll /out:"$out\CatalogTests.exe" `
  /reference:System.Web.Extensions.dll `
  "$root\Core\DeviceModels.cs" `
  "$root\Catalogs\CatalogModels.cs" `
  "$root\Catalogs\CatalogManager.cs" `
  "$root\tests\TestAssert.cs" `
  "$root\tests\CatalogTests.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$out\CatalogTests.exe"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifest = Get-Content "$root\Catalogs\catalog-manifest.json" -Raw | ConvertFrom-Json
$androidPath = "$root\Catalogs\android_devices.json"
$applePath = "$root\Catalogs\apple_devices.json"
$android = Get-Content $androidPath -Raw | ConvertFrom-Json
$apple = Get-Content $applePath -Raw | ConvertFrom-Json
if (@($android).Count -lt 1) { throw "Catalogo Android vazio" }
if (@($apple).Count -lt 1) { throw "Catalogo Apple vazio" }
$ah = (Get-FileHash $androidPath -Algorithm SHA256).Hash.ToLower()
$ph = (Get-FileHash $applePath -Algorithm SHA256).Hash.ToLower()
if ($ah -ne $manifest.androidSha256) { throw "Hash Android divergente" }
if ($ph -ne $manifest.appleSha256) { throw "Hash Apple divergente" }
Write-Host "PASS catalog files and hashes"
