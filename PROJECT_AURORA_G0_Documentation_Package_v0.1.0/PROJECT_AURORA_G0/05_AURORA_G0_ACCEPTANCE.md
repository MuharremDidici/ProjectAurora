# PROJECT AURORA — G0 Acceptance Package

**Document ID:** AURORA-G0-ACC-001  
**Version:** 0.1.0-draft  
**Date:** 2026-11-17  
**Gate:** G0 — Product Discovery & Initial Architecture  
**Status:** SUBMITTED FOR REVIEW — NOT ACCEPTED  
**Implementation authorization:** NOT GRANTED

## 1. Gate objective

Confirm that Aurora has a sufficiently precise product definition, scope boundary, technical direction, visual identity, risk model and measurable acceptance framework to proceed to G1 feasibility and architecture validation.

G0 does not authorize implementation.

## 2. Submitted artifacts

| Artifact | Purpose | Status |
|---|---|---|
| `00_G0_README_AND_DECISIONS.md` | Baseline decisions and scope boundary | Draft |
| `01_AURORA_PRD.md` | Product requirements and release scope | Draft |
| `02_AURORA_TDD.md` | Technical architecture and API feasibility | Draft |
| `03_AURORA_DESIGN_SYSTEM.md` | Theme system and UX standards | Draft |
| `04_AURORA_RISK_REGISTER.md` | Risks, mitigations and gate ownership | Draft |
| `05_AURORA_G0_ACCEPTANCE.md` | This gate acceptance package | Draft |

## 3. G0 acceptance criteria

### A. Product definition
- [ ] Product vision and value proposition are explicit.
- [ ] Target platform is Windows 11 only.
- [ ] Free distribution model is explicit.
- [ ] Complete scope is enumerated.
- [ ] v1 exclusions are explicit.
- [ ] Taskbar “replace” semantics are unambiguous.

### B. Technical boundaries
- [ ] Product does not claim to replace `explorer.exe` or the Windows shell.
- [ ] AppBar and native taskbar limitations are documented.
- [ ] Unsupported shell modification is prohibited.
- [ ] Native taskbar hide/recovery feasibility is assigned to G1.
- [ ] API capability/failure behavior is defined for each system control.
- [ ] Standard-user privilege is the default.

### C. Design system
- [ ] Four theme families are defined.
- [ ] Semantic token model is defined.
- [ ] Component states and interaction rules are defined.
- [ ] Accessibility and reduced-motion requirements are included.
- [ ] Multi-monitor and mixed-DPI design rules are included.

### D. Quality and risk
- [ ] Non-functional targets are measurable.
- [ ] Top risks have mitigation plans.
- [ ] Recovery and uninstall are release-critical.
- [ ] Test strategy covers API, UI, system and performance behavior.

### E. Governance
- [ ] All documents have version and status.
- [ ] No implementation is performed under G0.
- [ ] G1 entry criteria and deliverables are defined.
- [ ] G0 approval is recorded by an authorized reviewer.

## 4. G1 entry package required after G0 approval

G1 must produce:
1. Windows 11 build support matrix.
2. AppBar proof-of-feasibility report.
3. Native taskbar user-hide/recovery feasibility report.
4. Multi-monitor and mixed-DPI prototype findings.
5. Audio endpoint/session proof.
6. Wi-Fi/Bluetooth capability report.
7. Brightness capability report across internal/external displays.
8. Media session proof.
9. Notification scope/API decision.
10. Updated TDD and ADRs.
11. Revised risk register with measured evidence.

No polished production UI is required during G1. Small isolated technical spikes are permitted only after G0 is approved and G1 is explicitly authorized.

## 5. G0 non-goals and freeze

During this submission:
- No source code or solution files are created.
- No repository is initialized or modified.
- No Windows configuration is changed.
- No shell process is touched.
- No package is installed.
- No commit is created.
- No implementation work is authorized.

## 6. Proposed gate decision

**Current recommendation:** `CONDITIONAL — RETURN FOR G0 CLARIFICATION / REVIEW`

Reason:
The overall product direction is coherent, but the user's “replace” decision contains a platform-level dependency that cannot be represented as a guaranteed native shell replacement. The baseline has narrowed this to a primary visible Aurora taskbar with user-managed native-taskbar hiding and a coexistence fallback. The Product Owner must explicitly accept this interpretation before G0 can pass.

## 7. Required Product Owner decision

Approve this exact interpretation:

> “Aurora will be the primary visible taskbar experience. It will not replace or patch Explorer. The native Windows taskbar will be hidden only through a user-controlled supported Windows configuration where reliable. If that cannot be validated on a supported Windows 11 build, Aurora will offer coexistence mode. No undocumented registry, injection, shell patching or Explorer termination will be used.”

- [ ] ACCEPT — proceed to G1 feasibility after formal G0 closure.
- [ ] REJECT — revise the taskbar product requirement before G0 closure.

## 8. Gate sign-off record

| Role | Name | Decision | Date |
|---|---|---|---|
| Product Owner | Pending | Pending | Pending |
| Technical Lead | Pending | Pending | Pending |
| UX Lead | Pending | Pending | Pending |
| QA / Reliability Lead | Pending | Pending | Pending |

**Final status:** `G0 NOT APPROVED`  
**Implementation status:** `FROZEN`  
**Next permitted activity:** G0 review and decision clarification only.
