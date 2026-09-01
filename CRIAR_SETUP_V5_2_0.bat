@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Bebel 155 v5.2.0 - Criar Setup.exe

call "%~dp0CRIAR_EXE_V5_2_0.bat"
if errorlevel 1 exit /b 1

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
  where winget >nul 2>&1
  if not errorlevel 1 winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements
)

if not exist "%ISCC%" (
  echo [ERRO] Inno Setup 6 nao encontrado.
  pause
  exit /b 1
)

"%ISCC%" "%~dp0Bebel155_v5_2_0.iss"
if errorlevel 1 (
  echo [ERRO] Falha ao criar Setup.exe.
  pause
  exit /b 1
)

if not exist "%~dp0dist\Bebel-155_Setup_V5_2_0.exe" (
  echo [ERRO] Setup esperado nao foi criado.
  pause
  exit /b 1
)

echo [OK] Setup criado:
echo %~dp0dist\Bebel-155_Setup_V5_2_0.exe
pause
