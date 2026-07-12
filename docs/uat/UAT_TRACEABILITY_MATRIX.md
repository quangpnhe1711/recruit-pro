# UAT Traceability Matrix — RecruitPro

> **Document:** `docs/uat/UAT_TRACEABILITY_MATRIX.md` · **Version:** 1.0 · **Date:** 2026-07-12
> Maps modules, routes, APIs, roles/permissions, requirements, state transitions, business flows, database
> entities, and risks to the test cases in [`UAT_TEST_CASES.md`](UAT_TEST_CASES.md). Full per-endpoint case
> mapping (matrix 5) lives in [`UAT_API_COVERAGE.md`](UAT_API_COVERAGE.md); it is summarized here.

## Contents
1. Module → Route · 2. Module → API · 3. Role → Permission → Action · 4. Requirement → Test Case ·
5. API Endpoint → Test Case (summary) · 6. State Transition → Test Case · 7. Business Flow → E2E ·
8. Database entity → Test Case · 9. Risk → Test Case · 10. Coverage gaps (uncoverable).

---

## 1. Module → Route

| Module | Portal | Routes (FE) | Test cases |
|---|---|---|---|
| Public | Public | `/`, `/home`, `/jobs`, `/jobs/:id`, `/internal/jobs*` (redirects) | UAT-PUB-* |
| Authentication | Public/all | `/login`, `/register`, `/internal/login`, `/internal` | UAT-AUTH-*, UAT-SEC-05/14 |
| Candidate registration | Public | `/register` | UAT-REG-* |
| Candidate profile | Candidate | `/candidate/profile/*` | UAT-PROF-* |
| Candidate jobs | Candidate/Public | `/jobs` (adaptive), `/jobs/:id`, `/jobs/:id/apply` | UAT-CJOB-*, UAT-APP-001 |
| Applications | Candidate + HR + Manager | `/candidate/my-applications`, `/hr/applications`, `/hr/applications/:id`, `/manager/applications`, `/manager/applications/:id` | UAT-APP-* |
| Job management | HR/SysAdmin | `/jobs` (JobManagementScreen), `/hr/jobs/create` | UAT-JOB-001..008/016..026 |
| Job approval | Manager/Head | `/jobs` (approval list), `/manager/jobs/:id/approval` | UAT-JOB-009..015, UAT-JOB-025 |
| Interviews | HR/Manager + Candidate | `/hr/interviews`, `/hr/interviews/schedule`, `/candidate/interviews` | UAT-INT-* |
| Offers | HR + Candidate | `/hr/applications/:id/send-offer` | UAT-OFFER-*, UAT-APP-017/018 |
| Notifications | all | bell (global), `data.url` deep links | UAT-NOTI-* |
| Dashboard/analytics | Candidate/HR/Manager | `/candidate/dashboard`, `/hr/dashboard`, `/manager/dashboard`, `/manager/reports` | UAT-DASH-* |
| Users & RBAC | SysAdmin | `/system-admin/dashboard`, `/users`, `/roles`, `/permissions`, `/audit-logs` | UAT-RBAC-*, UAT-LOG-* |
| Departments/master data | HR/Manager/Head/SysAdmin | (in job create + department mgmt) | UAT-MDATA-* |
| Workflow automation | SysAdmin | `/system-admin/automation/*`, `/system-admin/mcp/*` | UAT-WF-* |
| Copilot & AI | HR | `/hr/ai-copilot` | UAT-AI-* |
| Common UI | all | layouts, guards, `*` (404) | UAT-UI-* |
| Internal profile (placeholder) | Internal | `/internal/profile` | UAT-UI-* (Q-UI-01) |

> All `/candidate/*`, `/hr/*`, `/manager/*`, `/system-admin/*` routes wrap in `RequireAuth` +
> `AuthenticatedLayout`; `/system-admin/*` also `RouteGuard(system:admin)`.

## 2. Module → API (controllers)

