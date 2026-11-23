# ADR-007: Notification Scope & Privacy Boundary

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Product Lead, Security Lead

## Context
Per `01_AURORA_PRD.md` §5.3 and `R-008`, Aurora must not claim or attempt undocumented central interception of all third-party Windows notifications, and must protect user privacy by default (`NFR-012`).

## Decision
1. **Aurora-Managed Notification Engine (`NT-001`..`NT-006`):**
   - Aurora generates and presents notifications for Aurora modules (Workspace switches, Audio device changes, Battery thresholds, Quiet Hours transitions, Theme import validation, Recovery advisories, Reminder/Timer widgets, and user-created/API-submitted local Aurora alerts).
   - Notifications are stored locally in SQLite with configurable retention (default: 7 days / max 200 items), grouped by Source Category & Time bucket, and respect **Quiet Hours / Focus Mode** (`NT-004`).
   - Includes **Clear All with Undo** (`NT-006`) and individual dismissal.
2. **Third-Party Notification Boundary:**
   - UI explicitly communicates that Aurora Notification Center manages Aurora ecosystem alerts and does not scrape private third-party notification payloads, providing a one-click `ms-settings:notifications` shortcut for OS notification settings.
3. **Privacy-First Logging:**
   - Diagnostic logs (`%LOCALAPPDATA%\ProjectAurora\logs\`) never record window titles, notification message bodies, typed search text, media track names, or Wi-Fi SSIDs unless explicitly redacted/hashed or omitted. Zero network telemetry exists in v1.
