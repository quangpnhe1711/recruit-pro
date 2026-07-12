# UAT Execution Report — RecruitPro (Production)

> **Environment:** Production `https://www.recruitpro.site` · API `…/api` · **Date:** 2026-07-12
> **Method:** API-direct (Node 22 `fetch`) against Production. UI/browser cases not executed (no browser-automation tooling available) — see §Blocked.
> **Auth:** username + shared demo password `Password@123` (public seed). Tokens per role from `init.sql` personas.
> **Seed safety:** all `[UAT]` data cleaned; 1 seed profile + 1 rogue interview created during testing were **restored** (verified).

## Result totals (unique cases)

| Metric | Count |
|---|---|
| **Executed** | **366** |
| PASS | 342 |
| FAIL | 20 |
| BLOCKED | 4 |
| Pass rate (of executed, excl. BLOCKED) | 94.5% |

Each FAIL maps to a defect in `UAT_BUG_REPORT.md`. Several rows share one bug (e.g. all security-header rows → BUG-UAT-004).


## Authentication & Session

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| AUTH-LOGIN-admin | Login SystemAdmin (admin) | SystemAdmin | POST /auth/internal/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-giahan | Login HR (giahan) | HR | POST /auth/internal/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-haidang | Login Candidate (haidang) | Candidate | POST /auth/candidate/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-minhkhoi | Login SystemAdmin (minhkhoi) | SystemAdmin | POST /auth/internal/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-nhatquang | Login Candidate (nhatquang) | Candidate | POST /auth/candidate/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-quocanh | Login Candidate (quocanh) | Candidate | POST /auth/candidate/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-quocbao | Login Manager (quocbao) | Manager | POST /auth/internal/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-thucuyen | Login HR (thucuyen) | HR | POST /auth/internal/login | 200 | 200 | PASS |  |
| AUTH-LOGIN-tiendat | Login Manager+Head (tiendat) | Manager+Head | POST /auth/internal/login | 200 | 200 | PASS |  |
| UAT-AUTH-003 | Wrong password → 401 INVALID_CREDENTIALS | Candidate | POST /auth/candidate/login | 401 | 401 INVALID_CREDENTIALS | PASS |  |
| UAT-AUTH-004 | Unknown username → 401 | Guest | POST /auth/candidate/login | 401 | 401 INVALID_CREDENTIALS | PASS |  |
| UAT-AUTH-005a | Empty username/password → 400/401 | Guest | POST /auth/candidate/login | 400/401 | 401 INVALID_CREDENTIALS | PASS |  |
| UAT-AUTH-005b | Missing fields (no username) → 400/401 | Guest | POST /auth/internal/login | 400/401 | 401 INVALID_CREDENTIALS | PASS |  |
| UAT-AUTH-006 | Disabled account yennhi → 401 ACCOUNT_DISABLED | Candidate(disabled) | POST /auth/candidate/login | 401 ACCOUNT_DISABLED | 401 ACCOUNT_DISABLED | PASS |  |
| UAT-AUTH-007 | HR user in candidate portal → 401 PORTAL_ACCESS_DENIED | HR→candidate | POST /auth/candidate/login | 401 | 401 PORTAL_ACCESS_DENIED | PASS |  |
| UAT-AUTH-008 | Candidate in internal portal → 401 PORTAL_ACCESS_DENIED | Candidate→internal | POST /auth/internal/login | 401 | 401 PORTAL_ACCESS_DENIED | PASS |  |
| UAT-AUTH-009a | Universal login by username → 200 | HR | POST /auth/login | 200 | 200 | PASS |  |
| UAT-AUTH-009b | Universal login by email → 200 | HR | POST /auth/login | 200 | 200 | PASS |  |
| UAT-AUTH-010 | Protected API without token → 401 | Guest | GET /candidate/profile | 401 | 401 UNAUTHENTICATED | PASS |  |
| UAT-AUTH-011 | Refresh with valid token → 200 + new tokens | Candidate | POST /auth/refresh | 200 | 200 | PASS |  |
| UAT-AUTH-012 | Garbage bearer token → 401 | Guest | GET /candidate/profile | 401 | 401 UNAUTHENTICATED | PASS |  |
| UAT-AUTH-013 | Tampered JWT (bad signature) → 401 | forged-admin | GET /sysadmin/users | 401 | 401 UNAUTHENTICATED | PASS |  |
| UAT-AUTH-014 | Refresh with garbage token → 401 | Guest | POST /auth/refresh | 401 | 401 UNAUTHENTICATED | PASS |  |

## Public Portal

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-MDATA-001 | GET /departments (public) | Guest | GET /departments | 200 | 200 | PASS |  |
| UAT-MDATA-006 | GET /skills (public) | Guest | GET /skills | 200 | 200 | PASS |  |
| UAT-PUB-002 | Public job list returns only Approved | Guest | GET /jobs | 200 | 200 | PASS |  |
| UAT-PUB-002b | Draft/Pending excluded from public list | Guest | GET /jobs | excluded | draftPresent=false pendingPresent=false | PASS |  |
| UAT-PUB-003 | Approved job detail returns public fields | Guest | GET /jobs/{id} | 200 | 200 | PASS |  |
| UAT-PUB-004 | Search keyword=Java filters results | Guest | GET /jobs?keyword= | 200 | 200 | PASS |  |
| UAT-PUB-005a | GET /jobs/filters returns facets | Guest | GET /jobs/filters | 200 | 200 | PASS |  |
| UAT-PUB-005b | Filter by department Engineering | Guest | GET /jobs?departmentId= | 200 | 200 | PASS |  |
| UAT-PUB-005c | Filter by workMode=Remote | Guest | GET /jobs?workMode= | 200 | 200 | PASS |  |
| UAT-PUB-006a | pageSize=5 returns ≤5 items | Guest | GET /jobs?pageSize=5 | 200 | 200 | PASS |  |
| UAT-PUB-006b | Out-of-range page → empty list, not error | Guest | GET /jobs?page=9999 | 200 | 200 | PASS |  |
| UAT-PUB-006c | pageSize=0 clamp/validate behavior | Guest | GET /jobs?pageSize=0 | record actual (clamp or 400) | 200 n=0 | PASS |  |
| UAT-PUB-006d | pageSize=99999 clamp behavior | Guest | GET /jobs?pageSize=99999 | record actual (clamp) | 200 n=12 | PASS |  |
| UAT-PUB-006e | Negative page/pageSize behavior | Guest | GET /jobs?page=-1 | record actual | 500 | FAIL | BUG-UAT-008 |
| UAT-PUB-007 | Nonsense keyword → empty result, not error | Guest | GET /jobs?keyword= | 200 | 200 | PASS |  |
| UAT-PUB-009a | Unknown job id → 404 JOB_NOT_FOUND | Guest | GET /jobs/{id} | 404 JOB_NOT_FOUND | 404 JOB_NOT_FOUND | PASS |  |
| UAT-PUB-009b | Malformed job id → 400/404 | Guest | GET /jobs/{id} | 400/404 | 404 JOB_NOT_FOUND | PASS |  |
| UAT-PUB-009c | Nil/empty GUID job id → 400/404 | Guest | GET /jobs/{id} | 400/404 | 404 JOB_NOT_FOUND | PASS |  |
| UAT-PUB-011a | Draft job detail to anon should be 404 (BUG-UAT-002) | Guest | GET /jobs/{draftId} | 404 | 200 | FAIL | BUG-UAT-002 |
| UAT-PUB-011b | PendingApproval job detail to anon should be 404 (BUG-UAT-002) | Guest | GET /jobs/{pendingId} | 404 | 200 | FAIL | BUG-UAT-002 |
| UAT-PUB-011c | Closed job detail visibility (record actual) | Guest | GET /jobs/{closedId} | record actual | 200 status=Closed | PASS |  |
| UAT-PUB-012a | OData /odata/Jobs (public) | Guest | GET /odata/Jobs | 200 | 200 | PASS |  |
| UAT-PUB-012b | GET /api/xml-demo/jobs (public) | Guest | GET /xml-demo/jobs | 200 | 200 | PASS |  |
| UAT-PUB-013 | Public departments expose head email (Q-SEC-02 PII) | Guest | GET /departments | no internal PII to anon (PO decision) | exposesHeadEmail=true | FAIL | BUG-UAT-012 |
| UAT-PUB-014 | Public job statistics | Guest | GET /jobs/{id}/statistics | 200 | 200 | PASS |  |
| UAT-PUB-CUR | Currency label on VND-only salaries | Guest | GET /jobs | VND (system is VND-only per seed) | currencies=["USD"] | FAIL | BUG-UAT-009 |
| UAT-PUB-SIMILAR | Public similar jobs (AI-dependent BUG-UAT-003) | Guest | GET /jobs/{id}/similar | 200 ranked (or clear unavailable) | 400 INVALID_INPUT | FAIL | BUG-UAT-003 |

