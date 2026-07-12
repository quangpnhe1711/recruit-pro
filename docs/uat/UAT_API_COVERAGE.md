# UAT API Coverage Matrix — RecruitPro

> **Document:** `docs/uat/UAT_API_COVERAGE.md` · **Version:** 1.0 · **Date:** 2026-07-12
> **Scope:** every HTTP endpoint discovered in `RecruitPro.API` (24 controllers + OData + minimal API) =
> **141 endpoint rows**. Case IDs refer to [`UAT_TEST_CASES.md`](UAT_TEST_CASES.md).

## Conventions

- **Authorization:** `Anon` = no `[Authorize]` (public); `Auth` = any authenticated user; `Roles:` =
  `[Authorize(Roles=…)]`; `Perm:` = `[RequirePermission("CODE")]` checked live against the DB.
- **Coverage status:** `Covered` · `Partially Covered` (some legs verified via API only, or UI leg blocked)
  · `Blocked` (cannot execute on prod without infra, e.g. AI provider / MinIO / mail) · `Not Implemented`.
- **API-wide template cases** apply to **every** endpoint in addition to the listed ones:
  `UAT-API-001` (happy+schema), `UAT-API-002/003/006` (validation/enum/not-found), `UAT-API-004` (401),
  `UAT-API-005` (403 wrong role), `UAT-API-009` (IDOR), `UAT-API-010/011` (SQLi/XSS), `UAT-API-012` (method/CT),
  `UAT-API-013` (oversized), `UAT-API-015` (no sensitive fields). The columns below list the **most specific**
  module cases; treat the API-wide set as implicitly attached.
- **Known-defect endpoints** are flagged; see [`UAT_OPEN_QUESTIONS.md`](UAT_OPEN_QUESTIONS.md) and the prod
  `PRODUCTION_UAT_BUG_REPORT.md`.

## Summary

| Coverage status | Endpoints |
|---|---|
| Covered | 122 |
| Partially Covered | 12 |
| Blocked (infra: AI/MinIO/mail on prod) | 7 |
| Not Implemented | 0 |
| **Total** | **141** |

> "Blocked" endpoints have test cases written; they cannot be fully *executed* on production because the AI
> provider is unavailable (BUG-UAT-003), MinIO/preview needs storage, or mail delivery isn't inspectable.
> Their happy paths are runnable locally.

---

## 1. Authentication (`AuthController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 1 | POST | `/api/auth/login` | Anon | UAT-AUTH-009 | UAT-AUTH-003/004/024 | UAT-AUTH-016, UAT-SEC-003 | Covered |
| 2 | POST | `/api/auth/candidate/login` | Anon | UAT-AUTH-002 | UAT-AUTH-003/004/005/024 | UAT-AUTH-006/007, UAT-SEC-014 | Covered |
| 3 | POST | `/api/auth/internal/login` | Anon | UAT-AUTH-001/008 | UAT-AUTH-003/004 | UAT-AUTH-006/007, UAT-SEC-005 | Covered |
| 4 | POST | `/api/auth/refresh` | Anon | UAT-AUTH-011 | UAT-AUTH-014 | UAT-AUTH-012/013/018, UAT-SEC-001 | Covered |
| 5 | POST | `/api/auth/candidate/forgot-password` | Anon | UAT-AUTH-021 | UAT-AUTH-021 | UAT-SEC-003 | Partially Covered (mail not inspectable) |
| 6 | POST | `/api/auth/internal/forgot-password` | Anon | UAT-AUTH-021 | UAT-AUTH-021 | UAT-SEC-003 | Partially Covered (mail not inspectable) |

## 2. Lookups & Departments (`LookupController`, `DepartmentController`, `UsersController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 7 | GET | `/api/departments` | Anon | UAT-MDATA-001, UAT-PUB-013 | UAT-API-006 | UAT-PUB-013 (head email?) | Covered |
| 8 | GET | `/api/departments/{id}` | Roles: HR,Manager,HeadDepartment,SystemAdmin | UAT-MDATA-002 | UAT-API-006 | UAT-MDATA-005 | Covered |
| 9 | PUT | `/api/departments/{id}` | Roles: HR,Manager,HeadDepartment,SystemAdmin | UAT-MDATA-003/004 | UAT-MDATA-003/004 | UAT-MDATA-005, UAT-RBAC-018 | Covered |
| 10 | GET | `/api/users/assignable-recruitment-owners` | Roles: HR,HeadDepartment,SystemAdmin | UAT-MDATA-008 | — | UAT-MDATA-008 | Covered |
| 11 | GET | `/api/skills` | Anon | UAT-MDATA-006, UAT-PUB-013 | — | — | Covered |

