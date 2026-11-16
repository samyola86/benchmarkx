# Automatic hardware-access setup — BenchMark X 1.6

PawnIO 2.2.0 is bundled and installed automatically as a required prerequisite. No checkbox, PATH setup, separate download or manual driver step is needed. Setup requests administrator permission through Windows UAC, runs the signed vendor installer using its documented silent switches, and checks both the registered version and access to the driver device before installing the app. An already-ready compatible version 2.0 or newer is reused, avoiding downgrades or unnecessary reinstalls.

The official driver installer is pinned to SHA256 1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032 and its Authenticode signature is verified at packaging time. Setup rechecks its extracted SHA256 before execution.

Nonzero failures stop setup with a concrete error. Exit 3010 requests a Windows restart and suppresses immediate application launch. No forced restart is performed by the validation script. The shared PawnIO installation is retained if BenchMark X is uninstalled, since other hardware tools may depend on it.

The app now carries a requireAdministrator manifest, so its isolated sensor process inherits the access required by PawnIO. Windows UAC handles permission requests. App files are installed in Program Files instead of a user-writable application directory. Existing benchmark history/settings remain in the compatibility data directory.

Hardware that the monitoring library does not support still reports unavailable; installing a driver cannot manufacture unsupported sensor readings. No Windows security features are disabled.

Upstream references:
- https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0
- https://github.com/microsoft/winget-pkgs/blob/master/manifests/n/namazso/PawnIO/2.2.0/namazso.PawnIO.installer.yaml
- https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/PawnIo/PawnIo.cs
- https://jrsoftware.org/inno-setup/documentation/topic_scriptevents.htm

Actual elevated installation and sensor results are recorded under tests/automatic-pawnio when administrator approval is available. No disk write benchmark or GPU stress is needed for these checks.