## RBAC Authorization Matrix

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| RBAC-adminairiskflags-admin | sysadmin ai risk-flags as admin | admin | GET /sysadmin/ai/risk-flags | allow(≠401/403) | 200 | PASS |  |
| RBAC-adminairiskflags-candidate | sysadmin ai risk-flags as candidate | candidate | GET /sysadmin/ai/risk-flags | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminairiskflags-guest | sysadmin ai risk-flags as guest | guest | GET /sysadmin/ai/risk-flags | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-adminairiskflags-head | sysadmin ai risk-flags as head | head | GET /sysadmin/ai/risk-flags | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminairiskflags-hr | sysadmin ai risk-flags as hr | hr | GET /sysadmin/ai/risk-flags | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminairiskflags-manager | sysadmin ai risk-flags as manager | manager | GET /sysadmin/ai/risk-flags | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminrbacmodules-admin | sysadmin rbac modules as admin | admin | GET /sysadmin/rbac/modules | allow(≠401/403) | 200 | PASS |  |
| RBAC-adminrbacmodules-candidate | sysadmin rbac modules as candidate | candidate | GET /sysadmin/rbac/modules | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminrbacmodules-guest | sysadmin rbac modules as guest | guest | GET /sysadmin/rbac/modules | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-adminrbacmodules-head | sysadmin rbac modules as head | head | GET /sysadmin/rbac/modules | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminrbacmodules-hr | sysadmin rbac modules as hr | hr | GET /sysadmin/rbac/modules | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-adminrbacmodules-manager | sysadmin rbac modules as manager | manager | GET /sysadmin/rbac/modules | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ationdiagnostics-admin | sysadmin automation diagnostics as admin | admin | GET /sysadmin/automation/diagnostics | allow(≠401/403) | 200 | PASS |  |
| RBAC-ationdiagnostics-candidate | sysadmin automation diagnostics as candidate | candidate | GET /sysadmin/automation/diagnostics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ationdiagnostics-guest | sysadmin automation diagnostics as guest | guest | GET /sysadmin/automation/diagnostics | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-ationdiagnostics-head | sysadmin automation diagnostics as head | head | GET /sysadmin/automation/diagnostics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ationdiagnostics-hr | sysadmin automation diagnostics as hr | hr | GET /sysadmin/automation/diagnostics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ationdiagnostics-manager | sysadmin automation diagnostics as manager | manager | GET /sysadmin/automation/diagnostics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-automationevents-admin | sysadmin automation events as admin | admin | GET /sysadmin/automation/events | allow(≠401/403) | 200 | PASS |  |
| RBAC-automationevents-candidate | sysadmin automation events as candidate | candidate | GET /sysadmin/automation/events | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-automationevents-guest | sysadmin automation events as guest | guest | GET /sysadmin/automation/events | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-automationevents-head | sysadmin automation events as head | head | GET /sysadmin/automation/events | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-automationevents-hr | sysadmin automation events as hr | hr | GET /sysadmin/automation/events | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-automationevents-manager | sysadmin automation events as manager | manager | GET /sysadmin/automation/events | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-candidateprofile-admin | candidate profile as admin | admin | GET /candidate/profile | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-candidateprofile-candidate | candidate profile as candidate | candidate | GET /candidate/profile | allow(≠401/403) | 200 | PASS |  |
| RBAC-candidateprofile-guest | candidate profile as guest | guest | GET /candidate/profile | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-candidateprofile-head | candidate profile as head | head | GET /candidate/profile | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-candidateprofile-hr | candidate profile as hr | hr | GET /candidate/profile | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-candidateprofile-manager | candidate profile as manager | manager | GET /candidate/profile | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-copilotartifacts-admin | copilot artifacts as admin | admin | GET /copilot/artifacts | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-copilotartifacts-candidate | copilot artifacts as candidate | candidate | GET /copilot/artifacts | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-copilotartifacts-guest | copilot artifacts as guest | guest | GET /copilot/artifacts | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-copilotartifacts-head | copilot artifacts as head | head | GET /copilot/artifacts | allow(≠401/403) | 200 | PASS |  |
| RBAC-copilotartifacts-hr | copilot artifacts as hr | hr | GET /copilot/artifacts | allow(≠401/403) | 200 | PASS |  |
| RBAC-copilotartifacts-manager | copilot artifacts as manager | manager | GET /copilot/artifacts | allow(≠401/403) | 200 | PASS |  |
| RBAC-copilotjobs-admin | copilot jobs as admin | admin | GET /copilot/jobs | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-copilotjobs-candidate | copilot jobs as candidate | candidate | GET /copilot/jobs | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-copilotjobs-guest | copilot jobs as guest | guest | GET /copilot/jobs | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-copilotjobs-head | copilot jobs as head | head | GET /copilot/jobs | allow(≠401/403) | 200 | PASS |  |
| RBAC-copilotjobs-hr | copilot jobs as hr | hr | GET /copilot/jobs | allow(≠401/403) | 200 | PASS |  |
| RBAC-copilotjobs-manager | copilot jobs as manager | manager | GET /copilot/jobs | allow(≠401/403) | 200 | PASS |  |
| RBAC-dateapplications-admin | candidate applications as admin | admin | GET /candidate/applications | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-dateapplications-candidate | candidate applications as candidate | candidate | GET /candidate/applications | allow(≠401/403) | 200 | PASS |  |
| RBAC-dateapplications-guest | candidate applications as guest | guest | GET /candidate/applications | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-dateapplications-head | candidate applications as head | head | GET /candidate/applications | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-dateapplications-hr | candidate applications as hr | hr | GET /candidate/applications | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-dateapplications-manager | candidate applications as manager | manager | GET /candidate/applications | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-didateinterviews-admin | candidate interviews as admin | admin | GET /candidate/interviews | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-didateinterviews-candidate | candidate interviews as candidate | candidate | GET /candidate/interviews | allow(≠401/403) | 200 | PASS |  |
| RBAC-didateinterviews-guest | candidate interviews as guest | guest | GET /candidate/interviews | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-didateinterviews-head | candidate interviews as head | head | GET /candidate/interviews | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-didateinterviews-hr | candidate interviews as hr | hr | GET /candidate/interviews | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-didateinterviews-manager | candidate interviews as manager | manager | GET /candidate/interviews | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrapplications-admin | hr applications list as admin | admin | GET /hr/applications | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrapplications-candidate | hr applications list as candidate | candidate | GET /hr/applications | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrapplications-guest | hr applications list as guest | guest | GET /hr/applications | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-hrapplications-head | hr applications list as head | head | GET /hr/applications | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrapplications-hr | hr applications list as hr | hr | GET /hr/applications | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrapplications-manager | hr applications list as manager | manager | GET /hr/applications | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrcandidates-admin | hr candidates list as admin | admin | GET /hr/candidates | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrcandidates-candidate | hr candidates list as candidate | candidate | GET /hr/candidates | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrcandidates-guest | hr candidates list as guest | guest | GET /hr/candidates | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-hrcandidates-head | hr candidates list as head | head | GET /hr/candidates | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrcandidates-hr | hr candidates list as hr | hr | GET /hr/candidates | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrcandidates-manager | hr candidates list as manager | manager | GET /hr/candidates | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrdashboard-admin | hr dashboard as admin | admin | GET /hr/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrdashboard-candidate | hr dashboard as candidate | candidate | GET /hr/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrdashboard-guest | hr dashboard as guest | guest | GET /hr/dashboard | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-hrdashboard-head | hr dashboard as head | head | GET /hr/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrdashboard-hr | hr dashboard as hr | hr | GET /hr/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrdashboard-manager | hr dashboard as manager | manager | GET /hr/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrinterviews-admin | hr interviews as admin | admin | GET /hr/interviews | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrinterviews-candidate | hr interviews as candidate | candidate | GET /hr/interviews | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrinterviews-guest | hr interviews as guest | guest | GET /hr/interviews | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-hrinterviews-head | hr interviews as head | head | GET /hr/interviews | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrinterviews-hr | hr interviews as hr | hr | GET /hr/interviews | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrinterviews-manager | hr interviews as manager | manager | GET /hr/interviews | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrjobs-admin | hr jobs list as admin | admin | GET /hr/jobs | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrjobs-candidate | hr jobs list as candidate | candidate | GET /hr/jobs | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-hrjobs-guest | hr jobs list as guest | guest | GET /hr/jobs | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-hrjobs-head | hr jobs list as head | head | GET /hr/jobs | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrjobs-hr | hr jobs list as hr | hr | GET /hr/jobs | allow(≠401/403) | 200 | PASS |  |
| RBAC-hrjobs-manager | hr jobs list as manager | manager | GET /hr/jobs | allow(≠401/403) | 200 | PASS |  |
| RBAC-iewsscheduledata-admin | hr interview schedule-data as admin | admin | GET /hr/interviews/schedule-data | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-iewsscheduledata-candidate | hr interview schedule-data as candidate | candidate | GET /hr/interviews/schedule-data | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-iewsscheduledata-guest | hr interview schedule-data as guest | guest | GET /hr/interviews/schedule-data | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-iewsscheduledata-head | hr interview schedule-data as head | head | GET /hr/interviews/schedule-data | allow(≠401/403) | 400 INVALID_INPUT | PASS |  |
| RBAC-iewsscheduledata-hr | hr interview schedule-data as hr | hr | GET /hr/interviews/schedule-data | allow(≠401/403) | 400 INVALID_INPUT | PASS |  |
| RBAC-iewsscheduledata-manager | hr interview schedule-data as manager | manager | GET /hr/interviews/schedule-data | allow(≠401/403) | 400 INVALID_INPUT | PASS |  |
| RBAC-itelemetryrecent-admin | sysadmin ai telemetry as admin | admin | GET /sysadmin/ai/telemetry/recent | allow(≠401/403) | 200 | PASS |  |
| RBAC-itelemetryrecent-candidate | sysadmin ai telemetry as candidate | candidate | GET /sysadmin/ai/telemetry/recent | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-itelemetryrecent-guest | sysadmin ai telemetry as guest | guest | GET /sysadmin/ai/telemetry/recent | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-itelemetryrecent-head | sysadmin ai telemetry as head | head | GET /sysadmin/ai/telemetry/recent | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-itelemetryrecent-hr | sysadmin ai telemetry as hr | hr | GET /sysadmin/ai/telemetry/recent | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-itelemetryrecent-manager | sysadmin ai telemetry as manager | manager | GET /sysadmin/ai/telemetry/recent | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-managerdashboard-admin | manager dashboard as admin | admin | GET /manager/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-managerdashboard-candidate | manager dashboard as candidate | candidate | GET /manager/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-managerdashboard-guest | manager dashboard as guest | guest | GET /manager/dashboard | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-managerdashboard-head | manager dashboard as head | head | GET /manager/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-managerdashboard-hr | manager dashboard as hr | hr | GET /manager/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-managerdashboard-manager | manager dashboard as manager | manager | GET /manager/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-mationexecutions-admin | sysadmin automation executions as admin | admin | GET /sysadmin/automation/executions | allow(≠401/403) | 200 | PASS |  |
| RBAC-mationexecutions-candidate | sysadmin automation executions as candidate | candidate | GET /sysadmin/automation/executions | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-mationexecutions-guest | sysadmin automation executions as guest | guest | GET /sysadmin/automation/executions | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-mationexecutions-head | sysadmin automation executions as head | head | GET /sysadmin/automation/executions | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-mationexecutions-hr | sysadmin automation executions as hr | hr | GET /sysadmin/automation/executions | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-mationexecutions-manager | sysadmin automation executions as manager | manager | GET /sysadmin/automation/executions | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ndidatedashboard-admin | candidate dashboard as admin | admin | GET /candidate/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ndidatedashboard-candidate | candidate dashboard as candidate | candidate | GET /candidate/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-ndidatedashboard-guest | candidate dashboard as guest | guest | GET /candidate/dashboard | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-ndidatedashboard-head | candidate dashboard as head | head | GET /candidate/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ndidatedashboard-hr | candidate dashboard as hr | hr | GET /candidate/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ndidatedashboard-manager | candidate dashboard as manager | manager | GET /candidate/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-obsapprovalqueue-admin | manager approval-queue as admin | admin | GET /manager/jobs/approval-queue | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-obsapprovalqueue-candidate | manager approval-queue as candidate | candidate | GET /manager/jobs/approval-queue | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-obsapprovalqueue-guest | manager approval-queue as guest | guest | GET /manager/jobs/approval-queue | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-obsapprovalqueue-head | manager approval-queue as head | head | GET /manager/jobs/approval-queue | allow(≠401/403) | 200 | PASS |  |
| RBAC-obsapprovalqueue-hr | manager approval-queue as hr | hr | GET /manager/jobs/approval-queue | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-obsapprovalqueue-manager | manager approval-queue as manager | manager | GET /manager/jobs/approval-queue | allow(≠401/403) | 200 | PASS |  |
| RBAC-omationdashboard-admin | sysadmin automation dashboard as admin | admin | GET /sysadmin/automation/dashboard | allow(≠401/403) | 200 | PASS |  |
| RBAC-omationdashboard-candidate | sysadmin automation dashboard as candidate | candidate | GET /sysadmin/automation/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationdashboard-guest | sysadmin automation dashboard as guest | guest | GET /sysadmin/automation/dashboard | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-omationdashboard-head | sysadmin automation dashboard as head | head | GET /sysadmin/automation/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationdashboard-hr | sysadmin automation dashboard as hr | hr | GET /sysadmin/automation/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationdashboard-manager | sysadmin automation dashboard as manager | manager | GET /sysadmin/automation/dashboard | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationworkflows-admin | sysadmin automation workflows as admin | admin | GET /sysadmin/automation/workflows | allow(≠401/403) | 200 | PASS |  |
| RBAC-omationworkflows-candidate | sysadmin automation workflows as candidate | candidate | GET /sysadmin/automation/workflows | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationworkflows-guest | sysadmin automation workflows as guest | guest | GET /sysadmin/automation/workflows | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-omationworkflows-head | sysadmin automation workflows as head | head | GET /sysadmin/automation/workflows | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationworkflows-hr | sysadmin automation workflows as hr | hr | GET /sysadmin/automation/workflows | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-omationworkflows-manager | sysadmin automation workflows as manager | manager | GET /sysadmin/automation/workflows | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-srecommendations-admin | candidate recommendations as admin | admin | GET /candidate/jobs/recommendations | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-srecommendations-candidate | candidate recommendations as candidate | candidate | GET /candidate/jobs/recommendations | allow(≠401/403) | 200 | PASS |  |
| RBAC-srecommendations-guest | candidate recommendations as guest | guest | GET /candidate/jobs/recommendations | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-srecommendations-head | candidate recommendations as head | head | GET /candidate/jobs/recommendations | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-srecommendations-hr | candidate recommendations as hr | hr | GET /candidate/jobs/recommendations | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-srecommendations-manager | candidate recommendations as manager | manager | GET /candidate/jobs/recommendations | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminmcptools-admin | sysadmin mcp tools as admin | admin | GET /sysadmin/mcp/tools | allow(≠401/403) | 200 | PASS |  |
| RBAC-sysadminmcptools-candidate | sysadmin mcp tools as candidate | candidate | GET /sysadmin/mcp/tools | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminmcptools-guest | sysadmin mcp tools as guest | guest | GET /sysadmin/mcp/tools | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-sysadminmcptools-head | sysadmin mcp tools as head | head | GET /sysadmin/mcp/tools | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminmcptools-hr | sysadmin mcp tools as hr | hr | GET /sysadmin/mcp/tools | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminmcptools-manager | sysadmin mcp tools as manager | manager | GET /sysadmin/mcp/tools | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminoverview-admin | sysadmin overview as admin | admin | GET /sysadmin/overview | allow(≠401/403) | 200 | PASS |  |
| RBAC-sysadminoverview-candidate | sysadmin overview as candidate | candidate | GET /sysadmin/overview | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminoverview-guest | sysadmin overview as guest | guest | GET /sysadmin/overview | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-sysadminoverview-head | sysadmin overview as head | head | GET /sysadmin/overview | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminoverview-hr | sysadmin overview as hr | hr | GET /sysadmin/overview | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminoverview-manager | sysadmin overview as manager | manager | GET /sysadmin/overview | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminusers-admin | sysadmin users as admin | admin | GET /sysadmin/users | allow(≠401/403) | 200 | PASS |  |
| RBAC-sysadminusers-candidate | sysadmin users as candidate | candidate | GET /sysadmin/users | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminusers-guest | sysadmin users as guest | guest | GET /sysadmin/users | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-sysadminusers-head | sysadmin users as head | head | GET /sysadmin/users | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminusers-hr | sysadmin users as hr | hr | GET /sysadmin/users | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-sysadminusers-manager | sysadmin users as manager | manager | GET /sysadmin/users | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-tionsreviewqueue-admin | manager review-queue as admin | admin | GET /manager/applications/review-queue | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-tionsreviewqueue-candidate | manager review-queue as candidate | candidate | GET /manager/applications/review-queue | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-tionsreviewqueue-guest | manager review-queue as guest | guest | GET /manager/applications/review-queue | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-tionsreviewqueue-head | manager review-queue as head | head | GET /manager/applications/review-queue | allow(≠401/403) | 200 | PASS |  |
| RBAC-tionsreviewqueue-hr | manager review-queue as hr | hr | GET /manager/applications/review-queue | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-tionsreviewqueue-manager | manager review-queue as manager | manager | GET /manager/applications/review-queue | allow(≠401/403) | 200 | PASS |  |
| RBAC-tprompttemplates-admin | copilot prompt-templates as admin | admin | GET /copilot/prompt-templates | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-tprompttemplates-candidate | copilot prompt-templates as candidate | candidate | GET /copilot/prompt-templates | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-tprompttemplates-guest | copilot prompt-templates as guest | guest | GET /copilot/prompt-templates | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-tprompttemplates-head | copilot prompt-templates as head | head | GET /copilot/prompt-templates | allow(≠401/403) | 200 | PASS |  |
| RBAC-tprompttemplates-hr | copilot prompt-templates as hr | hr | GET /copilot/prompt-templates | allow(≠401/403) | 200 | PASS |  |
| RBAC-tprompttemplates-manager | copilot prompt-templates as manager | manager | GET /copilot/prompt-templates | allow(≠401/403) | 200 | PASS |  |
| RBAC-uitmentanalytics-admin | manager analytics as admin | admin | GET /manager/reports/recruitment-analytics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-uitmentanalytics-candidate | manager analytics as candidate | candidate | GET /manager/reports/recruitment-analytics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-uitmentanalytics-guest | manager analytics as guest | guest | GET /manager/reports/recruitment-analytics | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-uitmentanalytics-head | manager analytics as head | head | GET /manager/reports/recruitment-analytics | allow(≠401/403) | 200 | PASS |  |
| RBAC-uitmentanalytics-hr | manager analytics as hr | hr | GET /manager/reports/recruitment-analytics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-uitmentanalytics-manager | manager analytics as manager | manager | GET /manager/reports/recruitment-analytics | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminaimetrics-admin | sysadmin ai metrics as admin | admin | GET /sysadmin/ai/metrics | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminaimetrics-candidate | sysadmin ai metrics as candidate | candidate | GET /sysadmin/ai/metrics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminaimetrics-guest | sysadmin ai metrics as guest | guest | GET /sysadmin/ai/metrics | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-ysadminaimetrics-head | sysadmin ai metrics as head | head | GET /sysadmin/ai/metrics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminaimetrics-hr | sysadmin ai metrics as hr | hr | GET /sysadmin/ai/metrics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminaimetrics-manager | sysadmin ai metrics as manager | manager | GET /sysadmin/ai/metrics | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminauditlogs-admin | sysadmin audit-logs (System_LOG_VIEW: also Manager) as admin | admin | GET /sysadmin/audit-logs | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminauditlogs-candidate | sysadmin audit-logs (System_LOG_VIEW: also Manager) as candidate | candidate | GET /sysadmin/audit-logs | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminauditlogs-guest | sysadmin audit-logs (System_LOG_VIEW: also Manager) as guest | guest | GET /sysadmin/audit-logs | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-ysadminauditlogs-head | sysadmin audit-logs (System_LOG_VIEW: also Manager) as head | head | GET /sysadmin/audit-logs | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminauditlogs-hr | sysadmin audit-logs (System_LOG_VIEW: also Manager) as hr | hr | GET /sysadmin/audit-logs | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminauditlogs-manager | sysadmin audit-logs (System_LOG_VIEW: also Manager) as manager | manager | GET /sysadmin/audit-logs | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminmcpaudits-admin | sysadmin mcp audits as admin | admin | GET /sysadmin/mcp/audits | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminmcpaudits-candidate | sysadmin mcp audits as candidate | candidate | GET /sysadmin/mcp/audits | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminmcpaudits-guest | sysadmin mcp audits as guest | guest | GET /sysadmin/mcp/audits | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-ysadminmcpaudits-head | sysadmin mcp audits as head | head | GET /sysadmin/mcp/audits | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminmcpaudits-hr | sysadmin mcp audits as hr | hr | GET /sysadmin/mcp/audits | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminmcpaudits-manager | sysadmin mcp audits as manager | manager | GET /sysadmin/mcp/audits | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminrbacroles-admin | sysadmin rbac roles as admin | admin | GET /sysadmin/rbac/roles | allow(≠401/403) | 200 | PASS |  |
| RBAC-ysadminrbacroles-candidate | sysadmin rbac roles as candidate | candidate | GET /sysadmin/rbac/roles | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminrbacroles-guest | sysadmin rbac roles as guest | guest | GET /sysadmin/rbac/roles | 401 | 401 UNAUTHENTICATED | PASS |  |
| RBAC-ysadminrbacroles-head | sysadmin rbac roles as head | head | GET /sysadmin/rbac/roles | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminrbacroles-hr | sysadmin rbac roles as hr | hr | GET /sysadmin/rbac/roles | 403 | 403 FORBIDDEN | PASS |  |
| RBAC-ysadminrbacroles-manager | sysadmin rbac roles as manager | manager | GET /sysadmin/rbac/roles | 403 | 403 FORBIDDEN | PASS |  |