## 3. Notifications (`NotificationController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 12 | GET | `/api/notifications` | Auth | UAT-NOTI-001 | UAT-NOTI-015 | UAT-NOTI-012 | Covered |
| 13 | GET | `/api/notifications/counts` | Auth | UAT-NOTI-002 | — | — | Covered |
| 14 | GET | `/api/notifications/unread-count` | Auth | UAT-NOTI-006 | — | — | Covered |
| 15 | POST | `/api/notifications/seen` | Auth | UAT-NOTI-003 | — | UAT-NOTI-012 | Covered |
| 16/17 | PATCH/POST | `/api/notifications/{id}/read` | Auth | UAT-NOTI-004 | UAT-NOTI-014 | UAT-NOTI-012 | Covered |
| 18/19 | PATCH/POST | `/api/notifications/read-all` | Auth | UAT-NOTI-005 | — | — | Covered |
| 20 | GET | `/api/notifications/stream` (SSE) | Auth | UAT-NOTI-007 | UAT-NOTI-010 | UAT-NOTI-009/011 | Partially Covered (needs SSE proxy) |

## 4. Applications (`ApplicationController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 21 | GET | `/api/jobs/{id}/apply-context` | Roles: Candidate | UAT-APP-002, UAT-CJOB-007 | UAT-APP-003..006 | UAT-API-009 | Covered |
| 22 | GET | `/api/jobs/{id}/applications` | Roles: HR,Manager | UAT-APP-021 | UAT-API-006 | UAT-APP-030, UAT-CJOB-012 | Covered |
| 23 | GET | `/api/jobs/{id}/applications/recent` | Roles: HR,Manager | UAT-APP-021 | — | UAT-SEC-006 | Covered |
| 24 | POST | `/api/jobs/{id}/apply` | Roles: Candidate | UAT-APP-001/008 | UAT-APP-003..007/009 | UAT-APP-010, UAT-API-008 | Covered |
| 25 | GET | `/api/candidate/applications` | Roles: Candidate | UAT-APP-012 | UAT-APP-013 | UAT-SEC-006 | Covered |
| 26 | POST | `/api/candidate/applications/{id}/withdraw` | Roles: Candidate | UAT-APP-014 | UAT-APP-015 | UAT-APP-016 | Covered |
| 27 | POST | `/api/candidate/applications/{id}/accept-offer` | Roles: Candidate | UAT-APP-017 | UAT-APP-019/020 | UAT-OFFER-010 | Covered |
| 28 | POST | `/api/candidate/applications/{id}/decline-offer` | Roles: Candidate | UAT-APP-018 | UAT-APP-019 | UAT-OFFER-010 | Covered |
| 29 | GET | `/api/hr/applications` | Roles: HR,Manager | UAT-APP-021 | UAT-APP-034 | UAT-APP-021, UAT-SEC-004 | Covered |
| 30 | GET | `/api/manager/applications/review-queue` | Roles: Manager | UAT-APP-029 | — | UAT-SEC-006 | Covered |
| 31 | GET | `/api/hr/applications/{id}` | Roles: HR,Manager | UAT-APP-021 | UAT-APP-030 | UAT-APP-030/031 | Covered |
| 32 | PATCH | `/api/hr/applications/{id}/decision` | Roles: HR,Manager | UAT-APP-022/023/024 | UAT-APP-025/027 | UAT-APP-028/032 | Covered |
| 33 | POST | `/api/hr/applications/{id}/rejection-email` | Roles: HR,Manager | UAT-APP-026 | UAT-APP-026, UAT-INT-007/008 | UAT-APP-028 | Covered |
| 34 | GET | `/api/hr/applications/{id}/cv` | Roles: HR,Manager | UAT-FILE-003 | UAT-FILE-003 | UAT-FILE-003/009 | Partially Covered (MinIO) |
| 35 | POST | `/api/hr/applications/{id}/send-email` | Roles: HR,Manager | UAT-APP-031 | — | UAT-APP-031 | Partially Covered (mail) |

