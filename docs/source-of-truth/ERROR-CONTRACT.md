# Error Contract

## Response envelope

Every API response uses `ApiResponse<T>` (RecruitPro.Application.DTOs.Response):

```json
{
  "success": false,
  "statusCode": 409,
  "message": "Candidate already applied for this job.",
  "data": null,
  "errors": { "field": ["..."] },
  "extra": null
}
```

- `success` — boolean.
- `statusCode` — mirrors the HTTP status; controllers return `StatusCode(result.StatusCode, result)`.
- `message` — human-readable, localized (Vietnamese for candidate-facing flows).
- `errors` — present for validation failures (field → messages).
- `extra` — diagnostic payload; for 500 in non-development it is limited to `{ traceId }`.

> **Update:** the envelope now carries an optional stable machine `errorCode`
> (`ApiResponse.ErrorCode`; constants in `RecruitPro.Application.Common.ErrorCodes`, mirrored on the FE
> in `src/common/utils/apiError.ts`). It is populated for the application/apply/withdraw/offer/
> interview domain and the cross-cutting validation/auth paths; rollout to every remaining endpoint is
> incremental. Frontend precedence: **errorCode → HTTP status → localized message**. Codes:
> `APPLICATION_ALREADY_ACTIVE`, `APPLICATION_ALREADY_HIRED`, `JOB_NOT_ACCEPTING_APPLICATIONS`,
> `JOB_DEADLINE_PASSED`, `CANDIDATE_PROFILE_INCOMPLETE`, `RESUME_REQUIRED`, `APPLICATION_NOT_FOUND`,
> `APPLICATION_NOT_WITHDRAWABLE`, `INVALID_APPLICATION_TRANSITION`, `INTERVIEW_NOT_ACTIONABLE`,
> `OFFER_NOT_ACTIONABLE`, `UNAUTHENTICATED`, `FORBIDDEN`, `VALIDATION_ERROR`.

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
| Invalid reviewer transition | 400 | `Invalid transition from {a} to {b}.` | `UpdateApplicationDecisionAsync` |
| Notification / async-scoring side-effect failure after commit | (no error) | apply still returns 201 | `ApplyAsync` try/catch |

## 500 eradication

The middleware (`ExceptionMiddleware`) maps `BaseException` (400/401/403/404/409/422) and FluentValidation
errors (400) to the envelope; only truly unhandled exceptions become 500. Known application-domain
business failures are returned as the 4xx codes above — verified by `TEST-E2E-APPLICATION-001` and the
`ApiIntegrationTests` (`AssertNo500AndEnvelopeAsync`).
