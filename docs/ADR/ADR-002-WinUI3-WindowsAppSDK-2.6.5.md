# ADR-002: UI Framework — WinUI 3 and Windows App SDK 2.6.5 Stable

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Architecture Lead, UI Lead

## Context
Project Aurora requires native Windows 11 composition performance, per-monitor DPI scaling, HWND interop for `SHAppBarMessage`, accessibility (UI Automation), and low idle memory/CPU overhead (`NFR-001 ≤ 0.5% CPU`, `NFR-002 ≤ 120 MB` target). `06_TECHNICAL_SOURCE_REGISTER.md` identifies **Windows App SDK 2.6.5** (released 2026-11-02) as the latest production Stable channel release, while `2.6.6-experimental` is excluded by policy.

## Decision
Pin **`Microsoft.WindowsAppSDK` version `2.6.5`** (Stable) and **`Microsoft.Windows.SDK.BuildTools` version `10.0.28000.2957`** (Stable). Do not reference Preview or Experimental Windows App SDK packages.

## Consequences
- **Positive:** Production-supported WinUI 3 rendering, multi-adapter/multi-monitor composition fixes included in 2.6.5, native `AppWindow` + `WinRT.Interop.WindowNative` HWND interop, full UI Automation support.
- **Negative:** Requires Windows 11 (`10.0.22000.0+`) `win-x64` / `win-arm64` targets, matching `DEC-001` (Windows 11 only).
