# RGB Studio and Display Studio

Development addition to BenchMark X 1.7.0. The existing version, settings, reports, benchmarks, sensor service and installer prerequisites are retained.

## RGB Studio

Use an existing, trusted OpenRGB installation. Configure its SDK server to bind only to loopback, then enter its local port and click **Connect**. For this workspace, the user-approved portable OpenRGB 1.1 can be started with `Start-OpenRGB-Local.cmd`; it uses a separate local configuration and does not add startup services. BenchMark X connects exclusively to `127.0.0.1`; it does not install OpenRGB, download providers, start an SDK server or perform raw USB/SMBus writes. The independent client implements the documented SDK v3 representation, negotiating with servers supporting v3 or newer. Older servers are rejected explicitly. No upstream OpenRGB code, binaries or drivers are redistributed by this change.

Discovery reads controllers, modes, zones and LEDs. A controller needs a serial or location identity; duplicate identities remain read-only. Mode capabilities determine whether colour, speed, brightness and zone controls are enabled. Modes that automatically save to hardware are rejected to preserve reversibility. Effects can control the whole device; per-zone colour is supported only for modes reporting per-LED colours. There is no software animation or temperature-reactive write loop in this release.

Pause competing vendor lighting utilities and Windows Dynamic Lighting before checking the controller-conflict acknowledgement and applying lighting changes. Ctrl+click selects multiple controllers; **Apply to all capable devices** targets the inventory. Apply revalidates identities, modes and zones against fresh inventory and reports individual failures. OpenRGB SDK state is cached provider state, not a measurement of physical LED output. The drawn preview is illustrative.

RGB writes are blocked during this instance's benchmark runs unless the explicit override is enabled. Other processes' benchmark activity is not detectable reliably; leave lighting unchanged during those measurements. The local ownership file prevents two BenchMark X instances from writing concurrently. It cannot arbitrate control with unrelated RGB utilities. The SDK uses indexed IDs at negotiated v3: topology notifications invalidate operations, but the protocol cannot make hotplug and writes atomic.

## Display Studio

Read-only discovery uses Windows logical display identities, physical monitor APIs and the active display mode. Duplicate model names are displayed with their individual logical display names. Ambiguous logical-to-physical mappings remain read-only. Individual brightness/contrast reads determine availability even when the aggregate capability query fails. Supported physical brightness/contrast ranges are mapped to 0-100; colour-temperature choices require both reported support and successful initial readback. Native WMI brightness is used only when its instance identity matches the selected panel.

If controls are unavailable, enable **DDC/CI** in the monitor's on-screen menu and click **Refresh**. USB docks, graphics drivers, HDR modes and monitor firmware can restrict access. A successful capability probe does not guarantee every setting will accept writes. The helper re-reads ranges before applying, verifies afterward and reports mismatches. It never changes resolution, refresh rate, power or input source, and never substitutes gamma changes for brightness.

Display operations run in a persistent supervised helper process. A stuck call terminates the helper after 12 seconds; only explicit Refresh retries it. Normal disposal releases physical monitor handles. Topology/power notifications invalidate displayed capabilities and clear stale capabilities. Refresh is required after reconnect, sleep or display changes.

## ICC colour management

Installed profiles are enumerated through Windows ICM APIs. Import requires explicit action, checks the ICC header, size, RGB profile class and Windows profile validation, then calls Windows profile installation. Import does not select the profile automatically. Selecting an installed profile and applying associates it and selects its default in **current-user** scope. The previous default and per-user flag must be readable before a change is permitted.

This initial integration supports traditional SDR ICC associations only. Confirm SDR mode in the page before applying a profile; advanced-colour/HDR associations are not implemented. ICC affects applications that honour colour management; it is not universal physical calibration. Restore returns to the captured default/per-user selection; an imported profile and an additional profile association may remain installed. No system-wide default is changed.

## Apply, profiles and recovery

Hardware controls are enabled by default when the provider can read supported capabilities. Monitor brightness applies live while dragging or using arrow keys, without Apply or confirmation. A 120 ms update interval coalesces rapid edits into the latest value, serializes monitor writes, and confirms readback. Pending edits are cancelled when the page closes, selection changes, or topology changes. Other settings still use the Apply review dialog. Opening a page or assigning a slider value during discovery does not write hardware. The first live brightness edit captures the readable prior brightness for Restore previous. Apply snapshots other readable previous settings, applies devices sequentially and preserves snapshots for explicit **Restore previous**. Changes are best effort: a later setting or device can fail after earlier settings have changed. Inspect each outcome and use Restore; rollback itself can fail after a disconnect. Snapshots last for the application session, including page navigation.

Named presets are user-defined snapshots; labels such as Gaming, Productivity, Cinema, Reading, Night or Creator do not imply factory modes. JSON stores schema version, stable identities, capabilities, observed values, timestamps and intended settings. OpenRGB observations are labelled as SDK cache values. Existing application settings and history are untouched. Validated presets are written atomically under the existing local settings directory's `Studios` subfolder by default, and can be exported/imported. Loading never silently applies a preset. No commands or scripts are executed from JSON.

Unified RGB/display apply and reactive software effects are deferred until independent studios are validated on supported hardware. No automatic application on connection or startup is implemented.

## References

- [OpenRGB SDK](https://openrgb.org/sdk.html) and [protocol documentation](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/master/Documentation/OpenRGBSDK.md)
- [Windows high-level monitor configuration](https://learn.microsoft.com/en-us/windows/win32/monitor/using-the-high-level-monitor-configuration-functions)
- [Windows profile management](https://learn.microsoft.com/en-us/windows/win32/wcs/profile-management-functions)
- [Current-user default ICC profiles](https://learn.microsoft.com/en-us/windows/win32/api/icm/nf-icm-wcssetdefaultcolorprofile)

See `STUDIO-VALIDATION.md` for exact validation and remaining release gates. Do not treat mock fixtures or 100% DPI captures as supported-hardware or other-DPI verification.

## HDR and compact monitor controls

Display Studio and Compact monitor include an Enable HDR checkbox for the selected physical display. Windows CCD monitor interface identities are matched to current target IDs before each HDR operation. The HDR-specific capability query distinguishes HDR from SDR wide-colour support. Unsupported displays, unreadable capability/state, and Windows-policy restrictions leave the checkbox disabled. Changing the checkbox sets the per-display Windows HDR user state and verifies readback; it does not change resolution or refresh rate. Current HDR-specific APIs require Windows 11 24H2 or later. If only the legacy generic Advanced Color API is available, the control stays disabled rather than treating SDR wide-colour as HDR.

Compact monitor retains the three CPU/GPU/RAM cards and adds a display selector, live brightness, contrast, colour temperature when supported, HDR, Refresh, and Restore previous. Brightness applies while dragging; contrast/colour use their labelled Apply action. Default compact size is 860x610, with dark scrolling at the 640x520 minimum. Expand by clicking the heading. Advanced-colour ICC associations remain unsupported; the service rejects an SDR ICC apply while HDR is enabled.
