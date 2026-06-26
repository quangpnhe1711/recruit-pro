# Business Rules — Application Domain

Format: each rule is enforceable, has explicit allowed/forbidden cases, names its backend and
frontend enforcement points, the error it produces, and its tests.

Every rule traces to [00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md). State-group
vocabulary used below:

```
ActiveApplicationStates      = { Applied, Screening, ManagerReview, Interview, Offer }
ClosedForWorkflow            = { Rejected, Withdrawn, OfferDeclined, Hired }
ReapplyEligibleClosedStates  = { Rejected, Withdrawn, OfferDeclined }   // Hired excluded
```

Active = any non-closed state. Closed = `ClosedForWorkflow`.

---

## BR-APPLICATION-001 — At most one active application per (candidate, job)

**Description:** A candidate must not hold more than one *active* application for the same job.

**Allowed:**
- No application for the job → apply allowed.
- Only *closed* application(s) for the job → apply allowed (see BR-APPLICATION-002).

**Forbidden:**
- An *active* application (`Applied`/`Screening`/`ManagerReview`/`Interview`/`Offer`) exists →
  duplicate apply forbidden.

**Backend enforcement:** `ApplicationService.ApplyAsync` → `HasActiveApplication(existingApplication)`.
The eligibility blocker "You have already applied for this job." is added only when an active
application exists.

> **Target vs. current (INV-003).** The rule is `AlreadyApplied = EXISTS active application for
> (candidate, job)` — set semantics. Current code resolves `existingApplication` via
> `GetExistingApplicationAsync` → `FirstOrDefault(a => a.JobId == jobId)`, an **arbitrary** row, then
> checks `IsClosed` on it. Under dirty/racy data (more than one row per job) this can read the wrong
> row. Converging the check to an explicit `EXISTS active` query, plus a DB-level uniqueness guarantee
> (INV-014), is tracked in [DECISION-LOG.md](DECISION-LOG.md) DL-008.

**Frontend enforcement:** Apply button disabled when `eligibility.canApply == false`;
`eligibility.alreadyApplied` is true only for an active application.

**Error:** HTTP **409**, message `Candidate already applied for this job.`

**Tests:** `ApplyAsync_WhenActiveApplicationExists_Returns409Conflict` (all active states),
`ApplyAsync_WhenCandidateAlreadyApplied_ReturnsConflict`, `TEST-E2E-APPLICATION-001`.

---

## BR-APPLICATION-002 — Re-apply is allowed after a re-apply-eligible closed application; `Hired` is terminal for the job

**Description:** A closed application from `ReapplyEligibleClosedStates` (`Withdrawn`, `Rejected`,
`OfferDeclined`) must never block a new application. `Hired` is closed-for-workflow but **not**
re-apply-eligible for the same `jobId` — the candidate was already hired for that posting. (Decisions
[DL-001], [DL-007]; INV-015.)

**Allowed:**
- Existing `Withdrawn` application → re-apply allowed.
- Existing `Rejected` application → re-apply allowed.
- Existing `OfferDeclined` application → re-apply allowed.

**Forbidden:**
- An *active* application still blocks (BR-APPLICATION-001).
- Existing `Hired` application for the **same** job → re-apply forbidden. A new hiring need is a new
  job posting.

**Backend enforcement (target):** Eligibility allows a new application only when no active application
exists **and** the most relevant closed state is in `ReapplyEligibleClosedStates`. A prior `Hired` for
the same job adds a blocker.

