@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Bebel 155 - Reparar WinGet / App Installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0REPARAR_WINGET_APP_INSTALLER.ps1"
endlocal