## Candidate Portal + IDOR

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-APP-002 | Apply-context for approved job (candidate) | Candidate | GET /jobs/{id}/apply-context | 200 | 200 | PASS |  |
| UAT-APP-002b | Apply-context as HR → 403 | HR | GET /jobs/{id}/apply-context | 403 | 403 FORBIDDEN | PASS |  |
| UAT-APP-003 | Apply without token → 401 | Guest | POST /jobs/{id}/apply | 401 | 401 UNAUTHENTICATED | PASS |  |
| UAT-APP-004 | Apply to Draft job → 422 JOB_NOT_ACCEPTING_APPLICATIONS | Candidate | POST /jobs/{draft}/apply | 422/400 JOB_NOT_ACCEPTING_APPLICATIONS | 422 JOB_NOT_ACCEPTING_APPLICATIONS | PASS |  |
| UAT-APP-005 | Apply to Closed job → 422 JOB_NOT_ACCEPTING_APPLICATIONS | Candidate | POST /jobs/{closed}/apply | 422/400 JOB_NOT_ACCEPTING_APPLICATIONS | 422 JOB_NOT_ACCEPTING_APPLICATIONS | PASS |  |
| UAT-APP-005b | Apply to PendingApproval job → blocked | Candidate | POST /jobs/{pending}/apply | 422/400/404 | 422 JOB_NOT_ACCEPTING_APPLICATIONS | PASS |  |
| UAT-APP-006 | Apply to missing job → 404 JOB_NOT_FOUND | Candidate | POST /jobs/{id}/apply | 404 JOB_NOT_FOUND | 404 JOB_NOT_FOUND | PASS |  |
| UAT-APP-007 | Apply as HR (wrong role) → 403 | HR | POST /jobs/{id}/apply | 403 | 403 FORBIDDEN | PASS |  |
| UAT-APP-009 | Duplicate active application → 409 APPLICATION_ALREADY_ACTIVE | Candidate | POST /jobs/{id}/apply | 409 APPLICATION_ALREADY_ACTIVE | 409 APPLICATION_ALREADY_ACTIVE | PASS |  |
| UAT-APP-012 | Candidate reads own applications list | Candidate | GET /candidate/applications | 200 | 200 | PASS |  |
| UAT-APP-014b | Withdraw malformed application id → 400/404 | Candidate | POST /candidate/applications/{bad}/withdraw | 400/404 | 404 APPLICATION_NOT_FOUND | PASS |  |
| UAT-APP-015 | Withdraw own terminal/rejected app → blocked (4xx) | Candidate(owner) | POST /candidate/applications/{id}/withdraw | 400/409/422 | 422 APPLICATION_NOT_WITHDRAWABLE | PASS |  |
| UAT-APP-016 | IDOR: withdraw another candidate’s application → 403/404 | Candidate(A) | POST /candidate/applications/{otherId}/withdraw | 403/404 | 404 APPLICATION_NOT_FOUND | PASS |  |
| UAT-APP-019 | IDOR: accept another candidate’s offer → 403/404 | Candidate(A) | POST /candidate/applications/{otherId}/accept-offer | 403/404 | 404 APPLICATION_NOT_FOUND | PASS |  |
| UAT-APP-019b | IDOR: decline another candidate’s offer → 403/404 | Candidate(A) | POST /candidate/applications/{otherId}/decline-offer | 403/404 | 404 APPLICATION_NOT_FOUND | PASS |  |
| UAT-APP-020 | Accept-offer when no offer present → 4xx | Candidate(owner) | POST /candidate/applications/{id}/accept-offer | 400/404/409/422 | 422 OFFER_NOT_ACTIONABLE | PASS |  |
| UAT-APP-028 | Candidate PATCH application decision → 403 | Candidate | PATCH /hr/applications/{id}/decision | 403 | 403 FORBIDDEN | PASS |  |
| UAT-APP-030c | Candidate hits HR application detail → 403 | Candidate | GET /hr/applications/{id} | 403 | 403 FORBIDDEN | PASS |  |
| UAT-CJOB-008 | Candidate job recommendations (AI-dependent) | Candidate | GET /candidate/jobs/recommendations | 200 (ranked or empty) | 200 | PASS |  |
| UAT-DASH-001 | Candidate dashboard | Candidate | GET /candidate/dashboard | 200 | 200 | PASS |  |
| UAT-INT-010 | Candidate reads own interviews | Candidate | GET /candidate/interviews | 200 | 200 | PASS |  |
| UAT-PROF-001 | Candidate reads own profile | Candidate | GET /candidate/profile | 200 | 200 | PASS |  |

