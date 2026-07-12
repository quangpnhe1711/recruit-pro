# UAT Open Questions & Requirement Gaps — RecruitPro

> **Document:** `docs/uat/UAT_OPEN_QUESTIONS.md` · **Version:** 1.0 · **Date:** 2026-07-12
> Items found while deriving test cases from the implementation. Per task rules, when docs and code
> disagreed the **implementation** was taken as the UAT baseline and the discrepancy logged here.
> Each item: *Observed behavior* (from code) vs *Expected/documented* · *Source* · *UAT impact* · *Owner*.

**Legend — Owner:** PO = Product Owner · BE = Backend · FE = Frontend · DevOps = Infra/Edge · QA = Test.

---

## A. Known production defects (already filed — cross-referenced)

These are confirmed in `docs/uat/PRODUCTION_UAT_BUG_REPORT.md`; listed here so the UAT suite's expected
results and coverage status are unambiguous.

| ID | Module | Observed (prod) | Expected | Source | UAT impact | Owner |
|---|---|---|---|---|---|---|
| BUG-UAT-001 | Auth/SPA session | After a `200` login the SPA persists no token; every authenticated route redirects to login | Session persists across nav/refresh | `recruit-pro-internal/src/services/auth/authToken.ts` (`hasValidStoredSession`) | **Blocks all authenticated UI cases on prod** until redeploy; fixed in source | FE/DevOps |
| BUG-UAT-002 | Jobs (public) | `GET /api/jobs/{id}` returns `200` for Draft/PendingApproval jobs to anonymous | `404` for non-publicly-visible statuses | `JobController` public detail | UAT-PUB-011/UAT-SEC-011 expected to FAIL until fixed | BE |
| BUG-UAT-003 | Semantic/AI discovery | Valid queries return `400 INVALID_INPUT` (provider unavailable) | Ranked `200`, or clear `AI_PROVIDER_UNAVAILABLE`/empty `200` | `SemanticDiscoveryController` + AI provider config | UAT-AI-018, endpoints 62–67 Blocked; misleading error | BE/DevOps |
| BUG-UAT-004 | Edge/nginx | Missing HSTS/CSP/X-Frame/X-Content-Type/Referrer-Policy; `Server` version disclosed | Security headers present; `server_tokens off` | nginx edge config | UAT-SEC-008 FAIL | DevOps |

---

## B. Requirement / behavior gaps (implementation-derived)

### Q-JOB-01 — No job state-transition matrix; terminal jobs can be re-opened
- **Observed:** `JobService.PatchJobAsync` assigns any parsed target status directly. Only the
  `Approved`/`Rejected` transition is authorization-guarded (department head). A `Closed` or `Rejected` job
  can be `PATCH`ed back to `Approved`/`Draft`/etc. The `INVALID_JOB_TRANSITION` code exists but is **never
  used**.
- **Expected/documented:** STATE-MACHINE implies Closed/Rejected are effectively terminal for a posting.
- **Source:** `RecruitPro.Application/Services/JobService.cs` (~:695–716, :1077); `ErrorCodes.cs:39`.
- **UAT impact:** UAT-JOB-017 documents actual (likely re-open succeeds); expected behavior undefined.
- **Owner:** PO/BE.

### Q-JOB-02 — `Draft` status is never assigned by any flow
- **Observed:** `CreateJobAsync` always creates jobs as `PendingApproval`; no code path sets `Draft`. Seed
  has one Draft job. FE has a "Save Draft" affordance.
- **Expected:** JOB-APPROVAL-FLOW describes "HR creates a job (Draft) → submit for approval".
- **Source:** `JobService.cs` create; seed `init.sql`; `JobCreatingScreen.tsx`.
- **UAT impact:** Cannot create a Draft via the API create flow; UAT-JOB-001 notes Pending as actual.
- **Owner:** PO/BE/FE.

### Q-JOB-03 — Job rejection stores no reason
- **Observed:** `PublishJobRejectedAsync(reason: null)`; there is no rejection-reason field on the job.
- **Expected:** Approval flows usually record why a posting was rejected.
- **Source:** `JobService.cs:807–809`.
- **UAT impact:** UAT-JOB-011 cannot verify a stored reason.
- **Owner:** PO/BE.

