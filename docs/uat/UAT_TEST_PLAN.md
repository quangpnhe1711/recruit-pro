# UAT Test Plan — RecruitPro (Full Product)

> **Document:** `docs/uat/UAT_TEST_PLAN.md` · **Version:** 1.0 · **Status:** Draft for review
> **Author:** QA / UAT · **Date:** 2026-07-12 · **Target:** Production `https://www.recruitpro.site/`

## Document metadata

| Field | Value |
|---|---|
| Product | RecruitPro — AI-native Applicant Tracking System (PRN232) |
| Backend | .NET (RecruitPro.API, Application, Domain, Infrastructure, ScoringService), PostgreSQL, MinIO |
| Frontend | React + TypeScript (Vite) SPA (`recruit-pro-internal`) |
| Test environment | Production (UAT-on-live) + local fallback |
| Seed dataset | `RecruitProInternal/init.sql` (shared demo data) |
| Related docs | [`UAT_TEST_CASES.md`](UAT_TEST_CASES.md), [`UAT_TEST_DATA.md`](UAT_TEST_DATA.md), [`UAT_API_COVERAGE.md`](UAT_API_COVERAGE.md), [`UAT_TRACEABILITY_MATRIX.md`](UAT_TRACEABILITY_MATRIX.md), [`UAT_OPEN_QUESTIONS.md`](UAT_OPEN_QUESTIONS.md), [`README.md`](README.md) |
| Prior UAT | `PRODUCTION_UAT_BUG_REPORT.md`, `PRODUCTION_UAT_EXECUTION_LOG.md`, `v4-workflow-automation-uat.md` |

## Version history

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-07-12 | QA | Initial full-product UAT plan + 378 cases derived from implementation |

---

## 1. Objective

Verify that RecruitPro meets business and functional requirements from the perspective of real users
(Candidate, HR/Recruiter, Manager, DepartmentHead, SystemAdmin) across every portal, module, screen, API,
business flow, and state — including positive, negative, boundary, validation, permission/RBAC, security,
integration, concurrency, recovery, and end-to-end scenarios. Test cases are derived from the **actual
implementation** (controllers, services, enums, validators, `init.sql`) and the `docs/source-of-truth/*`
contracts, not from assumptions.

## 2. Scope (in scope)

- **Portals:** Public, Candidate, Internal (HR / Manager / HeadDepartment), System-Admin.
- **Modules:** Authentication & session; Candidate registration; Candidate profile & CV; Job browse/search;
  Applications & workflow; Internal job management & approval; Interviews; Offers; Notifications (in-app +
  SSE); Dashboard & analytics; Users & RBAC; Departments & master data; Configurable workflow automation +
  MCP; Copilot & AI; Validation & error contract; File storage/MinIO; System & audit logs; Common UI/nav.
- **All 141 API endpoints** (24 controllers + OData + XML-demo + health).
- **All business state machines** (Application, Job, Interview, Offer, User status, Workflow engine).
- **Cross-cutting:** error contract, RBAC/ownership/IDOR, security, i18n (vi/en), responsive/mobile.
- **10 end-to-end business scenarios.**

## 3. Out of scope

- Executing destructive tests against production data; any DB mutation outside the documented `[UAT]`-marked
  flows; load/performance/stress testing; penetration testing beyond the documented security cases;
  automated test implementation (Playwright/Cypress/Postman scripting); source/UI/API/DB/CI changes;
  email-content delivery verification (no mail sink); third-party AI provider internals.

## 4. Product overview

An ATS where candidates register, build a profile + CV, browse/apply to approved jobs; HR screens
applications; DepartmentHeads approve jobs and review candidates; interviews and offers are managed;
notifications are delivered in-app (SSE realtime); SystemAdmins manage users/RBAC and a configurable
recruitment-automation engine (+ read-only MCP tools); an AI Copilot ranks CVs (deterministic-first, AI
optional). See `docs/overview/*` and `docs/source-of-truth/*`.

## 5. Roles under test

| Business role | Code role(s) | Seed persona | Notes |
|---|---|---|---|
| Candidate | `Candidate` | `nhatquang` (+ `yennhi` disabled) | Self-service portal |
| HR / Recruiter | `HR` | `thucuyen` (owner), `giahan` (non-owner) | Screens, coordinates |
| Manager | `Manager` | `quocbao` (heads no dept) | Analytics, approval (if head) |
| DepartmentHead | `HeadDepartment` | `tiendat` (dual role; heads all depts) | Approves jobs, ManagerReview |
| SystemAdmin | `SystemAdmin` | `admin`, `minhkhoi` | RBAC, automation, MCP |
| Anonymous | — | (no token) | Public portal |

## 6. Test environment