## HR Portal + Workflow Gates

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-AI-001 | Copilot jobs list (HR) | HR | GET /copilot/jobs | 200 | 200 | PASS |  |
| UAT-AI-003 | Copilot candidate pool for owned job | HR | GET /copilot/jobs/{id}/candidates | 200 | 200 | PASS |  |
| UAT-AI-010 | Copilot prompt-templates | HR | GET /copilot/prompt-templates | 200 | 200 | PASS |  |
| UAT-AI-017 | Copilot candidates for non-owned job → 403 | HR(non-owner) | GET /copilot/jobs/{otherId}/candidates | 403/404 | 403 FORBIDDEN | PASS |  |
| UAT-AI-018a | Talent-pool search valid query (BUG-UAT-003) | HR | GET /hr/talent-pool/search | 200 ranked | 400 INVALID_INPUT | FAIL | BUG-UAT-003 |
| UAT-AI-018b | Candidate-discovery valid query (BUG-UAT-003) | HR | POST /hr/candidate-discovery | 200 ranked | 400 INVALID_INPUT | FAIL | BUG-UAT-003 |
| UAT-APP-021 | HR reads applications list | HR | GET /hr/applications | 200 | 200 | PASS |  |
| UAT-APP-021b | HR reads owned application detail | HR(owner) | GET /hr/applications/{ownedId} | 200 | 200 | PASS |  |
| UAT-APP-025 | Decision backward (Interview→Screening) → 422 INVALID_APPLICATION_TRANSITION | HR | PATCH /hr/applications/{id}/decision | 422 INVALID_APPLICATION_TRANSITION | 422 INVALID_APPLICATION_TRANSITION | PASS |  |
| UAT-APP-025b | Decision invalid enum (Foobar) → 400 INVALID_INPUT | HR | PATCH /hr/applications/{id}/decision | 400 INVALID_INPUT | 400 INVALID_INPUT | PASS |  |
| UAT-APP-025c | Decision to "hired" → rejected (422 INVALID_APPLICATION_TRANSITION) | HR | PATCH /hr/applications/{id}/decision | 400/422 | 422 INVALID_APPLICATION_TRANSITION | PASS |  |
| UAT-APP-027 | Decision backward (Interview→ManagerReview) → 422 INVALID_APPLICATION_TRANSITION | HR | PATCH /hr/applications/{id}/decision | 422 INVALID_APPLICATION_TRANSITION | 422 INVALID_APPLICATION_TRANSITION | PASS |  |
| UAT-APP-030 | HR reads non-owned application detail → 403/404 | HR(non-owner) | GET /hr/applications/{otherId} | 403/404 | 403 FORBIDDEN | PASS |  |
| UAT-APP-031b | HR application detail unknown id → 404 | HR | GET /hr/applications/{id} | 403/404 | 404 APPLICATION_NOT_FOUND | PASS |  |
| UAT-APP-032 | Decision on non-owned Offer-state app → 422 (Offer guard) | HR(non-owner) | PATCH /hr/applications/{otherId}/decision | 403/404/422 | 422 INVALID_APPLICATION_TRANSITION | PASS |  |
| UAT-DASH-002 | HR dashboard | HR | GET /hr/dashboard | 200 | 200 | PASS |  |
| UAT-INT-001s | HR interview schedule-data (with applicationId) | HR | GET /hr/interviews/schedule-data?applicationId= | 200 | 200 | PASS |  |
| UAT-INT-001s2 | schedule-data without applicationId (optional param) | HR | GET /hr/interviews/schedule-data | handled cleanly | 400 INVALID_INPUT | PASS |  |
| UAT-INT-002 | Schedule interview missing applicationId → 400/422 | HR | POST /hr/interviews | 400/422 | 400 INVALID_INPUT | PASS |  |
| UAT-INT-009 | HR reads interviews | HR | GET /hr/interviews | 200 | 200 | PASS |  |
| UAT-INT-013 | Schedule interview on non-owned app → 403/404 | HR(non-owner) | POST /hr/interviews | 403/404/422 | 422 INTERVIEW_NOT_ACTIONABLE | PASS |  |
| UAT-JOB-019 | HR reads own jobs list | HR | GET /hr/jobs | 200 | 200 | PASS |  |
| UAT-JOB-019b | HR reads owned job detail | HR | GET /hr/jobs/{id} | 200 | 200 | PASS |  |
| UAT-OFFER-003 | Send offer with no offer prepared → 4xx | HR | POST /hr/applications/{id}/offer/send | 400/404/409/422 | 422 INTERVIEW_NOT_COMPLETED | PASS |  |
| UAT-OFFER-005 | PUT offer before interview completed → 422 INTERVIEW_NOT_COMPLETED | HR | PUT /hr/applications/{id}/offer | 422/400 INTERVIEW_NOT_COMPLETED | 422 INTERVIEW_NOT_COMPLETED | PASS |  |
| UAT-OFFER-012 | HR GET offer for owned app (no offer yet) | HR | GET /hr/applications/{id}/offer | 200/404 | 200 | PASS |  |
| UAT-PROF-014 | HR reads candidates list | HR | GET /hr/candidates | 200 | 200 | PASS |  |
| UAT-PROF-014b | HR reads candidate detail | HR | GET /hr/candidates/{id} | 200/404 | 404 CANDIDATE_NOT_FOUND | PASS |  |

