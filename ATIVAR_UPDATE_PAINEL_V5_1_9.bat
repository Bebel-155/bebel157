@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Bebel 155 - Ativar atualizacao pelo painel v5.1.9

echo ============================================================
echo BEBEL 155 - PONTE DE ATUALIZACAO v5.1.9 ^> v5.2.0
echo ============================================================
echo.
echo Este ajuste muda somente a fonte de update do seu usuario Windows.
echo O Bebel continuara baixando a atualizacao pelo proprio painel.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ATIVAR_UPDATE_PAINEL_V5_1_9.ps1"
if errorlevel 1 (
  echo.
  echo [ERRO] Nao foi possivel configurar o atualizador.
  pause
  exit /b 1
)
echo.
echo [OK] Configurado.
echo Abra o Bebel 155 ^> Atualizacoes ^> Verificar Bebel 155.
pause
endlocal
