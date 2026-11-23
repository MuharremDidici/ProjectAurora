# PROJECT AURORA — G0 Documentation Package

**Document set:** G0 Product & Engineering Baseline  
**Version:** 0.1.0-draft  
**Date:** 2026-11-17  
**Status:** Submitted for G0 review — NOT APPROVED FOR IMPLEMENTATION  
**Platform:** Windows 11  
**Product model:** Free  
**Visual target:** Premium  
**Taskbar decision:** Replace as the primary visible taskbar experience

---

## 1. Executive decision

Aurora is a Windows 11 desktop-experience application that provides its own taskbar, control center, notification experience, widgets, media controls, workspace profiles and personalization studio.

The approved product direction is **replace / primary experience**, not a supported replacement of the Windows shell. Aurora will not patch, inject into, terminate, or replace `explorer.exe`, nor will it claim ownership of the native Start menu, system tray, or Windows notification infrastructure.

The native Windows taskbar may be hidden by the user through supported Windows settings. Aurora may provide a guided setup link and detect whether the native bar remains visible. Programmatic suppression of the native taskbar is not a baseline capability unless a documented, supported Microsoft API is established and validated. This is a hard feasibility gate.

Aurora's own taskbar should use the documented Windows AppBar contract (`SHAppBarMessage`) to register its edge reservation and position. AppBar registration makes an application-defined desktop toolbar cooperate with the system taskbar; it does **not** turn the application into the Windows shell or guarantee native-taskbar replacement.

## 2. Binding decisions

| ID | Decision | Baseline |
|---|---|---|
| DEC-001 | Operating system | Windows 11 only |
| DEC-002 | Visual direction | Four premium theme families: Aurora Glass, Obsidian Pro, Aurora Flow, Nebula |
| DEC-003 | Product scope | Taskbar, Control Center, Notifications, Widgets, Media Hub, Workspace Manager, Personalization Studio, Settings |
| DEC-004 | Taskbar strategy | Aurora is intended to be the primary visible taskbar; no Explorer replacement or patching |
| DEC-005 | Distribution | Free; no paid tier or account requirement in v1 |
| DEC-006 | Connectivity | Core experience must work offline |
| DEC-007 | Privilege | Standard-user operation by default; no always-on elevation |
| DEC-008 | Implementation status | Frozen; documentation and feasibility only; no code or repository changes authorized |

## 3. Key architectural constraint

Windows documents AppBar APIs for application-defined desktop toolbars. These APIs let an app register an AppBar, request/set its position and receive relevant notifications. They do not provide a general supported API for a third-party application to become the native Windows taskbar.

Therefore, the word **replace** is defined for this project as a user-visible product outcome:
- Aurora becomes the user's primary taskbar surface.
- The Windows taskbar is hidden using a user-controlled, supported Windows configuration where possible.
- Windows shell services remain intact.
- The user can restore the native taskbar without Aurora running.
- If the desired hidden-native-taskbar configuration cannot be made reliable on a supported Windows 11 build, Aurora must disclose the limitation and offer coexistence mode rather than use unsupported hacks.

This distinction is a release-blocking product truth, not a minor implementation detail.

## 4. G0 deliverables

1. `01_AURORA_PRD.md` — Product Requirements Document.
2. `02_AURORA_TDD.md` — Technical Design Document and API feasibility.
3. `03_AURORA_DESIGN_SYSTEM.md` — Visual identity, design tokens and interaction rules.
4. `04_AURORA_RISK_REGISTER.md` — Risk register and mitigations.
5. `05_AURORA_G0_ACCEPTANCE.md` — G0 gate criteria, evidence requirements and decision record.
6. `00_G0_README_AND_DECISIONS.md` — This package index and binding decisions.

## 5. Explicit exclusions from this authorization

- No application source code.
- No project/solution scaffolding.
- No package installation.
- No Windows registry edits.
- No shell modification, injection, patching or process termination.
- No changes to Windows taskbar settings on the user's machine.
- No installer, updater or release artifact.
- No implementation commits.

## 6. G0 exit condition

G0 may be accepted only when product scope, taskbar semantics, non-functional targets, platform constraints, initial architecture, design system principles, risks and testable acceptance criteria are reviewed and signed off. Acceptance of G0 authorizes the next design/feasibility gate only; it does not authorize production implementation.