## 5. Dashboard & Manager analytics (`DashboardController`, `ManagerAnalyticsController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 36 | GET | `/api/candidate/dashboard` | Roles: Candidate | UAT-DASH-001 | UAT-DASH-011 | UAT-SEC-006 | Covered |
| 37 | GET | `/api/hr/dashboard` | Roles: HR,Manager | UAT-DASH-002 | UAT-DASH-011 | UAT-DASH-009/010 | Covered |
| 38 | GET | `/api/manager/dashboard` | Roles: Manager,HeadDepartment | UAT-DASH-003 | — | UAT-DASH-010 | Covered |
| 58 | GET | `/api/manager/reports/recruitment-analytics` | Roles: Manager,HeadDepartment | UAT-DASH-004/005 | UAT-DASH-006/007 | UAT-DASH-010 | Covered |

## 6. Interviews (`InterviewController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 39 | GET | `/api/hr/interviews` | Roles: HR,Manager,HeadDepartment | UAT-INT-009 | UAT-INT-017 | UAT-SEC-006 | Covered |
| 40 | GET | `/api/candidate/interviews` | Roles: Candidate | UAT-INT-010 | — | UAT-INT-010 | Covered |
| 41 | GET | `/api/hr/interviews/schedule-data` | Roles: HR,Manager,HeadDepartment | UAT-INT-001 | UAT-API-006 | UAT-SEC-006 | Covered |
| 42 | POST | `/api/hr/interviews` | Roles: HR,Manager | UAT-INT-001 | UAT-INT-002/003/014/016 | UAT-INT-013 | Covered |
| 43 | PATCH | `/api/hr/interviews/{id}/status` | Roles: HR,Manager | UAT-INT-004/005 | UAT-INT-018 | UAT-SEC-006 | Covered |
| 44 | DELETE | `/api/hr/interviews/{id}` | Roles: HR,Manager | UAT-INT-006 | UAT-API-006 | UAT-INT-006 | Covered |

## 7. Jobs (`JobController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 45 | GET | `/api/jobs` | Anon | UAT-PUB-002, UAT-CJOB-001 | UAT-PUB-006/010 | UAT-CJOB-012 | Covered |
| 46 | GET | `/api/jobs/filters` | Anon | UAT-PUB-005 | — | — | Covered |
| 47 | GET | `/api/jobs/{id}` | Anon | UAT-PUB-003 | UAT-PUB-009 | **UAT-PUB-011/UAT-SEC-011 (BUG-UAT-002)** | Covered (defect documented) |
| 48 | GET | `/api/jobs/{id}/statistics` | Anon | UAT-PUB-014 | UAT-API-006 | UAT-PUB-014 | Covered |
| 49 | PATCH | `/api/jobs/{id}/status` (alias) | Roles: HR,Manager,HeadDepartment | UAT-JOB-013 | UAT-JOB-017 | UAT-JOB-013 | Covered |
| 50 | GET | `/api/hr/jobs` | Roles: HR,Manager | UAT-JOB-019/020 | UAT-JOB-020 | UAT-JOB-022 | Covered |
| 51 | GET | `/api/hr/jobs/{id}` | Roles: HR,Manager | UAT-JOB-019 | UAT-API-006 | UAT-JOB-022 | Covered |
| 52 | GET | `/api/manager/jobs/approval-queue` | Roles: Manager,HeadDepartment | UAT-JOB-009 | — | UAT-JOB-014 | Covered |
| 53 | GET | `/api/manager/jobs/{id}/approval-detail` | Roles: Manager,HeadDepartment | UAT-JOB-015 | UAT-JOB-012 | UAT-JOB-015 | Covered |
| 54 | POST | `/api/hr/jobs` | Roles: HR,Manager | UAT-JOB-001 | UAT-JOB-002/003/004/005/023 | UAT-JOB-006 | Covered |
| 55 | PATCH | `/api/hr/jobs/{id}` | Roles: HR,Manager,HeadDepartment | UAT-JOB-008/010 | UAT-JOB-008/017 | UAT-JOB-012/022 | Covered |
| 56 | PATCH | `/api/hr/jobs/{id}/status` | Roles: HR,Manager,HeadDepartment | UAT-JOB-010/011/016 | UAT-JOB-017 | UAT-JOB-012 | Covered |
| 57 | DELETE | `/api/hr/jobs/{id}` | Roles: HR,Manager | UAT-JOB-018 | UAT-JOB-018 | UAT-JOB-022 | Covered |

