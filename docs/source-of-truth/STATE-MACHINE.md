# State Machines

> Trace to [00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md). Every transition here is
> gated by the dependency model: a state is reachable only when the earlier checkpoint produced the
> facts it needs, and `Application.status` gates Interview/Offer — never the reverse (INV-008).

## Canonical state groups

```
ActiveApplicationStates      = { Applied, Screening, ManagerReview, Interview, Offer }
ClosedForWorkflow            = { Rejected, Withdrawn, OfferDeclined, Hired }
ReapplyEligibleClosedStates  = { Rejected, Withdrawn, OfferDeclined }   // Hired excluded (INV-015)
```

## Application status

`ApplicationStatus` (RecruitPro.Domain.Enums) — persisted as a string via
`ApplicationStatusValueConverter`.

States: `Applied`, `Screening`, `ManagerReview`, `Interview`, `Offer`, `Hired`, `Rejected`,
`OfferDeclined`, `Withdrawn`.

> **Canonical vs presentation.** These enum values are the canonical contract across DB / API / FE logic
> — APIs return them verbatim in the `status` field (`entity.Status.ToString()`). Localized labels are
> presentation-only (`statusLabel`/`displayStatus`), never the `status` value. `ManagerReview` is the
> code value (= the **DepartmentHeadReview** business stage); presentation may render it as "Head Review",
> but the enum is **not** renamed. An unknown status presents as neutral `Unknown`, never `Rejected`.

Closed/terminal: `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn` (`ApplicationStatusWorkflow.IsClosed`).
Of these, only `Rejected`, `Withdrawn`, `OfferDeclined` are **re-apply-eligible**; `Hired` is terminal
for its `jobId` (INV-015).

### Dependency model

The state machine is a dependency chain, not a set of independent labels. A later state must only be
reachable when the earlier workflow checkpoint has produced the facts it needs.

| Status | Required previous facts | Dependent workflows |
|---|---|---|
| `Applied` | Valid job + valid candidate profile/resume + no active duplicate | Notification, semantic scoring, HR queue |
| `Screening` | A committed `Applied` application exists and HR accepted it into screening | Candidate status notification, HR review queue |
| `ManagerReview` | HR screening passed | Manager review queue, department-head review context |
| `Interview` | Manager review passed | Interview scheduling and interviewer assignment |
| `Offer` | Interview stage passed and HR/Manager is ready to send offer | Offer draft/send/response |
| `Hired` | Offer exists and candidate accepted it | Hiring completion/reporting |
| `Rejected` | Reviewer explicitly closed the application from an allowed active state | Candidate history, re-apply eligibility |
| `OfferDeclined` | Offer exists and candidate declined it | Candidate history, re-apply eligibility |
| `Withdrawn` | Candidate owns the active application and withdraws before offer stage | Candidate history, re-apply eligibility |

Derived flags such as `Active`, `Closed`, `AlreadyApplied`, `CanApply`, and visible UI actions must be
computed from the application status plus its related job/profile/resume/interview/offer context.
They must not be treated as independent workflow truth.

### Reviewer (HR/Manager) transitions — `ApplicationStatusWorkflow.AllowedTransitions`

| State | Allowed next | Actor | Endpoint |
|---|---|---|---|
| Applied | Screening | HR | PATCH /api/hr/applications/{id}/decision |
| Screening | ManagerReview | HR | PATCH /api/hr/applications/{id}/decision (records `DepartmentHeadReviewRequestedAt`) |
| ManagerReview | Interview | **Assigned DepartmentHead** or SystemAdmin (Manager fallback only when no head snapshotted) | PATCH /api/hr/applications/{id}/decision |
| Interview | Offer | HR (via the offer **email** flow; requires a **completed** interview) | POST /api/hr/applications/{id}/offer/send |
| Interview | Rejected | HR (via the rejection **email** flow; requires a **completed** interview) | POST /api/hr/applications/{id}/rejection-email |
| Applied / Screening / ManagerReview | Rejected | HR (ManagerReview: assigned head/SystemAdmin) via the rejection **email** flow | POST /api/hr/applications/{id}/rejection-email |
| Offer | Hired, OfferDeclined | Candidate (accept/decline) | POST /api/candidate/applications/{id}/accept-offer · /decline-offer |