> **Current code gap (DL-007).** Eligibility today keys solely off `HasActiveApplication` (i.e. "no
> active application"), and `IsClosed` lumps `Hired` with the re-apply-eligible states. So the code
> would currently **permit** re-apply after `Hired`. There is no `ReapplyEligibleClosedStates`
> predicate yet. This rule is the binding target; closing the gap is tracked in DL-007.

**Frontend enforcement:** After a re-apply-eligible closed state the apply-context returns
`canApply: true`, `alreadyApplied: false`, re-enabling the Apply button. After `Hired` for the same
job, `canApply: false` with a "already hired for this job" blocker.

**Error:** Happy path → HTTP **201 Created**. Re-apply attempt after `Hired` for same job → **422**
(business blocker, not a duplicate-active 409).

**Tests:** `ApplyAsync_AfterWithdrawal_AllowsReapplyAndReturnsCreated`,
`ApplyAsync_AfterRejection_AllowsReapplyAndReturnsCreated`,
`GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied`, `TEST-E2E-APPLICATION-001`.
Planned: `ApplyAsync_AfterHired_ForSameJob_IsBlocked` (see [TEST-MATRIX.md](TEST-MATRIX.md) T-RE-003).

---

## BR-APPLICATION-003 — Withdrawal is candidate-initiated and recorded as `Withdrawn`

**Description:** A candidate may withdraw an application while it is in an in-progress state. The
result is the `Withdrawn` state — never `Rejected`. (Decision [DL-002].)

**Allowed (withdrawable):** `Applied`, `Screening`, `ManagerReview`, `Interview`.

**Forbidden (not withdrawable):** `Offer`, `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn`.

**Backend enforcement:** `ApplicationService.WithdrawApplicationAsync` →
`ApplicationStatusWorkflow.CanCandidateWithdraw`; sets `ApplicationStatus.Withdrawn`. Ownership is
enforced (`application.UserId == profile.UserId`), else 404.

**Frontend enforcement:** "withdraw" appears in `availableActions` only when withdrawable; withdrawn
applications render the neutral "Đã rút đơn" badge (not the red "Từ chối").

**Error:** Not withdrawable → HTTP **422** `This application can no longer be withdrawn.`
Not owned / not found → HTTP **404**.

**Tests:** `WithdrawApplicationAsync_WhenAllowed_SetsStatusToWithdrawnNotRejected`,
`WithdrawApplicationAsync_WhenAlreadyClosed_Returns422`, `CanCandidateWithdraw_MatchesPolicy`.

---

## BR-APPLICATION-004 — Apply is only possible on an open, non-expired job with a complete profile

**Description:** A candidate may apply only when the job is open and their profile is apply-ready.

**Forbidden (each is a 422 blocker, not a duplicate):**
- Job status ≠ `Approved` → "This job posting is not accepting new applications."
- `Deadline` in the past → "The application deadline for this job has passed."
- Missing name/email → "Your profile is missing required contact information."
- No current resume → "Please upload your latest resume before applying."

**Backend enforcement:** `ApplicationService.BuildApplyEligibility`. When `CanApply` is false and no
active application exists, the first blocker is returned with HTTP **422** (never the "already
applied" message).

**Error:** HTTP **422** with the specific blocker message.

**Tests:** `ApplyAsync_WhenBlockedByNonDuplicateReason_DoesNotClaimAlreadyApplied`.

---

## BR-APPLICATION-005 — A committed apply never fails on a side effect

**Description:** Notification dispatch and semantic-scoring enqueue run only after the application is
committed and are best-effort. (Decision [DL-004].)

**Backend enforcement:** `ApplicationService.ApplyAsync` isolates each side effect in try/catch with
logging; exceptions never propagate to the response.

**Error:** None — apply returns **201** even if a side effect fails. 500 is reserved for unexpected
infrastructure faults before/at commit.

**Tests:** `ApplyAsync_WhenNotificationPublishFails_StillReturnsCreated`,
`ApplyAsync_WhenSemanticEnqueueFails_StillReturnsCreated`, `TEST-E2E-APPLICATION-001`.

---

## BR-APPLICATION-006 — HR/Manager status transitions follow the workflow

**Description:** Application status changes by reviewers must follow the configured state machine.

**Backend enforcement:** `ApplicationService.UpdateApplicationDecisionAsync` →
`ApplicationStatusWorkflow.CanTransition`. Invalid transition → HTTP **400/422**
`Invalid transition from {current} to {target}.` Notification is emitted after commit.

**Tests:** `ApplicationStatusWorkflowTests.CanTransition_RespectsConfiguredWorkflow`. See
[STATE-MACHINE.md](STATE-MACHINE.md).

---

## BR-APPLICATION-007 — Derived workflow state must come from dependencies

**Description:** Application labels, actions, and blockers are derived from the application status
and its dependent workflow context. They must not be interpreted as standalone strings.

**Dependency inputs:**
- Application status (`Applied`, `Screening`, `ManagerReview`, `Interview`, `Offer`, closed states).
- Job state (`Approved`, deadline, whether the posting still accepts applications).
- Candidate readiness (contact info and current resume).
- Application history for the same candidate/job (active vs closed).
- Related interview and offer records when the application reaches those stages.
- Current actor (candidate, HR, manager) and ownership.

**Derived outputs:**
- `CanApply` and `AlreadyApplied`.
- Candidate visible actions (`withdraw`, `acceptOffer`, `declineOffer`).
- Reviewer allowed transitions.
- Candidate-facing labels and next-step copy.
- Notification recipients and event codes.

**Allowed:**
- A closed previous application allows a new `Applied` row only when the job/profile/resume
  dependencies are still satisfied.
- `acceptOffer` and `declineOffer` are visible only when both application status is `Offer` and the
  offer workflow allows candidate response.
- Interview UI/actions are tied to `Interview` stage and related interview data.

**Forbidden:**
- FE or BE logic that treats `"Rejected"`, `"Withdrawn"`, or `"Applied"` as enough context by itself.
- Showing offer actions for an application that is not in `Offer`.
- Blocking re-apply solely because any historical application row exists.
- Advancing to interview/offer before the required previous workflow checkpoint has been reached.

**Backend enforcement:** `ApplicationStatusWorkflow`, `ApplicationService.BuildApplyEligibility`,
`BuildCandidateAvailableActions`, offer response validation, and interview scheduling validation.

**Frontend enforcement:** Render badges/actions from API-provided status/action fields and apply
context; do not duplicate the workflow with ad hoc status-string checks.

**Tests:** Covered by workflow, apply eligibility, withdrawal/re-apply, and notification tests in
[TEST-MATRIX.md](TEST-MATRIX.md). Add targeted tests when a new dependent workflow output is added.

---

## BR-APPLICATION-008 — Interview validity is gated by `Application.status`

**Description:** An `Interview` record is only valid/actionable while the application is at (or has
reached) the `Interview` stage. The interview is a dependent record; it never drives the application
(INV-008).

**Allowed:** Schedule/complete/cancel an interview while `Application.status = Interview`.

**Forbidden:**
- Creating an actionable interview while the application is `Applied`/`Screening`/`ManagerReview`.
- Treating a `Scheduled` interview as still actionable after the application becomes
  `Withdrawn`/`Rejected`.

**Required downstream effect:** When the application leaves the pipeline
(`Withdrawn`/`Rejected`) a `Scheduled` interview must be `Canceled` or treated as stale.

> **Current code gap (DL-009):** the withdraw/reject paths do not yet proactively cancel a pending
> interview; the FE treats it as stale. Server-side cascade is the target.

**Error:** Interview action on an application not in `Interview` → HTTP **422**.

**Tests:** Planned — see [TEST-MATRIX.md](TEST-MATRIX.md) T-INT-001.

---

## BR-APPLICATION-009 — Offer validity is gated by `Application.status`; `Hired` only from candidate accept

**Description:** An `Offer` is only valid/actionable while `Application.status = Offer`. The candidate's
response to a `Sent` offer drives the application: accept → `Hired`, decline → `OfferDeclined`. HR/
Manager must **not** use the reviewer decision endpoint to move `Offer → Hired` (INV-009).

**Allowed:** `Offer` in state `Sent` while `Application = Offer`; candidate accept/decline.

**Forbidden (must never occur):**
- `Offer Sent` while application is `Screening` (or any non-`Offer` state).
- `Offer Accepted` while application is not `Hired`; application `Hired` with no `Accepted` offer.
- A reviewer transitioning `Offer → Hired` via `PATCH …/decision`.

**Backend enforcement:** `CanCandidateRespondToOffer` (status == `Offer`); accept/decline endpoints
require offer `Sent` and candidate ownership. Reviewer `AllowedTransitions` from `Offer` are only
`Hired`/`OfferDeclined` and are reserved for the candidate-driven accept/decline actions.

**Error:** Offer response when not in `Offer`/offer not `Sent` → HTTP **422**; not owned → **404**.

**Tests:** Accept/decline tests in [TEST-MATRIX.md](TEST-MATRIX.md); planned T-OFR-001 for the
forbidden combinations.

---

## BR-APPLICATION-010 — Notifications are post-commit side effects, never source of truth

**Description:** Notification state must never drive application state; `Application.status` drives
notifications (INV-010). A notification failure cannot roll back or fail a committed business action.

**Backend enforcement:** Notifications publish **after** the DB commit, each isolated in try/catch.
(Same mechanism as BR-APPLICATION-005 for apply.)

**Error:** None propagated — the business action's HTTP result is unchanged by notification failure.

**Tests:** `ApplyAsync_WhenNotificationPublishFails_StillReturnsCreated`,
`PublishNewApplicationReceivedAsync_FansOutToHrAndDeduplicatesRecipients`.

---

## BR-APPLICATION-011 — Dashboard/analytics derive from canonical state groups

**Description:** Reporting metrics must be computed from canonical state groups, never hard-coded per
status (INV-011). In particular `Withdrawn` is neither `Rejected` nor active.

**Canonical derivations:**
```
ActiveApplications  = Applied + Screening + ManagerReview + Interview + Offer
ClosedApplications  = Rejected + Withdrawn + OfferDeclined + Hired
PipelineApplications = ActiveApplications
SuccessfulApplications = Hired
CandidateWithdrawals = Withdrawn
CompanyRejections   = Rejected
```

**Backend enforcement:** Active/closed counts derive from `ApplicationStatusWorkflow.IsClosed`
(`ApplicationRepository`, `JobService`, `DashboardService`).

**Forbidden:** Counting `Withdrawn` as `Rejected`, or as active pipeline.

**Tests:** Active/closed split covered by workflow tests; planned analytics assertions T-DASH-001.

---

# Recruitment Ownership Rules (BR-OWN-*)

> **Status:** the **data foundation is implemented (Phase 1, 2026-06-26)** — `Department.HeadUserId`,
> `Job.RecruiterId`, `Application.AssignedRecruiterId`, `Application.AssignedDepartmentHeadId` exist in
> `init.sql` + EF, and apply snapshots the owners (BR-OWN-005). Still **planned**: job-approval routing
> by `Department.HeadUserId` (Phase 3), DTO/API field exposure (Phase 2), frontend (Phase 4), and
> **notification routing (Phase 6, not implemented)**. Backend status enums and roles are **not**
> renamed; `ManagerReview` (code) **=** the **DepartmentHeadReview** business stage. See
> [01-system-overview.md](01-system-overview.md),
> [RECRUITMENT-OWNERSHIP-MATRIX.md](RECRUITMENT-OWNERSHIP-MATRIX.md), and
> [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md).

Business roles: **Candidate**, **HR / Recruiter**, **DepartmentHead**, **SystemAdmin**.

## BR-OWN-001 — A Department must have a Head for routing
Each Department should have a DepartmentHead user (`Department.HeadUserId`). The head is the
default job approver and default business reviewer for that Department. If a Department has no head, job
approval and DepartmentHead review cannot be routed safely.
**Phase 1 (implemented):** `Department.HeadUserId` column + EF mapping exist; seed sets it to the
DepartmentHead persona. **Still planned:** approval *authorization* by `Department.HeadUserId` (Phase 3)
— today approval is still gated by the generic `Manager` role.

## BR-OWN-002 — HR creates the job; CreatedBy is an audit field
HR / Recruiter creates a job, capturing **Department** and **Recruiter phụ trách**
(`Job.RecruiterId`, the business owner of the job's applications). `Job.CreatedBy` is an **audit**
field and must **not** be treated as the long-term recruiter owner except as a legacy fallback.
**Phase 1 (implemented):** `Job.RecruiterId` column + EF mapping exist; seed/backfill sets
`recruiter_id = created_by` (the demo jobs are created by the HR persona). **Still planned:** the job
create/edit flow capturing/validating `RecruiterId` explicitly (Phases 2–4).

## BR-OWN-003 — DepartmentHead approves the job; ApprovedBy is an audit field
A job is not public/applyable until approved. The approver is the Department's head
(`Department.HeadUserId` _(planned)_). `Job.ApprovedBy` **(current)** is an **audit** field, not the
long-term head owner except as a legacy fallback.
**Current:** approval is authorized by the `Manager` role (`JobController` approval endpoints).

## BR-OWN-004 — Candidate applies only to approved jobs
A candidate can apply only when `Job.Status = Approved` (and the deadline has not passed). Only approved
jobs appear in the public listing. **Current:** enforced (INV-001 / BR-APPLICATION-004) — unchanged.

## BR-OWN-005 — Application snapshots its owners at apply time
On apply, the application snapshots the current recruiter and DepartmentHead into
`Application.AssignedRecruiterId` and `Application.AssignedDepartmentHeadId`. Resolution order in
[APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) §2. Re-apply creates a new row (INV-007)
and a fresh snapshot.
**Phase 1 (implemented):** `ApplicationService.ApplyAsync` sets
`AssignedRecruiterId = Job.RecruiterId ?? Job.CreatedBy` and
`AssignedDepartmentHeadId = Job.Department.HeadUserId ?? Job.ApprovedBy`. Covered by unit tests
(see [TEST-MATRIX.md](TEST-MATRIX.md) T-OWN-007).

## BR-OWN-006 — Candidate apply is owned by HR first, not DepartmentHead
On apply, the primary owner is `AssignedRecruiterId` (the HR / Recruiter). The DepartmentHead must
**not** receive every new application immediately — HR screens first to avoid spamming the head.
**Current:** `new_application_received` already routes to `Job.CreatedBy` + `HR` role (HR-first by role).

## BR-OWN-007 — DepartmentHead participates after HR screening
When HR moves the application to `ManagerReview` / DepartmentHeadReview, the primary owner becomes
`AssignedDepartmentHeadId`. **Current:** the `ManagerReview → Interview` transition is driven by the
generic `Manager` role, not a per-application assigned head.

## BR-OWN-008 — Interview and offer responsibilities
Interview: HR coordinates, DepartmentHead evaluates/participates, Candidate attends. Offer: HR sends,
Candidate accepts/declines, DepartmentHead is informed when the outcome affects the Department. Planned
notification routing in [NOTIFICATION-EVENT-MATRIX.md](NOTIFICATION-EVENT-MATRIX.md).

## BR-OWN-009 — SystemAdmin is not part of normal recruitment ownership
SystemAdmin manages configuration and access and may override/maintain data, but is **never** the
default owner or recipient of recruitment workflow items.

**Tests:** Planned — see [TEST-MATRIX.md](TEST-MATRIX.md) §Planned ownership tests (T-OWN-001…009).
