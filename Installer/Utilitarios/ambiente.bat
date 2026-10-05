@echo off
rem ==========================================================================
rem Fest Agenda - variaveis comuns dos utilitarios (chamado pelos outros .bat).
rem As ferramentas do Firebird (isql, gbak, gfix) usam o Firebird embarcado que
rem fica na pasta do sistema (um nivel acima desta).
rem Se o config.ini apontar para outro arquivo de banco, ajuste BANCO abaixo.
rem ==========================================================================
set "UTIL=%~dp0"
for %%i in ("%~dp0..") do set "RAIZ=%%~fi"
set "FIREBIRD=%RAIZ%"
set "PATH=%RAIZ%;%PATH%"
set "BANCO=%RAIZ%\BD\basefestagenda.fdb"
