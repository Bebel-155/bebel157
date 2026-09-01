$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "csc.exe nao encontrado" }
& $csc /nologo /target:exe /codepage:65001 /reference:System.Core.dll /reference:System.Web.Extensions.dll /out:"$out\UpdateManifestTests.exe" `
  "$root\Core\GithubReleaseParser.cs" `
  "$root\tests\TestAssert.cs" `
  "$root\tests\UpdateManifestTests.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$out\UpdateManifestTests.exe"
exit $LASTEXITCODE
