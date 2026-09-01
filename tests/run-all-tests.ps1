$ErrorActionPreference = "Stop"

python "$PSScriptRoot\verify_release_structure.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

python "$PSScriptRoot\test_compile_regressions.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

python "$PSScriptRoot\test_update_device_catalogs_cross_volume.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot\run-device-recognition-tests.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot\run-catalog-tests.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot\run-market-tests.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot\run-update-tests.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "ALL TESTS PASSED"
exit 0
