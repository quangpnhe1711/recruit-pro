# Implementation Plan — Recruitment Ownership

**Status:** Phase 0 output — **plan only, no code in this phase.** This is the coding plan for the
next phases. Every concrete field/schema below is **Planned / Target design** until the relevant phase
lands. Backend status enums and role names are **not** renamed until a phase explicitly does so with a
compatibility note.

Goal of the program: replace the vague generic `Manager` workflow ownership with **DepartmentHead**
ownership routed through `Department.HeadUserId`, snapshot recruiter/head onto applications at apply
time, and (finally) wire notifications to those owners.

Verified starting point (current code):
- `Department` = `Id, Name, Description` (no `HeadUserId`).
- `Job` = `DepartmentId?, CreatedBy, ApprovedBy?, Status, …` (no `RecruiterId`, no `HiringManagerId`).
- `Application` = `UserId, JobId, ReviewedBy?, Status, …` (no `Assigned*` fields).
- Roles seeded: `Candidate, HR, HeadDepartment, Manager, SystemAdmin`; approval gated by `Manager`.
- Notifications partially exist (`new_application_received`, `application_status_changed`,
  `interview_scheduled`).

---

## Phase 1 — DB / init.sql + seed normalization

> **Status: IMPLEMENTED (2026-06-26).** Columns + FKs + indexes added to `init.sql` and to a standalone
> patch (`db/patches/20260626-add-department-head-ownership-foundation.sql`); EF entities + mappings
> added; apply-time owner snapshot implemented; seed normalized around the three demo personas; backend
> `dotnet build` + `dotnet test` green (162 tests). **Simpler model used:** `EffectiveDepartmentHead =
> Department.HeadUserId ?? Job.ApprovedBy`; `Job.HiringManagerId` deferred. Notification and frontend
> remain **not implemented** (Phases 6 and 4).
>
> **Pre-commit cleanup (2026-06-26).** Source-of-truth consistency closed before committing: the
> active-application partial unique index (`ux_applications_active_user_job`, INV-014) is now also in
> `init.sql` + a dedicated patch (`db/patches/20260626-add-active-application-unique-index.sql`, with a
> duplicate-active guard); seeded `Offer`/`Hired` applications now have consistent `application_offers`
> rows (Offer→`Sent`, Hired→`Accepted`). Fresh Postgres load of `init.sql` verified **0 dirty rows** for
> all active-duplicate, offer/hired-consistency, and ownership checks; both patches re-run idempotently.

**Goal:** introduce the ownership columns and seed the demo personas/department head.
- Add `departments.head_user_id` (nullable FK → users).
- Add `jobs.recruiter_id` (nullable FK → users); optionally `jobs.hiring_manager_id` (nullable).
- Add `applications.assigned_recruiter_id`, `applications.assigned_department_head_id` (nullable FKs).
  _(If keeping legacy naming, add `assigned_manager_id` and document it as the assigned head.)_
- Seed: set the demo department's `head_user_id` to Trần Trọng Tiến Đạt; set demo jobs'
  `recruiter_id` to Nguyễn Thục Uyên; backfill existing rows from `created_by` / `approved_by`.

**Files likely touched:** `RecruitProInternal/init.sql` (schema + seed); a new EF migration later in
Phase 2.
**Validation:** `init.sql` loads on a clean DB; demo users resolve; backfill leaves no null owners for
seeded data.
**Risks:** column nullability vs existing rows; FK violations if seed order wrong.
**Rollback note:** drop the added columns / revert the `init.sql` section; no behavior depends on them
until Phase 2+.

---

## Phase 2 — Backend entities / mapping / DTO / API

> **Status: IMPLEMENTED (2026-06-26).** Entities/mappings landed in Phase 1. Phase 2 exposed the
> ownership fields on DTOs/API: Department (`headUser*`) via `GET/PUT /api/departments`; Job
> (`recruiter*`, `departmentHead*`, `effectiveDepartmentHead*`, audit `createdBy/approvedBy`) on HR
> detail/list; Application (`assignedRecruiter*`, `assignedDepartmentHead*`) on HR/internal endpoints;
> plus `GET /api/users/assignable-recruitment-owners`. Field names use `assigned*` (not the legacy
> `assignedManagerId`). `Job.HiringManagerId` deferred. Build + 181 tests green.

