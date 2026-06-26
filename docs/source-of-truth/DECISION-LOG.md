# Decision Log

Decisions taken while hardening the application/apply/withdraw domain. Each is intentional and
binding until superseded.

## DL-001 — Re-apply after a re-apply-eligible closed application is ALLOWED

> **Amended by [DL-007]:** the original wording ("any closed application allows re-apply, incl. Hired
> as a new posting cycle") is narrowed — `Hired` is **not** re-apply-eligible. The text below reflects
> the amended decision.

**Decision:** A candidate may submit a new application for a job whenever they do **not** currently
hold an *active* application for it **and** their prior closed state is re-apply-eligible
(`Withdrawn`, `Rejected`, `OfferDeclined`). `Hired` is closed but terminal for that `jobId`.

**Rationale:** Withdrawal is candidate-initiated and non-punitive; rejection/offer-decline end a
prior attempt but circumstances (and postings) change. Blocking re-apply on stale closed records was
the defect behind BUG-APPLICATION-001. `Hired` is excluded because the candidate was already hired for
that exact posting; a new need is a new posting.

**Consequences:** "Already applied" is defined strictly as "an active application exists". See
[BUSINESS-RULES.md](BUSINESS-RULES.md) BR-APPLICATION-001/002 and
[00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md) §3.

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

> **Status update:** DL-007, DL-008, and DL-009 below were **implemented** in the conformance pass
> (commit "fix: align application workflow with state dependency rules"). The "CODE GAP" framing
> reflects the audit baseline; the "Target implementation" in each is now the shipped behavior, covered
> by tests T-RE-003, T-DUP-003/004, T-INT-001/002, T-OFR-001/002/003.

## DL-007 — `Hired` is closed-for-workflow and analytics, but NOT re-apply-eligible (IMPLEMENTED)

**Decision:** Define three canonical groups (see
[00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md) §3): `ActiveApplicationStates`,
`ClosedForWorkflow`, and `ReapplyEligibleClosedStates = { Rejected, Withdrawn, OfferDeclined }`.
`Hired` is in `ClosedForWorkflow` (counts in analytics, blocks new active app) but **not** in
`ReapplyEligibleClosedStates` — re-apply to the same `jobId` after `Hired` is forbidden (INV-015).

**Current code state:** `ApplicationStatusWorkflow.IsClosed` returns the four closed states; there is
**no** `ReapplyEligibleClosedStates` predicate, and apply eligibility blocks only on "active
application exists". So today the code would **permit** re-apply after `Hired`. This is a documented
divergence, not yet fixed.

**Target implementation (when fixes resume):** add a `ReapplyEligibleClosedStates`/`IsReapplyEligible`
predicate; in `BuildApplyEligibility`, add a blocker when a prior `Hired` exists for the same job
(422). Cover with TEST-MATRIX T-RE-003.

## DL-008 — `AlreadyApplied` must be `EXISTS active`, not an arbitrary/most-recent row (CODE GAP)

**Decision:** Duplicate detection is set-based: `EXISTS application WHERE UserId=? AND JobId=? AND
status ∈ ActiveApplicationStates` (INV-003). It must not depend on row ordering.

**Current code state:** `ApplicationService.GetExistingApplicationAsync` returns
`existingApplications.FirstOrDefault(a => a.JobId == jobId)` — an arbitrary row — and
`HasActiveApplication` checks `IsClosed` on that single row. Under dirty/racy data (>1 row per job)
this can read the wrong row. Documented divergence; fix tracked here.

**Target implementation:** resolve the active application via an explicit `Any(... && !IsClosed)`
query (or repository method), independent of ordering; pair with DL-005 follow-up (DB-level uniqueness
on the active application, INV-014) as defense-in-depth. Cover with TEST-MATRIX T-DUP-003.

## DL-009 — Pipeline exit must cascade to pending Interview/Offer (CODE GAP)

**Decision:** When an application leaves the active pipeline (`Withdrawn`/`Rejected`), any pending
(`Scheduled`) interview must be `Canceled` or treated as stale, and no offer path may continue
(INV-008, BR-008/009).

**Current code state:** withdraw/reject set the `Application` status but do not proactively cancel a
pending interview; the FE is expected to treat it as stale. Server-side cascade is the target.

**Target implementation:** in withdraw/reject handlers, cancel `Scheduled` interviews for the
application and invalidate any non-final offer. Cover with TEST-MATRIX T-INT-001.

## DL-010 — Documented enum gaps (states referenced in earlier docs that do not exist)

**Decision:** Docs must describe only states that exist in `RecruitPro.Domain.Enums`. Earlier drafts
referenced non-existent states; those references were corrected to reality:
- `JobStatus` has **no** `Archived`/`Deleted` (actual: `Draft`, `PendingApproval`, `Approved`,
  `Closed`, `Rejected`).
- `OfferStatus` has **no** `Cancelled` (actual: `Draft`, `Sent`, `Accepted`, `Declined`).
- `InterviewStatus` cancel value is spelled `Canceled` (single `l`).

**Note:** If the product later needs job archival/soft-delete or offer cancellation, those become
explicit enum additions + migrations, not implied behavior.

## Remaining work (not done in this iteration)

- **DL-007** — implement `Hired` re-apply block (`ReapplyEligibleClosedStates` predicate + eligibility
  blocker) and T-RE-003.
- **DL-008** — replace `FirstOrDefault` duplicate detection with an `EXISTS active` query and T-DUP-003.
- **DL-009** — cascade pending Interview/Offer invalidation on `Withdrawn`/`Rejected` and T-INT-001.
- Extend source-of-truth depth (BRD/SRD/full traceability) to Jobs, Interviews, Offers, Copilot,
  Notifications domains.
- Consider a partial unique index on active applications as DB-level defense-in-depth for DL-005/INV-014.
- Address pre-existing `AutoMapper 12.0.0` advisory (NU1903) — requires a vetted major upgrade.
- Frontend has no automated test/typecheck gate (only eslint + vite build); pre-existing `tsc`
  strictness errors exist in unrelated components.
