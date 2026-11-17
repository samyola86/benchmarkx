<div align="center">
  <img src="assets/logo.png" alt="BenchMark X" width="520" />

  <h1>BenchMark X</h1>
  <p><strong>Your hardware. Your performance. In focus.</strong></p>
  <p>A native Windows hardware dashboard, storage benchmark and GPU stress utility.</p>
  <p><img src="https://img.shields.io/badge/version-1.7.0-b83240" alt="Version 1.7.0" /> <img src="https://img.shields.io/badge/platform-Windows%2011%20x64-202024" alt="Windows 11 x64" /> <img src="https://img.shields.io/badge/runtime-.NET%20Framework%204.8-202024" alt=".NET Framework 4.8" /></p>
</div>



BenchMark X brings live hardware telemetry, clear storage results and an independent OpenGL stress workload into a graphite interface with angular metallic cards, red headers, vendor-aware hardware artwork and filled live charts. It runs as a native C# Windows Forms application, with no browser or Python runtime.

The Precision redesign adds shared dark scrolling, a three-pane Sensors workspace, hardware identity banners and consistent racing-inspired panels across every route. Application version 1.7.0 and existing data formats are retained. See [redesign notes](docs/PRECISION-REDESIGN.md).

## Features

| Feature | What you get |
| --- | --- |
| Live dashboard | CPU/GPU utilization, temperatures and history; memory, network and sensor summaries |
| Storage overview | Total internal capacity, named drives, and used/free space meters |
| Storage benchmarks | Sequential and random read/write tests, copy workloads and compression sensitivity measurements |
| Shareable results | Large read/write scorecards, PNG export and clipboard copying, with optional IOPS and latency |
| S.M.A.R.T. health | Available ATA/NVMe health, temperature, endurance, error counters and raw smartctl reports |
| GPU stress | 720p through 4K, windowed/fullscreen rendering, live telemetry overlay and an **Until stopped** duration |
| Reports | Saved runs, comparable-workload graphs, and CSV/JSON/text exports |
| RGB Studio | Optional loopback OpenRGB SDK integration, capability-driven controls, explicit Apply/Restore and local presets |
| Display Studio | Read-only Windows discovery, supported DDC/CI/WMI controls and explicit current-user SDR ICC associations |
| Compact monitor | Always-on-top CPU/GPU/RAM view and system-tray controls |

## Getting started

**Requirements:** Windows 11 x64, .NET Framework 4.8, and administrator permission for low-level hardware access. GPU stress currently requires exactly one NVIDIA GPU with working NVIDIA SMI temperature telemetry. Monitoring support varies by hardware and driver.

For a packaged build, run `BenchMark-X-1.7.0-Precision-Windows-x64-Setup.exe`. Setup installs the app into Program Files and automatically provisions the bundled, signed PawnIO driver. There is no separate driver checkbox or download. Windows displays its normal administrator prompt; a restart may be required by the driver.

This repository includes the application source, logo, icon and hardware artwork. Download the Windows installer from [Releases](https://github.com/samyola86/benchmarkx/releases). Downloaded third-party dependencies and generated development files are excluded from the source tree. See [Building from source](docs/BUILDING.md) for the dependency and packaging steps.

## Storage results you can share

Choose a physical drive and workload preset, review the test settings, then run all tests or an individual measurement. Results report **MB/s**, with **IOPS** and average latency available on demand. Use **Save PNG** or **Copy image** for a branded scorecard, or share a saved run from Reports.

Storage tests use newly created temporary files on a validated mounted volume. They do not write directly to raw disks. Tests still generate real I/O and writes; review capacity, duration and repetitions before starting. A forced termination can leave that run's uniquely named temporary folder.

See [Hardware Studios](docs/HARDWARE-STUDIOS.md) for opt-in control, provider requirements, profiles and recovery, and [studio validation](docs/STUDIO-VALIDATION.md) for remaining hardware/DPI release gates.

## Hardware health and stress testing

Drive Health reads supported S.M.A.R.T. data through smartctl and can refresh every two minutes. Missing attributes stay unavailable; a health pass does not erase reported errors. Some drive controllers and USB bridges restrict access.

The GPU workload displays FPS, elapsed time, temperature, load, power, clocks, VRAM and available fan telemetry directly over the renderer. **Until stopped** removes the duration limit while preserving thermal cutoff, telemetry-loss detection and manual cancellation. Stay present during a stress run; use Stop, Escape or close the renderer to cancel.

These are independent workloads. BenchMark X does not calculate proprietary CrystalDiskMark, AS SSD or FurMark scores, and does not claim equivalent results. See [workload methodology](docs/METHODOLOGY.md) for cache policy, queue depth and comparison limits.

## Development

```bat
Build-Windows.cmd
Start-BenchMark-X.cmd
```

Prepare the dependencies first using [BUILDING.md](docs/BUILDING.md). The build uses the Windows .NET Framework compiler and produces `dist/BenchMarkX.exe`. `Verify-Windows.ps1` performs safe functional checks without starting SSD write benchmarks or GPU stress. `Prepare-Release.ps1` stages the dependencies, notices and corresponding source archives, then compiles the Inno Setup installer.

| Path | Purpose |
| --- | --- |
| `Program.cs`, `MainModern.cs`, `ModernUi.cs` | Application entry point and interface |
| `Monitoring.cs`, `Workers.cs` | Supervised telemetry and workload workers |
| `GpuStress.cs`, `StressOverlay.cs` | OpenGL stress renderer and telemetry overlay |
| `HealthView.cs`, `SensorView.cs`, `StorageCapacity.cs` | Drive health, sensors and capacity reporting |
| `BenchmarkResults.cs`, `DiskScoreboard.cs`, `Reports.cs` | Results, image sharing and report persistence |
| `Branding.cs`, `MetricCard.cs`, `assets/` | Branding and dashboard rendering |
| `Tests.cs`, `Verify-Windows.ps1` | Functional checks and isolated fixtures |
| `Installer.iss`, `Prepare-Release.ps1` | Windows packaging and driver prerequisite checks |

Existing settings and history remain in `%LOCALAPPDATA%\OverclockNative` for compatibility. Reports are stored in its `Reports` subdirectory and GPU telemetry in `Telemetry`.

## Project status

Version **1.7.0** is a development build. Build, interface navigation, safe functional checks, and installer/uninstaller validation have been performed locally. This is not a claim that every hardware configuration, workload mode or failure path has been validated. Application and installer binaries are currently unsigned; the bundled upstream PawnIO installer is signed.

For behavior and usage details, see the [user guide](docs/USAGE.md). To report a problem, [open an issue](https://github.com/samyola86/benchmarkx/issues) with the app version, Windows version, hardware model, reproduction steps and relevant logs. Review logs for serial numbers or other personal information before sharing them.

## Third-party components

BenchMark X uses [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [Microsoft DiskSpd](https://github.com/microsoft/diskspd), [smartmontools](https://www.smartmontools.org/) and [PawnIO](https://github.com/namazso/PawnIO.Setup). These components retain their own licenses. See [third-party notices](THIRD-PARTY-NOTICES.md) and the dependency packages for full terms.

No license for the original BenchMark X source or branding has been granted in this repository yet. Third-party license files apply only to their respective components.
