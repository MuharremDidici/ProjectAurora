# PROJECT AURORA — G1 Technical Feasibility & Architecture Validation Plan

**Document ID:** AURORA-G1-PLAN-001  
**Version:** 0.1.0-draft  
**Date:** 2026-11-17  
**Gate:** G1  
**Status:** PLAN SUBMITTED — EXECUTION NOT AUTHORIZED  
**Implementation status:** FROZEN

## 1. G1 objective

Establish, with reproducible evidence on clean Windows 11 environments, whether Aurora's critical platform integrations are feasible using documented APIs and standard-user permissions.

G1 is a feasibility and architecture-validation gate. It is not a production implementation phase. The immediate priority is the taskbar/AppBar/native-taskbar interaction because it is the highest-risk dependency behind the product's primary promise.

## 2. G1 authorization boundary

### Authorized by this plan
- Test design and acceptance criteria.
- Windows build/hardware matrix definition.
- API/documentation review.
- Risk refinement.
- Evidence templates and architecture decision preparation.

### Not authorized yet
- Writing or compiling proof-of-concept code.
- Creating a Visual Studio solution or application scaffold.
- Installing SDKs, workloads or packages.
- Changing Windows taskbar, registry, startup or shell settings.
- Registering an AppBar on the user's everyday desktop.
- Running Explorer/taskbar manipulation experiments.
- Creating repository branches, commits or release artifacts.

**Execution rule:** Each technical spike must be separately approved after the Product Owner reviews the environment, exact experiment, rollback procedure and expected evidence. G1 execution approval is distinct from product implementation authorization.

## 3. G1 workstreams and priority

| Workstream | Priority | Question | Required outcome |
|---|---:|---|---|
| G1-WS1 Taskbar / AppBar | P0 | Can Aurora reserve its own edge and coexist predictably with Explorer? | AppBar lifecycle and behavior report |
| G1-WS2 Native taskbar visibility | P0 | Can users hide the native taskbar through supported Windows UI and recover it? | Supported configuration + recovery proof |
| G1-WS3 Recovery / shell resilience | P0 | Does Aurora failure leave Windows usable? | Crash/restart/recovery evidence |
| G1-WS4 Multi-monitor / DPI | P0 | Are per-monitor bounds, DPI and hot-plug handled correctly? | Geometry and topology report |
| G1-WS5 Audio | P1 | Can master endpoint and per-session controls work? | Endpoint/session capability report |
| G1-WS6 Network / Bluetooth / brightness | P1 | Which controls work as standard user on supported hardware? | Capability matrix and honest fallbacks |
| G1-WS7 Media sessions | P1 | Can media sessions and transport commands be enumerated/controlled? | Session integration report |
| G1-WS8 Notifications | P1 | What can Aurora create/manage without claiming universal inbox access? | Notification scope/API decision |
| G1-WS9 Runtime / packaging | P1 | Which .NET, Windows App SDK and deployment model is viable? | Version/deployment ADR proposal |
| G1-WS10 Performance baseline | P1 | Can the architecture plausibly meet idle/interaction budgets? | Measurement method and baseline |

## 4. Workstream details

### G1-WS1 — Taskbar and AppBar protocol (P0)

**Objective:** Validate Aurora's own AppBar behavior without attempting to replace Explorer.

**Test cases**
- Register one appbar and request bottom-edge placement.
- Query and set position using the documented AppBar protocol.
- Remove the appbar cleanly.
- Repeat on top, left and right edges if those positions remain in product scope.
- Observe behavior with native taskbar visible.
- Observe behavior with native taskbar configured to auto-hide.
- Observe behavior with another appbar present, where a safe test fixture exists.
- Observe taskbar geometry after display resolution and scaling changes.
- Observe behavior after Explorer restart, Aurora process termination and user sign-out/sign-in.

**Evidence**
- OS build and shell version.
- API message sequence and return values.
- Requested and granted rectangles.
- Before/after screenshots.
- Event timeline and failure logs.
- Explicit statement of any undocumented or inconsistent behavior.

**Pass criteria**
- Registration, position negotiation and removal are repeatable.
- No persistent screen-edge reservation after clean removal.
- No overlap or unusable desktop state in the tested supported configurations.
- Failures are detectable and lead to a safe fallback.

### G1-WS2 — Native taskbar visibility and recovery (P0)

**Objective:** Establish whether the user's requested primary-taskbar experience can be achieved without unsupported system modification.

**Test cases**
- Document the native Windows 11 taskbar settings available in the target build.
- User manually configures native taskbar auto-hide through Windows Settings.
- Verify whether Aurora's own AppBar remains accessible and whether edge-reveal behavior conflicts.
- Revert the setting manually through Windows Settings.
- Repeat after reboot/sign-in.
- Verify recovery when Aurora is not running.

