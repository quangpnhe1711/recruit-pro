# UAT Summary — RecruitPro (Production)

> **Environment:** Production `https://www.recruitpro.site` · API `…/api` · **Date:** 2026-07-12
> **Tester:** UAT (Claude Code) · **Method:** API-direct (Node 22 `fetch`) against Production, grounded in
> `init.sql` seed identities. **UI/browser cases were not executed** (no browser-automation tooling in this
> environment) — see §Not-tested / Blocked.
> **Reports:** [`UAT_EXECUTION_REPORT.md`](UAT_EXECUTION_REPORT.md) · [`UAT_BUG_REPORT.md`](UAT_BUG_REPORT.md) · [`UAT_COVERAGE_MATRIX.md`](UAT_COVERAGE_MATRIX.md) · evidence in [`evidence/`](evidence/)

## 1. Execution totals

| Metric | Count |
|---|---:|
| **Test cases executed** | **366** |
| **PASS** | **342** |
| **FAIL** | **20** (→ 9 unique defects) |
| **BLOCKED** | **4** (MinIO/CV-dependent apply flow) |
| **NOT RUN** (UI/infra — see §5) | UI browser flows, file/AI-generative/email/SSE, OData |
| **Pass rate** (executed, excl. BLOCKED) | **94.5%** |

The 366 executed cases are API-layer assertions (status + error-code + data/side-effect) covering all 4
portals, all 5 roles + anonymous, and 100+ endpoints. The FAIL rows collapse to **9 unique new/confirmed
defects** (many rows share one bug, e.g. every security-header row → BUG-UAT-004).

## 2. Bugs by severity

| Severity | Count | IDs |
|---|---:|---|
| Blocker | 1 | BUG-UAT-001 (auth UI — not re-testable without a browser; API mechanics OK) |
| Critical | 0 | — |
| High | 3 | BUG-UAT-002 (draft/pending job leak), **BUG-UAT-005** (validation filter dead — systemic), **BUG-UAT-006** (profile accepts malicious input / stored-XSS) |
| Medium | 6 | **BUG-UAT-007** (interview 500), **BUG-UAT-008** (pagination 500), **BUG-UAT-009** (currency=USD), **BUG-UAT-010** (BAC read/write asymmetry), BUG-UAT-003 (AI discovery), BUG-UAT-004 (security headers) |
| Low | 3 | **BUG-UAT-011** (CV/MinIO 500), **BUG-UAT-012** (dept email PII), **BUG-UAT-013** (OData unreachable) |

**Bold = found in this run (9 new).** Non-bold = re-confirmed from the prior production UAT (BUG-UAT-001..004).

## 3. Bugs by module

| Module | Defects |
|---|---|
| Validation (cross-cutting) | BUG-UAT-005 (dead validator filter) → drives BUG-UAT-006, -007 |
| Jobs — public | BUG-UAT-002 (detail leak), BUG-UAT-008 (pagination 500), BUG-UAT-009 (currency), BUG-UAT-013 (OData) |
| Candidate profile | BUG-UAT-006 (no validation / stored-XSS URL) |
| Interviews | BUG-UAT-007 (out-of-range time → 500) |
| Applications / RBAC | BUG-UAT-010 (non-owner HR write vs read asymmetry) |
| AI / semantic discovery | BUG-UAT-003 (400 for valid queries) |
| Edge / nginx | BUG-UAT-004 (missing headers, version disclosure) |
| Files / MinIO | BUG-UAT-011 (storage 500) |
| Departments | BUG-UAT-012 (public head email PII) |
| Auth / SPA session | BUG-UAT-001 (prior; UI not re-testable here) |

## 4. Most serious issues

1. **BUG-UAT-005 — request-body validation is globally disabled.** The `ValidationActionFilter` looks up the
   `ValidateAsync` overload on the wrong interface (`IValidator<T>` instead of base `IValidator`), so
   `GetMethod` returns null and **every** validator is silently skipped. Critical flows survive on
   service-level guards, but validator-only fields have **zero** server-side validation. This is the single
   highest-leverage fix (one method) and the root cause of BUG-UAT-006 and BUG-UAT-007.
2. **BUG-UAT-006 — profile update accepts a `javascript:` URL** (and oversized/blank/malformed fields) →
   stored-XSS risk on the authenticated candidate/HR view + data corruption.
3. **BUG-UAT-002 — Draft/PendingApproval jobs are readable by anonymous** via the detail endpoint (IDs are
   enumerable). Broken access control on unpublished postings. (Unchanged since the prior run.)
4. **Two unhandled 500s from unvalidated input** — pagination `page/pageSize ≤ 0` (anonymous, BUG-UAT-008)
   and interview `startMinutes` out of range (BUG-UAT-007). No stack/secret leak, but crashes instead of `400`.
5. **BUG-UAT-010 — access-control asymmetry:** a non-owner HR is blocked from **reading** an application
   (403) yet the decision endpoint has **no ownership gate** for early-stage transitions (source-confirmed) —
   they can change its status blind. Documented as intentional in code → needs PO confirmation.

## 5. Flows that could NOT be tested (Blocked / Not run) + reasons

