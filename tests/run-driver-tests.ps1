$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "csc.exe nao encontrado." }

& $csc /nologo /target:exe /codepage:65001 `
  /out:"$out\DriverTests.exe" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Web.Extensions.dll `
  "$root\Core\ICommandRunner.cs" `
  "$root\Drivers\DriverModels.cs" `
  "$root\Drivers\UsbDriverDiscovery.cs" `
  "$root\Drivers\DriverResolver.cs" `
  "$root\Drivers\DriverPackageManager.cs" `
  "$root\Drivers\DriverInstaller.cs" `
  "$root\Drivers\DriverService.cs" `
  "$root\tests\TestAssert.cs" `
  "$root\tests\DriverTests.cs"

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$out\DriverTests.exe"
exit $LASTEXITCODE