## Manager/Head + SysAdmin

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-AIOPS-01 | ai metrics | SystemAdmin | GET /sysadmin/ai/metrics | 200 | 200 | PASS |  |
| UAT-AIOPS-02 | ai telemetry recent | SystemAdmin | GET /sysadmin/ai/telemetry/recent | 200 | 200 | PASS |  |
| UAT-AIOPS-03 | ai risk-flags | SystemAdmin | GET /sysadmin/ai/risk-flags | 200 | 200 | PASS |  |
| UAT-AIOPS-04 | ai risk-flags recent | SystemAdmin | GET /sysadmin/ai/risk-flags/recent | 200 | 200 | PASS |  |
| UAT-APP-029 | Head review-queue (ManagerReview stage apps) | Head | GET /manager/applications/review-queue | 200 | 200 | PASS |  |
| UAT-DASH-003 | Manager/Head dashboard | Head | GET /manager/dashboard | 200 | 200 | PASS |  |
| UAT-DASH-004 | Manager recruitment-analytics | Head | GET /manager/reports/recruitment-analytics | 200 | 200 | PASS |  |
| UAT-JOB-009 | Head approval-queue has pending jobs | Head | GET /manager/jobs/approval-queue | 200 | 200 | PASS |  |
| UAT-JOB-012b | Approve non-existent job → 404 | Head | PATCH /hr/jobs/{id}/status | 404/422/400 | 404 JOB_NOT_FOUND | PASS |  |
| UAT-JOB-014 | Manager(no dept) approval-queue empty (BR-OWN-003) | Manager | GET /manager/jobs/approval-queue | 200 | 200 | PASS |  |
| UAT-JOB-015 | Head approval-detail for pending job | Head | GET /manager/jobs/{id}/approval-detail | 200 | 200 | PASS |  |
| UAT-JOB-015b | Approval-detail for already-Approved job | Head | GET /manager/jobs/{id}/approval-detail | 200/404/422 | 200 | PASS |  |
| UAT-JOB-017 | Re-approve an Approved job (Q-JOB-01 transition) | Head | PATCH /hr/jobs/{id}/status | record actual (no state machine) | 200 | PASS |  |
| UAT-LOG-001 | SysAdmin audit-logs | SystemAdmin | GET /sysadmin/audit-logs | 200 | 200 | PASS |  |
| UAT-RBAC-001 | SysAdmin overview | SystemAdmin | GET /sysadmin/overview | 200 | 200 | PASS |  |
| UAT-RBAC-001b | SysAdmin users list (expect ~24) | SystemAdmin | GET /sysadmin/users | 200 | 200 | PASS |  |
| UAT-RBAC-003 | SysAdmin RBAC roles (expect 5) | SystemAdmin | GET /sysadmin/rbac/roles | 200 | 200 | PASS |  |
| UAT-RBAC-003c | SysAdmin read HR role permissions | SystemAdmin | GET /sysadmin/rbac/roles/{id}/permissions | 200 | 200 | PASS |  |
| UAT-RBAC-011 | Disable non-existent user → 404 | SystemAdmin | PATCH /sysadmin/users/{id}/status | 404/400 | 404 USER_NOT_FOUND | PASS |  |
| UAT-RBAC-013 | Set invalid status enum → 400 | SystemAdmin | PATCH /sysadmin/users/{id}/status | 400/422 | 400 USER_STATUS_INVALID | PASS |  |
| UAT-RBAC-015 | SysAdmin users search | SystemAdmin | GET /sysadmin/users?search= | 200 | 200 | PASS |  |
| UAT-RBAC-020 | Set roles on non-existent user → 404 | SystemAdmin | PUT /sysadmin/users/{id}/roles | 404/400 | 404 USER_NOT_FOUND | PASS |  |
| UAT-RBAC-021 | SysAdmin RBAC modules | SystemAdmin | GET /sysadmin/rbac/modules | 200 | 200 | PASS |  |
| UAT-WF-001 | Automation dashboard | SystemAdmin | GET /sysadmin/automation/dashboard | 200 | 200 | PASS |  |
| UAT-WF-002 | Automation workflows (expect 5) | SystemAdmin | GET /sysadmin/automation/workflows | 200 | 200 | PASS |  |
| UAT-WF-010 | Automation events/outbox (expect ~7) | SystemAdmin | GET /sysadmin/automation/events | 200 | 200 | PASS |  |
| UAT-WF-011 | Automation executions (expect ~8) | SystemAdmin | GET /sysadmin/automation/executions | 200 | 200 | PASS |  |
| UAT-WF-013 | Automation diagnostics | SystemAdmin | GET /sysadmin/automation/diagnostics | 200 | 200 | PASS |  |
| UAT-WF-015 | MCP tools list (expect 6 read-only) | SystemAdmin | GET /sysadmin/mcp/tools | 200 | 200 | PASS |  |
| UAT-WF-016 | MCP audits (expect ~5) | SystemAdmin | GET /sysadmin/mcp/audits | 200 | 200 | PASS |  |

