# API Contract — Application Domain

All responses use the `ApiResponse<T>` envelope ([ERROR-CONTRACT.md](ERROR-CONTRACT.md)). Controller:
`ApplicationController`. Frontend client: `src/services/http/api-client.ts` + `endpoints.ts`.

> **Status-field canonicality (binding).** Every workflow `status` field (application, job, interview,
> offer) is the **canonical English enum value** across DB / API / frontend logic — `status =
> entity.Status.ToString()`. Localized (Vietnamese) text is presentation-only and is returned in a
> **separate** field (`statusLabel` / `displayStatus`), never in `status`. The frontend must branch on
> the canonical `status` and must never parse a localized label as status, nor fall back to `Rejected`
> for an unknown value (unknown → neutral `Unknown`). `ManagerReview` is the canonical code value (= the
> **DepartmentHeadReview** business stage); presentation may show "Head Review" but the enum is **not**
> renamed.

---

## GET /api/jobs/{jobId}/apply-context

- **Auth:** required, role `Candidate`.
- **Purpose:** drives the Apply screen — job summary, candidate profile, current resume, eligibility.
- **Response 200 `data.eligibility`:**
  ```json
  {
    "canApply": true,
    "alreadyApplied": false,
    "existingApplicationId": "…|null",
    "existingApplicationStatus": "Withdrawn|Applied|…|null",
    "blockers": ["…"],
    "guidanceMessage": "…"
  }
  ```
  - `alreadyApplied` is true **only when an active application exists** (BR-APPLICATION-001).
  - After withdrawal: `canApply: true`, `alreadyApplied: false`, `existingApplicationStatus: "Withdrawn"`.
- **Errors:** 401 (no/!candidate), 404 (job not found).
- **FE consumer:** `ApplyJobScreen.tsx` (Apply button enabled iff `canApply`).
- **Tests:** `GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied`,
  `ApplicationController_ApplyContext_Should_Require_Candidate…`.

## POST /api/jobs/{jobId}/apply

- **Auth:** required, role `Candidate`.
- **Request:** `{ "coverLetter": "string|null" }`.
- **Response 201:** `{ applicationId, status, ruleScore, semanticScore, finalScore, scoreStatus }`.
- **Errors:**
  - 409 — active application already exists (`Candidate already applied for this job.`).
  - 422 — job not open / deadline passed / missing profile / missing resume / prior `Hired` for the
    same job (terminal, INV-015; target behavior per DL-007).
  - 404 — job not found.
  - 401 — unauthenticated / not candidate.
  - **No 500 for known business outcomes; side-effect failures do not fail the request.**
- **FE consumer:** `ApplyJobScreen.tsx` → `jobsService.applyToJob`.
- **Tests:** `ApplyAsync_*` (regression suite), `TEST-E2E-APPLICATION-001`.

## POST /api/candidate/applications/{applicationId}/withdraw

- **Auth:** required, role `Candidate`; **ownership enforced**.
- **Response 200:** `"Đã rút đơn ứng tuyển."`; application transitions to `Withdrawn`.
- **Errors:** 422 (not withdrawable), 404 (not found / not owned).
- **FE consumer:** `MyApplicationScreen.tsx` → `candidateService.withdrawApplication`, then `loadData()`.
- **Tests:** `WithdrawApplicationAsync_*`, `TEST-E2E-APPLICATION-001`.

## GET /api/candidate/applications

- **Auth:** required, role `Candidate`.
- **Response 200:** paginated candidate applications. Status fields are **canonical** (status-contract):
  - `status` — the **canonical English** `ApplicationStatus` enum value (`Applied`, `Screening`,
    `ManagerReview`, `Interview`, `Offer`, `Hired`, `Rejected`, `OfferDeclined`, `Withdrawn`). This is
    the business/logic field — **never** localized text.
  - `statusLabel` — localized (Vietnamese) **display** label for `status` (e.g. `Screening` →
    `HR đang sàng lọc`, `Withdrawn` → `Đã rút đơn`). Presentation only; must not drive FE logic.
  - `nextStep` — localized guidance text (Vietnamese).
  - `availableActions` — stable action keys (`viewDetail`, `withdraw` when withdrawable,
    `acceptOffer`/`declineOffer` when an offer is pending).
- **FE consumer:** `MyApplicationScreen.tsx` — badges render via
  `getApplicationStatusPresentation(status)` (canonical key → English label; `ManagerReview` →
  `Head Review`; unknown → neutral `Unknown`, never `Rejected`). The FE branches on canonical `status`
  only, never on `statusLabel`.

## POST /api/candidate/applications/{applicationId}/accept-offer · /decline-offer

- **Auth:** `Candidate`, ownership enforced. Offer must be `Sent`.
- **Accept** → `Hired`; **Decline** → `OfferDeclined`. Errors 422/404 per [STATE-MACHINE.md](STATE-MACHINE.md).

