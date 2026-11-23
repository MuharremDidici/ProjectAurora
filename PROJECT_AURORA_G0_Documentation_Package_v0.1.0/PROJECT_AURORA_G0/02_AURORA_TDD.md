# PROJECT AURORA — Technical Design Document

**Document ID:** AURORA-TDD-001  
**Version:** 0.1.0-draft  
**Date:** 2026-11-17  
**Status:** G0 review draft — no implementation authorized

## 1. Technical objectives

1. Build a Windows 11 desktop application, not a replacement OS shell.
2. Separate UI/presentation from Windows-specific system adapters.
3. Use documented and supported APIs wherever possible.
4. Keep default operation at standard-user privilege.
5. Make taskbar registration, settings and startup behavior reversible.
6. Ensure the app can fail or exit without taking down Windows shell functionality.

## 2. Recommended platform baseline

| Layer | Recommendation | Rationale |
|---|---|---|
| Language | C# | Strong Windows/.NET ecosystem and maintainability |
| Runtime | .NET 10 LTS | LTS support through Nov 2028 according to current .NET policy |
| UI | WinUI 3 | Native Windows desktop UI framework |
| App framework | Windows App SDK stable channel | Supported production APIs |
| Current SDK evaluation point | Windows App SDK 2.6.5 stable | Current stable release reported Nov 2, 2026; verify at implementation start |
| Windowing | WinUI `Window`, `AppWindow`, HWND interop | Documented desktop window model |
| Shell edge integration | Win32 AppBar API (`SHAppBarMessage`) | Documented appbar registration and positioning |
| Audio | Windows Core Audio APIs | Endpoint and per-session audio management |
| Notifications | Windows App SDK `AppNotificationManager` for Aurora-originated notifications | Current recommended API for new Windows App SDK apps |
| Media | `GlobalSystemMediaTransportControlsSessionManager` | Windows media session control |
| Persistence | SQLite for structured state + versioned JSON theme packages | Transactional local data and portable theme files |
| Logging | Structured local logging | Diagnostics without mandatory cloud telemetry |
| Tests | xUnit + Windows integration test harness + UI automation | Unit, adapter and end-to-end validation |

**Version policy:** Pin stable package versions during implementation. Do not use Preview or Experimental Windows App SDK APIs in production-critical paths. Re-check Microsoft release notes before creating the solution. .NET 11 is the newest release as of this document date, but it is STS; .NET 10 is the preferred baseline because it is LTS. A deliberate ADR may override this if Windows App SDK compatibility or support policy changes.

## 3. Architectural style

Use a modular monolith for v1. Modules have explicit contracts and dependency boundaries, but do not require a process or assembly per feature. Avoid premature microservices and avoid unnecessary project fragmentation.

### 3.1 Logical layers

```text
Presentation Layer
  Taskbar UI | Control Center UI | Notifications UI | Widgets | Settings
                 |
Design & Interaction Layer
  Theme tokens | Components | Layout | Motion | Accessibility
                 |
Application Core
  Commands | Queries | State | Event contracts | Profile orchestration
                 |
Platform Abstraction
  IWindowCatalog | IAudioService | INetworkStatus | IRadioService
  IDisplayService | IPowerService | IMediaSessionService | IAppBarService
                 |
Windows Adapters
  Win32 | WinRT | Windows App SDK | Core Audio | AppBar | GSMTC
                 |
Windows 11
```

Infrastructure is a cross-cutting adapter for persistence, logging, package/update metadata and diagnostics. UI must not call Win32/WinRT APIs directly.

### 3.2 Proposed solution layout

```text
ProjectAurora/
  src/
    Aurora.App/
    Aurora.Core/
    Aurora.Application/
    Aurora.Presentation/
    Aurora.Taskbar/
    Aurora.ControlCenter/
    Aurora.Notifications/
    Aurora.Widgets/
    Aurora.Media/
    Aurora.Workspaces/
    Aurora.Personalization/
    Aurora.Platform.Windows/
    Aurora.Infrastructure/
  tests/
    Aurora.Core.Tests/
    Aurora.Application.Tests/
    Aurora.Platform.Windows.Tests/
    Aurora.Integration.Tests/
    Aurora.UITests/
  docs/
    Product/
    Architecture/
    Design/
    ADR/
    Security/
    QA/
```

Project boundaries are logical first. Separate assemblies only where they enforce a real dependency or deployment boundary.

## 4. Process and window model

### 4.1 Initial process model
- One primary user-session process for v1.
- Single-instance coordination.
- A small set of top-level HWND windows: taskbar per active monitor as required, transient Control Center, notification surface, launcher and settings window.
- No Windows service in v1.
- No elevated helper in v1.
- Crash/restart behavior must not depend on Explorer restart.

A separate watchdog process is not baseline scope. If later introduced, it must not kill or manipulate Explorer and must have a strict lifecycle contract.

