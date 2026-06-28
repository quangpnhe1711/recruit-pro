# Source of Truth — RecruitPro

This folder is the authoritative specification for RecruitPro's recruitment workflows.
When code and these documents disagree, one of them is a bug — resolve it intentionally,
never leave the contradiction.

## Scope of this iteration

This source-of-truth set was created together with a production-hardening pass that fixed two
reported defects in the **candidate application** domain:

1. **BUG-APPLICATION-001** — withdraw then re-apply was rejected with *"Candidate already applied
   for this job."*
2. **BUG-APPLICATION-002** — a normal, valid apply could intermittently return **HTTP 500**.

Accordingly these documents are **complete and verified for the Application / Apply / Withdraw /
Re-apply domain** and the cross-cutting error contract. Other domains (Jobs lifecycle, Interviews,
Offers, Copilot, Notifications) are described where they intersect the application flow; rounding
them out to the same depth is tracked in [DECISION-LOG.md](DECISION-LOG.md) as remaining work.

## Documents

Read **[00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md) first** — it defines the
state-dependency model that every rule, state machine, contract, and test traces back to.

| Document | Purpose |
|---|---|
| [00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md) | **Read first.** Source of truth per decision, state classification, canonical groups, dependency map, invariants. |
| [BUSINESS-RULES.md](BUSINESS-RULES.md) | Enforceable business rules (BR-*) with BE/FE enforcement points and tests. |
| [STATE-MACHINE.md](STATE-MACHINE.md) | Application and Job state machines, allowed/forbidden transitions. |
| [APPLY-STATUS-FLOW.md](APPLY-STATUS-FLOW.md) | Candidate apply-flow statuses, UI labels, actors, and notification behavior. |
| [API-CONTRACT.md](API-CONTRACT.md) | Application-domain endpoints: request/response/error/consumers. |
| [ERROR-CONTRACT.md](ERROR-CONTRACT.md) | Stable HTTP status semantics and the response envelope. |
| [TEST-MATRIX.md](TEST-MATRIX.md) | Tests mapped to requirements and rules. |
| [DECISION-LOG.md](DECISION-LOG.md) | Decisions taken during hardening, with rationale, and remaining work. |

### Recruitment ownership (DepartmentHead) — Phase 0 design + Phase 1–3 backend implemented

| Document | Purpose |
|---|---|
| [01-system-overview.md](01-system-overview.md) | Roles (Candidate / HR-Recruiter / DepartmentHead / SystemAdmin) and demo personas. |
| [RECRUITMENT-OWNERSHIP-MATRIX.md](RECRUITMENT-OWNERSHIP-MATRIX.md) | Who owns each workflow state/event + future notification recipient. |
| [JOB-APPROVAL-FLOW.md](JOB-APPROVAL-FLOW.md) | HR creates → DepartmentHead approves → public/applyable. |
| [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) | Apply snapshots owners; HR-first, DepartmentHead-after-screening. |
| [NOTIFICATION-EVENT-MATRIX.md](NOTIFICATION-EVENT-MATRIX.md) | Planned notification events/routing (**not implemented**). |
| [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md) | Phased coding plan; **Phases 1–4 (DB/EF/snapshot, DTO/API exposure, approval + ManagerReview guards, frontend ownership/approval + canonical English status) implemented**; notifications (Phase 6) planned. |

> `ManagerReview` (code enum) **=** the **DepartmentHeadReview** business stage — the enum is **not**
> renamed (nor are the `Manager`/`HeadDepartment` roles). Job approval and ManagerReview decisions are
> now scoped to the DepartmentHead/SystemAdmin (BR-OWN-003/007). **Frontend ownership work (Phase 4) is
> implemented** (approval UI + canonical English status display + Playwright E2E); **notification routing
> (Phase 6) remains not implemented**.

## Key invariants (read first)

- A candidate has **at most one _active_ application per job** — enforced as `EXISTS active`, not
  "latest row is active" (INV-003). Active = not in a closed state.
- **Closed** application states are `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn`. Of these, only
  `Rejected`, `Withdrawn`, `OfferDeclined` are **re-apply-eligible**; **`Hired` is terminal for its
  `jobId`** (closed + counted in analytics, but not re-apply-eligible — INV-015).
- **Withdrawal is its own state (`Withdrawn`)** — never `Rejected`. It is candidate-initiated and
  non-punitive.
- **Re-apply after a re-apply-eligible closed application is allowed** and creates a **new** row; such
  a closed application must never block a new one. Re-apply after `Hired` for the same job is blocked.
- A **duplicate of an active application → HTTP 409**. Any other unmet apply precondition → **422**.
  **500 is reserved for genuinely unexpected infrastructure failures only.**
- A successfully committed write must **never** be reported as 500 because a downstream best-effort
  side effect (notification, async scoring) failed.
