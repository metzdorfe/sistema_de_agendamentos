@echo off
setlocal
rem ==========================================================================
rem Fest Agenda - abre o isql (console SQL do Firebird) no banco do sistema.
rem Para manutencao/suporte. Comandos terminam com ponto e virgula; sair: QUIT;
rem Alteracoes so valem depois de COMMIT;  Feche o Fest Agenda antes.
rem ==========================================================================
call "%~dp0ambiente.bat"
chcp 65001 >nul

"%UTIL%isql.exe" -user SYSDBA -ch UTF8 "%BANCO%"
