# Project Aurora — Security, Secrets & Privacy Boundary (`v7.2.0`)

## 1. Zero-Privilege Execution (`asInvoker`)
- Both `aurora.exe` and `aurora-setup` run with `requestedExecutionLevel level="asInvoker"` (zero UAC elevation, zero system-wide services, zero kernel drivers, zero `explorer.exe` injection).
- Keyboard/mouse hooks (`WH_KEYBOARD_LL`, `WH_MOUSE_LL`) never block system keys (`Alt+Tab`, Turkish `AltGr`, `Ctrl`) or modify input streams across foreign windows without user initiation.

## 2. DPAPI Secret Protection & Local Data (`aurora-storage`)
- AI provider API tokens for the Desktop AI Quota widget (ChatGPT / Gemini / Claude) are encrypted at rest using Windows DPAPI (`CryptProtectData` / `CryptUnprotectData` bound to current user credentials).
- Application preferences (`shell_settings.json`), pinned items/todos (`aurora.db`), and clipboard vault (`clipboard_vault.db`) reside exclusively under `%LOCALAPPDATA%\ProjectAurora\data\`.

## 3. Privacy & Telemetry Boundary (`PrivacySafeLogger`)
- **Zero Cloud Telemetry:** No analytics SDKs or background telemetry endpoints exist.
- **Sanitized Diagnostics:** Logs under `%LOCALAPPDATA%\ProjectAurora\logs\aurora-YYYYMMDD.jsonl` store lifecycle events, DPI/WorkArea geometry, and sanitized errors—never clipboard text contents, notification message bodies, or personal filenames.
- **SHA-256 Verified Auto-Update:** Updates require HTTPS transport (`https://github.com/MuharremDidici/ProjectAurora/...`) and verify cryptographic `SHA-256` digests before executing the installer.
