# Project Aurora — Modular Monolith Architecture Overview

## 1. Dependency Flow (`02_AURORA_TDD.md` §3.1)

```text
Aurora.App (WinUI 3 Self-Contained Unpackaged Host, Windows App SDK 2.6.5)
  ├── Aurora.Taskbar           ──┐
  ├── Aurora.ControlCenter       │
  ├── Aurora.Notifications       │
  ├── Aurora.Widgets             ├──► Aurora.Presentation ──► Aurora.Application ──► Aurora.Core
  ├── Aurora.Media               │
  ├── Aurora.Workspaces          │
  ├── Aurora.Personalization   ──┘
  ├── Aurora.Platform.Windows  ─────────────────────────────► Aurora.Application ──► Aurora.Core
  └── Aurora.Infrastructure    ─────────────────────────────► Aurora.Application ──► Aurora.Core
```

## 2. Strict Architectural Boundaries
- **`Aurora.Core` (`net10.0`):** Zero external dependencies. Holds semantic design tokens, WCAG contrast math, `AuroraResult<T>`, `CapabilityState`, `BoundedEventBus`, domain entities, and adapter contracts.
- **`Aurora.Application` (`net10.0`):** Pure application state orchestration (`AuroraStateStore`), `ThemePackageValidator`, `TaskbarLayoutCalculator`, window grouping, Quiet Hours logic, and deterministic workspace profile rollback.
- **`Aurora.Infrastructure` (`net10.0`):** SQLite WAL persistence (`SqliteAuroraRepository`) with transactional migrations & corrupt-DB recovery backup, SHA-256 verified `.auroratheme` packager, `PrivacySafeFileLogger`, and `StartupCrashSentinel`.
- **`Aurora.Platform.Windows` (`net10.0-windows10.0.22621.0`):** Isolates all Win32 (`SHAppBarMessage`, `EnumWindows`, `DwmGetWindowAttribute`, `GetDpiForMonitor`, `GetSystemPowerStatus`, `GetSystemTimes`), Core Audio COM (`IMMDeviceEnumerator`, `IAudioEndpointVolume`, `ISimpleAudioVolume`), WinRT (`GSMTC`, `Radio`, `NetworkInformation`), and WMI (`WmiMonitorBrightness`) calls.
- **UI Modules (`Aurora.Presentation` + 7 feature modules + `Aurora.App`):** Never invoke Win32/WinRT APIs directly from business ViewModels; render through semantic `AuroraThemeTokens` and the 17 shared components in `AuroraUiFactory`.
