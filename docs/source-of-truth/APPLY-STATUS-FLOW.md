# Apply Status Flow

This document is the practical status guide for the **candidate apply flow**. It complements
[STATE-MACHINE.md](STATE-MACHINE.md) with the UI labels, ownership, and notification behavior that
the FE/BE should present consistently.

## 1. Entry conditions

A candidate can submit `POST /api/jobs/{jobId}/apply` only when all checks below pass:

- Job status = `Approved`
- Job deadline has not passed
- Candidate profile has required contact info
- Candidate has a current resume
- Candidate does **not** already have an active application for the same job

If the candidate has a **closed** application (`Rejected`, `Withdrawn`, `OfferDeclined`, `Hired`),
re-apply creates a **new** application row with status `Applied`.

## 2. Canonical backend statuses

| Persisted status | Meaning | Closed |
|---|---|---|
| `Applied` | Candidate just submitted | No |
| `Screening` | HR is screening | No |
| `ManagerReview` | Hiring manager is reviewing | No |
| `Interview` | Candidate is in interview stage | No |
| `Offer` | Offer sent, waiting for candidate response | No |
| `Hired` | Candidate accepted offer / hired | Yes |
| `Rejected` | Process closed by company | Yes |
| `OfferDeclined` | Candidate declined offer | Yes |
| `Withdrawn` | Candidate withdrew application | Yes |

## 3. Workflow dependencies

Statuses in this flow are not standalone labels. Each status is a checkpoint that depends on earlier
domain facts and unlocks or blocks later workflow actions.

### Dependency chain

| Workflow checkpoint | Depends on | Unlocks | Blocks |
|---|---|---|---|
| Job can receive applications | Job is `Approved`; deadline has not passed | Candidate apply context and submit | All application creation |
| Candidate can apply | Job is apply-able; candidate profile has contact info; resume exists; no active application for same job | New `Application` row in `Applied`; HR notification; semantic scoring enqueue | Duplicate active application; incomplete profile/CV |
| `Applied` | Application row committed from a valid apply | HR can move to `Screening` or `Rejected`; candidate can withdraw | Manager review, interview, offer |
| `Screening` | HR has accepted the application into recruiter screening | HR can move to `ManagerReview` or `Rejected`; candidate can withdraw | Interview and offer until manager review happens |
| `ManagerReview` | HR screening passed and a manager needs to assess fit | Manager can move to `Interview` or `Rejected`; candidate can withdraw | Offer until interview stage is reached |
| `Interview` | Manager review passed and interview scheduling can happen | HR/Manager can move to `Offer` or `Rejected`; candidate can withdraw | Offer response actions until an offer exists |
| `Offer` | Interview stage passed and a sent offer exists | Candidate can accept (`Hired`) or decline (`OfferDeclined`) | Withdrawal; re-apply while offer remains active |
| Closed states | A terminal business decision or candidate action happened | Historical display; re-apply if job is still apply-able | Any mutation of the same application row |

### Cross-workflow dependencies

| Application status | Job workflow dependency | Interview dependency | Offer dependency | Notification dependency |
|---|---|---|---|---|
| `Applied` | Source job must have accepted applications when the row was created | None | None | `new_application_received` to `job.CreatedBy` + role `HR` |
| `Screening` | Job should still exist for review context | None | None | `application_status_changed` to candidate |
| `ManagerReview` | Job department/manager context determines reviewer queue | None | None | `application_status_changed` to candidate |
| `Interview` | Job context determines role, department, and interviewer options | Interview record may be created/scheduled for the application | None | `interview_scheduled` when an interview is scheduled |
| `Offer` | Job compensation/context feeds offer draft | Usually follows completed/sufficient interview review | Offer record should be `Sent` before candidate response | `application_status_changed` to candidate |
| `Hired` | Job vacancy/capacity may be affected by hiring outcome | Interview process is considered complete | Offer must be accepted | `application_status_changed` to candidate |
| `Rejected` | Job remains available to other candidates unless closed separately | Pending interview work should not continue | No active offer should continue | `application_status_changed` to candidate |
| `OfferDeclined` | Job may stay open for other candidates | Interview process is complete | Offer must be declined | `application_status_changed` to candidate |
| `Withdrawn` | Job may still allow a future new application | Pending interview work should be treated as stale/cancelled by UI/process | No offer response path remains | Candidate action may be shown in history |

