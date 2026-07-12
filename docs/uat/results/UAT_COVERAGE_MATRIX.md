# UAT Coverage Matrix — RecruitPro (Production)

> **Environment:** Production `https://www.recruitpro.site/api` · **Date:** 2026-07-12 · **Method:** API-direct.
> Maps *Requirement/Module → Role → UAT case(s) → API → Execution status → Bug*. Cross-references
> [`UAT_API_COVERAGE.md`](../UAT_API_COVERAGE.md) (141 endpoints) and [`UAT_TEST_CASES.md`](../UAT_TEST_CASES.md).
> **Legend:** ✅ Covered-PASS · ❌ Covered-FAIL(bug) · 🔒 Blocked (infra) · 🖥️ UI-only (not run, no browser) · ➖ N/A.

## A. Portal × Role authorization (API) — from the 198-case RBAC matrix

| Resource area | Guest | Candidate | HR | Manager | HeadDept | SystemAdmin | Status |
|---|:--:|:--:|:--:|:--:|:--:|:--:|---|
| Public jobs / departments / skills | 200 | 200 | 200 | 200 | 200 | 200 | ✅ |
| `/candidate/*` (profile, apps, interviews, dashboard) | 401 | 200 | 403 | 403 | 403 | 403 | ✅ |
| `/hr/*` (apps, jobs, candidates, interviews, dashboard) | 401 | 403 | 200 | 200 | 200 | 403 | ✅ |
| `/copilot/*` | 401 | 403 | 200 | 200 | 200 | 403 | ✅ |
| `/manager/applications/review-queue` | 401 | 403 | 403 | 200 | 200 | 403 | ✅ |
| `/manager/dashboard` · `/approval-queue` · `/reports/*` | 401 | 403 | 403 | 200 | 200 | 403 | ✅ |
| `/sysadmin/*` (users, rbac, automation, mcp, ai-ops) | 401 | 403 | 403 | 403 | 403 | 200 | ✅ |
| `/sysadmin/audit-logs` (System_LOG_VIEW → also Manager) | 401 | 403 | 403 | 200 | 200 | 200 | ✅ |

**Result:** 198/198 PASS — no broken access control, privilege escalation, or menu-hidden-but-API-open case.

## B. Module coverage

| # | Module | Roles exercised | Key APIs | Status | Bug(s) |
|---|---|---|---|---|---|
| 1 | Public portal (landing/list/detail/search/filter/paging/stats) | Guest | `GET /jobs`, `/jobs/{id}`, `/jobs/filters`, `/jobs/{id}/statistics`, `/departments`, `/skills`, `/xml-demo/jobs` | ✅ mostly | ❌ BUG-002 (detail leak), BUG-008 (paging 500), BUG-009 (currency), BUG-012 (dept email), BUG-013 (OData) |
| 2 | Authentication & session | all + Guest | `/auth/candidate/login`, `/auth/internal/login`, `/auth/login`, `/auth/refresh` | ✅ 23/23 | — (BUG-001 UI-only, not re-tested) |
| 3 | Candidate registration | Guest | `POST /candidates/register` (multipart) | ✅ base / 🔒 with-CV | BUG-011 (CV→500) |
| 4 | Candidate profile & CV | Candidate | `GET/PUT /candidate/profile`, `/profile/resume` | ❌ validation / 🔒 CV | BUG-006, BUG-005; 🔒 MinIO |
| 5 | Candidate job listing / recommendations | Candidate | `/candidate/jobs/recommendations`, `/jobs/{id}/apply-context` | ✅ | rec returns 200 (fallback) |
| 6 | Applications (apply + workflow) | Candidate, HR, Manager | `POST /jobs/{id}/apply`, `/candidate/applications*`, `/hr/applications*`, `/decision` | ✅ gates / 🔒 happy-apply | BUG-010 (owner asym); 🔒 apply needs CV |
| 7 | Internal job mgmt & approval | HR, Manager, Head | `/hr/jobs*`, `/manager/jobs/approval-*`, `PATCH /hr/jobs/{id}/status` | ✅ (create→approve→delete verified on [UAT]) | Q-JOB-01 (no transition matrix) |
| 8 | Interviews | HR, Manager, Head, Candidate | `/hr/interviews*`, `/candidate/interviews` | ✅ reads/gates | ❌ BUG-007 (startMinutes→500) |
| 9 | Offers | HR, Candidate | `/hr/applications/{id}/offer*`, `/accept-offer`, `/decline-offer` | ✅ gates | 🔒 send/email leg (mail) |
| 10 | Notifications | all auth | `/notifications`, `/counts`, `/unread-count`, `/seen`, `/read`, `/read-all` | ✅ reads/writes | 🖥️ SSE stream (browser) |
| 11 | Dashboard & analytics | Candidate, HR, Manager, Head | `/candidate/dashboard`, `/hr/dashboard`, `/manager/dashboard`, `/reports/recruitment-analytics` | ✅ | — |
| 12 | Users & RBAC | SystemAdmin (+neg roles) | `/sysadmin/users*`, `/sysadmin/rbac/*` | ✅ reads + safe negatives | mutation guards 🔒 (no spare internal user API) |
| 13 | Departments & master data | Guest, internal | `/departments`, `/departments/{id}`, `/skills` | ✅ | BUG-012 (PII); Q-MDATA-01 (no CRUD) |
| 14 | Workflow automation + MCP | SystemAdmin | `/sysadmin/automation/*`, `/sysadmin/mcp/*` | ✅ reads (5 wf, 8 exec, 6 tools, audits) | live dispatch 🔒 (needs event + CV) |
| 15 | Copilot & AI | HR, Manager | `/copilot/jobs`, `/conversations`, `/prompt-templates`, `/artifacts`, `/rules` | ✅ deterministic reads | 🔒 generative (AI provider), ❌ BUG-003 discovery |
| 16 | Validation & error handling | all | (all `[FromBody]` endpoints) | ❌ | BUG-005 (systemic), BUG-006, BUG-007, BUG-008 |
| 17 | File storage / MinIO / resumes | Candidate, HR | `/candidate/profile/resume`, `/resumes/{id}/*`, `/hr/applications/{id}/cv` | 🔒 | BUG-011 (MinIO 500) |
| 18 | System & audit logs | SystemAdmin, Manager | `/sysadmin/audit-logs` | ✅ | Q-LOG-01 (no status-history) |
| 19 | Common UI & navigation | all | (SPA) | 🖥️ not run | no browser tool |
| 20 | API-wide cross-cutting | all | error envelope, method/CT, oversized, IDOR, injection | ✅ | — |
| 21 | Security & session | Guest + all | headers, CORS, SQLi/XSS, leak, portal-sep | ✅ except headers | ❌ BUG-004 (headers) |
| 22 | End-to-end scenarios | mixed | job lifecycle (create→approve→public→delete), registration | ✅ partial / 🔒 full-hire | 🔒 hire chain needs CV/MinIO |

