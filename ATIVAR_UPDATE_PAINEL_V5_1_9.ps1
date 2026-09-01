$ErrorActionPreference = "Stop"
$dir = Join-Path $env:LOCALAPPDATA "BebelEquipe155"
$settings = Join-Path $dir "settings.ini"
$manifestUrl = "https://github.com/Bebel-155/bebel157/releases/latest/download/manifest.json"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$lines = @()
if (Test-Path $settings) { $lines = @(Get-Content -LiteralPath $settings) }

function Set-IniValue([string[]]$inputLines, [string]$key, [string]$value) {
    $out = New-Object System.Collections.Generic.List[string]
    $found = $false
    foreach ($line in $inputLines) {
        $trim = $line.Trim()
        $pos = $trim.IndexOf('=')
        if ($pos -gt 0) {
            $k = $trim.Substring(0,$pos).Trim()
            if ($k.Equals($key,[System.StringComparison]::OrdinalIgnoreCase)) {
                if (-not $found) { $out.Add("$key=$value"); $found = $true }
                continue
            }
        }
        $out.Add($line)
    }
    if (-not $found) { $out.Add("$key=$value") }
    return $out.ToArray()
}

$lines = Set-IniValue $lines "update_source" "manifest"
$lines = Set-IniValue $lines "update_manifest_url" $manifestUrl
$lines = Set-IniValue $lines "github_repo" "Bebel-155/bebel157"
$lines = Set-IniValue $lines "auto_check_updates" "true"
[System.IO.File]::WriteAllLines($settings, $lines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "[OK] Atualizador do Bebel 155 configurado para o manifest de compatibilidade." -ForegroundColor Green
Write-Host "Settings: $settings"
Write-Host "Manifest: $manifestUrl"
Write-Host ""
Write-Host "Agora abra o Bebel 155 v5.1.9, entre em Atualizacoes e clique em 'Verificar Bebel 155'."