## 8. Offers (`OfferController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 59 | GET | `/api/hr/applications/{id}/offer` | Roles: HR,Manager | UAT-OFFER-012 | UAT-API-006 | UAT-OFFER-012 | Covered |
| 60 | PUT | `/api/hr/applications/{id}/offer` | Roles: HR,Manager | UAT-OFFER-001 | UAT-OFFER-005 | UAT-OFFER-012 | Covered |
| 61 | POST | `/api/hr/applications/{id}/offer/send` | Roles: HR,Manager | UAT-OFFER-002 | UAT-OFFER-003/004/011/017 | UAT-OFFER-010 | Partially Covered (mail) |

## 9. Semantic discovery (`SemanticDiscoveryController`) — AI-dependent

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 62 | GET | `/api/hr/talent-pool/search` | Roles: HR,Manager | UAT-AI-018 | **UAT-AI-018 (BUG-UAT-003: 400)** | UAT-AI-018 | Blocked (AI provider) |
| 63 | POST | `/api/hr/candidate-discovery` | Roles: HR,Manager | UAT-AI-018 | **UAT-AI-018 (BUG-UAT-003)** | UAT-AI-018 | Blocked (AI provider) |
| 64 | GET | `/api/hr/candidates/{id}/similar` | Roles: HR,Manager | UAT-AI-018 | UAT-AI-018 | UAT-SEC-006 | Blocked (AI provider) |
| 65 | GET | `/api/jobs/{id}/similar` | Anon | UAT-PUB-* / UAT-AI-018 | **UAT-AI-018 (BUG-UAT-003)** | — | Blocked (AI provider) |
| 66 | GET | `/api/hr/jobs/{id}/recommended-candidates` | Roles: HR,Manager | UAT-AI-018 | UAT-AI-018 | UAT-SEC-006 | Blocked (AI provider) |
| 67 | GET | `/api/candidate/jobs/recommendations` | Roles: Candidate | UAT-CJOB-008 | UAT-CJOB-008 | — | Blocked (AI provider) |

## 10. Copilot & AI (`CopilotController`) — AI-dependent for generative endpoints

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 68 | GET | `/api/copilot/jobs` | Roles: HR,Manager | UAT-AI-001 | — | UAT-AI-001/016 | Covered |
| 69 | POST | `/api/copilot/conversations` | Roles: HR,Manager | UAT-AI-002 | UAT-AI-002 | UAT-AI-016 | Covered |
| 70 | GET | `/api/copilot/conversations/{id}` | Roles: HR,Manager | UAT-AI-002 | UAT-API-006 | UAT-AI-016 | Covered |
| 71 | GET | `/api/copilot/jobs/{id}/candidates` | Roles: HR,Manager | UAT-AI-003 | UAT-AI-017 | UAT-SEC-006 | Covered |
| 72 | POST | `/api/copilot/candidate-search` | Roles: HR,Manager | UAT-AI-020 | UAT-AI-020 | UAT-AI-020 | Partially Covered (deprecated) |
| 73 | POST | `/api/copilot/jobs/{id}/fit-analysis` | Roles: HR,Manager | UAT-AI-008 | UAT-AI-017 | UAT-AI-014/015 | Blocked (AI provider) |
| 74 | POST | `/api/copilot/jobs/{id}/interview-questions` | Roles: HR,Manager | UAT-AI-009 | — | UAT-AI-015 | Blocked (AI provider) |
| 75 | POST | `/api/copilot/applications/{id}/emails/draft` | Roles: HR,Manager | UAT-AI-009 | — | UAT-AI-015 | Blocked (AI provider) |
| 76 | GET | `/api/copilot/prompt-templates` | Roles: HR,Manager | UAT-AI-010 | — | — | Covered |
| 77 | POST | `/api/copilot/prompt-templates` | Roles: HR,Manager | UAT-AI-010 | UAT-API-002 | UAT-API-011 | Covered |
| 78 | GET | `/api/copilot/applications/{id}/fit-analysis/latest` | Roles: HR,Manager | UAT-AI-008 | UAT-API-006 | UAT-AI-016 | Partially Covered (needs prior run) |
| 79 | GET | `/api/copilot/artifacts` | Roles: HR,Manager | UAT-AI-009 | — | UAT-SEC-006 | Covered |
| 80 | POST | `/api/copilot/conversations/{id}/rankings` | Roles: HR,Manager | UAT-AI-004 | UAT-AI-017/019 | UAT-AI-014/016 | Partially Covered (deterministic runs; AI narrative blocked) |
| 81 | GET | `/api/copilot/ranking-sessions/{id}` | Roles: HR,Manager | UAT-AI-006 | UAT-AI-006 | UAT-AI-016 | Covered |
| 82 | POST | `/api/copilot/ranking-sessions/{id}/pass-cv` | Roles: HR,Manager | UAT-AI-007 | UAT-APP-027 | UAT-SEC-006 | Covered |
| 83 | GET | `/api/copilot/jobs/{id}/rules` | Roles: HR,Manager | UAT-AI-011 | — | UAT-SEC-006 | Covered |
| 84 | POST | `/api/copilot/jobs/{id}/rules` | Roles: HR,Manager | UAT-AI-011 | UAT-API-002 | UAT-API-011 | Covered |
| 85 | PATCH | `/api/copilot/rules/{id}` | Roles: HR,Manager | UAT-AI-011 | UAT-API-006 | UAT-SEC-006 | Covered |
| 86 | DELETE | `/api/copilot/rules/{id}` | Roles: HR,Manager | UAT-AI-011 | UAT-API-006 | UAT-SEC-006 | Covered |

