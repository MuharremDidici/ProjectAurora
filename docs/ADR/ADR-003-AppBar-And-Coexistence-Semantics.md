# ADR-003: AppBar Strategy & Native Taskbar Coexistence / Primary Surface Semantics

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Product Owner, Windows Platform Lead

## Context
Windows `SHAppBarMessage` (`ABM_NEW`, `ABM_QUERYPOS`, `ABM_SETPOS`, `ABM_REMOVE`, `ABM_GETSTATE`, `ABM_GETTASKBARPOS`) allows application-defined desktop toolbars to reserve work-area edges and coordinate with other AppBars. It does **not** make a third-party app the Windows shell or provide a supported programmatic API to hide Explorer's taskbar. Per `DEC-004` and `DEC-005`, Aurora must never kill `explorer.exe`, inject DLLs, patch binaries, or mutate undocumented shell registry keys (`StuckRects3`).

## Decision
1. **AppBar Lifecycle:** Aurora registers its taskbar HWND via `SHAppBarMessage(ABM_NEW)`, negotiates screen-edge bounds via `ABM_QUERYPOS` and `ABM_SETPOS`, handles `ABN_POSCHANGED` / `ABN_FULLSCREENAPP` / `ABN_STATECHANGE` callbacks, and deterministically calls `ABM_REMOVE` on shutdown, Safe Mode, and unhandled-exception/process-exit hooks.
2. **Native Taskbar Detection & Coexistence (`TB-015`):** Aurora queries `SHAppBarMessage(ABM_GETSTATE)` and `ABM_GETTASKBARPOS` (read-only) to detect whether Explorer's taskbar is always-on-top visible or user-configured to auto-hide (`ABS_AUTOHIDE`), and which edge it occupies.
   - When Explorer's taskbar is visible on the same edge (e.g., Bottom), Aurora automatically operates in **Coexistence Mode** (docking cleanly above Explorer's taskbar via AppBar work-area negotiation or on the user-selected Top/Bottom non-overlapping slot) and displays a clear Coexistence status pill.
   - When the user configures Windows Settings (`ms-settings:taskbar` -> *Automatically hide the taskbar*) via Aurora's **Primary Taskbar Setup Guide (`TB-014`)**, Aurora serves as the primary continuously visible taskbar surface. If both occupy the bottom edge while native auto-hide is enabled, Aurora recommends placing Aurora on Top edge OR offsetting its reveal trigger so bottom-edge pointer movement never causes dual-reveal conflict.
3. **Fullscreen Suspension (`R-012`):** Upon `ABN_FULLSCREENAPP` (`lParam == 1`), Aurora hides/suspends its topmost bar so games and fullscreen video are never obstructed.

## Consequences
- **Positive:** 100% compliant with supported Win32 contracts; Explorer remains completely intact; instant recovery even if Aurora is closed or uninstalled.
- **Negative:** Requires user action in Windows Settings if the user wants the native taskbar hidden.
