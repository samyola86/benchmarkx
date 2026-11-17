# Hardware Studios validation - 2026-11-17

Development build; not completion of every hardware release gate. Version remains 1.7.0. No GitHub release was published. Earlier checks were read-only. The subsequent live-brightness test temporarily changed one monitor by 1% and restored its exact original value. RGB and ICC settings were not changed.

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Baseline and updated Windows builds | PASS | `cmd /c Build-Windows.cmd` |
| Existing safe functional regression suite | PASS | `tests/studio-regression.txt` |
| OpenRGB SDK fixtures | PASS | Local mock server: negotiation, modes/zones/LED parsing, truncated data, bad signature, no devices, disconnect, incompatible protocol, encoded Apply and Restore |
| Queue/process fixtures | PASS | Serialization, cancellation, eight-item mailbox limit, hung display helper termination |
| Range/profile fixtures | PASS | Nonzero ranges, invalid boundaries/schema, atomic JSON roundtrip, invalid save preserves previous file |
| Existing route navigation and tray | PASS | `tests/studios-ui/ui-results.txt`; every existing page plus both studios; longest measured synchronous navigation 141 ms |
| Actual studio layouts at 100% DPI | PASS | `tests/studios-final/results.txt`; 1586x992, 1100x760 and 900x720 windows, dark-scrollbar audits and sidebar alignment |
| Native read-only display discovery | PASS | `tests/studio-native-inspection.json`; two HP 24f displays with separate IDs, each 1920x1080 at 75 Hz |
| Windows ICC enumeration/current state | PASS | Same inspection JSON; installed profiles, current `HP_24f.icm` and per-user selection flags read through Windows APIs |
| Installer rebuild | PASS | `tests/studio-release-build.txt`; same staging and Inno Setup commands as `Prepare-Release.ps1`, preserving prerequisite hash/signature checks and version |

The existing suite reports real **read-only** DiskSpd XML. No SSD write benchmark or GPU stress was started. Mock SDK tests connect only to a temporary loopback server owned by the fixture; they do not touch lighting hardware.

## Commands

Run from the repository root in an interactive Windows user session. The existing app manifest requests elevation. Launches used `Start-Process -WindowStyle Hidden -PassThru`, followed by `WaitForExit()` and exit-code inspection.

```text
cmd /c Build-Windows.cmd
dist\BenchMarkX.exe --worker test-suite tests\studio-regression.txt tests
dist\BenchMarkX.exe --ui-test tests\studios-ui
dist\BenchMarkX.exe --studio-ui-test tests\studios-final
dist\BenchMarkX.exe --worker studio-inspect tests\studio-native-inspection.json
powershell -NoProfile -File .\Prepare-Release.ps1
```

PowerShell execution policy blocked direct execution of `Prepare-Release.ps1`. Its staging commands and `tools\inno\ISCC.exe Installer.iss` were executed directly instead; no execution-policy or security setting was changed. The compiler retained an existing warning about admin installs referencing per-user shortcut areas; installer behavior was not altered.

Actual invocations supplied absolute output paths. Final studio screenshots are `tests/studios-final/RGB-Studio-1586-actual.png` and `Display-Studio-1586-actual.png`, with corresponding `1100` and `900` captures. These are native Windows screenshots; the PDF reference images were not used as application backgrounds.

## Remaining release gates

