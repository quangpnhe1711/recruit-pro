# API Contract — Application Domain

All responses use the `ApiResponse<T>` envelope ([ERROR-CONTRACT.md](ERROR-CONTRACT.md)). Controller:
`ApplicationController`. Frontend client: `src/services/http/api-client.ts` + `endpoints.ts`.

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
  - 422 — job not open / deadline passed / missing profile / missing resume.
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
- **Response 200:** paginated candidate applications with localized `status`, `nextStep`,
  `availableActions` (`viewDetail`, `withdraw` when withdrawable, `acceptOffer`/`declineOffer` when
  offer pending). Withdrawn applications report status `Đã rút đơn`.
- **FE consumer:** `MyApplicationScreen.tsx`; status rendered via `applicationPresentation.ts`
  (`withdrawn` → neutral badge).

## POST /api/candidate/applications/{applicationId}/accept-offer · /decline-offer

- **Auth:** `Candidate`, ownership enforced. Offer must be `Sent`.
- **Accept** → `Hired`; **Decline** → `OfferDeclined`. Errors 422/404 per [STATE-MACHINE.md](STATE-MACHINE.md).

## HR/Manager endpoints (summary)

| Method & path | Auth | Notes |
|---|---|---|
| GET /api/hr/applications | HR, Manager | paged list; filter by status incl. `withdrawn` |
| GET /api/hr/applications/{id} | HR, Manager | review detail; 404 if invalid id |
| PATCH /api/hr/applications/{id}/decision | HR, Manager | workflow-validated transition (BR-APPLICATION-006) |
| GET /api/hr/applications/{id}/cv | HR, Manager | 404 if no CV |
| POST /api/hr/applications/{id}/send-email | HR, Manager | composes candidate email |
| GET /api/manager/applications/review-queue | Manager | ManagerReview queue |
| GET /api/jobs/{jobId}/applications[/recent] | (see controller) | job applications |

Candidate (`Candidate`) hitting HR endpoints → **403** `Bạn không có quyền`.
