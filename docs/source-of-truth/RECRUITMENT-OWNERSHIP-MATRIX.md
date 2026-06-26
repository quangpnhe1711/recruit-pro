# Recruitment Ownership Matrix

**Status:** Phase 2/3 — the ownership **data model, API exposure, and authorization are implemented**:
`Department.HeadUserId`, `Job.RecruiterId`, `Application.AssignedRecruiterId`,
`Application.AssignedDepartmentHeadId` exist in `init.sql` + EF, are exposed on DTOs, and now gate job
approval (BR-OWN-003) and ManagerReview decisions (BR-OWN-007). **Notification routing** in this matrix
remains **Planned / Phase 6, not implemented** — the "Future notification recipient" column describes
the target; today notifications still use the **Fallback** column (role membership + audit fields). See
[API-CONTRACT.md](API-CONTRACT.md) and [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md).

Business roles: **Candidate**, **HR / Recruiter**, **DepartmentHead**, **SystemAdmin**.
`ManagerReview` (code status) **=** the **DepartmentHead review** business stage.

---

## 1. Ownership matrix

| Workflow area | State / Event | Primary owner | Secondary participant | Future notification recipient | Data source | Fallback | Notes |
|---|---|---|---|---|---|---|---|
| Job | Draft / Created | HR / Recruiter | — | (none) | `Job.RecruiterId` _(planned)_ → `Job.CreatedBy` | `Job.CreatedBy` (current) | HR drafts the posting; not public. |
| Job | PendingApproval | DepartmentHead | HR / Recruiter | DepartmentHead | `Department.HeadUserId` **(implemented)** | `SystemAdmin` override | Submitted for the Department head's approval. Approval **queue/detail scoped** to `Department.HeadUserId` (Phase 4); `SystemAdmin` sees all. |
| Job | Approved | DepartmentHead (approver) | HR / Recruiter | HR / Recruiter | `Department.HeadUserId` **(implemented)** + `Job.ApprovedBy` (audit) | `SystemAdmin` override | Job becomes public/applyable. Approve guarded to the head / SystemAdmin (BR-OWN-003). |
| Job | Rejected | DepartmentHead (approver) | HR / Recruiter | HR / Recruiter | `Department.HeadUserId` **(implemented)** + `Job.ApprovedBy` (audit) | `SystemAdmin` override | Posting rejected during approval; not public. Reject guarded to the head / SystemAdmin. |
| Application | Applied | HR / Recruiter | — | HR / Recruiter | `Application.AssignedRecruiterId` _(planned)_ | `Job.CreatedBy` + `HR` role (current) | **HR-first** — DepartmentHead is **not** notified yet (BR-OWN-006). |
| Application | Screening | HR / Recruiter | — | Candidate | `Application.AssignedRecruiterId` _(planned)_ | `Job.CreatedBy` + `HR` role | HR evaluates fit. |
| Application | DepartmentHeadReview (`ManagerReview`) | DepartmentHead | HR / Recruiter | DepartmentHead | `Application.AssignedDepartmentHeadId` _(planned)_ | `Manager` role + `Job.ApprovedBy` | Entered only after HR passes screening (BR-OWN-007). |
| Application | Interview | HR / Recruiter (coordinates) | DepartmentHead / Interviewer | Candidate + HR + DepartmentHead/Interviewer | `Application.AssignedRecruiterId` + `…DepartmentHeadId` _(planned)_ | `Job.CreatedBy` + `HeadDepartment` role (current `interview_scheduled` routing) | HR coordinates; DepartmentHead evaluates. |
| Application | Offer | Candidate (response) | HR / Recruiter (coordination) | Candidate (`offer_sent`) | `Application.AssignedRecruiterId` _(planned)_ | `Job.CreatedBy` | Candidate accepts/declines a `Sent` offer. |
| Application | Hired | HR / Recruiter (close-out) | DepartmentHead (informed) | HR / Recruiter + DepartmentHead | `Application.AssignedRecruiterId` + `…DepartmentHeadId` _(planned)_ | `Job.CreatedBy` | Terminal for the `jobId` (INV-015). |
| Application | Rejected | HR / Recruiter or DepartmentHead (whoever closed it) | — | Candidate | reviewer of record (`Application.ReviewedBy`) | `ReviewedBy` (current) | Company decision; closed. |
| Application | Withdrawn | Candidate | HR / Recruiter (informed) | HR / Recruiter | `Application.AssignedRecruiterId` _(planned)_ | `Job.CreatedBy` | Candidate decision; closed; pending interview becomes stale. |
| Application | OfferDeclined | Candidate | HR / Recruiter (follow-up) | HR / Recruiter + DepartmentHead | `Application.AssignedRecruiterId` + `…DepartmentHeadId` _(planned)_ | `Job.CreatedBy` | Candidate declined the offer; closed. |

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

> The **fields all exist (Phase 1)** and are now **used** for DTO exposure (Phase 2), job-approval
> authorization, and ManagerReview authorization (Phase 3). Only **notification routing (Phase 6)** is
> still planned; until then the listed fallback runs for notifications.

| Logical owner | Field | Exists today? | Used today by | Fallback in code |
|---|---|---|---|---|
| Job recruiter (business owner) | `Job.RecruiterId` | **Yes** (Phase 1, `Job.cs:19`) | apply snapshot; create-job capture/validation; job DTOs | `Job.CreatedBy` (`Job.cs:13`) when null |
| Department head (approver/reviewer) | `Department.HeadUserId` | **Yes** (Phase 1, `Department.cs:17`) | apply snapshot; **job approval authz (Phase 3)**; department DTOs/update | `SystemAdmin` override; `Job.ApprovedBy` (audit) |
| Per-application recruiter | `Application.AssignedRecruiterId` | **Yes** (Phase 1, `Application.cs:21`) | snapshotted on apply; HR/internal application DTOs | `Job.CreatedBy` when `RecruiterId` null |
| Per-application head | `Application.AssignedDepartmentHeadId` | **Yes** (Phase 1, `Application.cs:23`) | snapshotted on apply; **ManagerReview authz (Phase 3)**; application DTOs | `Job.ApprovedBy` when `HeadUserId` null; Manager-role fallback only when head is null |

See [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) for the snapshot resolution order.
