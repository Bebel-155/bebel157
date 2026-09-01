$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "csc.exe nao encontrado" }
$sources = @(
  "$root\Core\DeviceModels.cs",
  "$root\Core\ICommandRunner.cs",
  "$root\Core\CommandRunner.cs",
  "$root\Devices\DeviceDiscovery.cs",
  "$root\Devices\AndroidProbe.cs",
  "$root\Devices\AppleProbe.cs",
  "$root\Devices\DeviceResolver.cs",
  "$root\tests\TestAssert.cs",
  "$root\tests\DeviceRecognitionTests.cs"
)
& $csc /nologo /target:exe /codepage:65001 /reference:System.Core.dll /out:"$out\DeviceRecognitionTests.exe" $sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$out\DeviceRecognitionTests.exe"
exit $LASTEXITCODE