| Module | Controllers | Endpoint #s | Cases |
|---|---|---|---|
| Auth | AuthController | 1–6 | UAT-AUTH-* |
| Lookups/Dept/Users | Lookup, Department, Users | 7–11 | UAT-MDATA-*, UAT-PUB-013 |
| Notifications | Notification | 12–20 | UAT-NOTI-* |
| Applications | Application | 21–35 | UAT-APP-*, UAT-FILE-003 |
| Dashboard/analytics | Dashboard, ManagerAnalytics | 36–38, 58 | UAT-DASH-* |
| Interviews | Interview | 39–44 | UAT-INT-* |
| Jobs | Job | 45–57 | UAT-JOB-*, UAT-PUB-002/003/011 |
| Offers | Offer | 59–61 | UAT-OFFER-* |
| Semantic discovery | SemanticDiscovery | 62–67 | UAT-AI-018, UAT-CJOB-008 |
| Copilot | Copilot | 68–86 | UAT-AI-* |
| SysAdmin MCP | SysAdminMcp | 87–90 | UAT-WF-015..017 |
| SysAdmin Automation | SysAdminAutomation | 91–104 | UAT-WF-001..013/019..022 |
| SysAdmin RBAC/Directory | SysAdminRbac, SysAdminDirectory | 105–113 | UAT-RBAC-*, UAT-LOG-001/007 |
| SysAdmin AI-Ops | SysAdminAiOps | 114–118 | UAT-WF-* (Partial) |
| Candidate/registration/HR candidates | Candidate | 119–134 | UAT-REG-*, UAT-PROF-*, UAT-FILE-013 |
| Resumes/score/OData/XML/health | Resume, ExternalScore, OData, XmlDemo, minimal | 135–141 | UAT-FILE-002, UAT-API-014/016, UAT-PUB-012 |

## 3. Role → Permission → Action

Backend roles & DB permission grants (from `init.sql`), and where the FE gate differs. **Legend:** ✅ allowed
(role owns resource where applicable) · 🔒 owner/head-scoped · ❌ forbidden (403) · 401 (guest).

| Action / Resource | Guest | Candidate | HR | Manager | HeadDepartment | SystemAdmin | Backend perm | Cases |
|---|:--:|:--:|:--:|:--:|:--:|:--:|---|---|
| Public job browse | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | (anon) | UAT-PUB-002 |
| Register | ✅ | — | — | — | — | — | (anon) | UAT-REG-001 |
| Candidate login | 401 | ✅ | ❌portal | ❌portal | ❌portal | ❌portal | — | UAT-AUTH-002/006 |
| Internal login | 401 | ❌portal | ✅ | ✅ | ✅ | ✅ | — | UAT-AUTH-001/007 |
| View/edit own profile | 401 | ✅🔒 | ❌ | ❌ | ❌ | ❌ | CANDIDATE_PROFILE_* | UAT-PROF-001/013 |
| Apply to job | 401 | ✅ | ❌ | ❌ | ❌ | ❌ | Application_APPLY | UAT-APP-001/010 |
| Withdraw own application | 401 | ✅🔒 | ❌ | ❌ | ❌ | ❌ | — | UAT-APP-014/016 |
| Accept/decline offer | 401 | ✅🔒 | ❌ | ❌ | ❌ | ❌ | — | UAT-APP-017..019 |
| View HR applications | 401 | ❌ | ✅🔒 | ✅🔒 | ❌* | ❌ | Application_VIEW | UAT-APP-021, UAT-DASH-010 |
| Application decision (Applied→Screening→ManagerReview) | 401 | ❌ | ✅🔒 | ✅🔒 | (head via ownership) | ❌ | Application_REVIEW | UAT-APP-022/023/028 |
| ManagerReview→Interview | 401 | ❌ | ❌ | fallback🔒 | ✅🔒(assigned head) | ✅ | (ownership) | UAT-APP-024 |
| Send offer / rejection email | 401 | ❌ | ✅🔒 | ✅🔒 | ❌ | ❌ | (ownership) | UAT-OFFER-002, UAT-APP-026 |
| Create job | 401 | ❌ | ✅ | ✅ | ❌ | ❌ | Job_CREATE | UAT-JOB-001 |
| Approve/reject job | 401 | ❌ | ❌ | ✅🔒(if head) | ✅🔒(dept head) | ❌ | Job_APPROVE + ownership | UAT-JOB-010/012 |
| Delete job | 401 | ❌ | ✅🔒 | ✅🔒 | ❌ | ❌ | Job_DELETE(SA only in DB)* | UAT-JOB-018/022 |
| Create/complete interview | 401 | ❌ | ✅🔒 | ✅🔒 | view-only | ❌ | Interview_CREATE/UPDATE | UAT-INT-001/013 |
| Copilot / AI ranking | 401 | ❌ | ✅🔒 | ✅🔒 | ❌ | ❌ | (role HR,Manager) | UAT-AI-001/016 |
| Manager analytics | 401 | ❌ | ❌ | ✅🔒 | ✅🔒 | ❌ | (role) | UAT-DASH-004/010 |
| SysAdmin users/RBAC | 401 | ❌ | ❌ | ❌ | ❌ | ✅ | USER_*/ROLE_*/PERMISSION_* | UAT-RBAC-002/016 |
| Automation / MCP | 401 | ❌ | ❌ | ❌ | ❌ | ✅ | (role SystemAdmin) | UAT-WF-014 |
| Audit logs | 401 | ❌ | ❌ | ✅(Manager has System_LOG_VIEW) | ❌ | ✅ | System_LOG_VIEW | UAT-LOG-001 |

