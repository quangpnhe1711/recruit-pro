# Business Rules — Application Domain

Format: each rule is enforceable, has explicit allowed/forbidden cases, names its backend and
frontend enforcement points, the error it produces, and its tests.

Closed states = `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn`. Active = any non-closed state.

---

## BR-APPLICATION-001 — At most one active application per (candidate, job)

**Description:** A candidate must not hold more than one *active* application for the same job.

**Allowed:**
- No application for the job → apply allowed.
- Only *closed* application(s) for the job → apply allowed (see BR-APPLICATION-002).

**Forbidden:**
- An *active* application (`Applied`/`Screening`/`ManagerReview`/`Interview`/`Offer`) exists →
  duplicate apply forbidden.

**Backend enforcement:** `ApplicationService.ApplyAsync` → `HasActiveApplication(existingApplication)`
(`existingApplication` = most recent application for the job). The eligibility blocker
"You have already applied for this job." is added only when an active application exists.

**Frontend enforcement:** Apply button disabled when `eligibility.canApply == false`;
`eligibility.alreadyApplied` is true only for an active application.

**Error:** HTTP **409**, message `Candidate already applied for this job.`

**Tests:** `ApplyAsync_WhenActiveApplicationExists_Returns409Conflict` (all active states),
`ApplyAsync_WhenCandidateAlreadyApplied_ReturnsConflict`, `TEST-E2E-APPLICATION-001`.

---

## BR-APPLICATION-002 — Re-apply after a closed application is allowed

**Description:** A closed application (withdrawn, rejected, offer-declined) must never block a new
application. (Decision [DL-001].)

**Allowed:**
- Existing `Withdrawn` application → re-apply allowed.
- Existing `Rejected` application → re-apply allowed.
- Existing `OfferDeclined`/`Hired` application → re-apply allowed (a new posting cycle).

**Forbidden:** Nothing additional beyond BR-APPLICATION-001 (an active application still blocks).

**Backend enforcement:** Eligibility keys off `HasActiveApplication` (uses `IsClosed`), not "any
application exists". Re-apply inserts a new row; the closed row is retained as history (DL-005).

**Frontend enforcement:** After withdrawal the apply-context returns `canApply: true`,
`alreadyApplied: false`, re-enabling the Apply button.

**Error:** None on the happy path → HTTP **201 Created**.

**Tests:** `ApplyAsync_AfterWithdrawal_AllowsReapplyAndReturnsCreated`,
`ApplyAsync_AfterRejection_AllowsReapplyAndReturnsCreated`,
`GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied`, `TEST-E2E-APPLICATION-001`.

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