### 4.2 Windowing
- Use WinUI 3 windows and Windows App SDK `AppWindow` for supported top-level window management.
- Use HWND interop only where the required Windows API is HWND-based.
- Define window ownership, activation, z-order, focus and dismissal behavior for each surface.
- Treat fullscreen, exclusive fullscreen, secure desktop, lock screen, UAC prompts and remote desktop as special environments. Aurora must not cover secure desktop or intercept secure attention sequences.
- Per-monitor DPI and display topology changes must trigger layout recalculation.

## 5. Taskbar architecture and replacement semantics

### 5.1 Documented AppBar contract
Windows exposes `SHAppBarMessage` and AppBar messages such as `ABM_NEW`, `ABM_REMOVE`, `ABM_QUERYPOS`, `ABM_SETPOS`, `ABM_GETSTATE`, `ABM_GETTASKBARPOS` and related notifications. The AppBar model allows application-defined desktop toolbars to coordinate with the system and other appbars.

Aurora should:
1. Create its taskbar HWND.
2. Register using `ABM_NEW`.
3. Query and set its position using the AppBar protocol.
4. Process callback notifications and monitor/display changes.
5. Unregister with `ABM_REMOVE` on orderly shutdown.
6. Handle abrupt process termination and stale registration behavior through OS lifecycle testing.

**Important:** AppBar support is not equivalent to native taskbar replacement. It does not grant control over Explorer's taskbar, Start, tray, notification center or shell process.

### 5.2 Replacement UX
The product-level target is that Aurora is the primary visible taskbar. The Windows native taskbar may be hidden by the user through Windows settings. Aurora should:
- Explain the distinction during onboarding.
- Link users to the relevant Windows taskbar settings.
- Detect if the native bar is still visible when technically possible.
- Offer coexistence mode.
- Provide a recovery guide that works even if Aurora cannot launch.

No undocumented registry edit, Explorer process termination, DLL injection, binary patching or shell replacement is permitted in v1.

### 5.3 Release-blocking feasibility spike
Before taskbar implementation is authorized, test on clean Windows 11 builds:
- Can Aurora AppBar reserve the intended edge on primary and secondary monitors?
- Does Explorer taskbar auto-hide coexist without overlap or reveal conflicts?
- What happens after Explorer restart, Aurora crash, user sign-out, sleep/resume, display hot-plug and Windows update?
- Can user restore native taskbar with Aurora stopped?
- Does taskbar behavior remain predictable with fullscreen apps and virtual desktops?

If the native taskbar cannot be reliably hidden using a supported user-controlled route, the product must ship coexistence mode or revise its replacement promise. No hack-based workaround.

## 6. Windows adapter contracts

The following are proposed contracts, not implementation code:

| Contract | Responsibility | Failure behavior |
|---|---|---|
| `IWindowCatalog` | Enumerate and observe top-level windows | Mark stale/unavailable; do not crash UI |
| `IWindowActivationService` | Activate, restore, minimize windows | Return structured failure |
| `IAppBarService` | Register/reposition/unregister Aurora bar | Release reservation and enter safe mode |
| `IAudioEndpointService` | Enumerate endpoints and set master volume | Show unavailable/error state |
| `IAudioSessionService` | Enumerate sessions and control per-session volume | Drop invalid sessions safely |
| `INetworkStatusService` | Read network connectivity and supported status | Deep-link to Settings if action unsupported |
| `IRadioService` | Query/request supported radio state | Permission/capability state exposed |
| `IDisplayService` | Query displays and supported brightness controls | Per-display capability and fallback |
| `IPowerService` | Read battery and power status | Show unavailable state |
| `IMediaSessionService` | Enumerate/control media sessions | Disable unsupported commands |
| `INotificationService` | Manage Aurora-owned notifications | Local persistence fallback |
| `IMonitorTopologyService` | Detect display changes and DPI | Re-layout without stale coordinates |

All adapters must expose capability discovery, cancellation, structured errors and asynchronous operations where applicable.

## 7. API feasibility matrix

| Feature | Candidate API | Feasibility | Caveat / spike |
|---|---|---|---|
| AppBar positioning | `SHAppBarMessage` / `APPBARDATA` | High for appbar behavior | Does not replace Explorer taskbar |
| Window enumeration | Win32 `EnumWindows`, window events | High | Respect visibility, cloaking, owned windows and permissions |
| Window previews | DWM thumbnail APIs | Medium-high | Privacy, lifecycle and composition edge cases |
| Window activation | Win32 foreground/activation APIs | Medium | Foreground activation restrictions apply |
| Master audio | Core Audio endpoint APIs | High | Device changes and endpoint invalidation |
| Per-app audio | Audio Session APIs / `ISimpleAudioVolume` | High | Sessions may appear/disappear; app behavior varies |
| Media controls | GSMTC session manager | High | Player metadata and transport controls are optional |
| Wi-Fi status/control | Windows networking APIs | Medium | Radio state, connection management and permissions differ |
| Bluetooth | `Windows.Devices.Radios` and device APIs | Medium | Capability/consent and pairing UX limitations |
| Brightness | Display configuration / monitor-specific APIs | Medium-low | Internal panel and external monitor support differ; DDC/CI not universal |
| Battery | Windows power status APIs | High | Desktop PCs may not expose battery |
| Own notifications | `AppNotificationManager` | High | This is Aurora's own notification surface, not a universal notification inbox |
| Third-party notification history | No assumed general public API | Low / unproven | Do not promise complete capture |
| Virtual desktops | Supported documented APIs must be confirmed | Unproven | Do not depend on undocumented COM interfaces |
| Native taskbar hide | User-managed Windows setting | Conditional | No assumed supported programmatic replacement API |