## C. Business state machines

| State machine | Coverage | Status |
|---|---|---|
| Application (Applied→Screening→ManagerReview→Interview→Offer→Hired; Rejected/OfferDeclined/Withdrawn) | Transition gates verified via API (valid→mutates avoided on seed; invalid→`422`); happy advance verified on `[UAT]` up to apply | ✅ gates / 🔒 full drive (CV) |
| Job (Draft→PendingApproval→Approved/Rejected→Closed) | Create→PendingApproval→Approved (Head)→public→delete verified on `[UAT]` job | ✅ ; Q-JOB-01 (terminal re-open) |
| Interview (Scheduled→Completed/Canceled) | Reads verified; schedule negative → `422`; out-of-range time → `500` | ✅ / ❌ BUG-007 |
| Offer (Draft→Sent→Accepted/Declined) | Gate `INTERVIEW_NOT_COMPLETED` verified; send/email leg blocked (mail) | ✅ gate / 🔒 email |
| User status (Active↔Inactive) | Disabled-login (`yennhi`→401 `ACCOUNT_DISABLED`); invalid-enum/non-existent guards | ✅ |
| Workflow engine (Disabled/Shadow/Live; executions) | Read-only inventory verified (definitions/versions/executions/dead-letters/heartbeats) | ✅ reads / 🔒 live cutover |

## D. Cross-cutting requirements

| Requirement | Verified? | Notes |
|---|---|---|
| Portal separation (candidate ↔ internal) | ✅ | 401 `PORTAL_ACCESS_DENIED` both directions |
| Ownership / IDOR (reads) | ✅ | candidate & HR read scoping enforced |
| Ownership (writes) | ❌ | BUG-010: early-stage HR decision has no ownership gate (asymmetry) |
| Error envelope + traceId, no leak | ✅ | incl. all 500s (no stack/SQL/secret) |
| Injection (SQLi/XSS) | ✅ | not reflected; no SQL error |
| Input validation | ❌ | BUG-005: filter never runs validators |
| Security headers / TLS hardening | ❌ | BUG-004 |
| i18n / responsive / console errors | 🖥️ | UI-only, not run |

## E. Traceability to prior known defects (BUG-UAT-001..004)

| Prior defect | This run |
|---|---|
| BUG-UAT-001 (auth UI session) | Not re-testable via API tooling; API login/refresh mechanics correct; **re-verify in browser after redeploy** |
| BUG-UAT-002 (draft/pending detail leak) | **Re-confirmed OPEN** (200 for Draft & Pending to anon) |
| BUG-UAT-003 (AI discovery 400) | **Re-confirmed OPEN** (talent-pool/discovery/similar → 400) |
| BUG-UAT-004 (security headers) | **Re-confirmed OPEN** (all 5 headers missing; `Server: nginx/1.24.0`) |

## F. Coverage honesty statement

- **Executed:** 366 API-layer cases across all 22 UAT suites and all 6 state machines.
- **Not executed as written:** UI-interaction cases (dashboards, forms, navigation, responsive, console/JS
  errors, SSE realtime rendering) — this environment had **no browser-automation tool**; their **API legs
  were executed** instead and are reflected above.
- **Blocked by production infra:** file/CV/MinIO flows, AI-generative & semantic discovery, email delivery,
  OData, and any flow requiring a candidate CV (full apply→hire chain).
- Every `UAT-*` suite in [`UAT_TEST_CASES.md`](../UAT_TEST_CASES.md) is represented here by its API-verifiable
  subset with an explicit status; UI-only and infra-blocked cases carry a documented reason rather than a
  fabricated PASS.
