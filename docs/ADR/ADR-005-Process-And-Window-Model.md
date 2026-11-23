# ADR-005: Process & Window Model

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Architecture Lead, Reliability Lead

## Context
Per `02_AURORA_TDD.md` Section 4, v1 requires a single primary user-session process with single-instance coordination, no Windows service, no elevated helper, and strict window ownership/dismissal semantics.

## Decision
1. **Single-Instance Process:** Coordinated via a named per-user mutex (`Local\ProjectAurora_SingleInstance_v1`) and named-pipe activation channel so launching `Aurora.App.exe` a second time focuses/opens the Aurora Studio & Settings window (or switches into `--safe-mode` / `--recover` as requested).
2. **Top-Level HWND Surfaces:**
   - **Smart Taskbar Window(s):** Borderless, non-stealing (`WS_EX_TOOLWINDOW` / `AppWindowPresenterKind.Overlapped` borderless chrome), registered with `SHAppBarMessage` per enabled monitor.
   - **Transient Flyout Window (Control Center / Notification Center / Media Hub / Window Preview):** Shared lightweight borderless transient flyout anchored inside the active monitor's work area; dismisses on `Escape` or outside focus loss (`Deactivated`) and returns focus properly.
   - **Desktop Widget Board Surface:** Optional desktop-level or toggleable widget canvas with bounded background refresh timers that pause automatically when hidden or when Battery Saver / Reduced Motion is active.
   - **Aurora Studio & Settings Hub Window:** Full WinUI 3 workspace managing Personalization Studio, Workspace Manager, Smart Taskbar setup, Control Center card layout, Notifications, Widgets, Diagnostics, and Native Taskbar Recovery.
3. **Crash Resilience:** Global `UnhandledException` handlers ensure `IAppBarService.UnregisterAll()` executes before process exit so Explorer's work area is always restored immediately.
