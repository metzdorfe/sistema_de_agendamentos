@echo off
setlocal
rem ==========================================================================
rem Fest Agenda - verifica a integridade do banco (gfix -v -full).
rem So le e informa; nao corrige nada. Feche o Fest Agenda antes.
rem ==========================================================================
call "%~dp0ambiente.bat"

echo Verificando "%BANCO%"...
"%UTIL%gfix.exe" -v -full -user SYSDBA "%BANCO%"
if errorlevel 1 (
  echo.
  echo O gfix encontrou problemas ou nao conseguiu abrir o banco ^(o Fest Agenda esta aberto?^).
  echo Faca um backup e procure o suporte antes de continuar usando o sistema.
) else (
  echo.
  echo Nenhum problema encontrado.
)
pause
