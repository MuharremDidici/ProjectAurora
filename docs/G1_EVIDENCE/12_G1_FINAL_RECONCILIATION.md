# PROJECT AURORA — G1 Technical Feasibility & Capability Reconciliation

- **Date:** 2026-11-17
- **Target Reference Environment:** Lenovo 82RB, 12th Gen Intel Core i7-12700H (14C/20T), 64 GB RAM, NVIDIA GeForce RTX 3070 Ti Laptop GPU (`2560x1440 @ 165Hz`), Laptop Battery `L21B4PC0`, Multiple Audio Endpoints (`Realtek`, `SteelSeries Sonar`, `USB Audio`, `NVIDIA`), Microsoft Windows 11 (`10.0.26320`), Standard-User Account (`Running Elevated: False`).
- **Toolchain Baseline Verified:** `.NET 10.0.404` LTS SDK + `Microsoft.WindowsAppSDK 2.6.5` Stable + `Microsoft.Windows.SDK.BuildTools 10.0.28000.2957` + `Microsoft.Data.Sqlite 10.0.15`.

## Capability & Honest Fallback Matrix (`G1-WS1` .. `G1-WS10`)

| Workstream | Capability Classification | Implementation & Fallback Contract |
|---|---|---|
| **WS1: Taskbar / AppBar** | **Directly Supported** | `SHAppBarMessage` (`ABM_NEW`, `ABM_QUERYPOS`, `ABM_SETPOS`, `ABM_REMOVE`) coordinates edge bounds cleanly; `UnregisterAll` wired to shutdown and crash handlers. |
| **WS2: Native Taskbar Visibility** | **Auto-Hide Suppression + Coexistence (`ADR-011`)** | Documented `SHAppBarMessage(ABM_SETSTATE, ABS_AUTOHIDE)` + `ShowWindow(Shell_TrayWnd, SW_HIDE)` suppresses Windows' bar while Aurora runs (zero `explorer.exe` termination or registry mutation), backed by a 3-layer recovery guarantee (`ShutdownCleanly`, detached `--watchdog-for-pid=` process, and `Restore-Windows-Taskbar.cmd`). Live toggle available in Studio. |
| **WS3: Shell Resilience & Recovery** | **Directly Supported** | Zero Explorer process modification; detached crash watchdog (`< 50 ms` taskbar restore on hard kill); standalone offline HTML/MD recovery guide; `--safe-mode` & `--restore-taskbar` CLI flags. |
| **WS4: Multi-Monitor & Per-Monitor DPI** | **Directly Supported** | `EnumDisplayMonitors` + `GetDpiForMonitor` (`MDT_EFFECTIVE_DPI`) + `PerMonitorV2` with `AutoScaleMode.None` physical pixel lock. |
| **WS5: Audio Endpoints & Sessions** | **Directly Supported + Deep-Link** | Master & per-app volume/mute directly controlled via Core Audio COM (`IAudioEndpointVolume`, `ISimpleAudioVolume`); active endpoints enumerated via `IMMDeviceEnumerator`; OS default endpoint switching routes honestly to `ms-settings:sound` (no undocumented `IPolicyConfig`). |
| **WS6: Wi-Fi, Bluetooth, Brightness, Battery** | **Capability-Gated + Deep-Link** | Wi-Fi/Network status via `NetworkInformation` + `Windows.Devices.Radios`; Bluetooth radio status via `Windows.Devices.Radios` with pairing routed to `ms-settings:bluetooth`; Laptop internal brightness via WMI `WmiMonitorBrightness` / `WmiMonitorBrightnessMethods` with explicit unavailable badge on external non-WMI panels; Battery via `GetSystemPowerStatus`. |
| **WS7: Media Hub (GSMTC)** | **Directly Supported** | `GlobalSystemMediaTransportControlsSessionManager` enumerates live sessions, async thumbnail streams, and per-session transport capability flags (`IsPlayEnabled`, `IsPauseEnabled`, `IsNextEnabled`, `IsPreviousEnabled`). |
| **WS8: Notifications & Universal App Search** | **Aurora-Owned Supported** | Aurora Toast + SQLite Notification Center + `InstalledAppCatalogService` indexing Start Menu `.lnk` & system apps with real `SHGetFileInfo` icons, live math calculator, open window search, and Web search fallback. |
| **WS9: Runtime & Presentation Host** | **Directly Supported (`ADR-011`)** | Self-contained unpackaged `win-x64` `.NET 10.0.404` (`asInvoker`, zero UAC) using per-pixel transparent DirectComposition `WinForms + WebView2` (`WS_EX_LAYERED | WS_EX_TRANSPARENT` dynamic hit-testing) for frameless Dynamic Crown + Parabolic Crystal Dock. |
| **WS10: Performance & Test Evidence** | **Bounded & Verified** | `36/36` automated xUnit tests passing across 5 test projects (`0` warnings, `0` errors); measured ~`114–135 MB` Working Set / ~`29–36 MB` Private Memory (`< 150 MB` G1 budget). |

## Gate Disposition
**G1 Outcome:** `CONDITIONAL PASS -> ARCHITECTURE BASELINE APPROVED FOR IMPLEMENTATION` (Coexistence-safe primary taskbar model + honest capability fallbacks across all hardware controls).