Production `https://www.recruitpro.site/` (UI + API `…/api` + SSE `…/api/notifications/stream`). Local
fallback for storage/AI/mail flows and to reproduce the auth-session blocker. Details + browsers/viewports:
[`UAT_TEST_DATA.md`](UAT_TEST_DATA.md) §11.

## 7. Entry criteria

- Build deployed and reachable; seed data (`init.sql`) loaded; all seed accounts log in via API.
- This test plan + cases reviewed; test-data doc available; tester has accounts per role.
- Known-blocker status confirmed (esp. **BUG-UAT-001** auth-session — if still live, authenticated UI runs
  locally).

## 8. Exit criteria

- 100% of P0 and ≥95% of P1 cases executed; **zero open Blocker/Critical** defects.
- All executed defects triaged; every Blocked case has a documented reason (infra/known-defect).
- Traceability confirms every module/route/API/role/state/E2E flow has ≥1 executed case (or a logged gap).
- Sign-off recorded (§18).

## 9. Suspension & resumption

- **Suspend** if: the environment is down; a Blocker (e.g. BUG-UAT-001) prevents a whole portal; seed data
  is corrupted; a security-critical leak is found.
- **Resume** when: the blocking defect is fixed/redeployed and a smoke set (login per role, public list,
  apply, one internal decision) passes.

## 10. Test strategy

- **Source-grounded:** every case cites a requirement/route/API/rule/constraint (see traceability).
- **Dual verification:** business-critical cases assert UI **and** API **and** data/side-effect results.
  When BUG-UAT-001 blocks the UI, the API leg is verified directly (curl/Postman) and the UI leg deferred.
- **Risk-based ordering:** P0 security/auth/workflow first, then P1 core flows, then P2/P3.
- **Equivalence partitioning & boundary analysis** for all validated inputs (embedded partition tables).
- **Role × action matrix:** each protected action tested with an allowed role, a forbidden role, anonymous,
  and a wrong-owner (IDOR) attempt.
- **State-machine coverage:** each transition has a valid and an invalid/negative case.
- **Negative/​security by default:** injection, IDOR, oversized, malformed-UUID, method/content-type on the
  API-wide template cases (UAT-API-*).

## 11. Test types

Functional, Negative, Boundary, Validation, Permission/RBAC, Security, Integration, Concurrency, Recovery,
Usability, Compatibility, End-to-End. (See §Coverage report for counts.)

## 12. Risk-based priority

| Priority | Meaning | Examples |
|---|---|---|
| **P0** | Blocker — cannot release | auth/session, RBAC/IDOR, apply/duplicate, workflow gates, disable-user |
| **P1** | Core business flow | full hire flow, job approval, interviews, offers, notifications |
| **P2** | Important behavior | filters/search, dashboards, master data, automation UI |
| **P3** | Minor / edge / usability | responsive, i18n, empty states, rare edges |

## 13. Defect severity

| Severity | Definition |
|---|---|
| Critical | Data loss/corruption, security breach, core flow broken with no workaround |
| High | Major function broken, workaround exists; broken access control of medium impact |
| Medium | Non-core function broken, or hardening gap |
| Low | Cosmetic, minor UX, rare edge |

## 14. UAT execution process

1. Pick a case from [`UAT_TEST_CASES.md`](UAT_TEST_CASES.md); prepare data per
   [`UAT_TEST_DATA.md`](UAT_TEST_DATA.md) (create `[UAT]`-marked entities as needed).
2. Execute the detailed steps; capture UI + API (Network/curl) + data observations.
3. Record **Pass/Fail/Blocked** with evidence (screenshot + request/response, redacting secrets) in the
   execution log (`PRODUCTION_UAT_EXECUTION_LOG.md` style).
4. On Fail, raise a defect (severity/priority, repro steps, expected vs actual, traceId, evidence) in the
   bug report.
5. Clean up per [`UAT_TEST_DATA.md`](UAT_TEST_DATA.md) §9; restore any mutated seed/RBAC state.

## 15. Evidence requirements

Per executed case: request/response (status + `error.code`/`traceId` for failures), screenshot for UI
results, and a note of the data/side-effect verification. Store under `docs/uat/evidence/` with the case ID
in the filename. **Never** capture secrets (tokens, passwords, keys, connection strings).

## 16. Test-data strategy

Shared seed personas (password `Password@123`, login by username) + `[UAT]`-marked created data. Full
account/role/master/business-data matrix and the data-prep list for states not in the seed:
[`UAT_TEST_DATA.md`](UAT_TEST_DATA.md).

## 17. Assumptions, dependencies, constraints, risks