> **Email-gated Offer/Reject (BR-WF-001/002).** `Offer` and `Rejected` can **no longer** be reached
> through the status-decision endpoint (the dropdown/buttons) — that endpoint returns 422
> `EMAIL_REQUIRED_FOR_OFFER` / `EMAIL_REQUIRED_FOR_REJECTION`. They are reached only through their email
> flows, which send the candidate email first and transition the status only on a successful send (a
> send failure leaves the status unchanged: 422 `EMAIL_SEND_FAILED`).
>
> **Interview gate (BR-WF-005).** From the `Interview` stage, Offer and Reject require a scheduled AND
> completed interview: no interview → 422 `INTERVIEW_REQUIRED`; scheduled-but-not-completed → 422
> `INTERVIEW_NOT_COMPLETED`. Interview completion is derived from `Interview.Status == Completed`; no new
> `ApplicationStatus.Interviewed` was added — the presentation ("Interview Pending Schedule" /
> "Interview Scheduled" / "Interviewed") is derived on the client from the latest interview state.
>
> **Head Review hand-off date (BR-WF-003).** Moving `Screening → ManagerReview` stamps
> `Application.DepartmentHeadReviewRequestedAt`. The Manager/DepartmentHead review queue and detail expose
> this as the "received for review" work date (FE falls back to `AppliedAt` only for legacy rows).
| Hired | — (terminal) | — | — |
| Rejected | — (terminal) | — | — |
| OfferDeclined | — (terminal) | — | — |
| Withdrawn | — (terminal) | — | — |

Any transition not in this table is rejected with a stable error (BR-APPLICATION-006). The reviewer
workflow does **not** include `Withdrawn` as a target — withdrawal is a separate candidate action.

### Candidate actions (orthogonal to the reviewer workflow)

| From state | Action | To state | Endpoint | Validation |
|---|---|---|---|---|
| Applied, Screening, ManagerReview, Interview | Withdraw | Withdrawn | POST /api/candidate/applications/{id}/withdraw | `CanCandidateWithdraw`; ownership |
| Offer | Accept offer | Hired | POST …/accept-offer | offer must be `Sent` |
| Offer | Decline offer | OfferDeclined | POST …/decline-offer | offer must be `Sent` |
| Rejected, OfferDeclined, Withdrawn (re-apply-eligible closed) | Re-apply (new row) | Applied | POST /api/jobs/{jobId}/apply | BR-APPLICATION-001/002/004 |
| Hired (closed, terminal for jobId) | Re-apply **forbidden** for same job | — | — | BR-APPLICATION-002, INV-015 |

Re-apply always inserts a **new** `Application` row (INV-007); it never reactivates a closed row.

Side effects: status changes emit notifications **after** the DB commit; a notification failure does
not roll back or fail the request (BR-APPLICATION-005).

For the FE-facing labels, derived actions, cross-workflow dependencies, and notification behavior, see
[APPLY-STATUS-FLOW.md](APPLY-STATUS-FLOW.md).

## Job status

`JobStatus` (RecruitPro.Domain.Enums) — actual enum values: `Draft`, `PendingApproval`, `Approved`,
`Closed`, `Rejected`. (There is **no** `Archived`/`Deleted` value; an earlier draft of this doc named
them — they do not exist. Enum gaps are tracked in [DECISION-LOG.md](DECISION-LOG.md).)

| State | Public / listable | Apply-able | Notes |
|---|---|---|---|
| Draft | No | No | HR drafting; not yet submitted |
| PendingApproval | No | No | Awaiting manager approval |
| Approved | Yes | **Yes** (until deadline) | The only apply-able state (BR-APPLICATION-004) |
| Closed | History only | No | No new applications |
| Rejected | No | No | Posting rejected during approval; excluded from selection |

Only `Approved` jobs accept applications; `BuildApplyEligibility` blocks all others with HTTP 422.

## Interview status

`InterviewStatus` — values: `Scheduled`, `Completed`, `Canceled` (note the single-`l` spelling).

An `Interview` record is **only valid/actionable while `Application.status = Interview`** (INV-008). It
is a dependent record, not an independent workflow.

