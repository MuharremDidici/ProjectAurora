# Project Aurora — Standalone Native Windows 11 Taskbar Recovery Guide

> **Safety Guarantee (`DEC-004`, `DEC-005`, `R-003`):** Project Aurora never replaces, patches, injects into, or terminates `explorer.exe`. This guide works completely offline even when Project Aurora is stopped or deleted.

## 1. Restore Permanent Visibility of the Native Windows 11 Taskbar
1. Press `Win + R`, type `ms-settings:taskbar`, and press `Enter` (or press `Win + I` -> **Personalization** -> **Taskbar**).
2. Expand **Taskbar behaviors** (*Görev çubuğu davranışları*).
3. Uncheck **Automatically hide the taskbar** (*Görev çubuğunu otomatik olarak gizle*).
4. The native Windows taskbar immediately returns to permanent visibility without rebooting.

## 2. Reclaim Reserved Screen Area (If Aurora Hung)
- Close `Aurora.App.exe` via Task Manager (`Ctrl + Shift + Esc`). Windows automatically releases orphaned `SHAppBarMessage` reservations, or toggle display scaling/resolution or sign out/in if an OS refresh is desired.

## 3. Launch Aurora in Safe Mode
```powershell
.\Aurora.App.exe --safe-mode
```
Safe Mode resets active runtime surfaces to the built-in **Aurora Glass** recovery theme, skips custom theme assets, and opens Diagnostics & Recovery directly.

## 4. Clean Removal Locations (Standard User Only)
- **Startup Registry Value (if enabled by user):** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` -> `ProjectAurora`
- **Local Data & Logs:** `%LOCALAPPDATA%\ProjectAurora`
- **System / Admin Footprint:** None (zero Windows services, zero kernel drivers, zero shell modifications).
