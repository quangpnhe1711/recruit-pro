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

| Document | Purpose |
|---|---|
| [BUSINESS-RULES.md](BUSINESS-RULES.md) | Enforceable business rules (BR-*) with BE/FE enforcement points and tests. |
| [STATE-MACHINE.md](STATE-MACHINE.md) | Application and Job state machines, allowed/forbidden transitions. |
| [API-CONTRACT.md](API-CONTRACT.md) | Application-domain endpoints: request/response/error/consumers. |
| [ERROR-CONTRACT.md](ERROR-CONTRACT.md) | Stable HTTP status semantics and the response envelope. |
| [TEST-MATRIX.md](TEST-MATRIX.md) | Tests mapped to requirements and rules. |
| [DECISION-LOG.md](DECISION-LOG.md) | Decisions taken during hardening, with rationale, and remaining work. |

## Key invariants (read first)

- A candidate has **at most one _active_ application per job**. Active = not in a closed state.
- **Closed** application states are `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn`.
- **Withdrawal is its own state (`Withdrawn`)** — never `Rejected`. It is candidate-initiated and
  non-punitive.
- **Re-apply after a closed application is allowed.** A closed application must never block a new one.
- A **duplicate of an active application → HTTP 409**. Any other unmet apply precondition → **422**.
  **500 is reserved for genuinely unexpected infrastructure failures only.**
- A successfully committed write must **never** be reported as 500 because a downstream best-effort
  side effect (notification, async scoring) failed.