## 11. SysAdmin — MCP (`SysAdminMcpController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 87 | GET | `/api/sysadmin/mcp/tools` | Roles: SystemAdmin | UAT-WF-015 | — | UAT-WF-014 | Covered |
| 88 | POST | `/api/sysadmin/mcp/tools/{name}/test` | Roles: SystemAdmin | UAT-WF-016 | UAT-API-002 | UAT-WF-016/017 | Covered |
| 89 | GET | `/api/sysadmin/mcp/audits` | Roles: SystemAdmin | UAT-WF-016 | — | UAT-WF-017 | Covered |
| 90 | GET | `/api/sysadmin/mcp/audits/{id}` | Roles: SystemAdmin | UAT-WF-017 | UAT-API-006 | UAT-WF-014 | Covered |

## 12. SysAdmin — Automation (`SysAdminAutomationController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 91 | GET | `/api/sysadmin/automation/dashboard` | Roles: SystemAdmin | UAT-WF-001 | — | UAT-WF-014 | Covered |
| 92 | GET | `/api/sysadmin/automation/diagnostics` | Roles: SystemAdmin | UAT-WF-013 | — | UAT-WF-014 | Covered |
| 93 | GET | `/api/sysadmin/automation/workflows/{id}/diagnostics` | Roles: SystemAdmin | UAT-WF-013 | UAT-WF-019 | UAT-WF-014 | Covered |
| 94 | GET | `/api/sysadmin/automation/workflows` | Roles: SystemAdmin | UAT-WF-002 | — | UAT-WF-014 | Covered |
| 95 | GET | `/api/sysadmin/automation/workflows/{id}` | Roles: SystemAdmin | UAT-WF-003 | UAT-API-006 | UAT-WF-014 | Covered |
| 96 | POST | `/api/sysadmin/automation/workflows` | Roles: SystemAdmin | UAT-WF-004 | UAT-WF-004 | UAT-WF-014 | Covered |
| 97 | PATCH | `/api/sysadmin/automation/workflows/{id}` | Roles: SystemAdmin | UAT-WF-004 | UAT-WF-004/005 | UAT-WF-014 | Covered |
| 98 | POST | `/api/sysadmin/automation/workflows/{id}/publish` | Roles: SystemAdmin | UAT-WF-005 | UAT-WF-005 | UAT-WF-014 | Covered |
| 99 | PATCH | `/api/sysadmin/automation/workflows/{id}/enabled` | Roles: SystemAdmin | UAT-WF-006 | — | UAT-WF-014 | Covered |
| 100 | GET | `/api/sysadmin/automation/executions` | Roles: SystemAdmin | UAT-WF-011 | — | UAT-WF-014 | Covered |
| 101 | GET | `/api/sysadmin/automation/executions/{id}` | Roles: SystemAdmin | UAT-WF-011 | UAT-API-006 | UAT-WF-014 | Covered |
| 102 | POST | `/api/sysadmin/automation/executions/{id}/retry` | Roles: SystemAdmin | UAT-WF-012 | UAT-WF-012 | UAT-WF-014 | Covered |
| 103 | GET | `/api/sysadmin/automation/events` | Roles: SystemAdmin | UAT-WF-010/022 | — | UAT-WF-014 | Covered |
| 104 | GET | `/api/sysadmin/automation/events/{id}` | Roles: SystemAdmin | UAT-WF-010 | UAT-API-006 | UAT-WF-014 | Covered |