| State | Valid when | Transitions | Notes |
|---|---|---|---|
| Scheduled | Application is `Interview` | → Completed, → Canceled | Active interview |
| Completed | Application reached `Interview` | terminal | Feeds the `Interview → Offer/Rejected` decision |
| Canceled | any | terminal | Set when application is withdrawn/rejected, or interview is dropped |

When the application leaves the pipeline (`Withdrawn`/`Rejected`) a `Scheduled` interview must be
`Canceled` or treated as stale (see 00-DOMAIN-STATE-DEPENDENCY §7; cascade enforcement tracked in
DECISION-LOG DL-009).

## Offer status

`OfferStatus` — values: `Draft`, `Sent`, `Accepted`, `Declined`. (There is **no** `Cancelled` value.)

An `Offer` is **only valid/actionable while `Application.status = Offer`** (INV-008). The offer never
drives the application; the candidate's response on a `Sent` offer drives it (INV-009).

| Offer state | Required Application state | Drives |
|---|---|---|
| Draft | Application is `Interview` or `Offer` | — |
| Sent | Application = `Offer` | enables candidate accept/decline |
| Accepted | Application → `Hired` (via candidate accept only) | hiring completion |
| Declined | Application → `OfferDeclined` (via candidate decline) | candidate history |

Forbidden combinations (must never occur): `Offer Sent` while Application is `Screening`; `Offer
Accepted` while Application is not `Hired`; Application `Hired` with no `Accepted` offer.

## Recruitment ownership per state

> **Status:** Phase 3 (implemented) — the ownership **fields exist** (`Application.AssignedRecruiterId` /
> `AssignedDepartmentHeadId`, snapshotted on apply) and `ManagerReview → Interview/Rejected` is now
> **authorized** against the assigned DepartmentHead/SystemAdmin (see "Current (Phase 3, implemented)"
> below). **No status enum is renamed** — only the actor authorization tightened. This section gives the
> ownership reading of each existing state.
> **`ManagerReview` = the DepartmentHeadReview business stage.** Business roles: Candidate,
> HR / Recruiter, DepartmentHead, SystemAdmin. See
> [RECRUITMENT-OWNERSHIP-MATRIX.md](RECRUITMENT-OWNERSHIP-MATRIX.md).

| Status (code) | Business stage | Owner | Notes |
|---|---|---|---|
| `Applied` | Applied | **HR / Recruiter** | HR-first (BR-OWN-006); DepartmentHead not notified yet. |
| `Screening` | Screening | **HR / Recruiter** | HR evaluates fit. |
| `ManagerReview` | **DepartmentHeadReview** | **DepartmentHead** | Entered only after HR passes screening (BR-OWN-007). |
| `Interview` | Interview | **HR / Recruiter** (coordinates) + **DepartmentHead / Interviewer** (evaluates) | HR coordinates scheduling. |
| `Offer` | Offer | **Candidate** (response) + **HR / Recruiter** (coordination) | Candidate accepts/declines a `Sent` offer. |
| `Hired` | Hired | **HR / Recruiter** (close-out) + **DepartmentHead** (informed) | Terminal for the `jobId` (INV-015). |
| `Rejected` | Rejected | closed (company decision) | — |
| `Withdrawn` | Withdrawn | closed (candidate decision) | Pending interview becomes stale/cancelled. |
| `OfferDeclined` | OfferDeclined | closed (candidate decision) | HR follow-up. |

Job statuses keep their meaning; ownership note: `Draft`/`PendingApproval` are owned by HR (drafting)
and the DepartmentHead (approval queue) respectively; `Approved`/`Rejected` are the DepartmentHead's
decision (audit in `Job.ApprovedBy`). See [JOB-APPROVAL-FLOW.md](JOB-APPROVAL-FLOW.md).

**Current (Phase 3, implemented):** `ManagerReview → Interview/Rejected` is authorized against the
application's `AssignedDepartmentHeadId` or a SystemAdmin (Manager-role fallback only when no head was
snapshotted) — `UpdateApplicationDecisionAsync` returns 403 otherwise. The earlier HR stages and the
`Interview → Offer/Rejected` stage keep their existing HR/Manager behavior. **No status enum or
transition changes** — only the actor authorization tightened.
