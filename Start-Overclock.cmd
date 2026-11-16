@echo off
cd /d "%~dp0"
call Build-Windows.cmd
if errorlevel 1 exit /b 1
start "" "%~dp0dist\BenchMarkX.exe"

