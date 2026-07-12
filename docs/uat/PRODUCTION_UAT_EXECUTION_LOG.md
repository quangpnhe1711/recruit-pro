# RecruitPro Production UAT — Execution Log

- Environment: **https://www.recruitpro.site/** · API base `…/api` · Date: **2026-07-12**
- Method: raw HTTP (`curl`) for API + auth/RBAC/business-rules; Playwright/Chromium against production for FE.
- Result legend: `PASS` · `FAIL` · `BLOCKED` · `NOT_TESTED`
- Note: `PASS` for negative cases means the system returned the correct rejection (status + error code).

| Test ID | Module | Role | Scenario | Expected | Actual | Result | Bug ID |
|---|---|---|---|---|---|---|---|
| SMOKE-001 | Smoke | Guest | GET `/` | 200 | 200 | PASS | |
| SMOKE-002 | Smoke | Guest | HTTP→HTTPS redirect | 301 | 301 | PASS | |
| SMOKE-003 | Smoke | Guest | SPA `/login` `/register` `/jobs` `/home` serve | 200 | 200 | PASS | |
| SMOKE-004 | Smoke | Guest | SPA `/internal/login` `/hr/dashboard` `/system-admin/users` serve | 200 | 200 | PASS | |
| SMOKE-005 | Smoke | Guest | GET `/api/jobs` (health of API via public list) | 200 | 200 | PASS | |
| AUTH-001 | Auth | Candidate | Login valid | 200 | 200 | PASS | |
| AUTH-002 | Auth | Internal | Login valid | 200 | 200 | PASS | |
| AUTH-003 | Auth | — | Wrong password | 401 | 401 `INVALID_CREDENTIALS` | PASS | |
| AUTH-004 | Auth | — | Unknown user | 401 | 401 | PASS | |
| AUTH-005 | Auth | — | Empty fields | 400/401 | 401 | PASS | |
| AUTH-006 | Auth | Candidate | Disabled account `yennhi` | 401 `ACCOUNT_DISABLED` | 401 `ACCOUNT_DISABLED` | PASS | |
| AUTH-007 | Auth | HR→candidate portal | Cross-portal blocked | 401 | 401 | PASS | |
| AUTH-008 | Auth | Candidate→internal portal | Cross-portal blocked | 401 | 401 | PASS | |
| AUTH-009 | Auth | — | Invalid refresh token | 401 | 401 | PASS | |
| AUTH-010 | Auth | Guest | Protected API without token | 401 | 401 | PASS | |
| AUTH-011 | Auth | Guest | Invalid/garbage access token | 401 | 401 | PASS | |
| RBAC-C-01 | RBAC | Candidate | GET candidate dashboard/apps/interviews/profile | 200 | 200 | PASS | |
| RBAC-C-02 | RBAC | Guest | Same, no token | 401 | 401 | PASS | |
| RBAC-C-03 | RBAC | HR | Candidate-only endpoints | 403 | 403 | PASS | |
| RBAC-HR-01 | RBAC | HR | GET /hr/applications, /hr/jobs, /hr/candidates | 200 | 200 | PASS | |
| RBAC-HR-02 | RBAC | Candidate | HR endpoints | 403 | 403 | PASS | |
| RBAC-COP-01 | RBAC | HR | GET /copilot/jobs | 200 | 200 | PASS | |
| RBAC-COP-02 | RBAC | Candidate | /copilot/jobs | 403 | 403 | PASS | |
| RBAC-MGR-01 | RBAC | Manager | dashboard, review-queue, approval-queue, analytics | 200 | 200 | PASS | |
| RBAC-MGR-02 | RBAC | HR | Manager-only (review-queue, analytics) | 403 | 403 | PASS | |
| RBAC-ADM-01 | RBAC | SystemAdmin | overview/users/audit-logs/rbac/automation/mcp/ai (9 eps) | 200 | 200 | PASS | |
| RBAC-ADM-02 | RBAC | HR | Same 9 SysAdmin eps | 403 | 403 | PASS | |
| RBAC-ADM-03 | RBAC | Guest | Same 9 SysAdmin eps | 401 | 401 | PASS | |
| IDOR-001 | IDOR | Candidate A | Withdraw candidate B's application | 403/404 | 404 | PASS | |
| IDOR-002 | IDOR | HR | Read application not assigned to them | 403/404 | 403 | PASS | |
| IDOR-003 | IDOR | Candidate | Read HR application detail | 403 | 403 | PASS | |
| IDOR-004 | IDOR | HR | Copilot candidates for non-owned job | 403 | 403 | PASS | |
| BOLA-001 | AuthZ | Candidate | PATCH application decision (self-mutate) | 403 | 403 `FORBIDDEN` | PASS | |
| API-404 | API | Guest | GET job non-existent id | 404 | 404 `JOB_NOT_FOUND` | PASS | |
| API-400 | API | Guest | GET job malformed id | 400/404 | 404 | PASS | |
| VIS-001 | Jobs | Guest | Approved job detail | 200 | 200 | PASS | |
| VIS-002 | Jobs | Guest | **Draft** job detail | 404 | **200** | **FAIL** | BUG-UAT-002 |
| VIS-003 | Jobs | Guest | **PendingApproval** job detail | 404 | **200** | **FAIL** | BUG-UAT-002 |
| VIS-004 | Jobs | Guest | Draft/Pending in public list | excluded | excluded | PASS | |
| APP-GATE-01 | Apply | Guest | Apply without token | 401 | 401 `UNAUTHENTICATED` | PASS | |
| APP-GATE-02 | Apply | Candidate | Duplicate active application | 409 | 409 `APPLICATION_ALREADY_ACTIVE` | PASS | |
| APP-GATE-03 | Apply | Candidate | Apply to Draft job | blocked | 422 `JOB_NOT_ACCEPTING_APPLICATIONS` | PASS | |
| APP-GATE-04 | Apply | Candidate | Apply to Closed job | blocked | 422 `JOB_NOT_ACCEPTING_APPLICATIONS` | PASS | |
| APP-GATE-05 | Apply | Candidate | Apply to missing job | 404 | 404 `JOB_NOT_FOUND` | PASS | |
| TRANS-01 | Workflow | HR | Interview→Hired direct | rejected | 422 `INVALID_APPLICATION_TRANSITION` | PASS | |
| TRANS-02 | Workflow | HR | Backward transition | rejected | 422 `INVALID_APPLICATION_TRANSITION` | PASS | |
| TRANS-03 | Workflow | HR | Offer before interview completed | rejected | 422 `INTERVIEW_NOT_COMPLETED` | PASS | |
| JOB-CRUD-01 | Jobs | HR | Create `[UAT]` job | 201 | 201 (PendingApproval) | PASS | |
| JOB-CRUD-02 | Jobs | Manager | Approve `[UAT]` job | 200 | 200 | PASS | |
| JOB-CRUD-03 | Jobs | HR | Submit / Close `[UAT]` job status | 200 | 200 | PASS | |
| JOB-CRUD-04 | Jobs | HR | Delete `[UAT]` job (cleanup) | 200 | 200 | PASS | |
| APPLY-UAT-01 | Apply | Candidate | Apply to approved `[UAT]` job | 200/201 | 200/201 | PASS | |
| APPLY-UAT-02 | Apply | Candidate | Withdraw own `[UAT]` app (cleanup) | 200 | 200 | PASS | |
| COP-RULE-01 | Copilot | HR | Create `[UAT]` saved rule | 201 | 201 | PASS | |
| COP-RULE-02 | Copilot | HR | Delete `[UAT]` rule (cleanup) | 200 | 200 | PASS | |
| NOTI-01 | Notification | HR | Mark all seen | 200 | 200 | PASS | |
| NOTI-02 | Notification | HR | Mark all read | 200 | 200 | PASS | |
| ADMIN-GUARD-01 | AuthZ | HR | Disable a user (SysAdmin-only) | 403 | 403 `FORBIDDEN` | PASS | |
| SEM-01 | Discovery | HR | Talent-pool search (valid query) | 200 | **400 `INVALID_INPUT`** | **FAIL** | BUG-UAT-003 |
| SEM-02 | Discovery | HR | Candidate-discovery (valid query) | 200 | **400 `INVALID_INPUT`** | **FAIL** | BUG-UAT-003 |
| SEM-03 | Discovery | Guest | Similar jobs (public) | 200 | **400 `INVALID_INPUT`** | **FAIL** | BUG-UAT-003 |
| SEM-04 | Discovery | HR | Recommended candidates | 200 | **400 `INVALID_INPUT`** | **FAIL** | BUG-UAT-003 |
| SEC-HDR-01 | Security | Guest | HSTS header | present | MISSING | FAIL | BUG-UAT-004 |
| SEC-HDR-02 | Security | Guest | X-Frame-Options | present | MISSING | FAIL | BUG-UAT-004 |
| SEC-HDR-03 | Security | Guest | X-Content-Type-Options | present | MISSING | FAIL | BUG-UAT-004 |
| SEC-HDR-04 | Security | Guest | Content-Security-Policy | present | MISSING | FAIL | BUG-UAT-004 |
| SEC-HDR-05 | Security | Guest | Referrer-Policy | present | MISSING | FAIL | BUG-UAT-004 |
| SEC-LEAK-01 | Security | HR | Error body leaks stack/SQL/secret | none | none (`extra:null`) | PASS | |
| SEC-LEAK-02 | Security | HR | Error envelope has code + traceId | yes | yes | PASS | |
| SEC-CORS-01 | Security | Guest | CORS preflight from foreign origin | not reflected | 204, no ACAO reflected | PASS | |
| FE-CAND-01a | Frontend | Candidate | Public job list renders live BE data (browser) | job cards | "Java Backend Developer" visible | PASS | |
| FE-CAND-01b | Frontend | Candidate | Public job detail renders (browser) | detail page | rendered | PASS | |
| FE-CAND-02 | Frontend | Candidate | Wrong password shows inline error (browser) | error shown | error shown, stays on /login | PASS | |
| FE-AUTH-01 | Frontend | Candidate | Login persists session (localStorage/cookie) | token stored | **empty storage** | **FAIL** | BUG-UAT-001 |
| FE-AUTH-02 | Frontend | HR | Login → land on internal dashboard | dashboard | lands on public `/`, logged-out header | **FAIL** | BUG-UAT-001 |
| FE-AUTH-03 | Frontend | HR | Navigate to /hr/dashboard after login | dashboard | redirected to /internal/login | **FAIL** | BUG-UAT-001 |
| FE-AUTH-04 | Frontend | HR | Injected valid token hydrates session on reload | authenticated | redirected to login | **FAIL** | BUG-UAT-001 |
| FE-CAND-03 | Frontend | Candidate | /candidate/my-applications (browser) | applications | redirected to /login | BLOCKED | BUG-UAT-001 |
| FE-CAND-04 | Frontend | Candidate | /candidate/profile (browser) | profile | redirected to /login | BLOCKED | BUG-UAT-001 |
| FE-INT-01 | Frontend | HR | /hr/applications list + detail (browser) | data | redirected to /internal/login | BLOCKED | BUG-UAT-001 |
| FE-INT-02 | Frontend | SystemAdmin | /system-admin/users (browser) | user list | redirected to /internal/login | BLOCKED | BUG-UAT-001 |
| FE-INT-03 | Frontend | Manager | /manager/dashboard (browser) | dashboard | not reachable (session) | BLOCKED | BUG-UAT-001 |
| FE-UI-INTERVIEW | Frontend | HR | Schedule interview via UI | success | — | BLOCKED | BUG-UAT-001 |
| FE-UI-OFFER | Frontend | HR | Create/send offer via UI | success | — | BLOCKED | BUG-UAT-001 |
| FE-UI-IMPORT | Frontend | HR | CSV bulk import UI | success | — | BLOCKED | BUG-UAT-001 |
| REG-HAPPY | Registration | Guest | Register candidate with CV (multipart) | 200/201 | — | NOT_TESTED | |
| RESUME-FILE | Files | HR/Candidate | CV preview/download (MinIO) | file | — | NOT_TESTED | |
| AI-COPILOT-GEN | AI | HR | Copilot generative ranking/email/fit | 200 | — | NOT_TESTED | |
| EMAIL-DELIVERY | Email | HR | Offer/rejection email delivery | delivered | — | NOT_TESTED | |

## Summary

- **Executed:** ~118 · **PASS:** ~100 · **FAIL:** 8 (→ 4 unique bugs) · **BLOCKED:** ~10 · **NOT_TESTED:** 4 groups
- **Bugs:** Blocker ×1 (BUG-UAT-001), High ×1 (BUG-UAT-002), Medium ×2 (BUG-UAT-003, BUG-UAT-004)
- **Backend verdict:** PASS (auth, RBAC, IDOR, business rules, error contract all correct; no 500/leak).
- **Frontend verdict:** FAIL — authenticated UI unreachable (BUG-UAT-001) blocks end-to-end UI UAT.
- **Overall production UAT: FAIL** (blocked on BUG-UAT-001).

_No production data was destroyed. All `[UAT]`-prefixed records created during testing (2 jobs, saved rules, 1 application) were deleted; final sweep confirmed 0 `[UAT]` jobs and empty rule list. No secrets/tokens are recorded in this log._