**Strict restrictions**
- No undocumented registry edits.
- No process termination or restart automation for Explorer.
- No shell injection, patching or binary modification.
- No attempt to impersonate the native taskbar.
- No claim of complete native taskbar suppression unless directly proven through a documented user-controlled path.

**Pass criteria**
- A repeatable user-controlled supported configuration exists for the intended experience.
- Recovery does not require Aurora to be running.
- If not achieved, coexistence mode is declared the supported default; the product claim is updated accordingly.

### G1-WS3 — Failure, recovery and shell resilience (P0)

**Scenarios**
- Aurora exits normally.
- Aurora process crashes.
- AppBar registration fails.
- Display topology changes while Aurora is active.
- Explorer restarts independently.
- Sleep/resume.
- User signs out and back in.
- Aurora startup is disabled or application is uninstalled.

**Pass criteria**
- Explorer and Windows shell remain operational.
- No persistent overlay or reservation prevents normal desktop use.
- Native taskbar recovery instructions are independently usable.
- No always-on elevated service is required.
- Any limitation is documented and treated as a release blocker if it threatens desktop usability.

### G1-WS4 — Multi-monitor and mixed DPI (P0)

**Minimum topology matrix**
- Single monitor, 100% scaling.
- Single monitor, 125% and 150% scaling.
- Dual monitors with equal scaling.
- Dual monitors with mixed 100% / 150% scaling.
- Primary monitor changed while app is active.
- Secondary monitor unplugged/reconnected.
- Resolution changed while app is active.
- Laptop internal panel plus external display.
- Taskbar edge on primary and secondary displays, where supported.

**Pass criteria**
- No stale physical-pixel coordinates used as the only persisted geometry.
- Taskbar bounds remain on the intended monitor.
- Transient panels remain within the intended work area.
- Hot-plug and primary-monitor changes do not orphan windows or surfaces.

### G1-WS5 — Audio endpoint and session controls (P1)

**Scope**
- Enumerate active render/capture endpoints.
- Read default endpoint and volume/mute state.
- Set endpoint volume/mute.
- Enumerate application audio sessions.
- Set session volume/mute where supported.
- Handle endpoint removal, default-device change and session disappearance.

**Pass criteria**
- UI state reflects actual platform state.
- Device/session invalidation is handled without crash.
- No optimistic success is shown when the API operation fails.
- Standard-user operation is confirmed on the target machine.

### G1-WS6 — Network, Bluetooth and brightness (P1)

Build a capability matrix per Windows build and hardware:
- Network connectivity and Wi-Fi status.
- Radio state and Bluetooth status.
- Device settings/pairing handoff.
- Internal panel brightness.
- External monitor brightness (DDC/CI only if applicable and explicitly optional).

**Pass criteria**
- Each action is classified as `directly supported`, `supported with consent/capability`, `deep-link fallback`, or `unsupported`.
- Aurora never displays a successful toggle when the OS operation was not performed.
- External monitor brightness is not promised as universal.

### G1-WS7 — Media session integration (P1)

**Scope**
- Request the global media session manager.
- Enumerate sessions.
- Observe current-session and session-list changes.
- Read available metadata and artwork.
- Attempt supported transport commands only when the session reports capability.

**Pass criteria**
- Session addition/removal is reflected.
- Unsupported metadata/commands are represented honestly.
- Multiple sessions do not create ambiguous or stale UI state.

### G1-WS8 — Notifications (P1)

**Scope**
- Validate Aurora-originated notifications through the selected Windows App SDK API and packaging model.
- Define local Aurora notification history.
- Review whether any specific third-party integration has a supported public contract.

**Pass criteria**
- Aurora-owned notifications are viable under the chosen deployment model.
- No universal third-party notification-history claim is made.
- Permission, activation and package identity requirements are documented.

### G1-WS9 — Runtime and deployment model (P1)

**Candidates**
- .NET 10 LTS baseline.
- .NET 11 STS comparison only if Windows App SDK support or a required capability justifies it.
- Windows App SDK 2.6.5 Stable as the current investigation baseline on 2026-11-17.
- Packaged MSIX vs unpackaged/external-location deployment.

**Pass criteria**
- Supported API availability is confirmed for the chosen target.
- Startup registration, notifications, file associations, updates and uninstall behavior are understood.
- No Preview or Experimental API is required for P0 functionality.
- Exact versions are pinned only after compatibility and deployment tests.

### G1-WS10 — Performance feasibility (P1)

**Reference measurements**
- Idle CPU over 5 minutes after warm-up.
- Idle private working set.
- Taskbar interaction latency P50/P95.
- Control Center open latency P50/P95.
- Animation frame pacing.
- CPU/GPU impact of blur and widgets.
- Sleep/resume and monitor hot-plug overhead.

**Targets inherited from PRD**
- Idle CPU average ≤0.5% on the documented reference machine.
- Idle private working set ≤120 MB initial target.
- Taskbar interaction P95 ≤100 ms.
- Control Center open P95 ≤150 ms.

