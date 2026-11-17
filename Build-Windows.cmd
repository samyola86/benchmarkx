@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo .NET Framework 4.8 or later is required.
  exit /b 1
)
if not exist dist mkdir dist
if not exist dist\LibreHardwareMonitorLib.dll (
  echo Missing LibreHardwareMonitor library. Restore the packaged dependencies.
  exit /b 1
)
set "ART="
for %%f in (assets\hardware\cpu-*.png assets\hardware\gpu-*.png assets\hardware\network-*.png assets\hardware\memory.png assets\hardware\storage.png assets\hardware\sensors.png) do call set "ART=%%ART%% /resource:%%f,BenchMarkX.Art.%%~nf"
"%CSC%" /nologo /target:winexe %ART% /platform:x64 /optimize+ /out:dist\BenchMarkX.exe /win32manifest:BenchMarkX.manifest /win32icon:assets\BenchMarkX.ico /resource:assets\logo.png,BenchMarkX.Logo /resource:assets\favcon.png,BenchMarkX.Symbol /resource:assets\BenchMarkX.ico,BenchMarkX.Icon /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.Xml.dll /reference:dist\LibreHardwareMonitorLib.dll StudioModels.cs HdrDisplay.cs DisplayStudio.cs RgbStudio.cs StudioView.cs StudioTests.cs StudioUiTests.cs Sidebar.cs ScrollTests.cs DarkScrolling.cs PrecisionUi.cs DashboardArt.cs Program.cs Workers.cs GpuStress.cs Monitoring.cs Reports.cs SensorView.cs HealthView.cs Tests.cs ModernUi.cs MainModern.cs StressOverlay.cs Branding.cs StorageCapacity.cs MetricCard.cs DiskScoreboard.cs BenchmarkResults.cs AssemblyInfo.cs
if errorlevel 1 (
  echo Build failed. Please share the error above.
  exit /b 1
)
"%CSC%" /nologo /target:exe /platform:x64 /optimize+ /out:dist\DriverPrerequisiteProbe.exe DriverPrerequisiteProbe.cs
if errorlevel 1 exit /b 1
copy /y BenchMarkX.exe.config dist\BenchMarkX.exe.config >nul
echo Build successful.
exit /b 0



