param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][string]$OutputExe,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [string]$SourceFile = "Bebel155_v5_2_0.cs"
)

$ErrorActionPreference = "Stop"
$dotnetRoot = Join-Path $env:LOCALAPPDATA "BebelEquipe155\dotnet-sdk"
$installScript = Join-Path $env:TEMP "bebel155-dotnet-install.ps1"

function LogLine([string]$m) { Add-Content -LiteralPath $LogFile -Value $m -Encoding UTF8 }

try {
    New-Item -ItemType Directory -Force -Path (Split-Path $OutputExe), $dotnetRoot | Out-Null
    LogLine "Fallback moderno iniciado em $(Get-Date)."

    $dotnet = Join-Path $dotnetRoot "dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installScript -Channel LTS -InstallDir $dotnetRoot -NoPath
        if ($LASTEXITCODE -ne 0) { throw "dotnet-install.ps1 retornou codigo $LASTEXITCODE" }
    }
    if (-not (Test-Path $dotnet)) { throw "dotnet.exe nao encontrado." }

    $sdkLines = & $dotnet --list-sdks
    $sdkVersions = @()
    foreach ($line in $sdkLines) {
        if ($line -match '^([0-9]+\.[0-9]+\.[0-9]+[^\s]*)\s+\[(.+)\]') {
            $sdkVersions += [PSCustomObject]@{ Version=$matches[1]; Root=$matches[2] }
        }
    }
    if ($sdkVersions.Count -eq 0) { throw "Nenhum SDK encontrado." }
    $sdk = $sdkVersions | Sort-Object { [version](($_.Version -replace '-.*$','')) } -Descending | Select-Object -First 1
    $csc = Join-Path $sdk.Root ($sdk.Version + "\Roslyn\bincore\csc.dll")
    if (-not (Test-Path $csc)) { throw "Roslyn csc.dll nao encontrado: $csc" }

    $framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
    if (-not (Test-Path (Join-Path $framework "mscorlib.dll"))) { $framework = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319" }
    if (-not (Test-Path (Join-Path $framework "mscorlib.dll"))) { throw ".NET Framework nao encontrado." }

    $source = Join-Path $SourceDir $SourceFile
    if (-not (Test-Path $source)) { throw "Fonte principal ausente: $source" }

    $sources = @($source)
    foreach ($folder in @("Core","Devices","Catalogs","Market","Drivers")) {
        $dir = Join-Path $SourceDir $folder
        if (-not (Test-Path $dir)) { throw "Diretorio de fontes ausente: $dir" }
        $sources += Get-ChildItem -LiteralPath $dir -Filter *.cs -File | Sort-Object Name | ForEach-Object { $_.FullName }
    }
    if ($sources.Count -lt 22) { throw "Lista de fontes incompleta: $($sources.Count)" }

    $icon = Join-Path $SourceDir "Assets\bebel155.ico"
    $logo = Join-Path $SourceDir "Assets\logo_b155.png"
    $banner = Join-Path $SourceDir "Assets\banner_b155.png"
    foreach ($asset in @($icon,$logo,$banner)) { if (-not (Test-Path $asset)) { throw "Asset ausente: $asset" } }

    $refs = @(
        "mscorlib.dll", "System.dll", "System.Core.dll", "System.Drawing.dll",
        "System.Windows.Forms.dll", "System.Management.dll", "System.IO.Compression.dll",
        "System.IO.Compression.FileSystem.dll", "System.Web.Extensions.dll", "System.Security.dll"
    )

    $args = @($csc, "/nologo", "/target:winexe", "/optimize+", "/platform:anycpu", "/langversion:latest", "/nostdlib+",
        "/out:$OutputExe", "/win32icon:$icon", "/resource:$logo,Bebel155.Logo", "/resource:$banner,Bebel155.Banner")
    foreach ($r in $refs) {
        $full = Join-Path $framework $r
        if (-not (Test-Path $full)) { throw "Referencia ausente: $full" }
        $args += "/reference:$full"
    }
    $args += $sources

    LogLine "Compilando $($sources.Count) fontes com Roslyn moderno."
    $output = & $dotnet @args 2>&1
    $output | ForEach-Object { LogLine $_.ToString() }
    if ($LASTEXITCODE -ne 0) { throw "Roslyn retornou codigo $LASTEXITCODE." }
    if (-not (Test-Path $OutputExe)) { throw "Roslyn terminou sem criar o EXE." }

    LogLine "EXE criado com sucesso."
    Write-Host "[OK] Compilado com Roslyn moderno." -ForegroundColor Green
    exit 0
}
catch {
    LogLine ("ERRO: " + $_.Exception.ToString())
    Write-Host "[ERRO] Fallback moderno falhou: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