## HR/Manager endpoints (summary)

| Method & path | Auth | Notes |
|---|---|---|
| GET /api/hr/applications | HR, Manager | paged list; filter by status incl. `withdrawn` |
| GET /api/hr/applications/{id} | HR, Manager | review detail; 404 if invalid id |
| PATCH /api/hr/applications/{id}/decision | HR, Manager | workflow-validated transition (BR-APPLICATION-006). **ManagerReview → Interview/Rejected** is scoped to the application's assigned DepartmentHead or SystemAdmin (BR-OWN-007); else 403. Manager-role fallback only when no head was snapshotted. |
| GET /api/hr/applications/{id}/cv | HR, Manager | 404 if no CV |
| POST /api/hr/applications/{id}/send-email | HR, Manager | composes candidate email |
| GET /api/manager/applications/review-queue | Manager | ManagerReview queue |
| GET /api/manager/jobs/approval-queue | Manager, HeadDepartment, SystemAdmin | job approval queue — **scoped** to `Department.HeadUserId` (SystemAdmin = all; non-head Manager = empty) (BR-OWN-003) |
| GET /api/manager/jobs/{id}/approval-detail | Manager, HeadDepartment, SystemAdmin | job approval detail — head/SystemAdmin only: no head → **422** `DEPARTMENT_HEAD_REQUIRED`; not head/admin → **403** `FORBIDDEN` |
| GET /api/jobs/{jobId}/applications[/recent] | (see controller) | job applications |

Candidate (`Candidate`) hitting HR endpoints → **403** `Bạn không có quyền`.

---

## Ownership fields

> **Status (updated 2026-06-26, Phase 2/3):** the data model **and** its API surface are now
> **implemented**. Ownership fields are exposed on the Department, Job, and HR/internal Application
> responses below; `init.sql` columns + EF mappings back them. `Job.HiringManagerId` is **deferred** —
> the effective head is `Department.HeadUserId ?? Job.ApprovedBy`. Notification routing remains
> **Phase 6, not implemented**. **Frontend (Phase 4) is implemented**, including DepartmentHead-scoped
> access to the approval queue/detail.

**Department response fields (implemented)** — `GET /api/departments`, `GET /api/departments/{id}`,
`PUT /api/departments/{id}`:

```
id, name, description
headUserId, headUserName, headUserEmail   // null when no head assigned
```

**Job response fields (implemented)** — HR detail `GET /api/hr/jobs/{id}` and list `GET /api/hr/jobs`:

```
recruiterId, recruiterName, recruiterEmail
departmentHeadId, departmentHeadName, departmentHeadEmail     // from Department.HeadUser
effectiveDepartmentHeadId, effectiveDepartmentHeadName, ...   // Department.HeadUser ?? ApprovedBy user
createdBy, createdByName        // audit
approvedBy, approvedByName      // audit (decision actor)
```
(The HR list carries the id+name subset incl. `effectiveDepartmentHead*`.)

**Application response fields (implemented)** — HR/internal endpoints (`GET /api/hr/applications`,
`GET /api/hr/applications/{id}`, `GET /api/manager/applications/review-queue`):

```
assignedRecruiterId, assignedRecruiterName, assignedRecruiterEmail
assignedDepartmentHeadId, assignedDepartmentHeadName, assignedDepartmentHeadEmail
```

The implemented field names are `assignedRecruiter*` / `assignedDepartmentHead*` (the legacy
`assignedManagerId` name was **not** used). `createdBy`/`approvedBy` remain **audit** fields, not the
long-term owners (BR-OWN-002/003). Candidate-facing endpoints do **not** expose these owner fields.

## Assignable recruitment owners

## GET /api/users/assignable-recruitment-owners

- **Auth:** `HR`, `HeadDepartment`, `SystemAdmin`.
- **Response 200:** `{ recruiters: [{ id, fullName, email }], departmentHeads: [{ id, fullName, email }] }`.
  `recruiters` = HR-role users; `departmentHeads` = HeadDepartment-role users. **Candidates never appear.**
- **Tests:** `OwnershipServiceIntegrationTests.AssignableRecruitmentOwners_*` (T-OWN-012/013).

## Department endpoints

| Method & path | Auth | Notes |
|---|---|---|
| GET /api/departments | (public) | lookup; now includes `headUser*` |
| GET /api/departments/{id} | HR, Manager, HeadDepartment, SystemAdmin | department detail incl. head |
| PUT /api/departments/{id} | HR, Manager, HeadDepartment, SystemAdmin | set `headUserId` (+ name/description); head must be a HeadDepartment/SystemAdmin user (else 422 `INVALID_DEPARTMENT_HEAD`) |

## Job approval (authorization, BR-OWN-003)

