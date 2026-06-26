# 01 — System Overview (Roles & Recruitment Ownership)

**Status:** Phase 0 — business + docs only. **No code, DB, or migration changes** accompany this
document. Wording below is labelled **Current** (verified from code) or **Planned / Target design**
(not yet implemented).

RecruitPro is a recruitment / ATS platform. This document defines the business roles, their
responsibilities, and the demo personas used consistently across the source-of-truth set. It is the
business-facing companion to [00-DOMAIN-STATE-DEPENDENCY.md](00-DOMAIN-STATE-DEPENDENCY.md).

---

## 1. Business roles

Business-facing role names (the vocabulary all ownership docs use going forward):

```
Candidate
HR / Recruiter
DepartmentHead
SystemAdmin
```

> **Naming direction.** The business workflow concept previously called the generic **`Manager`** is
> being replaced by **`DepartmentHead`** — the head user of a specific Department/BU, used as the
> default job approver and the default business reviewer for that Department's applications. This is a
> **Planned** business-vocabulary change. **Code is not renamed in this phase** (see §4 Compatibility).

### Responsibilities

```
Candidate:
  applies to jobs and tracks applications.

HR / Recruiter:
  creates jobs, owns early application screening, schedules interviews, sends offers.

DepartmentHead:
  approves jobs for their Department, reviews candidates after HR screening,
  participates in interview/offer decisions.

SystemAdmin:
  manages system access and configuration; not a default owner of recruitment workflow items.
```

---

## 2. Demo personas

Use these three primary personas consistently in all docs and demos:

```
Phùng Nhật Quang    = Candidate
Nguyễn Thục Uyên    = HR / Recruiter
Trần Trọng Tiến Đạt = DepartmentHead
```

### Main demo flow (target narrative)

```
1.  Nguyễn Thục Uyên (HR) creates a job.
2.  The job belongs to a Department.
3.  The Department Head is Trần Trọng Tiến Đạt.
4.  Trần Trọng Tiến Đạt approves the job.
5.  Phùng Nhật Quang (Candidate) applies to the approved job.
6.  Nguyễn Thục Uyên receives/handles the new application (HR is first owner).
7.  Nguyễn Thục Uyên screens the application.
8.  On pass, Nguyễn Thục Uyên moves the application to DepartmentHead review.
9.  Trần Trọng Tiến Đạt reviews the application.
10. Interview / offer steps follow.
```

---

## 3. Current role reality (verified from code)

| Concern | Current (verified) | Planned / Target |
|---|---|---|
| Seeded roles (`init.sql:536-540`) | `Candidate`, `HR`, `HeadDepartment`, `Manager`, `SystemAdmin` | Standardize business vocabulary on `DepartmentHead`; treat `Manager` as legacy |
| Frontend `ROLE_NAMES` (`src/permissions/rolePermissions.ts:14-20`) | `candidate`, `hr`, `headdepartment`, `manager`, `systemadmin` | same set; `headdepartment` becomes the primary internal-reviewer role |
| Job-approval authorization | `[Authorize(Roles = "Manager")]` on the manager approval-queue/approval-detail endpoints (`JobController.cs:71-85`); status change via `PatchJobAsync` is `HR,Manager` | Route approval by `Department.HeadUserId` and the `HeadDepartment` role |
| Application reviewer role | HR drives `Applied/Screening`; the generic `Manager` role drives `ManagerReview → Interview` (per [APPLY-STATUS-FLOW.md](APPLY-STATUS-FLOW.md) §5) | `ManagerReview` is owned by the application's assigned **DepartmentHead** |

> **Observation:** the `HeadDepartment` role already **exists** in the DB seed and the frontend, and one
> seeded user holds both `Manager` and `HeadDepartment` (`init.sql` user_roles). However the backend
> **authorization and ownership routing still key off the generic `Manager` role**, and there is **no
> `Department.HeadUserId`** field. Closing that gap is the subject of the ownership refactor — see
> [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md).

---

## 4. Compatibility note (code vs business vocabulary)

| Business term (target) | Current code term | Compatibility statement |
|---|---|---|
| DepartmentHead | `Manager` role; `ManagerReview` status | **Planned:** business docs say `DepartmentHead`; code keeps `Manager`/`ManagerReview` until the refactor phase. `ManagerReview` **means** the DepartmentHead-review business stage. |
| DepartmentHead (data) | `HeadDepartment` role (seeded), no `Department.HeadUserId` | **Planned:** introduce `Department.HeadUserId` and route approval/review through it + the `HeadDepartment` role. |
| HR / Recruiter | `HR` role | Same; "Recruiter" is the business synonym for the HR owner of a job's applications. |

**No status enum or role is renamed in this phase.** All renames are deferred to the implementation
phase and tracked in the implementation plan with a compatibility/rollback note.

---

## 5. Related documents

| Document | Purpose |
|---|---|
| [RECRUITMENT-OWNERSHIP-MATRIX.md](RECRUITMENT-OWNERSHIP-MATRIX.md) | Who owns each workflow state/event, and the future notification recipient. |
| [JOB-APPROVAL-FLOW.md](JOB-APPROVAL-FLOW.md) | HR creates → DepartmentHead approves → public/applyable. |
| [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) | Apply snapshots owners; HR-first, DepartmentHead-after-screening. |
| [NOTIFICATION-EVENT-MATRIX.md](NOTIFICATION-EVENT-MATRIX.md) | Planned notification events and routing (not implemented in this phase). |
| [IMPLEMENTATION-PLAN-OWNERSHIP.md](IMPLEMENTATION-PLAN-OWNERSHIP.md) | Phased coding plan for the next phase. |
