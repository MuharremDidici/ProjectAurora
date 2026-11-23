# PROJECT AURORA — G0 Acceptance Package

**Document ID:** AURORA-G0-ACC-001  
**Version:** 0.2.0  
**Date:** 2026-11-17  
**Gate:** G0 — Product Discovery & Initial Architecture  
**Status:** ACCEPTED / CLOSED  
**Implementation authorization:** NOT GRANTED

## 1. Gate objective

Confirm that Aurora has a sufficiently precise product definition, scope boundary, technical direction, visual identity, risk model and measurable acceptance framework to proceed to G1 feasibility planning.

## 2. Product Owner decision — recorded

**Decision:** ACCEPT

The Product Owner explicitly accepted this binding interpretation:

> Aurora will be the primary visible taskbar experience. It will not replace or patch Explorer. The native Windows taskbar will be hidden only through a user-controlled supported Windows configuration where reliable. If that cannot be validated on a supported Windows 11 build, Aurora will offer coexistence mode. No undocumented registry, injection, shell patching or Explorer termination will be used.

This decision resolves the G0 ambiguity around the word “replace.” It does not prove native-taskbar hiding or AppBar coexistence feasibility; those are G1 evidence requirements.

## 3. G0 acceptance reconciliation

| Area | Result | Reconciliation |
|---|---|---|
| Product vision and target users | PASS | Defined in PRD |
| Windows 11-only platform | PASS | Binding decision |
| Free distribution | PASS | No paid tier/account in v1 |
| Complete product scope | PASS | Modules and v1 exclusions defined |
| Taskbar replacement semantics | PASS | Product Owner accepted primary-visible-taskbar interpretation |
| Windows shell boundary | PASS | No Explorer replacement, patching, injection or termination |
| AppBar limitation | PASS | Documented as appbar coordination, not shell replacement |
| API capability/error model | PASS WITH G1 VALIDATION | Candidate adapters and failure states defined; runtime behavior remains to be proven |
| Standard-user privilege | PASS | Baseline architecture |
| Design system | PASS | Four themes, tokens, components and accessibility rules defined |
| Quality targets | PASS | Measurable target budgets defined; not yet measured |
| Risk register | PASS | Top risks have mitigations and gate assignments |
| Recovery/uninstall | PASS AS REQUIREMENT | Actual recovery behavior must be tested in G1/G8 |
| Implementation freeze | PASS | No implementation artifacts or machine changes authorized |

## 4. Residual risks transferred to G1

1. **R-001 — Native taskbar hide / replacement:** Critical feasibility blocker. Prove supported user-controlled hide + Aurora AppBar coexistence; otherwise use coexistence mode and revise user-facing claim.
2. **R-002 — AppBar conflicts:** Validate edge reservation, auto-hide, multiple monitors and Explorer restart.
3. **R-003 — Recovery:** Demonstrate user can recover native taskbar with Aurora stopped or unavailable.
4. **R-005 — Mixed DPI / monitor hot-plug:** Validate coordinate and layout behavior.
5. **R-006/R-007 — Radio and brightness controls:** Determine actual standard-user capability and fallback behavior.
6. **R-008 — Third-party notifications:** Keep universal notification-history capture out of scope unless a supported contract is proven.
7. **R-012 — Fullscreen behavior:** Validate overlay/AppBar behavior with fullscreen and borderless applications.

No residual risk above is silently accepted as solved by G0.

## 5. Gate decision

<badge color="success">G0 PASS — ACCEPTED</badge>

**Rationale:** Product scope, product promise, architecture boundaries, design system, non-functional targets and initial risk controls are documented. The Product Owner resolved the only explicit G0 decision hold: the meaning and safety boundary of taskbar “replace.”

**Condition carried forward:** Native-taskbar hide and AppBar behavior remain unproven and are mandatory G1 feasibility work. This condition does not prevent G0 closure because it is a technical feasibility question explicitly assigned to the next gate.

## 6. Authorization

| Activity | Status |
|---|---|
| G0 documentation | Accepted |
| G1 feasibility planning | Authorized |
| G1 isolated experiments | Not authorized yet |
| Product implementation | Not authorized |
| Repository/code changes | Not authorized |
| Windows configuration changes | Not authorized |
| Installer/release work | Not authorized |

## 7. Sign-off record

| Role | Decision | Record |
|---|---|---|
| Product Owner | ACCEPTED | Explicit approval in conversation, 2026-11-17 |
| Technical review | ACCEPTED WITH G1 BLOCKERS | Architecture boundary and feasibility risks recorded |
| UX / Design | BASELINE ACCEPTED FOR NEXT GATE | Design system documented; visual validation remains future work |
| QA / Reliability | G1 EVIDENCE REQUIRED | Test matrix and recovery evidence assigned to G1 |

**Final status:** `G0 PASS — CLOSED`  
**Next gate:** `G1 — Technical Feasibility & Architecture Validation`  
**Implementation status:** `FROZEN`
