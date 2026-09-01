$ErrorActionPreference = "Continue"
$Host.UI.RawUI.WindowTitle = "Bebel 155 - Reparar WinGet / App Installer"

function Test-Winget {
    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        Write-Host "WinGet encontrado: $($cmd.Source)" -ForegroundColor Green
        & $cmd.Source --version
        return $true
    }

    $wa = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"
    if (Test-Path $wa) {
        Write-Host "WinGet encontrado em WindowsApps: $wa" -ForegroundColor Green
        & $wa --version
        return $true
    }
    return $false
}

Clear-Host
Write-Host "===== BEBEL 155 - REPARAR WINGET / APP INSTALLER =====" -ForegroundColor Cyan
Write-Host ""

if (Test-Winget) {
    Write-Host ""
    Write-Host "Nenhum reparo necessario." -ForegroundColor Green
    Read-Host "Pressione ENTER para sair"
    exit 0
}

Write-Host "WinGet nao foi localizado." -ForegroundColor Yellow
Write-Host ""

$pkg = Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue |
       Sort-Object Version -Descending | Select-Object -First 1

if ($pkg) {
    Write-Host "App Installer encontrado: versao $($pkg.Version)" -ForegroundColor Cyan
    Write-Host "Tentando registrar novamente o pacote..." -ForegroundColor Cyan
    try {
        $manifest = Join-Path $pkg.InstallLocation "AppxManifest.xml"
        if (Test-Path $manifest) {
            Add-AppxPackage -DisableDevelopmentMode -Register $manifest -ErrorAction Stop
            Start-Sleep -Seconds 2
        }
    } catch {
        Write-Host "Nao foi possivel registrar novamente: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    if (Test-Winget) {
        Write-Host ""
        Write-Host "WinGet reparado." -ForegroundColor Green
        Read-Host "Pressione ENTER para sair"
        exit 0
    }
}

Write-Host ""
Write-Host "Abrindo a Microsoft Store para instalar/atualizar App Installer..." -ForegroundColor Cyan
Start-Process "ms-windows-store://search/?query=App%20Installer"
Write-Host ""
Write-Host "Na Microsoft Store:" -ForegroundColor White
Write-Host "1. Instale ou atualize 'App Installer'." -ForegroundColor White
Write-Host "2. Feche e reabra o Bebel 155." -ForegroundColor White
Write-Host "3. Verifique novamente as dependencias." -ForegroundColor White
Write-Host ""
Read-Host "Pressione ENTER para sair"
