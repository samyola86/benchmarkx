# Feature acceptance checklist

Updated 16 November 2026. **The full master-prompt acceptance criteria have not all passed.** See `TEST-RESULTS.md` for evidence and `METHODOLOGY.md` for workload differences.

| Area | Implementation and safe verification | Outstanding acceptance |
|---|---|---|
| Native architecture | C# WinForms x64; isolated collectors, persistent LibreHardwareMonitor service, disk and OpenGL workers; timeouts/cancellation; native build/navigation pass | Stress/render validation pending |
| Dashboard | Dark hardware cards; CPU/GPU history; RAM/storage/network; detailed CPU/core, GPU, module and storage activity pages; compact widget | Low-level CPU/motherboard access requires driver/access |
| Sensors | Direct library, hierarchical tree, current/min/max, 300-sample history, interval/reset; 118 real sensors | Unsupported readings remain unavailable |
| Disk benchmark | Default/NVMe/custom profiles; block/QD/thread/size/repetition/duration controls; separate read/write MB/s, IOPS and measured latency; individual buttons/Run All; progress/cancel | Approved sequential SSD pilot passed; other profiles and in-load cancellation unverified |
| Physical mapping | Disk/model/capacity/Windows and native serial selection; identity revalidation; safe mounted volume; four mappings pass; temporary files only | No raw write support by design |
| AS SSD inspired | Distinct sequential, random 4K, 64-thread and random 512-byte access workloads | Real execution/reference comparison pending; proprietary method not reproduced |
| Copy/compression | ISO/program/game file distributions; five compression points and graphical curve | Real execution pending; graph shows write curve, reads stay in report |
| Drive health | Per-drive smartctl, manual/automatic refresh, ATA/NVMe fields/error tables; partial and health bitmasks preserved | Model-dependent attributes unavailable where unsupported |
| GPU stress | Real OpenGL torus, offscreen 720p–4K, windowed/fullscreen, duration/cutoff, FPS statistics, telemetry/history, Escape/Stop and watchdogs | 720p windowed pilot passed after timer-loop fix; other resolutions/fullscreen and failure paths unverified; multi-GPU affinity unsupported |
| Reports | Hardware/settings/status and structured metrics; comparison bars; CSV/JSON/text; persistence tests pass | Three real pilot records persisted/exported; legacy text retained on disk |
| UI | Required pages connected; individual workloads; all pages/widget navigation pass; layout captures inspected | In-load interactive controls pending execution approval |
| Installation | Compiled installer with dependencies/notices/source archives and optional signed driver; install/launch/uninstall pass | Driver task/fresh OS VM untested; development build unsigned |

No simulated sensor or benchmark measurements are used by the application. Synthetic unit fixtures are isolated in the test directory. The approved SSD and 720p GPU pilots passed; see PILOT-RESULTS.md. No other write/stress parameters have been exercised.


## Version 1.3 interface update
Modern grouped navigation, telemetry cards/rings, structured tables, expandable technical views and advanced settings were implemented. Existing workload engines were retained. See UI-REDESIGN.md and tests/modern-release for layout and interaction checks.

## Version 1.4 GPU update
Unlimited duration and an in-render telemetry overlay were added. Safe logic/UI checks and a controlled ten-second 720p overlay run passed. See GPU-OVERLAY.md. No long unattended run was performed.

## BenchMark X 1.5 update
The app was rebranded with the supplied logo and icon; SSD scorecards, PNG/copy sharing, physical storage capacity/meter and prominent CPU/GPU names were added. See BENCHMARK-X.md and tests/benchmarkx. Existing workload limits remain as documented.

## Version 1.6 automatic hardware access
PawnIO is now a mandatory bundled prerequisite; setup and app startup request administrator access. Automatic upgrade/restart handling, compatible-driver reuse, integrity-failure blocking, elevated collection (278 channels including CPU/motherboard temperatures), installed app tests and shared-driver retention passed. Windows requested a restart during the upgrade; no restart was performed. See tests/automatic-pawnio/VALIDATION.md.

## Version 1.6.1 theme update
Black/charcoal surfaces and restrained red accents now cover app views, charts, scorecard exports, tray menu and GPU overlay. See RED-BLACK-THEME.md and tests/red-black for visual verification. Workload engines and automatic PawnIO setup are unchanged.