> `*` Notes: HeadDepartment DB grant is minimal (Interview_VIEW, DEPARTMENT_VIEW, SKILL_VIEW,
> NOTIFICATION_VIEW) — its job-approval / head-review authority is enforced by **ownership columns**, not a
> permission code. `Job_DELETE` is granted only to SystemAdmin in the DB seed, yet the endpoint admits
> HR,Manager — a grant/endpoint mismatch to verify (see Q-RBAC-01, UAT-RBAC-022). `Manager` uniquely holds
> `System_LOG_VIEW`.

Full seed grants: see [`UAT_TEST_DATA.md`](UAT_TEST_DATA.md) §3.3. FE permission bundles: §3.4.

## 4. Requirement → Test Case

| Requirement (source-of-truth) | Summary | Cases |
|---|---|---|
| BR-APPLICATION-001 | ≤1 active application per (candidate, job) → 409 | UAT-APP-007 |
| BR-APPLICATION-002 | Re-apply after Withdrawn/Rejected/OfferDeclined; Hired terminal | UAT-APP-008/009 |
| BR-APPLICATION-003 | Withdrawal → `Withdrawn` (not Rejected) | UAT-APP-014/015 |
| BR-APPLICATION-004 | Apply only on Approved, non-expired, complete profile+resume | UAT-APP-003/004/005/006 |
| BR-APPLICATION-005 | Committed apply never fails on side effect | UAT-APP-011, UAT-NOTI-013 |
| BR-APPLICATION-006 | Reviewer transitions follow the workflow | UAT-APP-022..027 |
| BR-APPLICATION-008 | Interview gated by application status | UAT-INT-002, UAT-APP-024 |
| BR-APPLICATION-009 | Offer gated by status; Hired only via candidate accept | UAT-APP-017/025, UAT-OFFER-002 |
| BR-APPLICATION-010 | Notifications post-commit, never source of truth | UAT-NOTI-013 |
| BR-APPLICATION-011 | Analytics from canonical state groups | UAT-DASH-004/005 |
| BR-APPLICATION-012 | Canonical English status; localized label separate | UAT-APP-012 |
| BR-OWN-003 | DepartmentHead approves the job | UAT-JOB-010/012 |
| BR-OWN-005 | Application snapshots owners at apply | UAT-APP-001 (data) |
| BR-OWN-006 | HR-first on apply (recruiter-only notify) | UAT-APP-001, UAT-NOTI-016 |
| BR-OWN-007 | DepartmentHead owns ManagerReview onward | UAT-APP-024 |
| BR-WF-001 | Offer email-gated | UAT-APP-025, UAT-OFFER-002 |
| BR-WF-002 | Rejection email-gated (subject+body) | UAT-APP-026 |
| BR-WF-003 | Head-review hand-off date stamped | UAT-APP-023/029 |
| BR-WF-004 | Interview scheduling mandatory before offer/reject | UAT-INT-007 |
| BR-WF-005 | Interview completion mandatory | UAT-INT-008 |
| BR-NOTI-002 | Ownership-based recipients (no broadcast) | UAT-NOTI-016 |
| BR-NOTI-005 | Role-aware deep links (frontend routes) | UAT-NOTI-008 |
| BR-NOTI-007 | Realtime via SSE, user-scoped | UAT-NOTI-007/009 |
| BR-NOTI-008 | Seen vs Read distinct | UAT-NOTI-003/004 |
| BR-NOTI-009 | Interview date no TZ off-by-one | UAT-INT-011 |
| INV-003/014 | EXISTS-active + partial unique index | UAT-APP-007 |
| INV-015 | Hired terminal for jobId | UAT-APP-009 |
| Error contract | Envelope, code-first, 401/403/404/409/422/500 semantics | UAT-ERR-*, UAT-API-004/005 |
| Auth: token_version revocation | Disabled/role change invalidates tokens | UAT-AUTH-017/018, UAT-RBAC-008/014 |
| RBAC: last-admin / self-deactivate guards | 409 guards | UAT-RBAC-010/011 |

