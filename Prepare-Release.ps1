$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
& cmd.exe /c Build-Windows.cmd
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$release = Join-Path $PSScriptRoot 'release-benchmarkx'
foreach ($dir in @($release, "$release\tools", "$release\licenses", "$release\sources", "$release\dependencies", "$release\docs")) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
Copy-Item dist\BenchMarkX.exe, dist\DriverPrerequisiteProbe.exe, dist\*.dll, BenchMarkX.exe.config -Destination $release -Force
Copy-Item tools\diskspd.exe, tools\smartctl.exe, tools\smart-package\bin\drivedb.h -Destination "$release\tools" -Force
Copy-Item README.md -Destination $release -Force
Copy-Item docs\*.md -Destination "$release\docs" -Force
Copy-Item tools\LHM-LICENSE.txt, tools\DiskSpd-LICENSE.txt, tools\HidSharp-LICENSE.txt, tools\Microsoft-Runtime-LICENSE.txt -Destination "$release\licenses" -Force
Copy-Item tools\lhm-source\LibreHardwareMonitor-0.9.6\THIRD-PARTY-NOTICES.txt -Destination "$release\licenses\LHM-THIRD-PARTY-NOTICES.txt" -Force
Copy-Item tools\smart-package\doc\COPYING.txt -Destination "$release\licenses\smartmontools-GPL-2.txt" -Force
Copy-Item tools\diskspd-package\EULA -Destination "$release\licenses\DiskSpd-binary-EULA.txt" -Force
Copy-Item tools\LibreHardwareMonitor-source.zip, tools\smartmontools-7.5.tar.gz, tools\PawnIO-Modules-source.zip -Destination "$release\sources" -Force
$pawnInstaller=Join-Path $PSScriptRoot 'tools\PawnIO_setup-2.2.0.exe'
if((Get-FileHash -LiteralPath $pawnInstaller -Algorithm SHA256).Hash -ne '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'){throw 'PawnIO payload hash mismatch'}
if((Get-AuthenticodeSignature -LiteralPath $pawnInstaller).Status -ne 'Valid'){throw 'PawnIO payload signature is invalid'}
Copy-Item -LiteralPath $pawnInstaller -Destination "$release\dependencies\PawnIO_setup.exe" -Force
Get-ChildItem -LiteralPath $release -Recurse -File | Where-Object Name -ne 'SHA256.json' | ForEach-Object { [pscustomobject]@{ Path=$_.FullName.Substring($release.Length+1); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } } | ConvertTo-Json | Set-Content "$release\SHA256.json" -Encoding UTF8
& tools\inno\ISCC.exe Installer.iss
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Get-FileHash artifacts\BenchMark-X-1.7.0-Precision-Windows-x64-Setup.exe -Algorithm SHA256