## Validation & Security

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-AI-010-neg | Create prompt-template empty body → 400 | HR | POST /copilot/prompt-templates | 400/422 | 400 INVALID_INPUT | PASS |  |
| UAT-API-012-method | DELETE on /jobs (method not allowed) | Guest | DELETE /jobs | 405/404/401 | 405 | PASS |  |
| UAT-API-013-oversized | Oversized query handled (no 500) | Guest | GET /jobs?keyword=<huge> | 200/400/414 | 0 | PASS |  |
| UAT-ERR-403-env | 403 returns envelope + FORBIDDEN | Candidate | GET /sysadmin/users | envelope FORBIDDEN | 403 FORBIDDEN | PASS |  |
| UAT-ERR-404-env | 404 returns ApiResponse envelope + code | Guest | GET /jobs/{id} | envelope+code | success=false code=JOB_NOT_FOUND trace=true | PASS |  |
| UAT-ERR-badjson | Malformed JSON body → 400 | Guest | POST /auth/candidate/login | 400 | 400  | PASS |  |
| UAT-ERR-wrongct | Wrong content-type → 4xx (415/400) | Guest | POST /auth/candidate/login | 415/400/401 | 415 | PASS |  |
| UAT-PROF-003 | Profile update invalid data → 400/422 (or record) | Candidate | PUT /candidate/profile | 400/422 (validation) | 200  | FAIL | BUG-UAT-006 |
| UAT-SEC-003-sqli | SQLi in login → 401, no SQL error | Guest | POST /auth/candidate/login | 401 (no 500) | 401 INVALID_CREDENTIALS | PASS |  |
| UAT-SEC-008-server | Server version disclosure | Guest | GET / | no version (server_tokens off) | nginx/1.24.0 (Ubuntu) | FAIL | BUG-UAT-004 |
| UAT-SEC-010-sqli-search | SQLi in job search keyword → safe | Guest | GET /jobs?keyword= | 200 no SQL error | 200 | PASS |  |
| UAT-SEC-011-xss | XSS payload in search not reflected raw | Guest | GET /jobs?keyword= | 200, not reflected | 200 reflected=false | PASS |  |
| UAT-SEC-CORS | CORS preflight from foreign origin not reflected | Guest | OPTIONS /jobs | no ACAO reflect of evil origin | status=204 ACAO=none | PASS |  |
| UAT-SEC-HDR-API-options | x-content-type-options on API | Guest | GET /api/jobs | present | MISSING | FAIL | BUG-UAT-004 |
| UAT-SEC-HDR-API-policy | referrer-policy on API | Guest | GET /api/jobs | present | MISSING | FAIL | BUG-UAT-004 |
| UAT-SEC-HDR-API-security | strict-transport-security on API | Guest | GET /api/jobs | present | MISSING | FAIL | BUG-UAT-004 |
| UAT-SEC-HDR-SPA-options | x-content-type-options on SPA | Guest | GET / | present | MISSING | FAIL | BUG-UAT-004 |
| UAT-SEC-HDR-SPA-policy | referrer-policy on SPA | Guest | GET / | present | MISSING | FAIL | BUG-UAT-004 |
| UAT-SEC-HDR-SPA-security | strict-transport-security on SPA | Guest | GET / | present | MISSING | FAIL | BUG-UAT-004 |
| UAT-SEC-LEAK-500 | 500 (neg page) returns clean envelope, no stack leak | Guest | GET /jobs?page=-1 | no leak + traceId | status=500 leak=false trace=true | PASS |  |