### Q-JOB-04 — `DEPARTMENT_HEAD_REQUIRED` (no-head) path unreachable on seed
- **Observed:** All 8 seeded departments have `head_user_id = tiendat`, so the "department has no head →
  422" branch cannot be exercised without creating a headless department (no create-department API).
- **Source:** `init.sql` departments; `EvaluateApprovalAccess`.
- **UAT impact:** UAT-JOB-012 no-head leg is Blocked on prod.
- **Owner:** QA (data prep) / BE.

### Q-JOB-05 — Deleting a job that has applications — behavior undefined
- **Observed:** `DELETE /api/hr/jobs/{id}` — unclear whether it blocks when applications exist or cascades.
- **Source:** `JobController` delete; FK `applications.job_id → jobs (CASCADE)`.
- **UAT impact:** UAT-JOB-018 records actual; PO must confirm intended rule.
- **Owner:** PO/BE.

### Q-INT-01 — Past-date interviews accepted (no time guard)
- **Observed:** `CreateInterviewAsync` enforces no past/time validation; `INTERVIEW_TIME_IN_PAST` code is
  unused. A past date is accepted.
- **Source:** `InterviewService.cs`; `ErrorCodes`.
- **UAT impact:** UAT-INT-014 documents actual; PO to confirm whether past scheduling should be blocked.
- **Owner:** PO/BE.

### Q-OFFER-01 — No offer terminal guard; re-sending an offer re-opens a closed application
- **Observed:** `CanPrepareOffer` includes `Hired`/`OfferDeclined`, so `SendOfferAsync` on an already-closed
  application is not blocked and would flip the application back to `Offer` and the offer to `Sent`.
  `OFFER_ALREADY_SENT` exists but is unused.
- **Expected:** A `Hired`/`OfferDeclined` application should be terminal for offer actions.
- **Source:** `OfferService.cs` (`CanPrepareOffer`, `SendOfferAsync`).
- **UAT impact:** UAT-OFFER-011 documents actual (potential regression of a terminal state).
- **Owner:** PO/BE.

### Q-STATUS-01 — `ManagerReview` enum = DepartmentHeadReview business stage
- **Observed:** The code enum value stays `ManagerReview`; the UI label is "Head Review". Unknown status
  must resolve to neutral `Unknown`, **never** `Rejected`.
- **Source:** BUSINESS-RULES BR-APPLICATION-012; `getApplicationStatusPresentation`.
- **UAT impact:** UAT-APP-012 verifies label mapping; documented, not a defect.
- **Owner:** QA (verify FE mapping).

---

## C. Frontend / backend contract mismatches

### Q-RBAC-01 — FE permission vocabulary ≠ backend DB permissions; FE sub-perms not enforced per route
- **Observed:** The SPA gates with its own keys (`system:admin`, `job:approve`, `application:view-all`, …)
  derived from roles + `user.permissions`, which are **distinct** from the 27 backend DB codes (`Job_APPROVE`,
  `PERMISSION_MANAGE`, …). Also the system-admin sub-permissions (`system:users-view`, `system:roles-view`,
  `system:permissions-view`, `system:audit-logs-view`) are defined and granted but **only `system:admin`
  gates the `/system-admin/*` tree client-side**; per-page enforcement is backend-only.
- **Expected:** A documented, consistent mapping so a menu never offers an action the API denies (or vice
  versa).
- **Source:** `src/permissions/permissions.ts`, `rolePermissions.ts`; backend `init.sql` permissions.
- **UAT impact:** UAT-RBAC-022 audits mismatches; low security risk (server is authoritative) but a UX/
  consistency concern.
- **Owner:** FE/BE/PO.

### Q-AI-02 — Copilot candidate pool returns `education` as a raw JSON string
- **Observed:** `GET /api/copilot/jobs/{id}/candidates` returns `education` as the raw `EducationRecordsJson`
  (or legacy plain string, or null); the DTO is not structured. FE normalizes and never renders raw JSON.
- **Expected:** A structured DTO field.
- **Source:** `CopilotRepository.GetCandidatePoolAsync`; `src/common/utils/aiRankingPresentation.ts`.
- **UAT impact:** UAT-AI-003 verifies FE never shows raw JSON; backend contract intentionally unchanged.
- **Owner:** BE (future) / QA (verify FE).

