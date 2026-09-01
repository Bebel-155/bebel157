param(
    [string]$DriversRoot = "Drivers"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $DriversRoot).Path
$manifestPath = Join-Path $root "drivers-manifest.json"
if (!(Test-Path $manifestPath)) { throw "Manifesto de drivers ausente: $manifestPath" }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$allowed = @($manifest.Packages | Where-Object { $_.RedistributionStatus -eq "Allowed" })

foreach ($package in $allowed) {
    $relative = if ([string]::IsNullOrWhiteSpace([string]$package.SignatureRelativePath)) {
        [string]$package.RelativePath
    } else {
        [string]$package.SignatureRelativePath
    }

    if ([string]::IsNullOrWhiteSpace($relative)) {
        throw "Pacote Allowed sem arquivo de assinatura: $($package.Id)"
    }

    $path = [System.IO.Path]::GetFullPath((Join-Path $root $relative))
    if (!$path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Caminho de assinatura fora de Drivers: $relative"
    }
    if (!(Test-Path $path)) { throw "Arquivo de assinatura ausente: $relative" }

    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne "Valid") {
        throw "Assinatura invalida em $($package.Id): $($signature.Status)"
    }

    $publisher = [string]$package.SignaturePublisher
    if ([string]::IsNullOrWhiteSpace($publisher)) {
        throw "SignaturePublisher ausente: $($package.Id)"
    }

    $subject = [string]$signature.SignerCertificate.Subject
    if ($subject.IndexOf($publisher, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Publisher inesperado em $($package.Id): $subject"
    }

    Write-Host "OK assinatura $($package.Id): $subject"
}

Write-Host "DRIVER SIGNATURE VALIDATION PASSED allowed=$($allowed.Count)"
