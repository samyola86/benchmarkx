# BenchMark X 1.6.1 — Windows 11 x64

Run `artifacts\BenchMark-X-1.6.1-Windows-x64-Setup.exe` to install, or launch `dist\BenchMarkX.exe` from this workspace. The application is native C# / Windows Forms and uses .NET Framework 4.8 included with Windows 11. No Python, browser, PATH configuration or user compilation is needed. `Build-Windows.cmd` rebuilds the developer source using the Windows Framework compiler.

## Branding and sharing

BenchMark X uses the supplied logo throughout the interface and shareable scorecards, and the supplied symbol for the application, installer and system tray icons. Minimize sends the app to the tray; double-click its icon or choose Open BenchMark X to restore it. Exit follows the normal workload-stop behavior.

SSD results use large read/write MB/s cells. Save PNG exports a branded image; Copy image puts the same scorecard on the clipboard. IOPS / latency reveals additional measured metrics. Reports also provides Share PNG for a single selected disk run. Images include the drive model/capacity, workload settings, status and timestamp; serial numbers are omitted from the normal drive title. Existing measurements and workload engines are retained.

The dashboard shows total physical internal storage, a used/free meter for unique mounted volumes, and individual drive names. The neutral remainder covers capacity outside the mounted-volume totals, including system, unallocated or unmounted space. SSDs are listed before other drives. CPU/GPU model names use larger bold text with a soft, Gaussian-blurred red halo.

For continuity, existing history and settings remain under `%LOCALAPPDATA%\OverclockNative`. The internal compatibility directory name is retained intentionally; no history is deleted by the rebrand.

## Interface

The black and charcoal dashboard uses large live metrics, temperature rings and history graphs. The sidebar groups hardware and storage tests into tabs. Benchmark settings start with a drive and preset; Advanced settings reveals timing, repetition and custom workload controls. Sensor and hardware readings use aligned tables. Drive Health highlights temperature, endurance and warnings, with raw SMART behind Technical details. Reports displays measured metrics and comparison graphs; full logs remain available on demand.

## Monitoring

Overview provides CPU, GPU, memory, storage, network and sensor cards with CPU/GPU history. CPU/GPU/Memory/Storage pages provide detail. Sensors has a hardware tree, current/minimum/maximum values, selected-sensor history and reset. Settings persists the 1–10 second sensor interval and always-on-top preference. Mini monitor is an always-on-top compact CPU/GPU/RAM view; click its heading to expand.

LibreHardwareMonitor runs inside a dedicated, supervised collector process. The standalone LibreHardwareMonitor application is unnecessary. Certain low-level CPU, motherboard, voltage and fan sensors require the signed PawnIO driver and elevated hardware access. The installer automatically provisions the bundled signed PawnIO driver and verifies hardware access before completing. There is no separate driver checkbox or download. Windows UAC requests administrator permission for setup and app startup. Setup installs the app into Program Files; a driver-requested restart is reported before app launch. Missing or unsupported readings remain unavailable. The app does not change Windows security settings. Providers that stall are terminated and retried without blocking navigation.

## Storage testing

Select a physical disk using disk number, model, capacity and serials. The native SMART serial distinguishes devices whose Windows-reported serial is duplicated. The app revalidates identity and resolves a suitable mounted volume before creating a unique temporary directory. It never writes to raw disks or existing user files.

Disk Benchmark offers CDM-inspired default/NVMe and custom workloads. AS SSD Benchmark, Copy Benchmark and Compression Benchmark have separate pages. Choose Run All or an individual test. Review the confirmation showing disk, file size, per-phase duration and repetitions before starting. Stop requests cooperative cancellation and terminates unresponsive subprocesses. A hard kill can leave the uniquely named `Overclock-test-...` folder; remove only that run's folder after the worker is stopped.

DiskSpd and smartctl are bundled. The benchmark reports MB/s, IOPS and average latency. Compression charts use actual measured points. These are independent workloads, without proprietary scores. See `docs\METHODOLOGY.md` for cache policy, workload differences and comparison limits.

## Drive health

Choose a physical drive on Drive Health. The native smartctl report includes available ATA/NVMe identity, firmware, temperature, life-used/spare, host read/write totals, power hours/cycles, critical warnings, media errors and attribute/error tables. Refresh is manual or every 120 seconds. Unsupported attributes remain absent/unavailable. Nonzero smartctl bitmasks are explained while retaining all returned data. A reported health pass does not erase error counters. Some storage providers require running the app as administrator.

## GPU stress

The independent native OpenGL workload renders an offscreen torus shader at the selected 720p/1080p/1440p/4K resolution, then presents it windowed or fullscreen. It requires exactly one NVIDIA GPU and valid NVIDIA SMI temperature telemetry; this version does not implement arbitrary multi-GPU affinity. Duration and cutoff are configurable. Select **Until stopped** for unlimited runtime; thermal cutoff, telemetry-loss detection, Escape, window close and Stop remain active. The duration watchdog is disabled only for this mode. Current/minimum/maximum/average FPS, elapsed time, frames, resolution, temperature, load, power, VRAM, core/memory clocks, fan and live temperature history are shown directly over the renderer. Unsupported telemetry is marked unavailable. Full temperature/FPS history streams to `%LOCALAPPDATA%\OverclockNative\Telemetry`; only the latest 600 samples stay in renderer memory. The report includes the CSV path. Escape, Stop or closing the renderer cancels the test. Temperature cutoff, telemetry loss, stale progress and duration stop it automatically. Stay present during stress testing. It is not a FurMark score.

## Reports

Reports are stored in `%LOCALAPPDATA%\OverclockNative\Reports` as structured JSON, preserving settings, hardware identity, status, measured metrics and the full report. Select multiple runs for workload comparison bars; compare equivalent settings. Export selected records as CSV, JSON or readable text. Failed/cancelled runs are retained with their status and excluded from completed-run comparison bars. Previous text-only reports are preserved in the same directory.

## Development and validation

`Verify-Windows.ps1` runs safe checks and does not start SSD write benchmarks or GPU stress. `tests` contains actual execution evidence and application-rendered layout captures. Test fixtures are isolated from real report history. `docs\ACCEPTANCE.md` lists verified and pending acceptance criteria. `Prepare-Release.ps1` builds/stages the application and compiles `Installer.iss` using the local Inno Setup compiler. The installer includes dependency license notices and corresponding source archives for monitoring components and smartmontools. Third-party tools retain their upstream licenses.

Real SSD write workloads and GPU stress tests require approval of the exact parameters before developer execution. Features awaiting that validation are not represented as tested. The application and installer are unsigned development builds; no reference-application performance equivalence is claimed.