## 5. API Endpoint → Test Case (summary)

Per-endpoint case IDs and coverage status for all 141 endpoints: **[`UAT_API_COVERAGE.md`](UAT_API_COVERAGE.md)**.
Summary: 122 Covered · 12 Partially Covered · 7 Blocked (AI/MinIO/mail infra) · 0 Not Implemented. Every
endpoint additionally inherits the API-wide template cases UAT-API-001..016.

## 6. State Transition → Test Case

### 6.1 Application (`ApplicationStatus`)
| From → To | Valid case | Invalid/negative case |
|---|---|---|
| Applied → Screening | UAT-APP-022 | UAT-APP-027 (backward/skip) |
| Screening → ManagerReview | UAT-APP-023 | UAT-APP-027 |
| ManagerReview → Interview (head guard) | UAT-APP-024 | UAT-APP-024 (non-head 403), UAT-APP-027 |
| Interview → Offer (email-gated) | UAT-OFFER-002 | UAT-APP-025, UAT-INT-007/008 |
| * → Rejected (email-gated) | UAT-APP-026 | UAT-APP-025, UAT-INT-008 |
| Offer → Hired (candidate accept) | UAT-APP-017 | UAT-APP-019/020 |
| Offer → OfferDeclined (candidate decline) | UAT-APP-018 | UAT-APP-019 |
| Active → Withdrawn (candidate) | UAT-APP-014 | UAT-APP-015 (from Offer/closed) |
| from terminal (Hired/Rejected/…) | — | UAT-APP-027 |
| Re-apply (closed → new Applied) | UAT-APP-008 | UAT-APP-009 (Hired) |
| Concurrent transition | UAT-E2E-10 | UAT-APP-032 |

### 6.2 Job (`JobStatus`)
| From → To | Valid | Invalid/edge |
|---|---|---|
| (create) → PendingApproval | UAT-JOB-001 | — |
| PendingApproval → Approved (head) | UAT-JOB-010 | UAT-JOB-012 (non-head) |
| PendingApproval → Rejected (head) | UAT-JOB-011 | UAT-JOB-012 |
| Approved → Closed | UAT-JOB-016 | — |
| Closed/Rejected → * (no guard) | — | UAT-JOB-017 (edge, Q-JOB-01) |
| status via alias route | UAT-JOB-013 | UAT-JOB-013 (401/403) |