| Area | Status | Reason |
|---|---|---|
| All **authenticated UI** flows (dashboards, forms, lists, nav, responsive, console/JS errors) | NOT RUN | No browser-automation tool in this environment; also gated by BUG-UAT-001 on prod. API legs were verified directly. |
| **Candidate apply → interview → offer → hire** happy path | BLOCKED | `apply` requires a CV; **CV upload (MinIO) returns 500** (BUG-UAT-011). Negative gates were all verified. |
| **File / CV / resume** upload, preview, download | BLOCKED | MinIO/object storage unavailable on prod (500). |
| **AI generative** (Copilot ranking narrative, fit-analysis, interview questions, email draft) + **semantic discovery** | BLOCKED | AI/embedding provider unavailable (BUG-UAT-003). Deterministic Copilot reads/pool work. |
| **Email delivery** (offer / rejection emails) | NOT RUN | No outbound-mail sink to inspect; status-transition gating verified. |
| **Notification SSE realtime** rendering | PARTIAL | SSE stream is auth-gated and needs a browser/EventSource client; list/counts/mark-read verified via API. |
| **OData** query surface | BLOCKED | `/odata/*` not reachable on prod (BUG-UAT-013). |
| Per-user **notification settings**, **multi-currency** offers, **master-data CRUD** | BLOCKED | No write API (documented gaps Q-NOTI-02, Q-MDATA-01). |
| **Live cross-HR decision-write demo** (BUG-UAT-010 write leg) | BLOCKED | Requires a `[UAT]` application (needs CV/MinIO) or seed mutation; read-leg verified live, write-leg source-confirmed. |

## 6. Areas that PASSED (backend strengths)

- **Authentication & session (API):** valid/invalid/disabled/portal-separation/refresh/tampered-token — all correct (23/23).
- **RBAC authorization matrix:** **198/198** — every protected endpoint returns the correct `200`/`401`/`403`
  for Candidate, HR, Manager, HeadDepartment, SystemAdmin, and anonymous. No menu-hidden-but-API-open case.
- **IDOR / BOLA / ownership on reads:** candidates cannot act on others' applications/offers; HR cannot read
  non-owned applications; cross-portal access denied.
- **Workflow transition gates:** invalid/backward transitions → `422 INVALID_APPLICATION_TRANSITION`;
  offer-before-completed-interview → `422 INTERVIEW_NOT_COMPLETED`; duplicate apply → `409`; apply to
  draft/closed/pending → `422 JOB_NOT_ACCEPTING_APPLICATIONS`.
- **Error contract:** every 4xx/5xx returns the `ApiResponse` envelope with `error.code` + `traceId`, **no**
  stack traces, SQL, or secrets — even on the 500s.
- **Injection handling:** SQLi in login/search → safe `401`/`200` (no SQL error); XSS payloads not reflected;
  CORS not reflected to foreign origins; method-not-allowed → `405`; malformed JSON → `400`.
- **Seed data consistency:** 24 users, 5 roles, 14 jobs (by status), workflows/executions/MCP audit counts
  match `init.sql`.

## 7. Release recommendation

**Production UAT verdict: CONDITIONAL FAIL — not ready for sign-off.**

The **backend authorization and business-rule core is release-quality**, but there are **blocking-class gaps**
that must be resolved first:

- **Must fix before release (Blocker/High):**
  - **BUG-UAT-005** — restore request-body validation (one-method fix; unblocks a whole class of bad-input bugs).
  - **BUG-UAT-006** — profile stored-XSS / no validation (follows from 005 + URL scheme allow-list).
  - **BUG-UAT-002** — stop leaking Draft/PendingApproval jobs to anonymous callers.
  - **BUG-UAT-001** — deploy & re-verify the SPA auth-session fix (confirm in a browser; the entire
    authenticated UX depends on it, and it could not be validated here).
- **Should fix before release (Medium):** BUG-UAT-007 & BUG-UAT-008 (invalid input → 500), BUG-UAT-009
  (currency mislabeled USD on every salary), BUG-UAT-010 (decide the HR ownership model), BUG-UAT-003
  (AI provider or clearer unavailable signal), BUG-UAT-004 (edge security headers).
- **Acceptable to defer (Low, track):** BUG-UAT-011 (MinIO provisioning + graceful storage error),
  BUG-UAT-012 (dept email PII), BUG-UAT-013 (OData routing).

**Cannot recommend acceptance** until BUG-UAT-005/006/002 are fixed and the authenticated UI is verified in a
browser (BUG-UAT-001), and until the CV/MinIO path is provisioned so the core candidate journey
(register-with-CV → apply → interview → offer → hire) can be exercised end-to-end.

## 8. Notes on test integrity

- All `[UAT]`-created data was removed (0 active `[UAT]` users, 0 `[UAT]` jobs — verified by final sweep).
- Two accidental seed mutations during negative testing were **restored and re-verified**: `nhatquang`'s
  profile (bio/fields back to `init.sql` values) and a rogue interview on `nhatquang`'s application (deleted;
  app back to its single seed interview).
- No secrets/tokens/passwords are recorded in any report; the shared demo password is public seed data.
