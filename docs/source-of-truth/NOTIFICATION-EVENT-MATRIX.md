# Notification Event Matrix

**Status:** Phase 0 — **Planned routing only. Notification is NOT (re)implemented in this phase.**
This document defines the **target** event set and recipient routing for the ownership refactor. A
partial notification system already exists in code (see §3 Current reality); the recipient *sources*
below (`AssignedRecruiterId`, `Department.HeadUserId`, …) are **planned** and depend on the ownership
fields described in [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) and
[API-CONTRACT.md](API-CONTRACT.md).

Recipient roles use business names: **Candidate**, **HR / Recruiter**, **DepartmentHead**.

---

## 1. Planned event matrix

| Event code | Trigger | Actor | Recipient | Recipient source | Title purpose | Body purpose | Payload fields | Required/optional | Implementation phase |
|---|---|---|---|---|---|---|---|---|---|
| `job_submitted_for_approval` | Job moved Draft → PendingApproval | HR / Recruiter | DepartmentHead | `Department.HeadUserId` _(planned)_ → `Manager` role (fallback) | "Job chờ bạn duyệt" | Which job, by whom, department | `jobId, jobTitle, departmentId, submittedBy` | Required | Phase 6 |
| `job_approved` | Job → Approved | DepartmentHead | HR / Recruiter | `Job.RecruiterId` _(planned)_ → `Job.CreatedBy` | "Job đã được duyệt" | Job is now public/applyable | `jobId, jobTitle, approvedBy` | Required | Phase 6 |
| `job_rejected` | Job → Rejected | DepartmentHead | HR / Recruiter | `Job.RecruiterId` _(planned)_ → `Job.CreatedBy` | "Job bị từ chối" | Reason / next step | `jobId, jobTitle, rejectedBy, reason?` | Required | Phase 6 |
| `application_applied` | Candidate applies | Candidate | HR / Recruiter | `Application.AssignedRecruiterId` _(planned)_ → `Job.CreatedBy` + `HR` role | "Hồ sơ mới" | New application to screen | `applicationId, jobId, jobTitle, candidateId` | Required | Phase 6 (event exists as `new_application_received`) |
| `application_screening_started` | HR moves Applied → Screening | HR / Recruiter | Candidate | `Application.UserId` | "Hồ sơ đang được sàng lọc" | Status update | `applicationId, jobId, status` | Optional | Phase 6 |
| `application_department_head_review_requested` | HR moves Screening → ManagerReview | HR / Recruiter | DepartmentHead | `Application.AssignedDepartmentHeadId` _(planned)_ → `Manager` role | "Ứng viên chờ bạn review" | Candidate passed HR screening | `applicationId, jobId, candidateId, departmentId` | Required | Phase 6 |
| `interview_scheduled` | Interview created | HR / Recruiter | Candidate + HR / Recruiter + DepartmentHead / Interviewer | `Application.UserId` + `AssignedRecruiterId` _(planned)_ + `AssignedDepartmentHeadId` _(planned)_ | "Lịch phỏng vấn" | When/where/who | `applicationId, interviewId, startAt, endAt, interviewerId` | Required | Phase 6 (event exists) |
| `offer_sent` | Offer → Sent | HR / Recruiter | Candidate | `Application.UserId` | "Bạn nhận được offer" | Respond to offer | `applicationId, offerId, jobId` | Required | Phase 6 |
| `offer_accepted` | Candidate accepts offer | Candidate | HR / Recruiter + DepartmentHead | `AssignedRecruiterId` + `AssignedDepartmentHeadId` _(planned)_ | "Offer được chấp nhận" | Hiring outcome for the Department | `applicationId, offerId, jobId, candidateId` | Required | Phase 6 |
| `offer_declined` | Candidate declines offer | Candidate | HR / Recruiter + DepartmentHead | `AssignedRecruiterId` + `AssignedDepartmentHeadId` _(planned)_ | "Offer bị từ chối" | Follow-up needed | `applicationId, offerId, jobId, candidateId` | Required | Phase 6 |
| `application_withdrawn` | Candidate withdraws | Candidate | HR / Recruiter | `AssignedRecruiterId` _(planned)_ → `Job.CreatedBy` | "Ứng viên rút đơn" | Pipeline update; cancel pending interview | `applicationId, jobId, candidateId` | Optional | Phase 6 |
| `candidate_hired` | Application → Hired | HR / Recruiter | HR / Recruiter + DepartmentHead | `AssignedRecruiterId` + `AssignedDepartmentHeadId` _(planned)_ | "Đã tuyển" | Close-out / headcount | `applicationId, jobId, candidateId` | Required | Phase 6 |

---

## 2. Routing principles

- **HR-first on apply (BR-OWN-006):** `application_applied` goes to the recruiter, **not** the
  DepartmentHead. The head is first notified at `application_department_head_review_requested`.
- **Snapshot-based recipients (BR-OWN-005):** application-scoped events resolve recipients from the
  application's **snapshotted** `AssignedRecruiterId` / `AssignedDepartmentHeadId`, with the audit/role
  fallbacks shown.
- **Best-effort, post-commit (BR-APPLICATION-005/010):** notifications are published **after** the DB
  commit and a delivery failure must never roll back or fail the business action. This is already the
  case in code and must be preserved.
- **SystemAdmin is not a recruitment recipient (BR-OWN-009).**

---

## 3. Current reality (verified from code)

A partial notification system already exists — **this phase does not change it**:

| Existing event (code) | Current recipients | Source in code | Maps to planned event |
|---|---|---|---|
| `new_application_received` | `Job.CreatedBy` + all `HR` role users (deduplicated) | `NotificationEventService.PublishNewApplicationReceivedAsync` (recipients = `[Job.CreatedBy]` ∪ `GetUsersInRolesAsync("HR")`) | `application_applied` |
| `application_status_changed` | Candidate | `PublishApplicationStatusChangedAsync` | `application_screening_started` and other status updates (to be split per status) |
| `interview_scheduled` | `Job.CreatedBy` + `HeadDepartment` role users | `NotificationEventService` (already calls `GetUsersInRolesAsync(HeadDepartmentRoleName)`) | `interview_scheduled` |

**Gaps vs target:** events `job_submitted_for_approval`, `job_approved`, `job_rejected`, `offer_sent`,
`offer_accepted`, `offer_declined`, `application_withdrawn`, `candidate_hired`, and a dedicated
`application_department_head_review_requested` are **not implemented**; current routing uses
`Job.CreatedBy` and **role membership** rather than the **planned** snapshot fields
(`AssignedRecruiterId` / `AssignedDepartmentHeadId` / `Department.HeadUserId`).

Implementation of this matrix is **Phase 6** in
[IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md), explicitly after the ownership
data model lands.