### 6.3 Interview (`InterviewStatus`)
| From → To | Valid | Invalid |
|---|---|---|
| (create) → Scheduled | UAT-INT-001 | UAT-INT-002 (wrong app stage), UAT-INT-014 (past date edge) |
| Scheduled → Completed | UAT-INT-004 | UAT-INT-018 (bad value) |
| Scheduled → Canceled | UAT-INT-005 | — |
| auto-cancel on withdraw/reject | UAT-INT-012 | — |

### 6.4 Offer (`OfferStatus`)
| From → To | Valid | Invalid/edge |
|---|---|---|
| → Draft | UAT-OFFER-001 | UAT-OFFER-005 |
| Draft → Sent (email-gated) | UAT-OFFER-002 | UAT-OFFER-003/004/017 |
| Sent → Accepted | UAT-APP-017 | UAT-APP-019/020 |
| Sent → Declined | UAT-APP-018 | UAT-APP-019 |
| closed app → Sent (no guard) | — | UAT-OFFER-011 (edge, Q-OFFER-01) |

### 6.5 User account status (`UserStatus`)
| From → To | Valid | Invalid |
|---|---|---|
| Active → Inactive | UAT-RBAC-008 | UAT-RBAC-010 (self), UAT-RBAC-011 (last admin) |
| Active → Blocked | UAT-RBAC-013 | UAT-RBAC-012 (invalid value) |
| Inactive → Active | UAT-RBAC-009 | — |
| effect on session | UAT-AUTH-017/018 | — |

### 6.6 Workflow (automation engine)
| Lifecycle | Cases |
|---|---|
| Event Pending→Processing→Processed | UAT-WF-022 |
| Event → Failed → DeadLetter | UAT-WF-012 |
| Execution Running→Success/Skipped/Failed | UAT-WF-007/011/012 |
| Publish/version activation | UAT-WF-005/018 |
| Enable/disable, mode cutover | UAT-WF-006/008 |

## 7. Business Flow → E2E Test Case

| Flow (task §6) | E2E case | Chained module cases |
|---|---|---|
| E2E-01 Candidate apply journey | UAT-E2E-01 | REG-001, AUTH-002, PROF-002, FILE-001, CJOB-002, PUB-003, APP-001/012, NOTI-016 |
| E2E-02 Job lifecycle | UAT-E2E-02 | JOB-001/010/016, CJOB-011, APP-001/003 |
| E2E-03 Successful hire | UAT-E2E-03 | APP-022/023/024/017, INT-001/004, OFFER-002, DASH-005 |
| E2E-04 Rejection | UAT-E2E-04 | APP-026/027, INT-012 |
| E2E-05 Decline offer | UAT-E2E-05 | APP-018/020 |
| E2E-06 Permission change mid-session | UAT-E2E-06 | RBAC-004/005/014 |
| E2E-07 Disable active user | UAT-E2E-07 | RBAC-008, AUTH-017/018 |
| E2E-08 AI ranking fallback | UAT-E2E-08 | AI-004/012/013 |
| E2E-09 File storage failure | UAT-E2E-09 | FILE-010, PROF-019 |
| E2E-10 Concurrent processing | UAT-E2E-10 | APP-032 |

## 8. Database entity → Test Case

