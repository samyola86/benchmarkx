@echo off
cd /d "%~dp0"
call Build-Windows.cmd
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Verify-Windows.ps1"
pause
