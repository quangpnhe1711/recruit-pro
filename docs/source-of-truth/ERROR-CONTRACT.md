# Error Contract

## Response envelope (code-first contract)

Every API response uses `ApiResponse<T>` (RecruitPro.Application.DTOs.Response). Error responses now carry
a nested, self-describing `error` block **in addition to** the legacy flat fields (kept for back-compat):

```json
{
  "success": false,
  "statusCode": 409,
  "message": "Bạn đang có một đơn ứng tuyển còn hiệu lực cho vị trí này.",
  "errorCode": "APPLICATION_ALREADY_ACTIVE",
  "errors": { "email": ["Email này đã được sử dụng."] },
  "data": null,
  "extra": null,
  "error": {
    "type": "CONFLICT",
    "code": "APPLICATION_ALREADY_ACTIVE",
    "message": "Bạn đang có một đơn ứng tuyển còn hiệu lực cho vị trí này.",
    "fieldErrors": [
      { "field": "email", "code": "EMAIL_ALREADY_EXISTS", "message": "Email này đã được sử dụng.", "params": {} }
    ],
    "globalErrors": [],
    "traceId": "..."
  }
}
```

- `success` — boolean.
- `statusCode` — mirrors the HTTP status; controllers return `StatusCode(result.StatusCode, result)`.
- `errorCode` / `message` / `errors` — legacy flat fields, kept so existing consumers keep working.
- `error` — **the code-first block the web frontend reads.** `type` is derived from the HTTP status
  (`VALIDATION_ERROR`/`AUTH_ERROR`/`FORBIDDEN`/`NOT_FOUND`/`CONFLICT`/`BUSINESS_ERROR`/`SERVER_ERROR`).
  `code` is a stable machine code; `fieldErrors[].field` is camelCase matching the FE form; `params` feed
  dynamic message interpolation on the FE.
- `message` (both flat and nested) — a **debug-friendly payload** for F12/Postman/logs/non-web clients.
  **The web UI never renders it** — it maps `code` (+ `params`) to its own i18n `errors.*` dictionary.
- `extra` — diagnostic payload; for 500 in non-development it is limited to `{ traceId }`.

### How the message is produced (never hardcoded)

Controllers/services set only a **code** (+ optional params/field errors) via the code-first
`ApiResponse<T>` factories (`Fail`/`BadRequest`/`NotFound`/`Conflict`/`UnprocessableEntity`/`Forbidden`/
`Unauthorized`/`Error`/`ValidationError`) or by throwing `BusinessAppException(code, status, params, fieldErrors)`.
The human message is resolved **centrally** from the code by `IErrorMessageProvider` (`ErrorMessageProvider`,
a single VI catalog) at the boundary:
- **Thrown** exceptions → `ExceptionMiddleware` (also maps FluentValidation failures to `fieldErrors` with
  rule-derived codes + placeholder params, and 500s to a safe `SERVER_ERROR`).
- **Returned** error `ApiResponse` → `ErrorEnvelopeResultFilter` (an `IAlwaysRunResultFilter`).
- JWT challenge/forbidden responses (written outside MVC) resolve inline in `JwtExtension`.

Codes live in `RecruitPro.Application.Common.ErrorCodes`, mirrored on the FE in
`src/common/utils/apiError.ts` (`ERROR_CODES`) and copy in the i18n `errors.*` namespace (vi/en).

> **Follow-up (optional precision):** ~30 previously-hardcoded messages were mapped to the closest
> generic code (`INVALID_INPUT`, `ENTITY_NOT_FOUND`, `BUSINESS_RULE_VIOLATION`). They are functionally
> correct; add dedicated codes (e.g. `SEARCH_QUERY_REQUIRED`, `INTERVIEWER_NOT_FOUND`,
> `WORKFLOW_TRIGGER_INVALID`, `RANKING_SESSION_NOT_FOUND`, `EXECUTION_NOT_RETRYABLE`) only if the UI needs
> to distinguish them.

