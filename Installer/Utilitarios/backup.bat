@echo off
setlocal
rem ==========================================================================
rem Fest Agenda - backup do banco (gbak) para Utilitarios\Backups,
rem com data e hora no nome do arquivo.
rem Feche o Fest Agenda antes: o Firebird embarcado abre o banco com exclusividade.
rem ==========================================================================
call "%~dp0ambiente.bat"

if not exist "%BANCO%" (
  echo Banco nao encontrado: "%BANCO%"
  pause
  exit /b 1
)

if not exist "%UTIL%Backups" mkdir "%UTIL%Backups"
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HHmmss"') do set "DATA=%%i"
set "DESTINO=%UTIL%Backups\basefestagenda_%DATA%.fbk"

echo Fazendo backup de "%BANCO%"...
"%UTIL%gbak.exe" -b -g -v -y "%UTIL%Backups\ultimo-backup.log" -user SYSDBA "%BANCO%" "%DESTINO%"
if errorlevel 1 (
  echo.
  echo ERRO no backup. O Fest Agenda esta aberto? Feche o sistema e tente de novo.
  echo Detalhes em "%UTIL%Backups\ultimo-backup.log".
  pause
  exit /b 1
)

echo.
echo Backup gravado em "%DESTINO%"
echo Guarde uma copia fora deste computador (pen drive, nuvem).
pause
