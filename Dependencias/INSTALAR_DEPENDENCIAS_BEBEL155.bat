@echo off
setlocal
cd /d "%~dp0"
title Bebel Equipe Do Mais Novo 155 - Corrigir Dependencias

net session >nul 2>&1
if errorlevel 1 (
    echo Solicitando permissao de Administrador...
    powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0INSTALAR_DEPENDENCIAS_BEBEL155.ps1"
if errorlevel 1 (
    echo.
    echo O instalador encontrou um erro.
    echo Envie uma captura da mensagem exibida acima.
    pause
)
endlocal