> **Update:** the envelope now carries an optional stable machine `errorCode`
> (`ApiResponse.ErrorCode`; constants in `RecruitPro.Application.Common.ErrorCodes`, mirrored on the FE
> in `src/common/utils/apiError.ts`). It is populated for the application/apply/withdraw/offer/
> interview domain and the cross-cutting validation/auth paths; rollout to every remaining endpoint is
> incremental. Frontend precedence: **errorCode → HTTP status → localized message**. Codes:
> `APPLICATION_ALREADY_ACTIVE`, `APPLICATION_ALREADY_HIRED`, `JOB_NOT_ACCEPTING_APPLICATIONS`,
> `JOB_DEADLINE_PASSED`, `CANDIDATE_PROFILE_INCOMPLETE`, `RESUME_REQUIRED`, `APPLICATION_NOT_FOUND`,
> `APPLICATION_NOT_WITHDRAWABLE`, `INVALID_APPLICATION_TRANSITION`, `INTERVIEW_NOT_ACTIONABLE`,
> `OFFER_NOT_ACTIONABLE`, `UNAUTHENTICATED`, `FORBIDDEN`, `VALIDATION_ERROR`.
>
> **Ownership / job-approval (Phase 2/3):** `DEPARTMENT_HEAD_REQUIRED`, `INVALID_DEPARTMENT_HEAD`,
> `JOB_RECRUITER_REQUIRED`, `INVALID_JOB_RECRUITER`, `INVALID_JOB_TRANSITION`, `DEPARTMENT_NOT_FOUND`.
> (`JOB_RECRUITER_REQUIRED`/`INVALID_JOB_TRANSITION` are reserved constants; not all are emitted yet.)
>
> **Workflow correctness (Interview → Offer/Reject):** `INTERVIEW_REQUIRED`, `INTERVIEW_NOT_COMPLETED`,
> `EMAIL_REQUIRED_FOR_OFFER`, `EMAIL_REQUIRED_FOR_REJECTION`, `EMAIL_SEND_FAILED`. The Offer and Rejected
> transitions are email-gated and (from the Interview stage) require a scheduled + completed interview —
> see the rows below and BUSINESS-RULES.md (BR-WF-001..005).

## HTTP status semantics (binding)

| Status | Meaning | Examples |
|---|---|---|
| 200 | Success | apply-context, lists, withdraw success |
| 201 | Created | apply success |
| 400 | Malformed request / bad input shape | invalid body, invalid status string |
| 401 | Unauthenticated | missing/expired token → `Không có quyền truy cập` |
| 403 | Authenticated but forbidden | candidate hitting HR endpoint → `Bạn không có quyền` |
| 404 | Resource not found / not owned | unknown job/application; another user's application |
| 409 | Conflict | **duplicate of an active application** |
| 422 | Valid shape, invalid business state | job not open/expired, missing resume, non-withdrawable state |
| 500 | Unexpected infrastructure failure ONLY | never a known business outcome |

## Application-domain error mapping (authoritative)

