@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
title Bebel 155 - Publicar no GitHub bebel157

set "SOURCE=%CD%"
set "REPO=https://github.com/Bebel-155/bebel157.git"
set "TMPROOT=%TEMP%\Bebel155_Publish_%RANDOM%_%RANDOM%"
set "CLONE=%TMPROOT%\repo"

echo ============================================================
echo BEBEL 155 - PUBLICACAO SEGURA
echo Repositorio: %REPO%
echo ============================================================
echo.
echo Esta versao publica usando um clone temporario limpo.
echo Ela NAO executa git pull/rebase dentro da pasta do programa.
echo.

where git >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Git for Windows nao foi encontrado.
  pause
  exit /b 1
)

where robocopy >nul 2>&1
if errorlevel 1 (
  echo [ERRO] robocopy nao foi encontrado no Windows.
  pause
  exit /b 1
)

if exist "%TMPROOT%" rmdir /s /q "%TMPROOT%"
mkdir "%TMPROOT%" >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Nao foi possivel criar a pasta temporaria:
  echo %TMPROOT%
  pause
  exit /b 1
)

echo [1/5] Clonando a versao atual do GitHub...
git clone "%REPO%" "%CLONE%"
if errorlevel 1 (
  echo.
  echo [ERRO] Nao foi possivel clonar o repositorio.
  echo Se o GitHub pedir login, conclua a autenticacao e tente novamente.
  echo Pasta temporaria: %TMPROOT%
  pause
  exit /b 1
)

cd /d "%CLONE%"

git checkout main >nul 2>&1
if errorlevel 1 (
  git show-ref --verify --quiet refs/remotes/origin/main
  if not errorlevel 1 git checkout -b main origin/main >nul 2>&1
)

if errorlevel 1 (
  echo [ERRO] A branch main nao foi encontrada no clone.
  echo Pasta temporaria: %TMPROOT%
  pause
  exit /b 1
)

echo [2/5] Limpando apenas arquivos versionados antigos do Bebel...
del /q "Bebel155_v5_1_*.cs" >nul 2>&1
del /q "Bebel155_v5_1_*.iss" >nul 2>&1
del /q "CRIAR_EXE_V5_1_*.bat" >nul 2>&1
del /q "CRIAR_SETUP_V5_1_*.bat" >nul 2>&1
del /q "INICIAR_V5_1_*.bat" >nul 2>&1
del /q "LEIA-ME_V5_1_*.txt" >nul 2>&1
del /q "Bebel155_v5_2_*.cs" >nul 2>&1
del /q "Bebel155_v5_2_*.iss" >nul 2>&1
del /q "CRIAR_EXE_V5_2_*.bat" >nul 2>&1
del /q "CRIAR_SETUP_V5_2_*.bat" >nul 2>&1
del /q "INICIAR_V5_2_*.bat" >nul 2>&1
del /q "LEIA-ME_V5_2_*.txt" >nul 2>&1

echo [3/5] Copiando a v5.2.0 para o clone temporario...
robocopy "%SOURCE%" "%CLONE%" /E /R:2 /W:1 /COPY:DAT /DCOPY:DAT /NFL /NDL /NJH /NJS /NP ^
 /XD "%SOURCE%\.git" "%SOURCE%\build" "%SOURCE%\dist" "%SOURCE%\.vs" "%SOURCE%\bin" "%SOURCE%\obj" ^
 /XF "device-images.json" "compilacao.log"
set "ROBO=!ERRORLEVEL!"

if !ROBO! GEQ 8 (
  echo.
  echo [ERRO] Robocopy falhou com codigo !ROBO!.
  echo Pasta temporaria preservada para diagnostico:
  echo %TMPROOT%
  pause
  exit /b 1
)

if not exist "%CLONE%\device-images.json" (
  if exist "%SOURCE%\device-images.json" copy /y "%SOURCE%\device-images.json" "%CLONE%\device-images.json" >nul
)

echo [4/5] Criando commit...
git config user.name "Bebel-155"
git config user.email "bebel155@users.noreply.github.com"
git add -A

git diff --cached --quiet
if not errorlevel 1 (
  echo Nenhuma alteracao nova para publicar.
) else (
  git commit -m "Bebel 155 v5.2.0 - reconhecimento, drivers offline e valor de mercado"
  if errorlevel 1 (
    echo [ERRO] Falha ao criar commit.
    echo Pasta temporaria: %TMPROOT%
    pause
    exit /b 1
  )
)

echo [5/5] Enviando para a branch main...
git push origin HEAD:main
if errorlevel 1 (
  echo.
  echo [ERRO] O push nao foi concluido.
  echo Nenhum force push foi usado.
  echo Pasta temporaria preservada para diagnostico:
  echo %TMPROOT%
  pause
  exit /b 1
)

cd /d "%SOURCE%"
rmdir /s /q "%TMPROOT%" >nul 2>&1

echo.
echo ============================================================
echo [OK] PUBLICACAO ENVIADA PARA O GITHUB
echo ============================================================
echo.
echo Configurando esta instalacao v5.1.9 para usar o manifest de compatibilidade...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SOURCE%\ATIVAR_UPDATE_PAINEL_V5_1_9.ps1"
if errorlevel 1 (
  echo [AVISO] O codigo foi publicado, mas a ponte do atualizador local nao foi configurada.
  echo Execute depois ATIVAR_UPDATE_PAINEL_V5_1_9.bat.
) else (
  echo [OK] Ponte de atualizacao local configurada.
)
echo.
echo Aguarde o GitHub Actions ficar verde. Depois use no Bebel 155:
echo Atualizacoes ^> Verificar Bebel 155 ^> Baixar / aplicar update.
start "" "https://github.com/Bebel-155/bebel157/actions"
start "" "https://github.com/Bebel-155/bebel157/releases"
pause
endlocal
