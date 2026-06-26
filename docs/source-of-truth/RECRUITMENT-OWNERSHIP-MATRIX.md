# Recruitment Ownership Matrix

**Status:** Phase 0 — **Target design** for recruitment ownership. Notification routing here is
**Planned**, not implemented. Data-source fields marked _(planned)_ do not exist in code yet (see
[API-CONTRACT.md](API-CONTRACT.md) §Planned ownership fields and
[IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md)).

Business roles: **Candidate**, **HR / Recruiter**, **DepartmentHead**, **SystemAdmin**.
`ManagerReview` (code status) **=** the **DepartmentHead review** business stage.

---

## 1. Ownership matrix

| Workflow area | State / Event | Primary owner | Secondary participant | Future notification recipient | Data source | Fallback | Notes |
|---|---|---|---|---|---|---|---|
| Job | Draft / Created | HR / Recruiter | — | (none) | `Job.RecruiterId` _(planned)_ → `Job.CreatedBy` | `Job.CreatedBy` (current) | HR drafts the posting; not public. |
| Job | PendingApproval | DepartmentHead | HR / Recruiter | DepartmentHead | `Department.HeadUserId` _(planned)_ | `Manager` role (current) | Submitted for the Department head's approval. |
| Job | Approved | DepartmentHead (approver) | HR / Recruiter | HR / Recruiter | `Job.ApprovedBy` (audit) + `Department.HeadUserId` _(planned)_ | `Manager` role (current) | Job becomes public/applyable. |
| Job | Rejected | DepartmentHead (approver) | HR / Recruiter | HR / Recruiter | `Job.ApprovedBy` (audit) | `Manager` role (current) | Posting rejected during approval; not public. |
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

## 3. Current vs planned data source (verified)

| Logical owner | Planned field | Exists today? | Current fallback in code |
|---|---|---|---|
| Job recruiter (business owner) | `Job.RecruiterId` | **No** (planned) | `Job.CreatedBy` (`Job.cs:13`) |
| Department head (approver/reviewer) | `Department.HeadUserId` | **No** (planned) | `Manager` role membership; `Job.ApprovedBy` (`Job.cs:15`) |
| Per-application recruiter | `Application.AssignedRecruiterId` | **No** (planned) | n/a (only `Application.ReviewedBy` exists, `Application.cs:15`) |
| Per-application head | `Application.AssignedDepartmentHeadId` (or compat `AssignedManagerId`) | **No** (planned) | n/a (only `Application.ReviewedBy`) |

See [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) for the snapshot resolution order.
