param([switch]$Auto)

$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "Bebel Equipe Do Mais Novo 155 - Dependencias"

$Base = Join-Path $env:LOCALAPPDATA "BebelEquipe155"
$Tools = Join-Path $Base "tools"
$Android = Join-Path $Tools "platform-tools"
$iOS = Join-Path $Tools "libimobiledevice"
$Downloads = Join-Path $Base "downloads"
$LogDir = Join-Path $Base "logs"
New-Item -ItemType Directory -Force -Path $Tools,$Downloads,$LogDir | Out-Null
$Log = Join-Path $LogDir ("deps_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")

function Write-Log([string]$m) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $m"
    Add-Content -LiteralPath $Log -Value $line -Encoding UTF8
}

function Add-UserPath([string]$dir) {
    if (-not (Test-Path $dir)) { return }
    $user = [Environment]::GetEnvironmentVariable("Path","User")
    if ([string]::IsNullOrWhiteSpace($user)) { $user = "" }
    $parts = $user -split ";" | Where-Object { $_ -and $_.Trim() }
    if ($parts -notcontains $dir) {
        $new = (($parts + $dir) -join ";")
        [Environment]::SetEnvironmentVariable("Path",$new,"User")
    }
    if (($env:Path -split ";") -notcontains $dir) {
        $env:Path = "$dir;$env:Path"
    }
}

function ExistsExe([string]$name) {
    if (Get-Command $name -ErrorAction SilentlyContinue) { return $true }
    if (Test-Path (Join-Path $Android $name)) { return $true }
    if (Test-Path (Join-Path $iOS $name)) { return $true }
    return $false
}

function Verify {
    Clear-Host
    Write-Host "===== VERIFICACAO =====" -ForegroundColor Cyan
    foreach ($x in @("adb.exe","fastboot.exe","idevice_id.exe","ideviceinfo.exe","idevicebackup2.exe","winget.exe")) {
        $ok = ExistsExe $x
        if ($ok) {
            Write-Host ("{0,-22} OK" -f $x) -ForegroundColor Green
        } else {
            Write-Host ("{0,-22} AUSENTE" -f $x) -ForegroundColor Yellow
        }
    }
    Write-Host ""
    Write-Host "Pastas:"
    Write-Host "Android: $Android"
    Write-Host "iOS:     $iOS"
    Write-Host ""
}

function Install-Android {
    Write-Host ""
    Write-Host "[ANDROID] Baixando Platform Tools oficial da Google..." -ForegroundColor Cyan
    $url = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
    $zip = Join-Path $Downloads "platform-tools-latest-windows.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    if (Test-Path $Android) { Remove-Item $Android -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $Tools -Force

    if (-not (Test-Path (Join-Path $Android "adb.exe"))) { throw "adb.exe nao encontrado apos extracao." }
    if (-not (Test-Path (Join-Path $Android "fastboot.exe"))) { throw "fastboot.exe nao encontrado apos extracao." }

    Add-UserPath $Android
    Write-Host "ADB/Fastboot instalados com sucesso." -ForegroundColor Green
    Write-Log "Android Platform Tools instalados em $Android"
}

function Install-AppleDevices {
    Write-Host ""
    Write-Host "[APPLE] Tentando instalar Apple Devices pela Microsoft Store..." -ForegroundColor Cyan
    $wg = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $wg) {
        Write-Host "winget nao encontrado. Atualize o App Installer pela Microsoft Store." -ForegroundColor Yellow
        return
    }

    & winget search --name "Apple Devices" --source msstore --accept-source-agreements
    Write-Host ""
    & winget install --name "Apple Devices" --source msstore --accept-source-agreements --accept-package-agreements
    Write-Log "Tentativa de instalacao do Apple Devices via winget."
}

