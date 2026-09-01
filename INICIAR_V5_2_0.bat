@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0build\Bebel-155_V5_2_0.exe"
if not exist "%EXE%" (
  call "%~dp0CRIAR_EXE_V5_2_0.bat"
  if errorlevel 1 exit /b 1
)
start "" "%EXE%"
endlocal
