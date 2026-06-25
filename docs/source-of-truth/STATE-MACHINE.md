# State Machines

## Application status

`ApplicationStatus` (RecruitPro.Domain.Enums) — persisted as a string via
`ApplicationStatusValueConverter`.

States: `Applied`, `Screening`, `ManagerReview`, `Interview`, `Offer`, `Hired`, `Rejected`,
`OfferDeclined`, `Withdrawn`.

Closed/terminal: `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn` (`ApplicationStatusWorkflow.IsClosed`).

### Dependency model

The state machine is a dependency chain, not a set of independent labels. A later state must only be
reachable when the earlier workflow checkpoint has produced the facts it needs.

| Status | Required previous facts | Dependent workflows |
|---|---|---|
| `Applied` | Valid job + valid candidate profile/resume + no active duplicate | Notification, semantic scoring, HR queue |
| `Screening` | A committed `Applied` application exists and HR accepted it into screening | Candidate status notification, HR review queue |
| `ManagerReview` | HR screening passed | Manager review queue, department/hiring-manager context |
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
| Applied | Screening, Rejected | HR | PATCH /api/hr/applications/{id}/decision |
| Screening | ManagerReview, Rejected | HR | PATCH /api/hr/applications/{id}/decision |
| ManagerReview | Interview, Rejected | Manager | PATCH /api/hr/applications/{id}/decision |
| Interview | Offer, Rejected | HR/Manager | PATCH /api/hr/applications/{id}/decision |
| Offer | Hired, OfferDeclined | Candidate (accept/decline) | POST /api/candidate/applications/{id}/accept-offer · /decline-offer |
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
| Hired, Rejected, OfferDeclined, Withdrawn (closed) | Re-apply (new row) | Applied | POST /api/jobs/{jobId}/apply | BR-APPLICATION-001/002/004 |

Side effects: status changes emit notifications **after** the DB commit; a notification failure does
not roll back or fail the request (BR-APPLICATION-005).

For the FE-facing labels, derived actions, cross-workflow dependencies, and notification behavior, see
[APPLY-STATUS-FLOW.md](APPLY-STATUS-FLOW.md).

## Job status

`JobStatus` — relevant to apply eligibility:

| State | Public / listable | Apply-able | Notes |
|---|---|---|---|
| Draft | No | No | Not yet submitted |
| PendingApproval | No | No | Awaiting manager approval |
| Approved | Yes | **Yes** (until deadline) | The only apply-able state (BR-APPLICATION-004) |
| Closed | Optional | No | No new applications |
| Archived/Deleted | No | No | Excluded from search/selection |

Only `Approved` jobs accept applications; `BuildApplyEligibility` blocks all others with HTTP 422.