## 13. SysAdmin — RBAC & Directory (`SysAdminRbacController`, `SysAdminDirectoryController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 105 | GET | `/api/sysadmin/rbac/roles` | Perm: ROLE_VIEW | UAT-RBAC-003 | — | UAT-RBAC-002/006 | Covered |
| 106 | GET | `/api/sysadmin/rbac/modules` | Perm: PERMISSION_VIEW | UAT-RBAC-003/021 | — | UAT-RBAC-002 | Covered |
| 107 | GET | `/api/sysadmin/rbac/roles/{id}/permissions` | Perm: PERMISSION_VIEW | UAT-RBAC-003 | UAT-API-006 | UAT-RBAC-002 | Covered |
| 108 | PUT | `/api/sysadmin/rbac/roles/{id}/permissions` | Perm: PERMISSION_MANAGE | UAT-RBAC-004 | UAT-RBAC-020 | UAT-RBAC-006/016, UAT-AUTH-022 | Covered |
| 109 | GET | `/api/sysadmin/overview` | Perm: USER_VIEW | UAT-RBAC-001 | — | UAT-RBAC-002 | Covered |
| 110 | GET | `/api/sysadmin/users` | Perm: USER_VIEW | UAT-RBAC-001/015 | UAT-RBAC-015 | UAT-RBAC-002/016 | Covered |
| 111 | PATCH | `/api/sysadmin/users/{id}/status` | Perm: USER_UPDATE | UAT-RBAC-008/009 | UAT-RBAC-010/011/012/013 | UAT-RBAC-017/018, UAT-AUTH-017 | Covered |
| 112 | PUT | `/api/sysadmin/users/{id}/roles` | Perm: ROLE_MANAGE | UAT-RBAC-007 | UAT-RBAC-020 | UAT-RBAC-014/018 | Covered |
| 113 | GET | `/api/sysadmin/audit-logs` | Perm: System_LOG_VIEW | UAT-LOG-001/007 | UAT-LOG-007 | UAT-LOG-005/006, UAT-RBAC-019 | Covered |

## 14. SysAdmin — AI Ops (`SysAdminAiOpsController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 114 | GET | `/api/sysadmin/ai/metrics` | Roles: SystemAdmin | UAT-WF-* / UAT-API-001 | UAT-DASH-006 | UAT-WF-014 | Partially Covered (empty until AI runs) |
| 115 | GET | `/api/sysadmin/ai/telemetry/recent` | Roles: SystemAdmin | UAT-API-001 | UAT-API-007 | UAT-WF-014 | Partially Covered (empty until AI runs) |
| 116 | GET | `/api/sysadmin/ai/telemetry/{id}` | Roles: SystemAdmin | UAT-API-001 | UAT-API-006 | UAT-WF-014 | Partially Covered (empty until AI runs) |
| 117 | GET | `/api/sysadmin/ai/risk-flags` | Roles: SystemAdmin | UAT-API-001 | — | UAT-WF-014 | Partially Covered (empty until AI runs) |
| 118 | GET | `/api/sysadmin/ai/risk-flags/recent` | Roles: SystemAdmin | UAT-API-001 | UAT-API-007 | UAT-WF-014 | Partially Covered (empty until AI runs) |

