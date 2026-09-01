@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Bebel 155 - Publicar no GitHub bebel157

echo ============================================================
echo BEBEL 155 - PUBLICACAO
echo Repositorio: https://github.com/Bebel-155/bebel157.git
echo ============================================================
echo.

where git >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Git for Windows nao foi encontrado.
  pause
  exit /b 1
)

if not exist ".git" git init

git branch -M main

git remote get-url origin >nul 2>&1
if errorlevel 1 (
  git remote add origin https://github.com/Bebel-155/bebel157.git
) else (
  git remote set-url origin https://github.com/Bebel-155/bebel157.git
)

git config user.name >nul 2>&1
if errorlevel 1 git config user.name "Bebel-155"

git config user.email >nul 2>&1
if errorlevel 1 git config user.email "bebel155@users.noreply.github.com"

echo Buscando o repositorio remoto...
git fetch origin

git show-ref --verify --quiet refs/remotes/origin/main
if not errorlevel 1 (
  echo Integrando main remota sem force push...
  git pull --rebase origin main
  if errorlevel 1 (
    echo [ERRO] Houve conflito ao integrar a branch main.
    echo Nenhum force push foi executado.
    pause
    exit /b 1
  )
)

git add .

git diff --cached --quiet
if errorlevel 1 (
  git commit -m "Bebel 155 v5.1.7 - novo repositorio e updater"
  if errorlevel 1 (
    echo [ERRO] Falha ao criar commit.
    pause
    exit /b 1
  )
) else (
  echo Nenhuma alteracao nova para commit.
)

echo Enviando para Bebel-155/bebel157...
git push -u origin main
if errorlevel 1 (
  echo [ERRO] Push nao concluido.
  echo Se o GitHub abrir autenticacao, conclua pelo navegador e tente novamente.
  pause
  exit /b 1
)

echo [OK] Publicado na branch main.
start "" "https://github.com/Bebel-155/bebel157/actions"
start "" "https://github.com/Bebel-155/bebel157/releases"
pause
endlocal
