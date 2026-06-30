# E2E Ownership & Status Manual Checklist (Backend ↔ Frontend)

End-to-end acceptance criteria for the department-head ownership workflow and the
canonical English status contract. These map to **E2E-OWN-001/002/003** in
[TEST-MATRIX.md](../source-of-truth/TEST-MATRIX.md) and to the automated Playwright
specs in `recruit-pro-internal/e2e/`.

> The automated frontend E2E is deterministic and **backend-free** (session seeded
> into `localStorage`; all `/api/**` responses mocked via Playwright route
> interception). Backend behaviour is covered separately by the `RecruitPro.Tests`
> integration suite (T-OWN-*, T-STATUS-*). This checklist is the full-stack manual
> fallback for environments that prefer a live backend + DB.

## Running

- **Frontend E2E (mocked):** `cd recruit-pro-internal && npm run e2e:install && npm run e2e`
- **Backend integration:** `cd RecruitProInternal && dotnet test` (needs Docker for Testcontainers PostgreSQL)
- **Full-stack manual:** run the API (`dotnet run --project RecruitPro.API` + Postgres) and the FE
  (`npm run dev`), then walk the scenarios below with the seeded users.

## E2E-OWN-001 — Candidate application status (canonical English)

Login as a candidate (seed: Phùng Nhật Quang). Open My Applications.

- [ ] API `status` is the canonical English enum key; Vietnamese only in `statusLabel`/`nextStep`
      (backend: T-STATUS-001/002/003).
- [ ] `Screening` → **Screening**, `Interview` → **Interview**, `ManagerReview` → **Head Review**,
      `Rejected` → **Rejected**, unknown → **Unknown** (never Rejected).
- [ ] A `Rejected` application exposes **no** `withdraw` action.

## E2E-OWN-002 — DepartmentHead job approval

Login as the department head (seed: Trần Trọng Tiến Đạt) and as a non-head Manager.

- [ ] Head sees the approval queue scoped to `Department.HeadUserId`; SystemAdmin-only is blocked;
      a non-head Manager sees an empty queue and 403 on detail (backend: T-OWN-034…040).
- [ ] Detail/submit guard: department with no head → **422 `DEPARTMENT_HEAD_REQUIRED`**; non-head,
      non-admin → **403 `FORBIDDEN`** (backend: T-OWN-021/028…032).
- [ ] Approve/reject submits `PATCH /api/hr/jobs/{id}/status` (and the `/api/jobs/{id}/status` alias
      enforces the same guard — no bypass).
- [ ] `ManagerReview → Interview/Rejected` allowed only for the assigned department head or SystemAdmin;
      Manager fallback only when no head is resolved (backend: T-OWN-025/026/027).
- [ ] Queue/detail job-status display is English (`Pending Approval`, not "Chờ duyệt").

## E2E-OWN-003 — Ownership snapshot & display

Login as HR (seed: Nguyễn Thục Uyên).

- [ ] Apply snapshots `AssignedRecruiterId = Job.RecruiterId ?? Job.CreatedBy` and
      `AssignedDepartmentHeadId = Job.Department.HeadUserId ?? Job.ApprovedBy` (backend: T-OWN-007*).
- [ ] Job list/detail expose recruiter + (effective) department head; the FE renders them with safe
      fallbacks (`Chưa phân công` / `Chưa có trưởng bộ phận`).

## Invariants

- `ManagerReview` = the DepartmentHeadReview business stage. The enum key is **not** renamed; the FE
  display label is **Head Review**.
- No `Job.HiringManagerId`. Roles `Manager` and `HeadDepartment` are preserved.
- Notification routing remains **Phase 6 / not implemented** (no FE notification UI claimed).