**Goal:** surface the new columns through the domain + API without changing behavior.
- Add properties to `Department` (`HeadUserId` + navigation), `Job` (`RecruiterId`, optional
  `HiringManagerId`), `Application` (`AssignedRecruiterId`, `AssignedDepartmentHeadId`).
- EF mapping/configuration + a migration matching Phase 1's `init.sql`.
- Extend DTOs/contracts with the read fields in [API-CONTRACT.md](API-CONTRACT.md) §Planned ownership
  fields (e.g. `departmentHeadId/Name`, `recruiterId/Name`, `assignedRecruiterId/Name`,
  `assignedDepartmentHeadId/Name`, `effectiveDepartmentHeadId/Name`).

**Files likely touched:** `RecruitPro.Domain/Entities/{Department,Job,Application}.cs`;
`RecruitPro.Infrastructure` EF config + migration; `RecruitPro.Application/DTOs/**`; mapping profiles.
**Validation:** builds; existing tests still pass; new fields serialize (null where unset).
**Risks:** mapping drift between `init.sql` and EF migration; over-fetching navigations.
**Rollback note:** revert entity/DTO/migration commits; columns remain unused (harmless).

---

## Phase 3 — Job approval guard + application ownership snapshot

> **Status: IMPLEMENTED (2026-06-26).** Apply-time snapshot landed in Phase 1. Phase 3 added the
> authorization guards: job **approve/reject** is scoped to `Department.HeadUserId`
> (`JobService.PatchJobAsync` — 403 otherwise, 422 `DEPARTMENT_HEAD_REQUIRED` when no head, `ApprovedBy`
> = acting user); the **ManagerReview → Interview/Rejected** decision is scoped to the application's
> `AssignedDepartmentHeadId` or `SystemAdmin` (`ApplicationService.UpdateApplicationDecisionAsync`), with
> a Manager-role fallback only when no head was snapshotted. Ownership resolution centralized in
> `IApplicationOwnershipResolver`. The legacy `Manager` role is **not** renamed; `ManagerReview` status
> unchanged (= DepartmentHeadReview).
>
> **Pre-commit hardening (2026-06-26):** the formerly-unauthenticated `PATCH /api/jobs/{id}/status` now
> requires auth and routes through the same `PatchJobAsync` guard (the old `UpdateJobStatusAsync` was
> removed) — no remaining public approval bypass. The `/api/departments` lookup route was preserved
> (moved to `DepartmentController`, same path + payload, now incl. head). Build + **187 tests** green
> (current working tree; not yet committed).

**Goal:** route approval to the DepartmentHead and snapshot owners at apply time.
- Job approval: authorize the approver against `Department.HeadUserId` (+ `HeadDepartment` role),
  falling back to the legacy `Manager` role during migration. Keep `Job.ApprovedBy` as the audit field.
- Apply: compute and persist `AssignedRecruiterId` / `AssignedDepartmentHeadId` using the resolution
  order in [APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md) §2.
- `ManagerReview` ownership becomes the application's `AssignedDepartmentHeadId` (status name unchanged;
  documented as DepartmentHeadReview).

**Files likely touched:** `RecruitPro.Application/Services/{JobService,ApplicationService}.cs`;
`RecruitPro.API/Controllers/JobController.cs` (authorization); `ApplicationStatusWorkflow` (ownership
helpers only, not new statuses).
**Validation:** approval by the correct head allowed; non-head denied; apply snapshots non-null owners;
existing application/apply/withdraw tests still green.
**Risks:** breaking existing `Manager`-based approval for users without a head assignment → keep the
fallback; ensure snapshot is taken inside the apply transaction (before commit).
**Rollback note:** revert guard + snapshot; approval reverts to `Manager` role; apply stops writing
`Assigned*` (columns stay nullable).

---

## Phase 4 — Frontend Department Head assignment + Job create/edit display

**Goal:** make ownership visible/editable in the UI.
- Department screen: show/edit `DepartmentHead` (`headUserId`).
- Job create/edit: select Department + Recruiter; display the resolved DepartmentHead for the chosen
  Department.
- Job detail: show Recruiter and DepartmentHead. Application detail: show Assigned Recruiter and
  Assigned DepartmentHead.
