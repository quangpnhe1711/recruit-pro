# Job Approval Flow

**Status:** Phase 0 — **Target design**. Fields marked _(planned)_ are not implemented yet.
**Current** behavior is verified from code and called out explicitly.

---

## 1. Target flow

```
HR / Recruiter creates a job (Draft)
        ↓ submit for approval
Job is PendingApproval (not public, not applyable)
        ↓ Department determines the DepartmentHead (Department.HeadUserId)  [planned]
DepartmentHead approves or rejects
        ↓ approve → Approved → public + applyable (until deadline)
        ↓ reject  → Rejected → not public, not applyable
```

- **HR creates the job** and selects the **Department** and the **Recruiter phụ trách** (owning
  recruiter). _(Recruiter selection is planned; today the creator is captured as `Job.CreatedBy`.)_
- **The Department determines the DepartmentHead.** Target: `Department.HeadUserId`. _(Planned.)_
- **The DepartmentHead approves/rejects** the job for their Department.
- **Approved** jobs become **public/applyable** (subject to deadline, INV-001/BR-APPLICATION-004).
- **Rejected / non-approved** jobs are **not public/applyable** (`Draft`, `PendingApproval`, `Closed`,
  `Rejected` are all non-applyable — see [STATE-MACHINE.md](STATE-MACHINE.md) Job status).

---

## 2. Data fields

| Field | Meaning | Status |
|---|---|---|
| `Department.HeadUserId` | The Department's head — default approver and business reviewer. | **Planned** (does not exist; `Department` has only `Id/Name/Description`). |
| `Job.RecruiterId` | Business owner: the recruiter who handles the job's applications. | **Planned** (does not exist). |
| `Job.CreatedBy` | **Audit** — who created the job. Legacy fallback for recruiter ownership only. | **Current** (`Job.cs:13`, `Guid`). |
| `Job.ApprovedBy` | **Audit** — who approved the job. Legacy fallback for head ownership only. | **Current** (`Job.cs:15`, `Guid?`). |
| `Job.DepartmentId` | The job's Department. | **Current** (`Job.cs:11`, `Guid?`). |
| `Job.Status` | `Draft / PendingApproval / Approved / Closed / Rejected`. | **Current** (`JobStatus` enum). |

> `CreatedBy` and `ApprovedBy` are **audit fields**. They must **not** be treated as the long-term
> recruiter / DepartmentHead owners except as a legacy fallback during the migration window
> (BR-OWN-002, BR-OWN-003).

---

## 3. Current reality (verified from code)

| Aspect | Current behavior | Gap vs target |
|---|---|---|
| Who can approve | `[Authorize(Roles = "Manager")]` on `GET /api/manager/jobs/approval-queue` and `…/approval-detail` (`JobController.cs:71-85`); the status change itself runs through `PatchJobAsync` (`HR,Manager`). | Approval is gated by the **generic `Manager` role**, not by `Department.HeadUserId`. Any Manager can approve any Department's job. |
| Approver identity | Recorded in `Job.ApprovedBy` when set. | No link to `Department.HeadUserId` (field absent). |
| Department head data | `HeadDepartment` role exists in `init.sql` + FE, but no `Department.HeadUserId` column. | Need `Department.HeadUserId` to route approval to the correct head. |
| Public listing / apply gating | Only `Approved` jobs accept applications (`BuildApplyEligibility`, INV-001); FE `JobDetailScreen.jobApplyState` disables the CTA otherwise. | Conformant — unchanged by this refactor. |

**Conclusion:** the approval *gate* exists and works, but ownership *routing to a specific Department
head* is **not** implemented. This document is the target; the gap is scheduled in
[IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md) (Phases 1–3).

---

## 4. Demo mapping

```
Nguyễn Thục Uyên (HR)        creates the job, picks Department + Recruiter.
Trần Trọng Tiến Đạt (Head)   is Department.HeadUserId for that Department → approves the job.
Approved job                 becomes public; Phùng Nhật Quang (Candidate) can apply.
```
