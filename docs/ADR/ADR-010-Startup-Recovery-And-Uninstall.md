# ADR-010: Startup, Safe Mode, Native Taskbar Recovery & Clean Uninstall Guarantees

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Reliability Lead, Release Lead

## Context
Per `R-003` and `R-016`, Aurora failure, exit, or removal must never leave the user with a broken desktop, stuck screen-edge reservation, orphaned scheduled task, or inaccessible native Windows taskbar.

## Decision
1. **Standard-User Optional Startup:** Off by default. When enabled by user in Settings, writes a single standard-user value `ProjectAurora` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. No Windows Service, no Task Scheduler elevated task, no shell replacement key (`Winlogon\Shell` is NEVER read or written).
2. **Safe Mode (`--safe-mode`):** Starts Aurora with the built-in `Aurora Glass` recovery theme, minimal modules (Studio + Recovery diagnostics only, skipping custom themes, third-party widgets, and AppBar edge reservation if requested), triggered automatically after 2 consecutive startup crashes (tracked via startup sentinel file) or manually via CLI / Settings button.
3. **Out-of-Band Native Taskbar Recovery (`TB-014`, `R-003`):**
   - Ships standalone `Native_Taskbar_Recovery_Guide.html` and `Native_Taskbar_Recovery_Guide.md` readable in any browser/Notepad even if Aurora is deleted:
     1. Press `Win + I` -> **Personalization** -> **Taskbar** -> **Taskbar behaviors** (or `Win + R` -> `ms-settings:taskbar`).
     2. Uncheck **Automatically hide the taskbar**.
     3. Explorer's taskbar immediately returns to permanent visibility with zero reboot required.
4. **One-Click Clean Reset / Uninstall Prep:** Settings includes "Reset & Prepare Clean Uninstall" which unregisters all AppBars (`ABM_REMOVE`), removes the `HKCU\...\Run` entry if set, and optionally purges `%LOCALAPPDATA%\ProjectAurora`.
