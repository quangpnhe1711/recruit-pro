# Job Approval Flow

**Status:** Phase 2/3 — the ownership data model **and** the approval guard are **implemented**.
Approve/reject is now authorized against the job's **DepartmentHead (`Department.HeadUserId`)**
(BR-OWN-003/009), with `Job.ApprovedBy` set to the acting user. Only the **frontend
(Phase 4)** remains. Behavior below is verified from code.

---

## 1. Target flow

```
HR / Recruiter creates a job (Draft)
        ↓ submit for approval
Job is PendingApproval (not public, not applyable)
        ↓ approval authorized against Department.HeadUserId [Phase 3, implemented]
DepartmentHead approves or rejects
        ↓ approve → Approved → public + applyable (until deadline)
        ↓ reject  → Rejected → not public, not applyable
```

- **HR creates the job** and selects the **Department** and the **Recruiter phụ trách** (owning
  recruiter). _(Recruiter selection is planned; today the creator is captured as `Job.CreatedBy`.)_
- **The Department determines the DepartmentHead.** `Department.HeadUserId` exists (Phase 1); routing
  approval *authorization* to it is _(planned — Phase 3)_.
- **The DepartmentHead approves/rejects** the job for their Department.
- **Approved** jobs become **public/applyable** (subject to deadline, INV-001/BR-APPLICATION-004).
- **Rejected / non-approved** jobs are **not public/applyable** (`Draft`, `PendingApproval`, `Closed`,
  `Rejected` are all non-applyable — see [STATE-MACHINE.md](STATE-MACHINE.md) Job status).

---

## 2. Data fields

| Field | Meaning | Status |
|---|---|---|
| `Department.HeadUserId` | The Department's head — default approver and business reviewer. | **Implemented (Phase 1)** — column + EF mapping + seed (`Department.cs:17`). Approval *authorization* by it is Phase 3. |
| `Job.RecruiterId` | Business owner: the recruiter who handles the job's applications. | **Implemented (Phase 1)** — column + EF mapping + backfill (`Job.cs:19`). Job create/edit capture is Phase 2–4. |
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
| Who can approve | `PatchJobAsync` guard: the **Approved/Rejected** transition requires `currentUserId == Department.HeadUserId`; else 403 `FORBIDDEN`. No head on the department → 422 `DEPARTMENT_HEAD_REQUIRED`. The controller admits `HR,Manager,HeadDepartment`; SystemAdmin-only is blocked by endpoint role authorization. | **Closed** — approval is scoped to the specific Department head (no longer "any Manager"). |
| Approver identity | `Job.ApprovedBy` is set to the acting user (decision-actor audit) on approve/reject. | Conformant. |
| Department head data | `Department.HeadUserId` exists and is settable via `PUT /api/departments/{id}` (validated against the HeadDepartment/SystemAdmin role). | Conformant. |
| `PATCH /api/jobs/{id}/status` | **Hardened (Phase 2/3):** now requires auth (`HR,Manager,HeadDepartment`) and routes through the same `PatchJobAsync` guard as `/api/hr/jobs/{id}/status` — an authenticated alias (the old unguarded `UpdateJobStatusAsync` was removed). | **Closed** — no longer a public approval bypass. |
| Approval **queue/detail** access (`GET /api/manager/jobs/approval-queue`, `…/{id}/approval-detail`) | **Hardened (Phase 4):** `[Authorize(Roles = "Manager,HeadDepartment")]` **and server-scoped** — `GetManagerApprovalQueueAsync`/`GetManagerApprovalDetailAsync` take the caller's id/roles. The queue is filtered to `Department.HeadUserId == currentUserId`; SystemAdmin-only is blocked at authorization; the detail uses the same `EvaluateApprovalAccess` predicate as the submit guard (422 no head → 403 not head). A non-head Manager sees an empty queue / 403 detail. | **Closed** — the DepartmentHead is now the approval workflow role; generic Manager no longer sees all departments. |
| Public listing / apply gating | Only `Approved` jobs accept applications (`BuildApplyEligibility`, INV-001); FE `JobDetailScreen.jobApplyState` disables the CTA otherwise. | Conformant — unchanged. |

**Conclusion:** the approval *gate* exists and works, the ownership *data model*
(`Department.HeadUserId`, `Job.RecruiterId`) is implemented, and approval is now authorized
*against the specific Department head* in `JobService.PatchJobAsync` (Phase 3, done).
Both status routes (`/api/hr/jobs/{id}/status` and the hardened `/api/jobs/{id}/status` alias) go through
that guard. **Phase 4 done:** the frontend consumes the ownership model, and the approval **queue/detail**
access is now the DepartmentHead's (server-scoped to `Department.HeadUserId`, SystemAdmin-only blocked; Manager
kept only as compatibility but scoped, not cross-department). The `HeadDepartment` role can now reach the
approval queue/detail in the UI without needing the generic `Manager` role. What remains is
**notifications** (Phase 6 — **not implemented**) in
[IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md).

---

## 4. Demo mapping

```
Nguyễn Thục Uyên (HR)        creates the job, picks Department + Recruiter.
Trần Trọng Tiến Đạt (Head)   is Department.HeadUserId for that Department → approves the job.
Approved job                 becomes public; Phùng Nhật Quang (Candidate) can apply.
```
