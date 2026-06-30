# Recruitment Ownership Matrix

**Status:** Current implementation — the ownership **data model, API exposure, authorization, and
notification routing are implemented**:
`Department.HeadUserId`, `Job.RecruiterId`, `Application.AssignedRecruiterId`,
`Application.AssignedDepartmentHeadId` exist in `init.sql` + EF, are exposed on DTOs, and now gate job
approval (BR-OWN-003) and ManagerReview decisions (BR-OWN-007). Notification routing now follows the
ownership targets in this matrix through `NotificationEventService` and
`NOTIFICATION-EVENT-MATRIX.md`; fallbacks are retained only for legacy or missing ownership data. See
[API-CONTRACT.md](API-CONTRACT.md), [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md),
and [NOTIFICATION-EVENT-MATRIX.md](NOTIFICATION-EVENT-MATRIX.md).

Business roles: **Candidate**, **HR / Recruiter**, **DepartmentHead**, **SystemAdmin**.
`ManagerReview` (code status) **=** the **DepartmentHead review** business stage.

---

## 1. Ownership matrix

| Workflow area | State / Event | Primary owner | Secondary participant | Notification recipient | Data source | Fallback | Notes |
|---|---|---|---|---|---|---|---|
| Job | Draft / Created | HR / Recruiter | — | (none) | `Job.RecruiterId` → `Job.CreatedBy` | `Job.CreatedBy` | HR drafts the posting; not public. |
| Job | PendingApproval | DepartmentHead | HR / Recruiter | DepartmentHead | `Department.HeadUserId` **(implemented)** | none for notification | Submitted for the Department head's approval. Approval **queue/detail scoped** to `Department.HeadUserId`; SystemAdmin-only business access is blocked unless paired with the relevant workflow role. |
| Job | Approved | DepartmentHead (approver) | HR / Recruiter | HR / Recruiter | `Department.HeadUserId` **(implemented)** + `Job.ApprovedBy` (audit) | none for SystemAdmin-only | Job becomes public/applyable. Approve guarded to the department head (BR-OWN-003/009). |
| Job | Rejected | DepartmentHead (approver) | HR / Recruiter | HR / Recruiter | `Department.HeadUserId` **(implemented)** + `Job.ApprovedBy` (audit) | none for SystemAdmin-only | Posting rejected during approval; not public. Reject guarded to the department head. |
| Application | Applied | HR / Recruiter | — | HR / Recruiter | `Application.AssignedRecruiterId` | `Job.RecruiterId` → `Job.CreatedBy` | **HR-first** — DepartmentHead is **not** notified yet (BR-OWN-006). |
| Application | Screening | HR / Recruiter | — | Candidate | `Application.AssignedRecruiterId` | `Job.RecruiterId` → `Job.CreatedBy` | HR evaluates fit. |
| Application | DepartmentHeadReview (`ManagerReview`) | DepartmentHead | HR / Recruiter | DepartmentHead | `Application.AssignedDepartmentHeadId` | `Department.HeadUserId` → `Job.ApprovedBy` | Entered only after HR passes screening (BR-OWN-007). |
| Application | Interview | HR / Recruiter (coordinates) | DepartmentHead / Interviewer | Candidate + HR + DepartmentHead/Interviewer | `Application.AssignedRecruiterId` + `…DepartmentHeadId` | recruiter/head ownership fallbacks | HR coordinates; DepartmentHead evaluates. |
| Application | Offer | Candidate (response) | HR / Recruiter (coordination) | Candidate + HR / Recruiter + DepartmentHead (`offer_sent`) | `Application.AssignedRecruiterId` + `…DepartmentHeadId` | recruiter/head ownership fallbacks | Candidate accepts/declines a `Sent` offer. |
| Application | Hired | HR / Recruiter (close-out) | DepartmentHead (informed) | HR / Recruiter + DepartmentHead | `Application.AssignedRecruiterId` + `…DepartmentHeadId` | recruiter/head ownership fallbacks | Terminal for the `jobId` (INV-015). |
| Application | Rejected | HR / Recruiter or DepartmentHead (whoever closed it) | — | Candidate | reviewer of record (`Application.ReviewedBy`) | `ReviewedBy` (current) | Company decision; closed. |
| Application | Withdrawn | Candidate | HR / Recruiter (informed) | HR / Recruiter; + DepartmentHead after ManagerReview | `Application.AssignedRecruiterId` | recruiter/head ownership fallbacks | Candidate decision; closed; pending interview becomes stale. |
| Application | OfferDeclined | Candidate | HR / Recruiter (follow-up) | HR / Recruiter + DepartmentHead | `Application.AssignedRecruiterId` + `…DepartmentHeadId` | recruiter/head ownership fallbacks | Candidate declined the offer; closed. |

---

## 2. Ownership principles

1. **CreatedBy / ApprovedBy are audit fields**, not long-term owners. `CreatedBy` records who created
   a job; `ApprovedBy` records who approved it. They are **legacy fallbacks** for ownership only until
   `Job.RecruiterId` / `Department.HeadUserId` exist (BR-OWN-002, BR-OWN-003).
2. **HR owns the early pipeline** (`Applied`, `Screening`). The DepartmentHead is **not** the default
   recipient of every new application — HR screens first to avoid spamming the head (BR-OWN-006).
3. **DepartmentHead owns `ManagerReview`/DepartmentHeadReview onward** for the Department's candidates
   (BR-OWN-007).
4. **Assignment is a snapshot taken at apply time** (BR-OWN-005), so later changes to a job's recruiter
   or a department's head do not silently re-route in-flight applications.
5. **SystemAdmin is never the default workflow owner** (BR-OWN-009).

---

## 3. Data source — implemented vs still-planned use (verified)

> The **fields all exist** and are used for DTO exposure, job-approval authorization, ManagerReview
> authorization, and notification routing. The listed fallbacks run only when legacy data is missing a
> primary owner.

| Logical owner | Field | Exists today? | Used today by | Fallback in code |
|---|---|---|---|---|
| Job recruiter (business owner) | `Job.RecruiterId` | **Yes** (Phase 1, `Job.cs:19`) | apply snapshot; create-job capture/validation; job DTOs | `Job.CreatedBy` (`Job.cs:13`) when null |
| Department head (approver/reviewer) | `Department.HeadUserId` | **Yes** (Phase 1, `Department.cs:17`) | apply snapshot; **job approval authz (Phase 3)**; department DTOs/update | `Job.ApprovedBy` (audit) for legacy application snapshots |
| Per-application recruiter | `Application.AssignedRecruiterId` | **Yes** (Phase 1, `Application.cs:21`) | snapshotted on apply; HR/internal application DTOs | `Job.CreatedBy` when `RecruiterId` null |
| Per-application head | `Application.AssignedDepartmentHeadId` | **Yes** (Phase 1, `Application.cs:23`) | snapshotted on apply; **ManagerReview authz (Phase 3)**; application DTOs | `Job.ApprovedBy` when `HeadUserId` null; Manager-role fallback only when head is null |

See [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) for the snapshot resolution order.