## 8. Data and persistence design

### 8.1 Local database
SQLite stores:
- User preferences.
- Pinned applications and shortcut metadata.
- Monitor-specific layout.
- Workspace profiles.
- Widget configuration.
- Aurora notification history.
- Theme metadata and migration version.

### 8.2 Theme package
A theme package is a versioned archive with:
- Manifest and schema version.
- Token values.
- Approved assets.
- Font references (no bundled unlicensed fonts).
- Component style variants.
- Motion settings.
- Integrity metadata.

Theme import must reject path traversal, oversized assets, unsupported schema versions, executable content and unsafe external references.

### 8.3 Migration
- Every persisted schema has a version.
- Migrations are transactional.
- Back up before destructive migration.
- Unknown future fields are preserved where feasible.
- Failed migration enters recovery mode and preserves original data.

## 9. Event and state model

Use explicit typed application events/commands. Avoid a global mutable service locator or unbounded event bus.

Examples:
- `WindowOpened`, `WindowClosed`, `WindowActivated`
- `MonitorTopologyChanged`
- `AudioEndpointChanged`
- `AudioSessionAdded`, `AudioSessionRemoved`
- `ThemePreviewStarted`, `ThemeApplied`
- `ProfileChanged`
- `AuroraNotificationReceived`

Events must be bounded, observable and unsubscribed on module shutdown. High-frequency telemetry (pointer movement, audio meter samples) must not be broadcast through a general-purpose event bus.

## 10. Performance strategy

- Avoid polling when event-driven APIs exist.
- Coalesce high-frequency events.
- Virtualize long lists.
- Load artwork and previews asynchronously.
- Use composition effects sparingly and capability-gate expensive blur.
- Pause or reduce animation when hidden, minimized, battery saver is active or reduced-motion is enabled.
- Cache theme resources and avoid full tree rebuild on minor token changes.
- Measure idle and active scenarios separately.
- Establish ETW/Windows Performance Recorder traces for regressions.

## 11. Security and reliability

- Standard-user process only by default.
- No kernel driver, injection, shell patching or always-on service.
- Validate imported theme data.
- Use signed updates and secure update metadata.
- Avoid logging sensitive content such as window titles, notification bodies or media metadata by default.
- Keep secrets out of logs and configuration.
- Implement Safe Mode and a documented native-taskbar recovery path.
- On shutdown, unregister AppBar and dispose all event subscriptions.
- On crash, Windows Explorer must remain untouched.

## 12. Packaging and deployment

Evaluate packaged MSIX and unpackaged deployment with package identity/external location as appropriate. Decide only after testing:
- Startup registration.
- AppNotificationManager requirements.
- File associations for theme packages.
- Update mechanism.
- Framework/runtime dependency handling.
- Install/uninstall rollback.
- Per-user vs per-machine installation.

No installer format is locked at G0.

## 13. Test strategy

- Unit: state transitions, theme schema, layout calculation, profile migration.
- Adapter: API capability, error mapping, cancellation, device removal.
- Integration: AppBar registration, audio endpoint changes, media sessions, display changes.
- UI: keyboard navigation, focus, dismissal, screen reader labels.
- System: sleep/resume, Explorer restart, sign-out, crash, multi-monitor, mixed DPI.
- Performance: idle, taskbar hover, Control Center, media artwork, widget sampling.
- Security: malformed theme archives, path traversal, update signature failure, standard-user permissions.

## 14. Architecture decision records required before implementation

- ADR-001: .NET 10 LTS vs .NET 11 STS.
- ADR-002: WinUI 3 and Windows App SDK stable version.
- ADR-003: AppBar strategy and native-taskbar coexistence/replacement semantics.
- ADR-004: Packaged vs unpackaged deployment.
- ADR-005: Process/window model.
- ADR-006: SQLite schema and theme package format.
- ADR-007: Notification scope and privacy boundary.
- ADR-008: Audio implementation and native interop library choice.
- ADR-009: Multi-monitor and DPI strategy.
- ADR-010: Startup, recovery and uninstall guarantees.
