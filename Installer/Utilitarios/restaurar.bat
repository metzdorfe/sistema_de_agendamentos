@echo off
setlocal
rem ==========================================================================
rem Fest Agenda - restaura um backup (.fbk) por cima do banco atual.
rem Uso: restaurar.bat [arquivo.fbk]   (sem parametro, pergunta o arquivo)
rem O banco atual NAO e apagado: fica guardado como basefestagenda_antes-da-restauracao_<data>.fdb
rem Feche o Fest Agenda antes.
rem ==========================================================================
call "%~dp0ambiente.bat"

set "ORIGEM=%~1"
if "%ORIGEM%"=="" (
  echo Backups disponiveis em "%UTIL%Backups":
  dir /b /o-d "%UTIL%Backups\*.fbk" 2>nul
  echo.
  set /p "ORIGEM=Caminho do arquivo .fbk (ou so o nome, se estiver em Backups): "
)
if not exist "%ORIGEM%" if exist "%UTIL%Backups\%ORIGEM%" set "ORIGEM=%UTIL%Backups\%ORIGEM%"
if not exist "%ORIGEM%" (
  echo Arquivo nao encontrado: "%ORIGEM%"
  pause
  exit /b 1
)

echo.
echo O banco atual sera substituido pelo backup:
echo   "%ORIGEM%"
set /p "OK=Confirma? (S/N) "
if /i not "%OK%"=="S" exit /b 0

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HHmmss"') do set "DATA=%%i"
set "NOVO=%RAIZ%\BD\basefestagenda_restaurando.fdb"
if exist "%NOVO%" del "%NOVO%"

echo Restaurando...
"%UTIL%gbak.exe" -c -v -y "%UTIL%Backups\ultima-restauracao.log" -user SYSDBA "%ORIGEM%" "%NOVO%"
if errorlevel 1 (
  echo.
  echo ERRO na restauracao. O banco atual nao foi alterado.
  echo Detalhes em "%UTIL%Backups\ultima-restauracao.log".
  if exist "%NOVO%" del "%NOVO%"
  pause
  exit /b 1
)

if exist "%BANCO%" ren "%BANCO%" "basefestagenda_antes-da-restauracao_%DATA%.fdb"
if errorlevel 1 (
  echo.
  echo ERRO: nao foi possivel trocar o banco. O Fest Agenda esta aberto?
  pause
  exit /b 1
)
ren "%NOVO%" "basefestagenda.fdb"

echo.
echo Restauracao concluida. O banco anterior ficou em BD\basefestagenda_antes-da-restauracao_%DATA%.fdb
pause
