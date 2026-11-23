# PROJECT AURORA — G0 Documentation Package

**Document set:** G0 Product & Engineering Baseline + G0 Reconciliation + G1 Feasibility Plan  
**Version:** 0.2.0  
**Date:** 2026-11-17  
**Status:** G0 ACCEPTED — G1 PLANNING AUTHORIZED  
**Implementation status:** FROZEN — NO IMPLEMENTATION AUTHORIZED

---

## 1. Executive decision

Aurora is a Windows 11 desktop-experience application that provides its own taskbar as the primary visible taskbar experience, a redesigned Control Center, Aurora-managed notifications, widgets, media controls, workspace profiles and a personalization studio.

The Product Owner has explicitly accepted the following interpretation of the taskbar replacement decision:

> Aurora will be the primary visible taskbar experience. It will not replace or patch Explorer. The native Windows taskbar will be hidden only through a user-controlled supported Windows configuration where reliable. If that cannot be validated on a supported Windows 11 build, Aurora will offer coexistence mode. No undocumented registry, injection, shell patching or Explorer termination will be used.

**G0 is closed as ACCEPTED.** The native-taskbar hiding and AppBar coexistence dependency remains a release-blocking feasibility question assigned to G1. G0 acceptance does not imply that the native taskbar can be hidden programmatically or that Aurora can assume shell ownership.

## 2. Binding decisions

| ID | Decision | Baseline |
|---|---|---|
| DEC-001 | Operating system | Windows 11 only |
| DEC-002 | Visual direction | Premium; Aurora Glass, Obsidian Pro, Aurora Flow, Nebula |
| DEC-003 | Product scope | Taskbar, Control Center, Notifications, Widgets, Media Hub, Workspace Manager, Personalization Studio, Settings |
| DEC-004 | Taskbar strategy | Aurora is the primary visible taskbar; Windows shell remains intact |
| DEC-005 | Native taskbar | User-controlled supported hide configuration only; coexistence fallback mandatory |
| DEC-006 | Distribution | Free; no paid tier or account requirement in v1 |
| DEC-007 | Connectivity | Core experience must work offline |
| DEC-008 | Privilege | Standard-user operation by default; no always-on elevation |
| DEC-009 | Implementation status | Frozen; G1 planning only; no code or repository changes authorized |

## 3. Architecture constraint

Windows documents AppBar APIs for application-defined desktop toolbars. These APIs let an app register an AppBar, request/set its position and receive relevant notifications. They do not turn an application into the Windows shell or provide a general supported API for a third-party application to replace Explorer's native taskbar.

Aurora will use the AppBar contract only for its own bar's system coordination. The native Windows taskbar remains an OS component. Native-taskbar visibility and coexistence behavior must be verified on the supported Windows 11 build matrix before any replacement-mode promise is made.

## 4. Document inventory

1. `01_AURORA_PRD.md` — Product Requirements Document.
2. `02_AURORA_TDD.md` — Technical Design Document and API feasibility baseline.
3. `03_AURORA_DESIGN_SYSTEM.md` — Visual identity, design tokens and interaction rules.
4. `04_AURORA_RISK_REGISTER.md` — Risk register and mitigations.
5. `05_AURORA_G0_ACCEPTANCE.md` — Updated G0 gate status.
6. `06_TECHNICAL_SOURCE_REGISTER.md` — Official technical source register.
7. `07_G0_ACCEPTANCE_RECONCILIATION.md` — Decision reconciliation and G0 closure record.
8. `08_G1_FEASIBILITY_PLAN.md` — G1 workstreams, test matrix, evidence and exit criteria.

## 5. Authorization boundary

**Authorized now**
- G0 documentation reconciliation and closure.
- G1 feasibility planning, test design, evidence templates and risk refinement.
- Review of official Microsoft documentation.

**Not authorized**
- Source code, solution/project scaffolding or proof-of-concept code.
- Repository initialization or modification.
- Package installation or SDK setup.
- Windows registry edits or taskbar setting changes.
- Explorer or shell process manipulation.
- Installer, updater or release artifact.
- Commits or implementation work.

G1 experiments require a separate explicit execution authorization after the G1 plan is reviewed. Any future experiment must be isolated, reversible, standard-user by default and limited to the approved test scope.

## 6. Gate status

- G0: `PASS — ACCEPTED`
- G1: `PLANNING AUTHORIZED`
- G1 execution: `NOT AUTHORIZED`
- Product implementation: `FROZEN`

The next decision is approval of the G1 feasibility plan and its test environment/matrix. This is not approval to begin implementation.
