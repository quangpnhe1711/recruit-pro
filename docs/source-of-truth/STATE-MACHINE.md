# State Machines

## Application status

`ApplicationStatus` (RecruitPro.Domain.Enums) — persisted as a string via
`ApplicationStatusValueConverter`.

States: `Applied`, `Screening`, `ManagerReview`, `Interview`, `Offer`, `Hired`, `Rejected`,
`OfferDeclined`, `Withdrawn`.

Closed/terminal: `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn` (`ApplicationStatusWorkflow.IsClosed`).

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
