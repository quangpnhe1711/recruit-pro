# Decision Log

Decisions taken while hardening the application/apply/withdraw domain. Each is intentional and
binding until superseded.

## DL-001 — Re-apply after a closed application is ALLOWED

**Decision:** A candidate may submit a new application for a job whenever they do **not** currently
hold an *active* application for it. Withdrawn, Rejected, and OfferDeclined are closed and never block
a new application.

**Rationale:** Withdrawal is candidate-initiated and non-punitive; rejection/offer-decline end a
prior attempt but circumstances (and postings) change. Blocking re-apply on stale closed records was
the defect behind BUG-APPLICATION-001.

**Consequences:** "Already applied" is defined strictly as "an active application exists". See
[BUSINESS-RULES.md](BUSINESS-RULES.md) BR-APPLICATION-001/002.

## DL-002 — Withdrawal is its own state (`Withdrawn`), not `Rejected`

**Decision:** Introduced `ApplicationStatus.Withdrawn`. `WithdrawApplicationAsync` sets `Withdrawn`.

**Rationale:** Reusing `Rejected` mislabelled the candidate's own action as a recruiter rejection
("Không phù hợp") and made withdrawn and rejected applications indistinguishable in analytics and UI.

**Implementation notes:**
- Status is persisted as a string (`varchar(50)`) via `ApplicationStatusValueConverter`, so the new
  value needs **no database migration**.
- `IsClosed` includes `Withdrawn`; `Withdrawn` is a terminal state (no outgoing transitions).
- All active-pipeline analytics queries that hard-code the closed set were updated to exclude
  `Withdrawn` (ApplicationRepository, JobService, DashboardService).

## DL-003 — Error status codes: 409 for active duplicate, 422 for other blockers, 500 only for infra

**Decision:**
- Duplicate of an **active** application → **409 Conflict**.
- Any other unmet apply/withdraw business precondition (job not open, deadline passed, missing
  resume/profile, non-withdrawable state) → **422 Unprocessable Entity**.
- **500 is reserved for genuinely unexpected infrastructure failures.**

**Rationale:** A known business outcome must be a stable, client-readable 4xx, never a 500. Previously
all of these returned 400, and the "already applied" message was emitted even when the real blocker
was different.

**Implementation notes:** Added `ApiResponse.Conflict` (409) and `ApiResponse.UnprocessableEntity`
(422); `ExceptionMiddleware` now maps 403/409/422 explicitly instead of collapsing them to 500.

## DL-004 — Side effects after commit must never fail the request

**Decision:** Once an apply is committed, notification dispatch and semantic-scoring enqueue are
best-effort: each is isolated in try/catch and logged. Their failure cannot change the HTTP result.

**Rationale:** Root cause of BUG-APPLICATION-002 — a post-commit side-effect exception surfaced as a
500 even though the application was already saved, causing duplicate/confusing retries.

## DL-005 — Re-apply creates a new application row (history preserved)

**Decision:** Re-applying inserts a new `Application` row; the prior closed row is retained as history.
The candidate's *current* relationship with a job is their most recent application.

**Rationale:** Preserves an auditable trail (withdrawn → re-applied) and matches the existing
architecture (fresh scoring per application). There is no DB unique constraint on
`(user_id, job_id)`; uniqueness of the *active* application is enforced in the service layer.

**Alternative considered:** Reactivating the withdrawn row in place — rejected because it destroys
history and complicates scoring/audit.

## DL-006 — Stale auth/authz message assertions aligned to the localized contract

**Decision:** Four pre-existing integration tests asserted English error strings ("Unauthorized",
"Forbidden", "Invalid email or password.", "User does not have permission…") while the application
returns Vietnamese messages (localized in an earlier commit). The test assertions were updated to the
actual localized messages.

**Rationale:** The localized messages are the intended product behavior; the tests were never updated.
Verified by running the untouched `HEAD` — the same 4 failures pre-existed this work.

## Remaining work (not done in this iteration)

- Extend source-of-truth depth (BRD/SRD/full traceability) to Jobs, Interviews, Offers, Copilot,
  Notifications domains.
- Consider a partial unique index on active applications as DB-level defense-in-depth for DL-005.
- Address pre-existing `AutoMapper 12.0.0` advisory (NU1903) — requires a vetted major upgrade.
- Frontend has no automated test/typecheck gate (only eslint + vite build); pre-existing `tsc`
  strictness errors exist in unrelated components.