| Table(s) | Module | Cases |
|---|---|---|
| users, roles, permissions, role_permissions, user_roles | Auth/RBAC | UAT-AUTH-*, UAT-RBAC-* |
| refresh_tokens | Auth | UAT-AUTH-011/012/018 |
| candidate_profiles, candidate_skills, candidate_projects, candidate_profile_sections(_items) | Profile | UAT-PROF-*, UAT-REG-001 |
| candidate_resumes | CV/File | UAT-FILE-001/012, UAT-PROF-012 |
| jobs, job_skills, departments, skills | Jobs/master | UAT-JOB-*, UAT-MDATA-*, UAT-PUB-* |
| applications | Applications | UAT-APP-* |
| interviews | Interviews | UAT-INT-* |
| application_offers, application_offer_benefits, offer_templates, offer_benefits, offer_currencies | Offers | UAT-OFFER-* |
| notifications, notification_events, user_notification_settings | Notifications | UAT-NOTI-* |
| copilot_conversations, copilot_messages, copilot_ranking_sessions/results, copilot_saved_rules, copilot_candidate_tags, copilot_prompt_templates, candidate_fit_analyses, copilot_generated_artifacts | Copilot/AI | UAT-AI-* |
| workflow_definitions/_versions, workflow_executions/_steps, workflow_action_dead_letters, workflow_worker_heartbeats, published_domain_events, mcp_tool_audits | Automation/MCP | UAT-WF-* |
| ai_run_telemetry, prompt_template_versions, provider_routing_policies, ai_evaluation_cases/results | AI-Ops (v5) | UAT-WF-* (114–118, Partial) |
| system_logs | Audit | UAT-LOG-*, UAT-RBAC-004/008 |
| (partial unique idx) ux_applications_active_user_job | Applications | UAT-APP-007 |

## 9. Risk → Test Case

| Risk | Severity | Mitigating cases |
|---|---|---|
| Broken access control / privilege escalation | Critical | UAT-RBAC-002/016/017, UAT-SEC-004, UAT-API-005 |
| IDOR / BOLA (cross-user data) | Critical | UAT-APP-016, UAT-PROF-013, UAT-FILE-009, UAT-AI-016, UAT-SEC-006 |
| Auth/session compromise (token replay, revocation) | Critical | UAT-AUTH-012/016/017, UAT-SEC-001/005 |
| Public exposure of non-approved jobs (BUG-UAT-002) | High | UAT-PUB-011, UAT-SEC-011 |
| Business-rule bypass (skip workflow, email gate) | High | UAT-APP-025/027, UAT-INT-007/008, UAT-OFFER-003 |
| Duplicate/concurrent state corruption | High | UAT-APP-007/032, UAT-API-008, UAT-E2E-10 |
| Injection (SQLi/XSS/CSV/prompt) | High | UAT-API-010/011, UAT-FILE-014, UAT-AI-014 |
| Sensitive-data leakage (secrets/PII in responses/errors/logs) | High | UAT-ERR-005, UAT-SEC-007, UAT-AI-015, UAT-LOG-005 |
| AI feature inert / misleading errors (BUG-UAT-003) | Medium | UAT-AI-012/018, UAT-E2E-08 |
| File storage failure → data corruption/orphans | Medium | UAT-FILE-010, UAT-PROF-019, UAT-E2E-09 |
| Missing security headers (BUG-UAT-004) | Medium | UAT-SEC-008 |
| Notification duplication / wrong recipient | Medium | UAT-WF-008, UAT-NOTI-008/016 |
| SPA session unusable (BUG-UAT-001) | Blocker | UAT-AUTH-001/020, UAT-SEC-005 |

## 10. Coverage gaps (cannot be fully covered — traced to open questions)

| Area | Why uncovered | Reference |
|---|---|---|
| Master-data CRUD (skills/benefits/currencies/templates) | No API (seed-only) | Q-MDATA-01, UAT-MDATA-009 |
| Multi-currency offers | Only VND seeded | Q-MDATA-01, UAT-OFFER-014 |
| Per-user notification settings | Table empty + no write API | Q-NOTI-02, DATA-PREP-07 |
| AI generative endpoints on prod | Provider unavailable (BUG-UAT-003) | Q-AI-03, UAT-AI-018 |
| No-head department approval (422 path) | All seed depts headed by tiendat | Q-JOB-04, UAT-JOB-012 |
| Draft job via create flow | Create always PendingApproval | Q-JOB-02, UAT-JOB-001 |
| Application status-history audit | No history table exists | Q-LOG-01, UAT-APP-033 |
| Email delivery verification | No mail sink on prod | UAT-AUTH-021, UAT-OFFER-002 |
| Authenticated UI on prod | BUG-UAT-001 until redeploy | UAT-AUTH-001 |

---
*Nine matrices complete. Uncoverable areas are explicitly traced to open questions / known defects rather
than silently omitted.*
