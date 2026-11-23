# PROJECT AURORA — Risk Register

**Document ID:** AURORA-RISK-001  
**Version:** 0.1.0-draft  
**Date:** 2026-11-17  
**Status:** G0 review draft

Scoring: Probability (P) and Impact (I) are 1–5. Initial score = P × I. Scores are qualitative and must be reassessed after feasibility spikes.

| ID | Risk | P | I | Score | Mitigation / control | Owner role | Gate |
|---|---|---:|---:|---:|---|---|---|
| R-001 | Native Windows taskbar cannot be reliably hidden using a supported route | 4 | 5 | 20 | Define replace as primary visible surface; user-managed hide; prove on clean Windows 11 builds; coexistence fallback; no undocumented hacks | Windows Platform Lead | G1 blocker |
| R-002 | AppBar conflicts with Explorer taskbar or other appbars | 3 | 5 | 15 | Implement AppBar protocol correctly; test all edges, monitors, Explorer restart and auto-hide combinations | Windows Platform Lead | G1/G4 |
| R-003 | Aurora crash leaves user with poor desktop usability | 3 | 5 | 15 | Never modify Explorer; native taskbar recovery guide; AppBar unregister; Safe Mode; crash/restart tests | Reliability Lead | G1/G8 |
| R-004 | Windows updates change shell/window behavior | 4 | 4 | 16 | Use documented APIs; compatibility matrix; Windows Insider pre-release validation; regression suite | QA Lead | G8 |
| R-005 | Multi-monitor and mixed-DPI bugs | 4 | 4 | 16 | Per-monitor DPI model; hot-plug tests; stable monitor identity; geometry recalculation | UI/Platform Lead | G3/G8 |
| R-006 | Wi-Fi/Bluetooth actions are restricted or inconsistent | 3 | 4 | 12 | Capability detection; deep-link fallback; never fake success; test standard-user permissions | Platform Lead | G1/G5 |
| R-007 | External monitor brightness unsupported | 4 | 3 | 12 | Treat DDC/CI as optional; capability discovery; show unsupported state and Settings fallback | Platform Lead | G1/G5 |
| R-008 | Third-party notification history cannot be accessed | 5 | 3 | 15 | Limit v1 to Aurora-owned notifications and explicitly supported integrations; avoid universal inbox claim | Product Lead | G0/G6 |
| R-009 | Theme import enables path traversal or unsafe asset loading | 2 | 5 | 10 | Data-only package; schema validation; archive limits; path normalization; fuzz tests | Security Lead | G7/G8 |
| R-010 | Blur/animation harms battery or GPU performance | 3 | 3 | 9 | Capability-gated effects; reduced motion; pause hidden surfaces; performance budgets | UI Lead | G2/G8 |
| R-011 | Audio device/session churn causes stale UI | 3 | 4 | 12 | Event-driven adapter; invalidation handling; structured errors; reconnect tests | Platform Lead | G5 |
| R-012 | AppBar or overlay interferes with fullscreen games | 3 | 4 | 12 | Fullscreen detection; suspend/auto-hide policy; test borderless and exclusive fullscreen | UX/Platform Lead | G1/G8 |
| R-013 | App startup or background process consumes excessive resources | 3 | 4 | 12 | Idle budgets; sampling limits; no unnecessary polling; ETW profiling | Performance Lead | G3/G8 |
| R-014 | Accessibility is degraded by custom controls | 3 | 4 | 12 | UI Automation semantics; keyboard-first tests; screen-reader review; contrast audit | UX Lead | G2/G8 |
| R-015 | Free model creates unsustainable update/support burden | 3 | 3 | 9 | No cloud dependency; narrow v1; automate tests/builds; transparent support scope | Product Owner | G0/G9 |
| R-016 | Installer/uninstaller leaves startup or shell residue | 2 | 5 | 10 | Install/uninstall test matrix; no shell patch; restore startup registration; clean-user testing | Release Lead | G8/G9 |
| R-017 | Native tray/Start behavior is mistaken for Aurora functionality | 3 | 4 | 12 | Explicit product boundaries; preserve Windows shell services; UI labels and docs | Product/UX Lead | G0/G2 |
| R-018 | Framework/API version drift invalidates assumptions | 3 | 3 | 9 | Pin stable SDK; dependency review; monthly servicing review; ADR update | Architecture Lead | G1/G8 |

## 1. Top risks requiring early evidence

### R-001 — Native taskbar hiding / replacement
This is the highest product risk. AppBar APIs support app-defined toolbars and system coordination; they do not grant native taskbar replacement. The team must prove the user-controlled hide + Aurora AppBar arrangement on supported Windows 11 configurations. If proof fails, revise product promise or ship coexistence.

### R-002 — AppBar conflicts
Prove edge reservation, monitor topology, auto-hide and Explorer restart behavior before building polished taskbar UI.

### R-003 — Recovery
The product must not make the user's desktop unusable if Aurora exits. Recovery must be possible without Aurora's UI.

### R-008 — Notification expectations
A universal third-party notification inbox is not a safe v1 assumption. Scope Aurora-owned notifications and separately approve each supported integration.

## 2. Risk acceptance rules

- Any score ≥15 requires a named mitigation owner and objective evidence before the dependent gate passes.
- Any risk with impact 5 affecting shell recovery or user access is release-blocking until tested.
- Unsupported API reliance is not accepted as a mitigation.
- A risk may be accepted only by the Product Owner and Technical Lead together, with documented residual impact.
