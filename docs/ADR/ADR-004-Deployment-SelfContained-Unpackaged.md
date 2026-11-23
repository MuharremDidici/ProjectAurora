# ADR-004: Deployment Model — Self-Contained Unpackaged Desktop baseline with MSIX Packaging Option

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Architecture Lead, Release Lead

## Context
Aurora must run under a standard-user account (`DEC-008`), work offline (`DEC-007`), leave zero system residue on removal (`R-016`), and launch reliably even on Windows 11 machines that do not have the Windows App Runtime 2.6 framework MSIX pre-installed or developer certificates trusted.

## Decision
Use **Self-Contained Unpackaged WinUI 3 deployment** (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `RuntimeIdentifier=win-x64`) as the primary build and standard-user portable/per-user install baseline:
- User data and SQLite state live strictly in `%LOCALAPPDATA%\ProjectAurora\`.
- Optional user-controlled "Start with Windows" uses standard-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`ProjectAurora`) only when explicitly toggled ON by the user (default: OFF), and is cleanly removed on toggle OFF or Reset/Uninstall cleanup.
- Aurora notifications use Aurora's own native WinUI 3 Notification Toast & Notification Center surface (`NT-001`..`NT-006`) persisted in SQLite, avoiding hard failure when unpackaged identity lacks MSIX COM registration, while keeping `INotificationService` ready for packaged MSIX builds.

## Consequences
- **Positive:** Zero UAC prompts, zero certificate trust requirements, runs out-of-the-box on clean Windows 11 x64 builds, trivial clean uninstall.
- **Negative:** Larger on-disk folder (~130–180 MB disk footprint due to bundled WinUI 3 runtime DLLs), while runtime working set remains compact.
