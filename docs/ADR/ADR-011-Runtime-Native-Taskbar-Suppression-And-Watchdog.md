# ADR-011: Product Owner Directive — Runtime Native Taskbar Auto-Suppression & Exit Watchdog

- **Status:** Accepted (Supersedes Coexistence-Default Clause of ADR-003 while retaining Explorer Safety Boundary)
- **Date:** 2026-11-17
- **Deciders:** Product Owner, Windows Platform Lead, Reliability Lead

## Context
During live interactive acceptance testing, the Product Owner explicitly directed:
> *"Windows'un kendi barının üstünde çıkıyor. Windows'un kendi barı olmasın; bu uygulama açıkken diğeri kapansın."*

At the same time, Project Aurora's non-negotiable safety boundary (`DEC-004`, `R-003`) forbids terminating `explorer.exe`, injecting DLLs, patching shell binaries, or modifying undocumented registry blobs (`StuckRects3`).

## Decision
1. **Non-Destructive Runtime Taskbar Suppression (`Win32AppBarService`):**
   - On startup (unless `--safe-mode` is active or the user toggles *Windows Çubuğunu Göster* in Studio), Aurora:
     1. Reads and snapshots the user's original `SHAppBarMessage(ABM_GETSTATE)` flags.
     2. Calls documented `SHAppBarMessage(ABM_SETSTATE, ABS_AUTOHIDE)` so Windows releases the bottom 48px work-area reservation cleanly (`rcWork` expands to full display height).
     3. Calls `ShowWindow(hwnd, SW_HIDE)` on `Shell_TrayWnd` and any `Shell_SecondaryTrayWnd` handles while keeping `explorer.exe` 100% running and unmodified (`Win` Start key, `Win+E`, `Alt+Tab`, desktop icons, and system shortcuts remain completely operational).
2. **Triple-Layer Deterministic Restoration (`R-003` Mitigation):**
   - **Layer 1 (In-Process):** `ShutdownCleanly()`, `AppDomain.ProcessExit`, and `UnhandledException` immediately invoke `RestoreNativeWindowsTaskbar()`, calling `ShowWindow(SW_SHOW)` and restoring `_originalNativeTaskbarState` via `ABM_SETSTATE`.
   - **Layer 2 (Out-of-Process Crash Watchdog):** Aurora spawns a headless standard-user companion watcher (`Aurora.App.exe --watchdog-for-pid=<pid> --orig-state=<state>`) that blocks on `Process.WaitForExit()`. Even if the main Aurora process is terminated abruptly via Task Manager (`TerminateProcess`), the watchdog restores `Shell_TrayWnd` within `< 50 ms` and exits.
   - **Layer 3 (Offline Manual Tool):** Standalone `Restore-Windows-Taskbar.cmd` and `--restore-taskbar` CLI flag restore permanent native taskbar visibility independently.