- **Assumptions:** production seed matches `init.sql`; seed passwords are the public test password.
- **Dependencies:** MinIO (CV), AI/embedding provider (Copilot/discovery), SMTP (offer/rejection email),
  gRPC ScoringService (external score), SSE-capable proxy (realtime).
- **Constraints:** cannot mutate seed terminal rows; RBAC-matrix and automation-mode changes are global (one
  owner, restore after); no master-data CRUD API; only VND currency + FullTime seeded.
- **Risks:** BUG-UAT-001 blocks authenticated UI on prod until redeploy; AI provider unavailable makes
  discovery inert (BUG-UAT-003); enumerable seed IDs amplify BUG-UAT-002.

## 18. Approval / sign-off

| Role | Name | Decision | Date | Signature |
|---|---|---|---|---|
| QA Lead |  | ☐ Approve ☐ Reject |  |  |
| Product Owner |  | ☐ Approve ☐ Reject |  |  |
| Engineering Lead |  | ☐ Approve ☐ Reject |  |  |
| UAT Sign-off |  | ☐ Pass ☐ Conditional ☐ Fail |  |  |

---

## Coverage report (computed from the actual documents)

### Discovery inventory

| Item | Count | Source |
|---|---|---|
| Portals | 4 (Public, Candidate, Internal, System-Admin) | FE routes |
| Functional modules | 18 (test suites: 22 prefixes) | this plan §2 |
| Frontend routes | ~47 (incl. redirects + catch-all) | `src/routes/*` |
| API endpoints | **141** (24 controllers + OData ×2 + XML-demo + health) | `RecruitPro.API` |
| Roles | 5 (Candidate, HR, Manager, HeadDepartment, SystemAdmin) | `init.sql` |
| Backend DB permissions | 27 codes | `init.sql :901–929` |
| Frontend permission keys | ~60 | `src/permissions/permissions.ts` |
| Business state machines | 6 (Application, Job, Interview, Offer, User status, Workflow) | Domain enums/services |
| Documented state transitions | ~41 edges across the 6 machines | STATE-MACHINE + services |
| Database tables | 49 | `init.sql` |

### Test case counts

| Metric | Count |
|---|---|
| **Total UAT test cases** | **378** |
| P0 (blocker) | 66 |
| P1 (core) | 121 |
| P2 (important) | 111 |
| P3 (minor/edge) | 80 |
| Severity — Critical / High / Medium / Low (of cases tagged) | 38 / 83 / 61 / 30 |
| Known-defect regression cases (BUG-UAT-001..004 referenced) | 3 tagged "KNOWN BUG" + 4 defects cross-referenced |

### Cases by module

| Module | Cases | Module | Cases |
|---|---|---|---|
| Public (PUB) | 14 | Users & RBAC (RBAC) | 22 |
| Authentication (AUTH) | 26 | Departments/master data (MDATA) | 10 |
| Registration (REG) | 14 | Workflow automation + MCP (WF) | 22 |
| Candidate profile (PROF) | 20 | Copilot & AI (AI) | 20 |
| Candidate jobs (CJOB) | 14 | Validation & errors (ERR) | 14 |
| Applications (APP) | 34 | File storage/MinIO (FILE) | 14 |
| Job management (JOB) | 26 | System & audit logs (LOG) | 8 |
| Interviews (INT) | 18 | Common UI (UI) | 14 |
| Offers (OFFER) | 18 | API-wide (API) | 16 |
| Notifications (NOTI) | 18 | Security (SEC) | 14 |
| Dashboard & analytics (DASH) | 12 | End-to-end (E2E) | 10 |
| | | **Total** | **378** |

### Cases by test type (a case may carry more than one type)

| Type | Occurrences | Type | Occurrences |
|---|---|---|---|
| Functional / Positive | 146 | Concurrency | 10 |
| Negative | 64 | Recovery | 19 |
| Boundary | 32 | Usability | 16 |
| Validation | 23 | Compatibility | 5 |
| Permission/RBAC | 34 | End-to-End | 10 |
| Security | 75 | Integration | 30 |

### API coverage

| Status | Endpoints |
|---|---|
| Covered | 122 |
| Partially Covered | 12 |
| Blocked (AI/MinIO/mail infra) | 7 |
| Not Implemented | 0 |
| **Total** | **141** |

### Automation candidates

| Automation candidate | Cases |
|---|---|
| Yes (API-scriptable / deterministic UI) | 200 |
| Partial (mixed UI+infra) | 112 |
| No (manual/observational) | 66 |

### Open questions & gaps

| Metric | Count |
|---|---|
| Open questions / requirement gaps | 25 |
| Known production defects cross-referenced | 4 (BUG-UAT-001..004) |
| Uncoverable areas (traced to gaps) | 9 (see traceability §10) |

> All numbers above are counted from the committed documents (`grep`-verified), not estimated.