### Q-UI-01 — `/internal/profile` is a placeholder (backend pending)
- **Observed:** Route renders `FeaturePlaceholderScreen`; no backend behind it yet.
- **Source:** `hr.routes.tsx`; `FeaturePlaceholderScreen`.
- **UAT impact:** No functional UAT beyond "placeholder shows"; exclude from functional coverage.
- **Owner:** PO/BE.

### Q-API-01 — Dead / unused FE endpoint definitions
- **Observed:** `endpoints.ts` defines endpoints with no service wrapper (e.g. `auth.logout` → `/auth/logout`,
  some `hrJobs.*` variants, singular automation event / mcp audit getters). No backend `/auth/logout` exists.
- **Source:** `src/services/http/endpoints.ts`.
- **UAT impact:** None functional; flagged so testers don't chase non-wired endpoints.
- **Owner:** FE (cleanup).

---

## D. Data / seed gaps affecting coverage

### Q-NOTI-01 — Seeded `notification_events` are legacy codes, not the emitted Phase-6 codes
- **Observed:** Seed lookup has `new_application_received`, `application_status_changed`,
  `interview_scheduled`, `candidate_score_ready`. The code emits Phase-6 codes (`application_applied`,
  `job_approved`, `application_department_head_review_requested`, …) that are **not** in the lookup table.
- **Expected:** Lookup table aligned with emitted codes (esp. if `user_notification_settings` keys off it).
- **Source:** `init.sql :2310`; `NotificationEventCodes.cs`.
- **UAT impact:** UAT-NOTI-017; in-app delivery still works (doesn't depend on lookup), but per-event settings
  and any lookup-driven UI may be incomplete.
- **Owner:** BE/PO.

### Q-NOTI-02 — `user_notification_settings` empty + no write API
- **Observed:** Table empty; no endpoint to set per-user notification preferences.
- **UAT impact:** DATA-PREP-07 Blocked; notification opt-out/email-toggle cases cannot be executed.
- **Owner:** BE/PO.

### Q-MDATA-01 — No CRUD API for master data; only VND currency; only FullTime seeded
- **Observed:** Skills, benefits, currencies, offer templates are **seed-only** (no create/update/delete
  API). Only **VND** currency and only **FullTime** employment type are seeded.
- **UAT impact:** UAT-MDATA-009, UAT-OFFER-014 Blocked; master-data management cannot be UAT'd via API;
  multi-currency offers untestable.
- **Owner:** PO/BE.

### Q-AI-03 — All Copilot/AI runtime tables + v5 AI-Ops tables start empty
- **Observed:** `copilot_*`, `candidate_fit_analyses`, `copilot_generated_artifacts`, and all `ai_*`/
  `prompt_template_versions`/`provider_routing_policies` tables are schema-only.
- **UAT impact:** AI-Ops dashboards (114–118) show empty until an AI run occurs; combined with BUG-UAT-003
  they are effectively unpopulated on prod.
- **Owner:** QA/BE/DevOps.

---

## E. File storage / security hardening questions

### Q-FILE-01 — Resume preview/download are `[Authorize]` (any auth) — ownership scope?
- **Observed:** `GET /api/resumes/{id}/preview|download` require authentication but the role is any
  authenticated user. Whether a candidate can fetch **another** candidate's resume id (or an HR a
  non-owned CV) must be verified.
- **UAT impact:** UAT-FILE-009 must confirm ownership enforcement beyond bare auth; potential IDOR if not.
- **Owner:** BE/QA (high priority to verify).

### Q-FILE-02 — Orphan handling on partial upload/DB failure
- **Observed:** Unclear whether a MinIO object is cleaned up if the DB write fails after upload (or a DB row
  is left if upload fails).
- **UAT impact:** UAT-FILE-010, UAT-PROF-019 record actual; data-integrity concern.
- **Owner:** BE.

### Q-FILE-03 — CSV/Excel export injection escaping
- **Observed:** Unverified whether exported CSV escapes formula-leading cells (`=`, `+`, `-`, `@`).
- **UAT impact:** UAT-FILE-014.
- **Owner:** BE/QA.

