@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Bebel 155 v5.2.0 - Compilar EXE

set "OUTDIR=%~dp0build"
set "EXE=%OUTDIR%\Bebel-155_V5_2_0.exe"
set "LOG=%OUTDIR%\compilacao.log"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"
del /q "%EXE%" >nul 2>&1
del /q "%LOG%" >nul 2>&1

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if exist "%CSC%" (
  "%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
   /out:"%EXE%" ^
   /win32icon:"%~dp0Assets\bebel155.ico" ^
   /resource:"%~dp0Assets\logo_b155.png",Bebel155.Logo ^
   /resource:"%~dp0Assets\banner_b155.png",Bebel155.Banner ^
   /reference:System.dll ^
   /reference:System.Core.dll ^
   /reference:System.Drawing.dll ^
   /reference:System.Windows.Forms.dll ^
   /reference:System.Management.dll ^
   /reference:System.IO.Compression.dll ^
   /reference:System.IO.Compression.FileSystem.dll ^
   /reference:System.Web.Extensions.dll ^
   /reference:System.Security.dll ^
   "%~dp0Bebel155_v5_2_0.cs" ^
   "%~dp0Core\DeviceModels.cs" ^
   "%~dp0Core\ICommandRunner.cs" ^
   "%~dp0Core\CommandRunner.cs" ^
   "%~dp0Core\GithubReleaseParser.cs" ^
   "%~dp0Devices\DeviceDiscovery.cs" ^
   "%~dp0Devices\AndroidProbe.cs" ^
   "%~dp0Devices\AppleProbe.cs" ^
   "%~dp0Devices\DeviceResolver.cs" ^
   "%~dp0Catalogs\CatalogModels.cs" ^
   "%~dp0Catalogs\CatalogManager.cs" ^
   "%~dp0Market\MarketModels.cs" ^
   "%~dp0Market\IMarketAdapter.cs" ^
   "%~dp0Market\MercadoLivreAdapter.cs" ^
   "%~dp0Market\PriceNormalizer.cs" ^
   "%~dp0Market\MarketCache.cs" ^
   "%~dp0Market\MarketPriceService.cs" >"%LOG%" 2>&1
)

if exist "%EXE%" (
  echo [OK] EXE criado:
  echo %EXE%
  pause
  exit /b 0
)

echo Compilador .NET Framework antigo nao criou o EXE. Tentando Roslyn moderno...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0COMPILAR_COM_DOTNET_MODERNO.ps1" ^
 -SourceDir "%~dp0" -OutputExe "%EXE%" -LogFile "%LOG%" -SourceFile "Bebel155_v5_2_0.cs"

if exist "%EXE%" (
  echo [OK] EXE criado:
  echo %EXE%
  pause
  exit /b 0
)

echo [ERRO] A compilacao falhou.
if exist "%LOG%" type "%LOG%"
pause
exit /b 1