- Continue to route status logic through the centralized `common/status/*` modules (no VI-label logic).

**Phase 4 follow-up — DepartmentHead approval-queue access (DONE):** the prior limitation (the approval
queue/detail were `Manager`-role only, so a `HeadDepartment` user could not reach them) is **fixed**.
Backend: `GET /api/manager/jobs/approval-queue` + `…/{id}/approval-detail` now
`[Authorize(Roles = "Manager,HeadDepartment")]` and **scoped** to `Department.HeadUserId`
(SystemAdmin-only blocked; non-head Manager = empty/403), sharing `EvaluateApprovalAccess` with the submit
guard. Frontend: `JOB_APPROVE` now includes `HeadDepartment`, `JobsRouteScreen` routes it to the queue,
and the side-nav exposes "Duyệt tin tuyển dụng". Route/screen names unchanged for compatibility. Covered
by `ApprovalQueueAccessIntegrationTests` (T-OWN-034..040).

**Files likely touched:** `recruit-pro-internal/src/pages/{hr,manager}/**` (Department, JobCreating,
JobDetail, CandidateReviewDetail), relevant services/DTO types.
**Validation:** `npm run build`, `npx tsc --noEmit`, `npm run lint` clean (no new errors vs baseline);
fields render and persist.
**Risks:** FE/BE role-name casing (`headdepartment` vs `HeadDepartment`); stale lists after edits.
**Rollback note:** revert FE screens; data model unaffected.

---

## Phase 5 — Tests

**Goal:** lock the ownership rules with tests (see [TEST-MATRIX.md](TEST-MATRIX.md) §Planned ownership).
- Backend: department head assignment, HR-creates-job, head-approves, non-head-cannot-approve,
  approved-job-applyable, apply-snapshots-owners, stage ownership (Applied/Screening = HR,
  ManagerReview = DepartmentHead).
- Frontend: verification checklist for the screens in Phase 4.

**Files likely touched:** `RecruitPro.Tests/**`; FE manual/automated verification notes.
**Validation:** `dotnet test` green; planned IDs T-OWN-001…009 implemented.
**Risks:** Testcontainers seed must include a department head; flaky auth setup.
**Rollback note:** tests are additive; revert if needed.

---

## Phase 6 — Notification implementation — ✅ DONE (backend)

**Status:** Backend notification routing is **implemented** — ownership-based recipients, recipient
deduplication, click-ready role-aware deep links (`url`/`targetType`/`targetId`), best-effort
post-commit publishing, and 24 unit tests (`NotificationRoutingTests.cs`, T-NOTI-001…023). All events in
the matrix are emitted from `JobService` / `ApplicationService` / `InterviewService` / `OfferService`.
New constants/helpers: `NotificationEventCodes`, `NotificationTargetTypes`, `NotificationLinks`,
`NotificationTemplates`. The apply event migrated `new_application_received` → `application_applied`
(recruiter-only). The **frontend notification bell/dropdown remains deferred**; records are click-ready.

**Goal:** implement the routing in [NOTIFICATION-EVENT-MATRIX.md](NOTIFICATION-EVENT-MATRIX.md) using
the now-existing ownership fields.
- Add the missing events; switch application-scoped recipients from `Job.CreatedBy`/role membership to
  the snapshotted `AssignedRecruiterId` / `AssignedDepartmentHeadId`; keep best-effort post-commit
  semantics (BR-APPLICATION-005/010).

**Files likely touched:** `RecruitPro.Application/Services/NotificationEventService.cs`; event
publishers in `JobService`/`ApplicationService`; FE notification rendering as needed.
**Validation:** each event fans out to the documented recipients; a notification failure never fails the
business action (existing 500-eradication tests stay green).
**Risks:** duplicate/over-notification; routing when an owner is null (use fallbacks).
**Rollback note:** revert notification publishers; business actions unaffected.

---

## Sequencing & dependencies

```
Phase 1 (DB) → Phase 2 (entities/DTO) → Phase 3 (guard + snapshot)
                                              ↓
                            Phase 4 (FE) and Phase 5 (tests) in parallel
                                              ↓
                                   Phase 6 (notifications)
```

Notifications are **deliberately last** — they depend on the ownership snapshot existing, which is why
this Phase 0 only documents the target routing.