`PATCH /api/hr/jobs/{id}`, `PATCH /api/hr/jobs/{id}/status`, **and `PATCH /api/jobs/{id}/status`** now
scope the **Approved/Rejected** transition to the job's **DepartmentHead (`Department.HeadUserId`)** or a
**SystemAdmin**. The formerly-public `PATCH /api/jobs/{id}/status` is now an **authenticated alias**
(`HR,Manager,HeadDepartment,SystemAdmin`) routed through the same guard — no auth → **401**:
- not the head / not SystemAdmin → **403** `FORBIDDEN`;
- department has no head → **422** `DEPARTMENT_HEAD_REQUIRED`.
On approve/reject, `Job.ApprovedBy` is set to the acting user (decision-actor audit). Other field edits
and non-approval status moves remain available to HR/Manager. `POST /api/hr/jobs` requires a valid
department and accepts `recruiterId` (falls back to the creating user when omitted).

**Approval queue/detail access (Phase 4).** `GET /api/manager/jobs/approval-queue` and
`…/{id}/approval-detail` admit `Manager,HeadDepartment,SystemAdmin` and are **scoped server-side** so the
DepartmentHead is the approval workflow role without needing the generic `Manager` role: the queue only
returns pending jobs of the department(s) the caller heads (`Department.HeadUserId`), a SystemAdmin sees
all, and a non-head Manager sees an empty queue. The detail uses the **same** `EvaluateApprovalAccess`
predicate as the submit guard (422 no head → 403 not head/admin). Route names keep the `manager` prefix
for compatibility.

These ownership fields back the planned notification routing in
[NOTIFICATION-EVENT-MATRIX.md](NOTIFICATION-EVENT-MATRIX.md) and the snapshot logic in
[APPLICATION-OWNERSHIP-FLOW.md](APPLICATION-OWNERSHIP-FLOW.md).

## Application decision & email-gated Offer/Reject (BR-WF-001…005)

| Method & path | Auth | Notes |
|---|---|---|
| PATCH /api/hr/applications/{id}/decision | HR, Manager | Forward stage moves only: `Applied→Screening`, `Screening→ManagerReview` (stamps `departmentHeadReviewRequestedAt`), `ManagerReview→Interview` (head/SystemAdmin guard). A target of `Offer`→422 `EMAIL_REQUIRED_FOR_OFFER`; `Rejected`→422 `EMAIL_REQUIRED_FOR_REJECTION`. |
| POST /api/hr/applications/{id}/offer/send | HR, Manager | Offer **email** flow. From `Interview` requires a completed interview (422 `INTERVIEW_REQUIRED`/`INTERVIEW_NOT_COMPLETED`); sends the offer email then transitions `Interview→Offer` (offer `Sent`). Send failure → 422 `EMAIL_SEND_FAILED`, no transition. |
| PUT /api/hr/applications/{id}/offer | HR, Manager | Save offer **draft** — does **not** transition the application (stays `Interview`). |
| POST /api/hr/applications/{id}/rejection-email | HR, Manager | Rejection **email** flow. Requires non-empty `{ subject, body }` (422 `EMAIL_REQUIRED_FOR_REJECTION`); from `Interview` requires a completed interview; `ManagerReview→Rejected` keeps the head guard. Sends the email then transitions to `Rejected` and cancels pending interviews. Returns the refreshed `ApplicationReviewDetailDto`. |

**Manager review queue / review detail DTOs** now expose `departmentHeadReviewRequestedAt`
(`DateTime?`, nullable for legacy rows). The Manager/DepartmentHead UI shows this as the
"received for review" date, falling back to `appliedDate` only when null. Example:

```json
{ "appliedDate": "2026-06-01T09:55:00", "departmentHeadReviewRequestedAt": "2026-06-03T10:15:00" }
```

> Email is recorded via `IEmailService` (`SendOfferEmailAsync` / `SendRejectionEmailAsync`); the local/dev
> implementation logs, tests use a fake. Candidate-facing **notification** dispatch is **Planned (Phase 6)**.

## AI Copilot candidate pool (known quirk)

`GET /api/copilot/jobs/{id}/candidates` (`CopilotCandidatePoolDto`) returns each candidate's **`education`
as a raw string** — the profile's `EducationRecordsJson` (a JSON array of objects), or the legacy plain
`Education` string, or null (`CopilotRepository.GetCandidatePoolAsync`). It is **not** structured in the
DTO. The frontend therefore **normalizes it before display and never renders raw JSON** (parses to
`{ school, degree, fieldOfStudy, startYear, endYear }`, plain-text fallback for legacy values, `[]` on
malformed JSON). `skills` is a `string[]`; the AI `score` comes from the ranking result's `totalScore` and
is **absent until the candidate is ranked** (FE shows "Chưa chấm"). See the FE normalizers in
`src/common/utils/aiRankingPresentation.ts` and `docs/testing/e2e-ai-ranking-checklist.md`. The backend
contract was intentionally left unchanged in this UI pass (lower risk than a DTO/migration change).
