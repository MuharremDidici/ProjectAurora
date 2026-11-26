# Project Aurora — Standalone Native Windows 11 Taskbar Recovery Guide

> **Safety Guarantee (`DEC-004`, `DEC-005`, `ADR-013`):** Project Aurora never replaces, patches, injects into, or terminates `explorer.exe`. A dedicated low-CPU Watchdog (`aurora.exe --watchdog-pid <PID>`) restores the native Windows taskbar in `<100ms` on any unexpected exit, and this guide also works completely offline.

## 1. Emergency CLI Taskbar Restore
Run directly from the installation directory (`%LOCALAPPDATA%\Programs\ProjectAurora`) or repository root:
```powershell
.\Restore-Windows-Taskbar.cmd
# or:
.\aurora.exe --restore-taskbar
```

## 2. Restore Permanent Visibility via Windows Settings
1. Press `Win + R`, type `ms-settings:taskbar`, and press `Enter` (or press `Win + I` -> **Personalization** -> **Taskbar**).
2. Expand **Taskbar behaviors** (*Görev çubuğu davranışları*).
3. Uncheck **Automatically hide the taskbar** (*Görev çubuğunu otomatik olarak gizle*).

## 3. Reclaim Reserved Screen Area
- Close `aurora.exe` gracefully or run `aurora.exe --restore-taskbar`. Windows automatically releases `SHAppBarMessage` reservations (`AppBar`) when Aurora exits.

## 4. Launch Aurora in Safe Mode
```powershell
.\Start-Aurora-SafeMode.cmd
# or:
.\aurora.exe --safe-mode
```
Safe Mode keeps the native Windows taskbar visible (`hide_native_taskbar_while_active = false`, `reserve_dock_work_area = false`) so you can safely inspect logs or adjust preferences.

## 5. Clean Removal Locations (Standard User Only)
- **Uninstall Command:** `"%LOCALAPPDATA%\Programs\ProjectAurora\uninstall.exe" --uninstall`
- **Startup Registry Value (if enabled):** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` -> `ProjectAurora`
- **Local Data & Logs:** `%LOCALAPPDATA%\ProjectAurora`
- **System / Admin Footprint:** None (`asInvoker`, zero UAC, zero kernel drivers, zero shell DLLs).
