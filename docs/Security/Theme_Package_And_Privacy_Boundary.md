# Project Aurora — Theme Package Security & Privacy Boundary

## 1. Theme Package Threat Model (`R-009`)
Imported `.auroratheme` files are untrusted user inputs. `ThemePackageValidator` enforces:
1. **Data-Only Contract:** Only declarative token JSON (`schemaVersion: "1.0"`) and optional approved raster images (`.png`, `.jpg`, `.jpeg`, `.webp`).
2. **Executable & Script Denylist:** Any entry ending in `.exe`, `.dll`, `.ps1`, `.bat`, `.cmd`, `.vbs`, `.js`, `.msi`, `.scr`, `.com`, `.xaml`, or `.cs` is immediately rejected.
3. **Path Traversal Protection:** Rejects archive entries containing `..`, leading `/` or `\`, drive letters (`:`), or control characters; validates normalized full path stays strictly within staging sandbox.
4. **Zip Bomb & Resource Caps:** Max 16 entries, max 2 MB per asset, max 8 MB uncompressed total archive size, max 1,024 KB `manifest.json`.
5. **External Reference Rejection:** Rejects `http://`, `https://`, `ftp://`, `file://`, and UNC (`\\`) references in manifest fields.
6. **Sandboxed Preview First:** Imported themes enter `Preview` state first and are never persisted as the active startup theme until the user clicks **Apply Theme**.

## 2. Privacy & Telemetry Boundary (`NFR-012`, `ADR-007`)
- **Default Telemetry:** Disabled (zero cloud endpoints, zero analytics SDKs).
- **Sensitive Data Redaction in Local Logs:** Local diagnostic logs in `%LOCALAPPDATA%\ProjectAurora\logs\` record module lifecycle events, adapter capability codes, latency metrics, and sanitized exception types—never window titles, notification message text, media track names, or network SSIDs.
- **Weather Widget Network Disclosure (`WG-003`):** Operates in **Offline Local Simulation / Disabled Network Mode** by default until the user explicitly opts in to network weather lookup in Widget Settings.