### Q-SEC-01 — No rate limiting / brute-force protection observed
- **Observed:** No throttling on login/refresh apparent in the auth pipeline.
- **UAT impact:** UAT-SEC-013 records actual; PO to confirm whether required.
- **Owner:** PO/DevOps.

### Q-SEC-02 — Public `GET /api/departments` exposes head user name + email
- **Observed:** The public department lookup includes `headUserName`/`headUserEmail`.
- **Expected:** Confirm whether exposing an internal user's email to anonymous callers is intended.
- **Source:** API-CONTRACT department fields; #7 is anonymous.
- **UAT impact:** UAT-PUB-013 flags; minor PII exposure.
- **Owner:** PO/BE.

---

## F. Auth / logging / misc

### Q-AUTH-01 — "Remember me" semantics undocumented
- **Observed:** Login forms carry a `remember` flag; the exact effect on token persistence isn't documented.
- **UAT impact:** UAT-AUTH-025 records actual behavior.
- **Owner:** PO/FE.

### Q-LOG-01 — No login audit; no application/job status-history table
- **Observed:** Login success/failure is not written to `system_logs`. ATS status transitions have **no**
  history/audit entity — only user-account status changes (RBAC) and automation executions are audited.
- **Expected:** For an ATS, a per-application status history is commonly expected.
- **Source:** `AuthService`; absence of a status-history entity (confirmed in code).
- **UAT impact:** UAT-LOG-002, UAT-APP-033 — cannot verify a transition audit trail; metrics derive from
  current status only.
- **Owner:** PO/BE.

### Q-WF-01 — Workflow validation is coarse (all invalid enums → `VALIDATION_FAILED`)
- **Observed:** `WorkflowDefinitionService.Validate` collapses unknown trigger/mode/action/operator into a
  single `VALIDATION_FAILED` (flagged with a `ponytail:` comment as intentionally coarse).
- **UAT impact:** UAT-WF-004 cannot distinguish which field was invalid from the code alone.
- **Owner:** BE (optional precision).

### Q-DEP-01 — Mixed authorization strategy across SysAdmin controllers
- **Observed:** RBAC/Directory controllers use `[RequirePermission]` (DB codes); MCP/Automation/AiOps use
  hard `[Authorize(Roles="SystemAdmin")]`. AiOps comments mention intended permissions (`ai.metrics.view`,
  `ai.risk_flags.view`) not actually enforced.
- **UAT impact:** UAT-WF-014 / UAT-RBAC-002 — different denial mechanisms; consistent 403 for non-admins, but
  the granularity differs. Document for PO.
- **Owner:** PO/BE.

### Q-ERR-01 — ~30 messages mapped to generic codes
- **Observed:** ERROR-CONTRACT notes ~30 previously-hardcoded messages mapped to generic codes
  (`INVALID_INPUT`, `ENTITY_NOT_FOUND`, `BUSINESS_RULE_VIOLATION`); some domain errors are less specific than
  ideal (e.g. `RANKING_SESSION_NOT_FOUND`, `INTERVIEWER_NOT_FOUND` not distinct).
- **UAT impact:** Some negative cases can only assert the generic code + status, not a precise domain code.
- **Owner:** BE (optional precision).

---

## G. Summary of UAT-blocking items

| Priority to resolve | Items |
|---|---|
| **Blocks authenticated UI UAT on prod** | BUG-UAT-001 (deploy fix) |
| **Security to confirm before sign-off** | BUG-UAT-002, Q-FILE-01 (resume IDOR), Q-SEC-02 (dept email), BUG-UAT-004 |
| **Feature effectively inert on prod** | BUG-UAT-003 + Q-AI-03 (AI provider), Q-MDATA-01 (master-data mgmt), Q-NOTI-02 (notif settings) |
| **PO business-rule decisions** | Q-JOB-01/02/03/05, Q-INT-01, Q-OFFER-01, Q-SEC-01, Q-LOG-01 |
| **Consistency / cleanup** | Q-RBAC-01, Q-AI-02, Q-API-01, Q-WF-01, Q-DEP-01, Q-ERR-01, Q-NOTI-01, Q-UI-01 |

Total open questions: **25** (+4 known production defects cross-referenced).