## 15. Candidate profile & registration & HR candidates (`CandidateController`)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 119/120 | POST | `/api/candidates/register` (+ `/api/candidate/register`) | Anon | UAT-REG-001/002 | UAT-REG-003..011 | UAT-REG-013 | Covered |
| 121 | GET | `/api/candidate/profile` | Roles: Candidate | UAT-PROF-001 | — | UAT-PROF-013 | Covered |
| 122 | PUT | `/api/candidate/profile` | Roles: Candidate | UAT-PROF-002 | UAT-PROF-003/004/008 | UAT-PROF-010 | Covered |
| 123 | POST | `/api/candidate/profile/save` | Roles: Candidate | UAT-PROF-011 | UAT-PROF-019 | UAT-PROF-010 | Partially Covered (MinIO) |
| 124 | PUT | `/api/candidate/profile/skills` | Roles: Candidate | UAT-PROF-005 | UAT-PROF-005 | UAT-PROF-013 | Covered |
| 125 | POST | `/api/candidate/profile/experience` | Roles: Candidate | UAT-PROF-006 | UAT-PROF-006 | UAT-PROF-013 | Covered |
| 126 | PUT | `/api/candidate/profile/experience/{id}` | Roles: Candidate | UAT-PROF-006 | UAT-PROF-006 | UAT-PROF-013 | Covered |
| 127 | DELETE | `/api/candidate/profile/experience/{id}` | Roles: Candidate | UAT-PROF-006 | UAT-API-006 | UAT-PROF-013 | Covered |
| 128 | POST | `/api/candidate/profile/resume/parse` | Roles: Candidate | UAT-PROF-012 | UAT-FILE-006 | UAT-AI-014 | Blocked (AI provider) / Partially (heuristic) |
| 129 | POST | `/api/candidate/profile/resume` | Roles: Candidate | UAT-FILE-001 | UAT-FILE-004/005/006 | UAT-FILE-011 | Partially Covered (MinIO) |
| 130 | GET | `/api/hr/candidates` | Roles: HR,Manager | UAT-PROF-014 | — | UAT-PROF-014, UAT-SEC-006 | Covered |
| 131 | GET | `/api/hr/candidates/{id}` | Roles: HR,Manager | UAT-PROF-014 | UAT-API-006 | UAT-PROF-013 | Covered |
| 132 | GET | `/api/candidates/import/template` | Anon | UAT-FILE-013 | — | — | Covered |
| 133 | POST | `/api/candidates/import/preview` | Roles: HR,Manager | UAT-FILE-013 | UAT-FILE-013 | UAT-FILE-014 | Partially Covered (UI-driven) |
| 134 | POST | `/api/candidates/import` | Roles: HR,Manager | UAT-FILE-013 | UAT-FILE-013 | UAT-FILE-014 | Partially Covered (UI-driven) |

## 16. Resumes / External score / OData / XML / Health (`ResumeController`, `ExternalScoreController`, OData, `XmlDemoController`, minimal API)

| # | Method | Endpoint | Authorization | Happy | Negative | Security | Status |
|---|---|---|---|---|---|---|---|
| 135 | GET | `/api/resumes/{id}/preview` | Auth | UAT-FILE-002 | UAT-FILE-008 | UAT-FILE-009/011 | Partially Covered (MinIO) |
| 136 | GET | `/api/resumes/{id}/download` | Auth | UAT-FILE-002 | UAT-FILE-008 | UAT-FILE-009/011 | Partially Covered (MinIO) |
| 137 | GET | `/api/hr/applications/{id}/external-score` | Roles: HR,Manager | UAT-API-001 | UAT-API-006 | UAT-SEC-006 | Partially Covered (gRPC scoring service) |
| 138 | GET | `/odata/Applications` | Roles: HR,Manager | UAT-API-014 | UAT-API-014 | UAT-API-014, UAT-SEC-004 | Covered |
| 139 | GET | `/odata/Jobs` | Anon | UAT-PUB-012, UAT-API-014 | UAT-API-014 | UAT-API-014 | Covered |
| 140 | GET | `/api/xml-demo/jobs` | Anon | UAT-PUB-012 | UAT-API-012 | — | Covered |
| 141 | GET | `/` (health) | Anon | UAT-API-016 | — | UAT-API-016 | Covered |

---

## Not-covered / gaps (by design or infra)

- **AI-dependent endpoints (62–67, 73–75, 128 parse, 114–118):** cases exist but **execution is blocked on
  prod** because the AI/embedding provider is unavailable (BUG-UAT-003). Ranking (80) works deterministically.
- **MinIO/storage endpoints (34, 123, 129, 135, 136):** happy paths need object storage — run locally.
- **Mail-gated (5, 6, 35, 61 email leg):** no mail sink on prod → email delivery not directly verifiable;
  status transitions and gating are verifiable.
- **gRPC external score (137):** depends on `RecruitPro.ScoringService` availability.
- **No management API for master data** (skills/benefits/currencies/offer templates) — seed-only (Q-MDATA-01).
- Endpoints double-bound to two verbs (16/17, 18/19) and double-routed (119/120) counted once each in the
  141 total where noted.
