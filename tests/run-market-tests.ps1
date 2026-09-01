$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "csc.exe nao encontrado" }
& $csc /nologo /target:exe /codepage:65001 /reference:System.Core.dll /out:"$out\MarketValueTests.exe" `
 /reference:System.Web.Extensions.dll `
 "$root\Core\DeviceModels.cs" `
 "$root\Market\MarketModels.cs" `
 "$root\Market\IMarketAdapter.cs" `
 "$root\Market\MercadoLivreAdapter.cs" `
 "$root\Market\PriceNormalizer.cs" `
 "$root\Market\MarketCache.cs" `
 "$root\Market\MarketPriceService.cs" `
 "$root\tests\TestAssert.cs" `
 "$root\tests\MarketValueTests.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$out\MarketValueTests.exe"
exit $LASTEXITCODE