- Corrected after user feedback: aggregate capability failure was incorrectly suppressing valid individual controls. Both HP 24f monitors now return brightness/contrast through direct high-level reads (81/80 and 80/80 percent). Colour temperature remains unavailable. See `tests/studio-connection-diagnosis.json` and `tests/studios-connected-final/`. Physical writes/rollback remain untested.
- Corrected after user feedback: with user approval, official portable OpenRGB 1.1 was configured at 127.0.0.1:6742, using the already-running PawnIO driver. Real read-only SDK inventory now discovers ASUS TUF GeForce RTX 3060 Gaming OC and ASRock B550 PG Velocita. Valid reversed speed ranges were fixed and added to regression fixtures. See `tests/rgb-real-discovery-fixed.json`. Real lighting Apply/Restore and physical LED validation remain untested.
- ICC import/default/association writes and restoration have not changed the user's settings during verification. Reads were verified. HDR/advanced-colour associations are intentionally unsupported.
- Actual host DPI is 96 (100%). Full-window 125% and 150% captures and per-monitor transitions remain unverified. Existing sidebar/card scale fixtures do not substitute for actual full-window DPI captures.
- Physical hotplug, sleep/resume, stuck real monitors, external-controller races, shutdown during physical writes and benchmark performance impact remain unverified. Timeout, topology invalidation and cancellation code exist; they are not hardware evidence.
- Installer compilation is separate from installation/uninstallation testing. No new installer was installed on the user's system during this work.
- The RGB preview uses illustrative native fan rings. The red/graphite identity is retained; a pixel-for-pixel match to the PDF is not claimed.
- Unified cross-studio presets, automatic startup/reconnect application and reactive/software frame effects are deferred. Independent local presets and explicit per-studio Apply/Restore are implemented.

Do not label this build hardware-certified or fully release-accepted until the remaining gates are resolved. See `HARDWARE-STUDIOS.md` for supported behavior and recovery limits.

## Connection/control fixes

Individual monitor reads no longer depend on a successful aggregate capability query. No VCP/gamma workaround was needed. RGB now supports increasing and decreasing provider ranges; the real GPU uses 4-to-0 and motherboard uses 255-to-0. RGB Studio explains the separate OpenRGB requirement and clears stale inventory before connection attempts. The approved portable provider has no startup service, is not bundled in the BenchMark X installer, and can be started manually with `Start-OpenRGB-Local.cmd`. Its process listens only on loopback. No lighting effects or monitor settings were applied during these checks.

## Default controls and live brightness

Per user request, the explicit-hardware-change checkbox was removed from both studios. Supported controls are available by default. Monitor brightness slider edits use a serialized/coalesced background path at a 120 ms interval, with initial brightness capture and readback; no Apply click or confirmation is required. Other display settings retain an explicitly labelled Apply other settings action.

`tests/live-brightness/live-brightness-results.txt`: PASS real monitor brightness 75% -> 76% from 21 rapid slider edits without Apply/opt-in; the slider remained enabled. PASS restoration to 75% via the same slider. Discovery/programmatic value updates do not trigger writes. No RGB or ICC changes were made.

## HDR and compact controls

Windows read-only discovery (`tests/hdr-discovery.json`) reports both attached HP 24f displays as HDR unsupported. Their HDR checkboxes correctly remain disabled in Display Studio and compact mode. `tests/hdr-compact/hdr-compact-results.txt` passes compact controls at 860x610 and 640x520, three telemetry cards, capability-dependent checkbox behavior, dark scrolling, and expansion/navigation. Compact screenshots were visually inspected; the disabled checkbox rendering was then adjusted to retain readable muted text. The HDR fixtures check supported-off, enabled/active, policy-restricted, and SDR-WCG cases. `tests/hdr-regression-final.txt` passes the final safe regression suite, including HDR capability fixtures and disabled-checkbox rendering. The rendering fixture creates a Windows synchronization context; asynchronous fixture delays explicitly avoid capturing that context so the console test runner cannot deadlock. No HDR state was changed, and actual HDR switching cannot be validated on these SDR monitors. HDR-specific Windows 11 24H2+ APIs are used; older ambiguous Advanced Color responses do not enable the control.

## Compact HDR header rendering correction

Replaced the header flow layout with explicit selector/Refresh/HDR columns. The owner-drawn HDR checkbox now paints an opaque background and fully invalidates on text/size changes, avoiding retained sibling/text pixels. The actual-window rerun was cancelled at the Windows administrator prompt. The replacement non-elevated, hardware-free renderer (`tools/CompactHeaderCheck.cs`, evidence `tests/compact-header-render-results.txt`) exercises the actual header factory and checkbox at 820, 780, 600 and 560 px, with 20 caption/resize cycles per width; all checks pass without overlap or overflow. Rendered fixtures were visually inspected. No hardware settings changed.