function Install-iOSTools {
    Write-Host ""
    Write-Host "ATENCAO:" -ForegroundColor Yellow
    Write-Host "As ferramentas iOS abaixo sao de uma build comunitaria do projeto libimobiledevice para Windows."
    Write-Host "Repositorio: github.com/jrjr/libimobiledevice-windows"
    Write-Host "Elas nao sao um pacote oficial da Apple."
    Write-Host ""

    if (-not $Auto) {
        $ans = Read-Host "Deseja continuar? (S/N)"
        if ($ans -notmatch '^[SsYy]') {
            Write-Host "Instalacao iOS cancelada."
            return
        }
    }

    Write-Host "[IOS] Consultando a release mais recente..." -ForegroundColor Cyan
    $headers = @{ "User-Agent" = "BebelEquipe155" }
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/jrjr/libimobiledevice-windows/releases/latest" -Headers $headers

    $assets = @($release.assets)
    if ($assets.Count -eq 0) { throw "A release mais recente nao possui assets." }

    $asset = $assets | Where-Object {
        $_.name -match '\.(zip|7z)$' -and $_.name -match '(64|x64|win)'
    } | Select-Object -First 1

    if (-not $asset) {
        $asset = $assets | Where-Object { $_.name -match '\.zip$' } | Select-Object -First 1
    }
    if (-not $asset) {
        Write-Host "Nao consegui identificar automaticamente um ZIP compativel." -ForegroundColor Yellow
        Write-Host "Assets encontrados:"
        $assets | ForEach-Object { Write-Host (" - " + $_.name) }
        throw "Asset ZIP nao identificado."
    }

    Write-Host ("Baixando: " + $asset.name)
    $archive = Join-Path $Downloads $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive -Headers $headers -UseBasicParsing

    if (Test-Path $iOS) { Remove-Item $iOS -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $iOS | Out-Null

    if ($archive.ToLower().EndsWith(".zip")) {
        Expand-Archive -LiteralPath $archive -DestinationPath $iOS -Force
    } else {
        throw "Esta versao do instalador suporta ZIP automaticamente. Asset recebido: $($asset.name)"
    }

    # Localiza a subpasta que realmente contem os executaveis
    $id = Get-ChildItem -Path $iOS -Filter "idevice_id.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $id) { throw "idevice_id.exe nao encontrado no pacote baixado." }

    $exeDir = $id.Directory.FullName
    Add-UserPath $exeDir

    $required = @("idevice_id.exe","ideviceinfo.exe","idevicebackup2.exe")
    foreach ($r in $required) {
        if (-not (Test-Path (Join-Path $exeDir $r))) {
            Write-Host "$r nao foi encontrado nesta build." -ForegroundColor Yellow
        }
    }

    Write-Host "Ferramentas iOS extraidas em:" -ForegroundColor Green
    Write-Host $exeDir
    Write-Log "libimobiledevice comunitario instalado de $($asset.browser_download_url) em $exeDir"
}

function Install-All {
    try { Install-Android } catch { Write-Host "Erro Android: $($_.Exception.Message)" -ForegroundColor Red; Write-Log $_.Exception.ToString() }
    try { Install-AppleDevices } catch { Write-Host "Erro Apple Devices: $($_.Exception.Message)" -ForegroundColor Red; Write-Log $_.Exception.ToString() }
    try { Install-iOSTools } catch { Write-Host "Erro iOS Tools: $($_.Exception.Message)" -ForegroundColor Red; Write-Log $_.Exception.ToString() }
    Verify
    Write-Host "Feche e abra novamente o Bebel Equipe Do Mais Novo 155 para ele ler o PATH atualizado." -ForegroundColor Cyan
}

if ($Auto) {
    Install-All
    exit
}

while ($true) {
    Verify
    Write-Host "1 - Instalar ADB/Fastboot (Google oficial)"
    Write-Host "2 - Instalar Apple Devices (Microsoft Store)"
    Write-Host "3 - Instalar iOS Tools / libimobiledevice (build comunitaria)"
    Write-Host "4 - Instalar tudo"
    Write-Host "5 - Abrir pasta das ferramentas"
    Write-Host "0 - Sair"
    Write-Host ""
    $op = Read-Host "Escolha"

    switch ($op) {
        "1" { try { Install-Android } catch { Write-Host $_.Exception.Message -ForegroundColor Red }; pause }
        "2" { try { Install-AppleDevices } catch { Write-Host $_.Exception.Message -ForegroundColor Red }; pause }
        "3" { try { Install-iOSTools } catch { Write-Host $_.Exception.Message -ForegroundColor Red }; pause }
        "4" { Install-All; pause }
        "5" { Start-Process explorer.exe $Tools }
        "0" { break }
        default { }
    }
}
