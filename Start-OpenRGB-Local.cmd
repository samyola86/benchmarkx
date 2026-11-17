@echo off
setlocal
set "BENCHMARKX_ROOT=%~dp0"
if not exist "%~dp0tools\openrgb-1.1\OpenRGB Windows 64-bit\OpenRGB.exe" (
  echo Portable OpenRGB 1.1 is missing. See docs\HARDWARE-STUDIOS.md.
  exit /b 1
)
powershell -NoProfile -Command "$ErrorActionPreference='Stop'; $available=$false; $probe=New-Object Net.Sockets.TcpClient; try{$connection=$probe.ConnectAsync('127.0.0.1',6742); $available=$connection.Wait(500) -and $probe.Connected}catch{}finally{$probe.Dispose()}; if(!$available){ $exe=Join-Path $env:BENCHMARKX_ROOT 'tools\openrgb-1.1\OpenRGB Windows 64-bit\OpenRGB.exe'; $config=Join-Path $env:BENCHMARKX_ROOT 'tools\openrgb-1.1\config'; New-Item -ItemType Directory -Path $config -Force | Out-Null; Start-Process -FilePath $exe -Verb RunAs -WindowStyle Hidden -ArgumentList @('--server','--server-host','127.0.0.1','--server-port','6742','--noautoconnect','--config',([string][char]34+$config+[char]34)) }"
if errorlevel 1 exit /b 1
echo OpenRGB local server: 127.0.0.1:6742. In BenchMark X, open RGB Studio and click Connect.
exit /b 0