### Derived-state rules

- `Active` is derived from application status, not stored separately: any non-closed state is active.
- `AlreadyApplied` is true only when an active application exists for the same candidate and job.
- `CanApply` depends on both job eligibility and application history; a closed old application alone is not a blocker.
- Candidate actions depend on application status and ownership; reviewer actions depend on role and allowed transition.
- Notification and scoring are downstream side effects; they depend on a committed application but must not decide whether the apply succeeded.
- FE badges, CTAs, empty states, and blockers must be derived from these dependencies, not from isolated status strings.

## 4. Candidate-facing labels

These are the recommended labels returned to candidate list/detail screens:

| Persisted status | Candidate label | Candidate next-step copy |
|---|---|---|
| `Applied` | `Mới nộp` | Hồ sơ đã được ghi nhận và đang chờ HR tiếp nhận. |
| `Screening` | `HR đang sàng lọc` | HR đang kiểm tra mức độ phù hợp của hồ sơ với vị trí. |
| `ManagerReview` | `Quản lý đang đánh giá` | Hồ sơ đang được quản lý chuyên môn đánh giá thêm. |
| `Interview` | `Phỏng vấn` | Bạn đã vào vòng phỏng vấn. Hãy theo dõi thông báo để cập nhật lịch. |
| `Offer` | `Chờ phản hồi offer` | Bạn đã nhận offer. Vui lòng phản hồi trong thời gian quy định. |
| `Hired` | `Đã tuyển dụng` | Chúc mừng! Bạn đã hoàn tất quy trình ứng tuyển. |
| `Rejected` | `Không phù hợp` | Quy trình ứng tuyển cho vị trí này đã kết thúc. |
| `OfferDeclined` | `Đã từ chối offer` | Bạn đã từ chối offer cho vị trí này. |
| `Withdrawn` | `Đã rút đơn` | Bạn đã rút đơn và có thể ứng tuyển lại nếu job còn mở. |

## 5. Who can move the status

| From | To | Actor | Endpoint |
|---|---|---|---|
| `Applied` | `Screening`, `Rejected` | HR | `PATCH /api/hr/applications/{id}/decision` |
| `Screening` | `ManagerReview`, `Rejected` | HR | `PATCH /api/hr/applications/{id}/decision` |
| `ManagerReview` | `Interview`, `Rejected` | Manager | `PATCH /api/hr/applications/{id}/decision` |
| `Interview` | `Offer`, `Rejected` | HR/Manager | `PATCH /api/hr/applications/{id}/decision` |
| `Offer` | `Hired` | Candidate | `POST /api/candidate/applications/{id}/accept-offer` |
| `Offer` | `OfferDeclined` | Candidate | `POST /api/candidate/applications/{id}/decline-offer` |
| `Applied`, `Screening`, `ManagerReview`, `Interview` | `Withdrawn` | Candidate | `POST /api/candidate/applications/{id}/withdraw` |

## 6. Notification behavior

### On apply success

- Application row is committed first
- Notification is then published as a best-effort side effect
- Recipients for `new_application_received`:
  - `job.CreatedBy`
  - all users in role `HR`
- If notification publishing fails, apply still returns `201 Created`

### On later status changes

- Candidate receives `application_status_changed` after commit
- Interview scheduling emits `interview_scheduled`
- Notification delivery failure must not roll back the business action

## 7. Practical FE rules

- Do not derive status by string hacks on the FE; use the backend label/next-step contract
- Show `withdraw` only when status is `Applied`, `Screening`, `ManagerReview`, or `Interview`
- Show `acceptOffer` / `declineOffer` only when status is `Offer`
- Treat `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn` as history/closed items
- Re-apply is allowed only after a closed status and only if the job is still open for apply
- When rendering an application row, compute the visible actions from dependencies:
  application status + current user role + ownership + related offer/interview availability.
- When rendering apply context, compute blockers from dependencies:
  job status/deadline + candidate profile completeness + resume + active application history.

## 8. Known API robustness rule

Notification read endpoints support both methods below to avoid environment/proxy-specific `405`:

- `PATCH /api/notifications/{id}/read`
- `POST /api/notifications/{id}/read`
- `PATCH /api/notifications/read-all`
- `POST /api/notifications/read-all`