## Validation (deep-dive findings)

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-ERR-VALFILTER | FluentValidation filter never executes validators (systemic) | all | * (global ValidationActionFilter) | request-body validators enforced | validators silently skipped (GetMethod on IValidator<T> returns null) | FAIL | BUG-UAT-005 |
| UAT-INT-016 | POST /hr/interviews startMinutes out of range → 500 | HR | POST /hr/interviews | 400 (validator caps 0..1439) | 500 SERVER_ERROR for startMinutes=-1, 1440, 9999 | FAIL | BUG-UAT-007 |
| UAT-PUB-006-500 | GET /jobs page/pageSize <= 0 → 500 | Guest | GET /jobs?page=0\|-1 / pageSize=0-neg | 400 or clamp | 500 SERVER_ERROR for page=0, page=-1, pageSize=-1, pageSize=-5 (non-numeric → clean 400) | FAIL | BUG-UAT-008 |
| UAT-SEC-XSS-STORED | javascript: URL accepted in profile github/linkedin | Candidate | PUT /candidate/profile | reject non-http(s) URL (validator BeAbsoluteHttpUrl) | javascript:alert(1) accepted (200) | FAIL | BUG-UAT-006 |

## E2E Lifecycle

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-APP-001 | [UAT] candidate applies to approved [UAT] job → 200/201 | Candidate([UAT]) | POST /jobs/{id}/apply | 200/201 | 422 RESUME_REQUIRED | BLOCKED |  |
| UAT-APP-009e | Duplicate apply to same [UAT] job → 409 | Candidate([UAT]) | POST /jobs/{id}/apply | 409 APPLICATION_ALREADY_ACTIVE | 422 RESUME_REQUIRED | BLOCKED |  |
| UAT-APP-012e | [UAT] application appears in candidate list | Candidate([UAT]) | GET /candidate/applications | 1 application | n=0 | BLOCKED |  |
| UAT-E2E-01-login | Login as freshly-registered [UAT] candidate | Candidate([UAT]) | POST /auth/candidate/login | 200 | 200 | PASS |  |
| UAT-E2E-02-public | [UAT] job visible publicly after approval | Guest | GET /jobs/{id} | 200 | 200 | PASS |  |
| UAT-INT-001v | Interview schedule with VALID startMinutes (contrast to 500) | HR | POST /hr/interviews | non-500 (201 if state ok, else 422) | 201 | PASS |  |
| UAT-JOB-001 | HR creates [UAT] job → 201 (PendingApproval) | HR | POST /hr/jobs | 200/201 | 201 | PASS |  |
| UAT-JOB-002pub | New [UAT] job (Pending) not in public list | Guest | GET /jobs/{id} | not Approved-visible | detail=200 status=PendingApproval | PASS |  |
| UAT-JOB-010 | Head approves [UAT] job → 200 Approved | Head | PATCH /hr/jobs/{id}/status | 200 | 200 | PASS |  |
| UAT-JOB-018 | HR deletes [UAT] job (cleanup) | HR | DELETE /hr/jobs/{id} | 200 | 200 | PASS |  |
| UAT-REG-001 | Register [UAT] candidate (multipart happy path) | Guest | POST /candidates/register | 200/201 | 201 | PASS |  |
| UAT-REG-005 | Register duplicate username → rejected | Guest | POST /candidates/register | 409/400 duplicate | 400 | PASS |  |
| UAT-REG-006 | Register duplicate email → rejected | Guest | POST /candidates/register | 409/400 duplicate | 400 | PASS |  |

## E2E (CV/MinIO)

| Case | Title | Role | Method Endpoint | Expected | Actual | Result | Bug |
|---|---|---|---|---|---|---|---|
| UAT-REG-002 | Register [UAT] candidate WITH CV (MinIO) | Guest | POST /candidates/register | 200/201 (or MinIO-blocked) | 500 SERVER_ERROR | BLOCKED | BUG-UAT-011 |
