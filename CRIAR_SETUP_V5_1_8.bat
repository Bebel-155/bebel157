@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Bebel 155 v5.1.8 - Criar Setup.exe

call "%~dp0CRIAR_EXE_V5_1_8.bat"
if errorlevel 1 exit /b 1

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
  where winget >nul 2>&1
  if not errorlevel 1 (
    winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements
  )
)

if not exist "%ISCC%" (
  echo [ERRO] Inno Setup 6 nao encontrado.
  pause
  exit /b 1
)

"%ISCC%" "%~dp0Bebel155_v5_1_8.iss"
if errorlevel 1 (
  echo [ERRO] Falha ao criar Setup.exe.
  pause
  exit /b 1
)

echo Setup criado:
echo %~dp0dist\Bebel-155_Setup_V5_1_8.exe
pause
