# ADR-001: Runtime Selection — .NET 10 LTS vs .NET 11 STS

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Architecture Lead, Product Owner

## Context
As of November 2026, Microsoft's official .NET Support Policy lists both **.NET 10 (LTS)** and **.NET 11 (STS)** as active releases, with .NET 10 LTS supported through November 14, 2028. `02_AURORA_TDD.md` designates .NET 10 LTS as the preferred runtime baseline unless Windows App SDK compatibility requires an override.

## Decision
Select **.NET 10.0 LTS** (`net10.0` for platform-independent libraries and `net10.0-windows10.0.22621.0` with `TargetPlatformMinVersion=10.0.22000.0` for Windows 11 adapters and WinUI 3 surfaces), pinned to the current `.NET 10.0.404` SDK / `10.0.15` runtime baseline.

## Consequences
- **Positive:** Long-Term Support stability, full compatibility with `Microsoft.WindowsAppSDK 2.6.5` Stable and `Microsoft.Data.Sqlite 10.0.x`, predictable servicing without STS churn.
- **Negative:** C# 15 / .NET 11 STS-only runtime features are excluded in v1.0.
