# ADR-009: Multi-Monitor & Per-Monitor DPI Strategy

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** UI/Platform Lead

## Context
Per `01_AURORA_PRD.md` (`TB-001`, `TB-010`, `NFR-010`) and `03_AURORA_DESIGN_SYSTEM.md` §8 (`R-005`), Aurora must support single and multi-monitor topologies with mixed DPI scaling (100%–200%), monitor hot-plug, and primary-monitor changes without persisting stale physical pixel coordinates.

## Decision
1. **Monitor Identity:** Identify displays via `EnumDisplayMonitors` + `GetMonitorInfoEx` (`MONITORINFOEXW.szDevice` + `DisplayConfig` friendly name fallback), marking primary monitor (`MONITORINFOF_PRIMARY`).
2. **Effective DIP Persistence:** Persist taskbar alignment, density (`Compact = 40 DIP`, `Standard = 48 DIP`, `Spacious = 56 DIP`), edge (`Bottom`, `Top`, `Left`, `Right`), and widget relative anchor positions in **effective DIPs**, converting to physical pixels ONLY at the Win32 `AppWindow.MoveAndResize` / `SHAppBarMessage` boundary using `GetDpiForMonitor` (`MDT_EFFECTIVE_DPI`, `scale = dpi / 96.0`).
3. **Topology Change Reconciliation:** Listen for `WM_DISPLAYCHANGE` and `WM_DPICHANGED`, debounce over 150 ms, re-enumerate active monitors, clamp all transient panels (`Control Center`, `Notification Center`, `Media Hub`, `Window Preview`) strictly inside the target monitor's `rcWork` work area, and re-negotiate AppBar edges cleanly.