These are targets, not assumed results. If a target is not met, record workload, hardware, trace and proposed trade-off.

## 5. Windows 11 test environment matrix

The exact supported Windows build range must be selected before G1 execution. Do not infer support from a single workstation.

| Environment | Purpose | Required |
|---|---|---|
| Clean Windows 11 x64 VM, current supported stable build | Repeatable shell/AppBar and recovery tests | Yes |
| Clean Windows 11 x64 physical device, single monitor | Real display/input behavior | Yes |
| Windows 11 physical device, dual monitors, mixed DPI | Multi-monitor and DPI | Yes |
| Windows 11 laptop with internal panel + external display | Brightness and power capability | Yes |
| Windows 11 system with multiple audio endpoints | Audio endpoint/session | Yes |
| Windows 11 test account with standard-user permissions | Permission boundary | Yes |
| Optional secondary Windows 11 build in support window | Compatibility comparison | Recommended |

For every run, record:
- Windows edition, version, OS build and revision.
- CPU, GPU, RAM and display adapters.
- Monitor model, resolution, refresh rate, connection and scaling.
- Audio endpoints and connected radios.
- Account privilege level.
- SDK/runtime version if an approved spike later uses one.
- Test date, operator, test ID and evidence location.

## 6. G1 test execution governance

Before any spike:
1. Submit a spike-specific execution request.
2. State exact objective and hypothesis.
3. List APIs and settings to be touched.
4. Provide a rollback and recovery procedure.
5. Define the test machine/VM and confirm it is not the user's primary work environment unless explicitly approved.
6. Define evidence to capture.
7. Obtain explicit Product Owner authorization.

A G1 spike must not evolve into a production feature or reusable application module without a separate implementation gate.

## 7. Evidence package structure

```text
G1_EVIDENCE/
  00_G1_EXECUTION_MANIFEST.md
  01_ENVIRONMENT_INVENTORY.md
  02_TASKBAR_APPBAR/
  03_NATIVE_TASKBAR_VISIBILITY/
  04_RECOVERY_SHELL_RESILIENCE/
  05_MULTI_MONITOR_DPI/
  06_AUDIO/
  07_NETWORK_BLUETOOTH_BRIGHTNESS/
  08_MEDIA/
  09_NOTIFICATIONS/
  10_RUNTIME_DEPLOYMENT/
  11_PERFORMANCE/
  12_G1_FINAL_RECONCILIATION.md
```

Every test report must contain: objective, preconditions, exact steps, expected result, actual result, pass/fail, logs/screenshots, environment, deviations and reviewer.

## 8. G1 exit criteria

G1 may pass only when:
- P0 AppBar behavior is demonstrated with repeatable evidence.
- Native taskbar hide/recovery feasibility is decided.
- Coexistence fallback is validated if replacement-mode conditions are not met.
- Crash/recovery does not impair Windows shell availability.
- Multi-monitor and mixed-DPI behavior is understood and bounded.
- Every P0 system integration has an explicit capability/fallback classification.
- Runtime and deployment direction is supported by compatibility evidence.
- Top risks are updated with residual scores and named owners.
- TDD and relevant ADRs are reconciled against measured results.
- No undocumented shell modification is required for the accepted product promise.

## 9. G1 decision outcomes

| Outcome | Meaning | Consequence |
|---|---|---|
| PASS | All P0 feasibility criteria met | Submit architecture baseline for G2 review |
| CONDITIONAL PASS | Core concept viable, bounded limitations remain | G2 may proceed only with explicit product scope/mitigation |
| FAIL | Primary taskbar promise or recovery cannot be met safely | Re-scope to coexistence-first product or revisit product concept |

**Critical rule:** If supported native-taskbar hiding is not reliable, Aurora may still be a viable premium desktop enhancement, but the product must be positioned as coexistence-first. Do not force a technical workaround to preserve the original wording.

## 10. G1 deliverables

1. `G1_Environment_Inventory.md`
2. `G1_Taskbar_AppBar_Feasibility_Report.md`
3. `G1_Native_Taskbar_Visibility_And_Recovery_Report.md`
4. `G1_Shell_Resilience_Report.md`
5. `G1_Multi_Monitor_DPI_Report.md`
6. `G1_System_Controls_Capability_Matrix.md`
7. `G1_Audio_Media_Notifications_Report.md`
8. `G1_Runtime_Deployment_Recommendation.md`
9. `G1_Performance_Feasibility_Report.md`
10. Updated risk register.
11. Updated TDD and ADRs.
12. `G1_Final_Reconciliation.md`

## 11. Current gate state

- G0: `PASS — CLOSED`
- G1 plan: `SUBMITTED`
- G1 execution: `NOT AUTHORIZED`
- Product implementation: `FROZEN`
- Repository/code changes: `NONE`
- Windows configuration changes: `NONE`

**Next decision required:** Product Owner approval of this G1 plan and a separate, bounded G1 execution request. No implementation begins by virtue of approving the plan.
