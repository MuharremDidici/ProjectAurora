# ADR-006: SQLite Schema, Transactional Migrations & Versioned Theme Package Format

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Architecture Lead, Security Lead

## Context
Aurora requires local offline persistence (`DEC-007`) with schema versioning, pre-migration automatic backups (`02_AURORA_TDD.md` §8.3), and a strictly validated, data-only theme package format (`03_AURORA_DESIGN_SYSTEM.md` §9, `R-009`).

## Decision
1. **SQLite Database (`Microsoft.Data.Sqlite 10.0.15`):**
   - Stored at `%LOCALAPPDATA%\ProjectAurora\data\aurora.db` in WAL mode (`PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;`).
   - Tables: `SchemaMigrations`, `UserPreferences`, `PinnedShortcuts`, `MonitorLayouts`, `WorkspaceProfiles`, `WidgetConfigurations`, `NotificationHistory`, `InstalledThemes`.
   - Every schema upgrade copies `aurora.db` to `aurora.db.bak.v{old}` before running inside an explicit `SqliteTransaction`. If migration fails, Aurora rolls back, preserves the original file, and boots into **Safe Recovery Mode**.
2. **Theme Package (`.auroratheme`):**
   - Supports both standalone JSON manifest (`schemaVersion: "1.0"`) and ZIP archive containing `manifest.json` + optional approved image assets (`.png`, `.jpg`, `.webp` ≤ 2 MB per asset, ≤ 8 MB total uncompressed archive, max 16 entries).
   - **Security Validator (`ThemePackageValidator`):** Rejects `..` or rooted paths (path traversal), zip bombs (compression ratio / total size caps), executable/script extensions (`.exe`, `.dll`, `.ps1`, `.bat`, `.cmd`, `.js`, `.vbs`, `.xaml`), external `http://` / `https://` / `file://` URIs, unknown schema versions, and out-of-range token values.
   - Imported themes load into a **sandboxed Preview state** first (`ThemePreviewStarted`) until explicitly confirmed (`ThemeApplied`).