| Condition | Status | Message | Source |
|---|---|---|---|
| Apply, active application exists | 409 | `Candidate already applied for this job.` | `ApplyAsync` |
| Apply, prior `Hired` for same job (terminal, INV-015) | 422 | `You have already been hired for this job.` (`errorCode: APPLICATION_ALREADY_HIRED`) | `BuildApplyEligibility` |
| Apply, job not `Approved` | 422 | `This job posting is not accepting new applications.` | `BuildApplyEligibility` |
| Apply, deadline passed | 422 | `The application deadline for this job has passed.` | `BuildApplyEligibility` |
| Apply, profile missing contact | 422 | `Your profile is missing required contact information.` | `BuildApplyEligibility` |
| Apply, no current resume | 422 | `Please upload your latest resume before applying.` | `BuildApplyEligibility` |
| Withdraw, not in withdrawable state | 422 | `This application can no longer be withdrawn.` | `WithdrawApplicationAsync` |
| Withdraw/any, application not found or not owned | 404 | `Không tìm thấy hồ sơ ứng tuyển.` | `GetTrackedApplicationForCandidateAsync` |
| Apply/withdraw, job id not a GUID or missing | 404 | `Job with ID {id} not found.` | `GetJobAsync` |
| Invalid reviewer transition | 422 | `Invalid transition from {a} to {b}.` (`INVALID_APPLICATION_TRANSITION`) | `UpdateApplicationDecisionAsync` / `SendRejectionEmailAsync` |
| Direct decision to Offer (status dropdown) | 422 | `Sending an offer requires the offer email flow…` (`EMAIL_REQUIRED_FOR_OFFER`) | `UpdateApplicationDecisionAsync` |
| Direct decision to Rejected (status dropdown) | 422 | `Rejecting an application requires the rejection email flow…` (`EMAIL_REQUIRED_FOR_REJECTION`) | `UpdateApplicationDecisionAsync` |
| Offer/Reject from Interview, no interview scheduled | 422 | `Schedule an interview before deciding the outcome.` (`INTERVIEW_REQUIRED`) | `OfferService.SendOfferAsync` / `SendRejectionEmailAsync` |
| Offer/Reject from Interview, interview not completed | 422 | `Complete the interview before sending an offer or rejection.` (`INTERVIEW_NOT_COMPLETED`) | `OfferService.SendOfferAsync` / `SendRejectionEmailAsync` |
| Rejection email missing subject/body | 422 | `A rejection email requires both a subject and a body.` (`EMAIL_REQUIRED_FOR_REJECTION`) | `SendRejectionEmailAsync` |
| Email send failed (Offer/Reject) | 422 | `…could not be sent; the application was not …` (`EMAIL_SEND_FAILED`) — status NOT changed | `OfferService.SendOfferAsync` / `SendRejectionEmailAsync` |
| Approve/reject a job, department has no head | 422 | `This job's department has no head assigned…` (`DEPARTMENT_HEAD_REQUIRED`) | `JobService.PatchJobAsync` guard (BR-OWN-003) |
| Approve/reject a job, actor is not the department head | 403 | `Only the department head can approve or reject this job.` (`FORBIDDEN`) | endpoint role authorization + `JobService.PatchJobAsync` guard |
| Advance ManagerReview, actor is not the assigned head/SystemAdmin | 403 | `Only the assigned department head or a system administrator can advance this application from manager review.` (`FORBIDDEN`) | `ApplicationService.UpdateApplicationDecisionAsync` guard (BR-OWN-007) |
| Create job, no valid department | 422 | `A valid department is required to create a job.` (`DEPARTMENT_NOT_FOUND`) | `JobService.CreateJobAsync` |
| Create job, recruiter not an existing HR user | 422 | `The selected recruiter must be an existing user with the HR role.` (`INVALID_JOB_RECRUITER`) | `JobService.CreateJobAsync` |
| Update department head, not an existing HeadDepartment/SystemAdmin user | 422 | `The selected department head must be an existing user with the HeadDepartment role.` (`INVALID_DEPARTMENT_HEAD`) | `JobService.UpdateDepartmentAsync` |
| Notification / async-scoring side-effect failure after commit | (no error) | apply still returns 201 | `ApplyAsync` try/catch |

## 500 eradication

The middleware (`ExceptionMiddleware`) maps `BaseException` (400/401/403/404/409/422) and FluentValidation
errors (400) to the envelope; only truly unhandled exceptions become 500. Known application-domain
business failures are returned as the 4xx codes above — verified by `TEST-E2E-APPLICATION-001` and the
`ApiIntegrationTests` (`AssertNo500AndEnvelopeAsync`).

## Notifications (Phase 6)

Notification publishing introduces **no new error codes**. Publishing is best-effort and post-commit:
a publishing failure is logged and swallowed at the call site, so it never changes a business endpoint's
HTTP status (no 500, no rollback). Offer/Rejection email gating keeps its existing `EMAIL_SEND_FAILED`
behavior — the in-app notification is only emitted after the email send succeeds.
