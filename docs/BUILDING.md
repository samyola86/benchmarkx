# Build and package BenchMark X

## Prerequisites

- Windows 11 x64 with .NET Framework 4.8.
- PowerShell and the Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
- The upstream dependency packages below. This source repository does not vendor executable dependencies.

## Branding files

The required logo, symbol, icon and ten hardware illustrations are tracked under `assets/`. The build embeds these resources in the executable, so no artwork download is needed. See [assets/README.md](../assets/README.md).

## Restore runtime dependencies

1. Download **LibreHardwareMonitor 0.9.6** from its [official release](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6). Extract it into `tools/lhm-package/`. Create `dist/` and copy all DLL files from the extracted package into `dist/`, preserving the complete dependency set. The standalone monitor executable is not needed by BenchMark X.
2. Obtain **DiskSpd 2.3 x64** from [Microsoft's releases](https://github.com/microsoft/diskspd/releases). Keep the extracted package under `tools/diskspd-package/` and copy its x64 `diskspd.exe` to `tools/diskspd.exe`. `Setup-DiskSpd.ps1` is a convenience downloader for the latest upstream version; use the specified version when reproducing this build.
3. Obtain **smartmontools 7.5 for Windows** from the [official project downloads](https://www.smartmontools.org/wiki/Download). Preserve the installed/extracted package under `tools/smart-package/`, including `bin/drivedb.h` and `doc/COPYING.txt`. Copy `smartctl.exe` to `tools/smartctl.exe`.

Then run `Build-Windows.cmd`. Successful compilation creates `dist/BenchMarkX.exe` and `dist/DriverPrerequisiteProbe.exe`. Launch through `Start-BenchMark-X.cmd`. Windows requests administrator permission; low-level sensors also need the signed PawnIO driver.

## Build the installer

In addition to the runtime dependencies, prepare these files and folders used by `Prepare-Release.ps1`:

| Path | Required contents |
| --- | --- |
| `tools/PawnIO_setup-2.2.0.exe` | Official signed [PawnIO 2.2.0 setup](https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0) |
| `tools/inno/ISCC.exe` | Inno Setup 6 compiler and its runtime files, from [jrsoftware.org](https://jrsoftware.org/isinfo.php) |
| `tools/LibreHardwareMonitor-source.zip` | Corresponding LibreHardwareMonitor 0.9.6 source archive |
| `tools/lhm-source/LibreHardwareMonitor-0.9.6/` | Extracted source, including `THIRD-PARTY-NOTICES.txt` |
| `tools/smartmontools-7.5.tar.gz` | Corresponding smartmontools 7.5 source archive |
| `tools/PawnIO-Modules-source.zip` | Corresponding PawnIO module source for the monitoring dependency |
| `tools/diskspd-package/EULA` | Upstream DiskSpd binary terms |

The four `tools/*-LICENSE.txt` files required by the packaging script are tracked here. Preserve other upstream notices in their packages. Check the corresponding source matches the binaries you distribute.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Prepare-Release.ps1
```

The script checks the PawnIO payload SHA-256 and Authenticode signature before packaging. Do not bypass those checks. It writes staged files to `release-benchmarkx/` and the installer to `artifacts/`.

Setup installs PawnIO when needed and probes access before completing. It reports a driver-requested restart and suppresses immediate app launch in that case. Uninstalling BenchMark X retains the shared driver.

## Validation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-Windows.ps1
```

This script requests elevation, compiles the app and runs functional checks, including read-only DiskSpd validation, hardware enumeration and S.M.A.R.T. queries. It generates its fixtures and outputs under `tests/`; those files are ignored because they can contain local device identifiers. It does not start SSD write benchmarks or GPU stress.

Validate real stress/write workloads separately with explicit settings and attentive supervision. Clean-checkout dependency restoration has been documented here; it has not been re-executed as part of publishing the source.
