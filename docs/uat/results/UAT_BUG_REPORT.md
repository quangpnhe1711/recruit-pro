# UAT Bug Report — RecruitPro (Production)

> **Environment:** Production `https://www.recruitpro.site` · API `…/api` · **Date:** 2026-07-12
> **Tester:** UAT (Claude Code) — API-direct via Node 22 `fetch`. UI/browser cases not executed (no
> browser-automation tooling in this environment); every finding below is at the API/contract layer.
> **Seed safety:** all `[UAT]`-created data was cleaned; the one seed profile (`nhatquang`) and one rogue
> interview created during testing were **restored to `init.sql` state and verified** (see notes).
> Tokens/passwords are **not** recorded here (shared demo password is public seed data).

## Severity legend
`Blocker` = release-stopping · `Critical` = data loss / security breach / core flow broken · `High` = major
function or access-control broken (workaround exists) · `Medium` = hardening / non-core / poor error handling ·
`Low` = cosmetic / minor / rare edge.

## Bug index

| ID | Severity | Priority | Module | Title | Status |
|---|---|---|---|---|---|
| BUG-UAT-001 | Blocker | P1 | Auth / SPA session | Authenticated UI unreachable after login (prior finding) | Not re-testable via API tooling — see note |
| BUG-UAT-002 | High | P2 | Jobs (public) | Public job **detail** exposes Draft & PendingApproval jobs to anonymous | **OPEN (re-confirmed)** |
| BUG-UAT-005 | High | P1 | Validation (global) | FluentValidation filter never runs any validator → systemic no request-body validation | **OPEN (NEW)** |
| BUG-UAT-006 | High | P1 | Candidate profile | `PUT /candidate/profile` accepts invalid & malicious input (stored-XSS via `javascript:` URL) | **OPEN (NEW)** |
| BUG-UAT-007 | Medium | P2 | Interviews | `POST /hr/interviews` with out-of-range `startMinutes` → **500** | **OPEN (NEW)** |
| BUG-UAT-008 | Medium | P2 | Jobs (public) | `GET /jobs` with `page`/`pageSize` ≤ 0 → **500** (anonymous-reachable) | **OPEN (NEW)** |
| BUG-UAT-010 | Medium | P2 | Applications / RBAC | Broken-access-control **asymmetry**: non-owner HR can write a decision but cannot read the application | **OPEN (NEW, PO-confirm)** |
| BUG-UAT-003 | Medium | P3 | Semantic/AI discovery | Valid queries return `400 INVALID_INPUT` (provider unavailable) | **OPEN (re-confirmed)** |
| BUG-UAT-004 | Medium | P3 | Edge / nginx | Missing security headers; server version disclosed | **OPEN (re-confirmed)** |
| BUG-UAT-009 | Medium | P3 | Jobs (public) | Salary currency reported as **`USD`** for VND-only amounts | **OPEN (NEW)** |
| BUG-UAT-011 | Low | P3 | Files / MinIO | CV upload / resume path returns raw **500** when storage unavailable | **OPEN (NEW, infra)** |
| BUG-UAT-012 | Low | P3 | Departments (public) | Public `GET /departments` exposes internal head user **email** (PII) | **OPEN (NEW / Q-SEC-02)** |
| BUG-UAT-013 | Low | P3 | OData | `/odata/Jobs` & `/odata/Applications` not reachable on prod (SPA catch-all / not proxied) | **OPEN (NEW)** |

> **Backend verdict:** authentication, the full RBAC/role matrix (198/198), IDOR/BOLA ownership on **reads**,
> application/job workflow-transition gates, the error-envelope contract (traceId, no stack/secret leak),
> SQLi/XSS handling, CORS, and method/content-type handling are all **correct**. The new defects are
> concentrated in **input validation** (a dead validation filter) and **edge/display/infra** concerns.

---

## BUG-UAT-002 — Public job *detail* exposes Draft & PendingApproval jobs (re-confirmed)

- **Severity:** High · **Priority:** P2 · **Module:** Jobs (public browse) · **Status:** OPEN (unchanged from prior run)
- **Environment / URL:** `GET https://www.recruitpro.site/api/jobs/{id}` · **Role:** Guest (no token)
- **Preconditions:** seed Draft job `30000000-0000-4000-8000-000000000005`, Pending `…0004`
- **Reproducibility:** 100%

**Steps to reproduce**
1. With no `Authorization` header, `GET /api/jobs/30000000-0000-4000-8000-000000000005` (Draft).
2. Repeat for `…0004` (PendingApproval).

**Expected:** `404` — non-Approved jobs are not public (the list `GET /api/jobs` correctly excludes them).
**Actual:** `200` with full detail (`{ "status": "Draft", "title": "Technical Support Specialist", … }` and `{ "status": "PendingApproval", "title": "Finance Analyst" }`).
**HTTP:** GET → 200. **Response body:** full candidate-facing job DTO incl. salary/description.
**Impact:** Unfinalized internal postings (salary/description) readable by anyone who guesses the ID; seed IDs are trivially enumerable (`30000000-0000-4000-8000-00000000000X`).
**Possible root cause:** `JobController` public detail returns any job by id without filtering on `status`/auth, unlike the list query.
**Suggested fix:** For unauthenticated callers return `404` unless status ∈ {Approved (and Closed if intended)}.
**Evidence:** `evidence/BUG-UAT-002-draft-leak.json` · **Related cases:** UAT-PUB-011a/b, UAT-SEC-011.

---

## BUG-UAT-005 — FluentValidation filter never executes any validator (systemic input-validation gap) — NEW

- **Severity:** High · **Priority:** P1 · **Module:** Cross-cutting validation (`RecruitPro.API/Filters/ValidationActionFilter.cs`) · **Status:** OPEN
- **Environment:** all `[FromBody]` endpoints · **Role:** any · **Reproducibility:** 100%

**Summary**
The global `ValidationActionFilter` is registered (`Program.cs:29`) and validators are registered
(`AddApplicationValidators()` → `AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>()`), **but no
validator ever runs**. Requests that violate every FluentValidation rule are accepted.

**Steps to reproduce (observed effects)**
- `PUT /api/candidate/profile` with `{ "bio": "<2000 chars>" }` (validator caps 1000) → **200** (accepted).
- `PUT /api/candidate/profile` with **no** `name` and **no** `email` (both `NotEmpty()` required) → **200**.
- `POST /api/hr/jobs` `{ "title": "x" }` (many required fields) → skips field validation, goes straight to
  business logic (`422 DEPARTMENT_NOT_FOUND`) instead of `400` with field errors.
- `PATCH /api/hr/applications/{id}/decision` `{ "targetStatus": "hired" }` (not in the validator allow-list
  `screening|managerreview|interview|offer|rejected`) → reaches the service (`422`), not `400`.

**Expected:** invalid request bodies rejected with `400 INVALID_INPUT` + `fieldErrors[]`.
**Actual:** validators silently skipped; only endpoints with **service-level** checks reject bad input.
**Confirmed root cause (source):** `ValidationActionFilter.OnActionExecutionAsync` (line ~36) resolves
`IValidator<T>` then calls
`validatorType.GetMethod("ValidateAsync", [typeof(IValidationContext), typeof(CancellationToken)])`.
That overload is declared on the **base non-generic `IValidator`**, not on `IValidator<T>`; `Type.GetMethod`
on an interface type does not return base-interface members, so it returns **null** and the filter hits
`if (validateAsyncMethod == null) { continue; }` — i.e. it **silently skips validation for every argument of
every request**.
**Impact:** No server-side request-body validation anywhere the endpoint relies on its validator. Mitigated
on critical flows (auth, apply, transitions, offers, copilot) which have explicit service-level guards, but
the blast radius includes profile update (BUG-UAT-006), interview scheduling (BUG-UAT-007), and any
validator-only field across 20+ DTOs.
**Suggested fix:** call the strongly-typed API instead of reflection —
`await ((IValidator<T>)validator).ValidateAsync(context, ct)` via a small generic helper, or use the
official `SharpGrip`/`FluentValidation.AspNetCore` auto-validation integration; add a regression test that a
known-invalid body returns `400`.
**Evidence:** `evidence/` (profile/interview captures) · **Related:** BUG-UAT-006, BUG-UAT-007, UAT-ERR-VALFILTER.

---

## BUG-UAT-006 — `PUT /candidate/profile` accepts invalid & malicious input (stored-XSS vector) — NEW

- **Severity:** High · **Priority:** P1 · **Module:** Candidate profile · **Status:** OPEN
- **Environment / URL:** `PUT https://www.recruitpro.site/api/candidate/profile` · **Role:** Candidate
- **Account:** tested with seed `nhatquang` (then **restored** — see note) · **Reproducibility:** 100%

**Steps to reproduce**
1. Log in as a candidate; `PUT /api/candidate/profile` with body:
   `{ "name":"X", "email":"not-an-email", "bio":"<2000 chars>", "github":"javascript:alert(1)" }`.
2. Observe response and re-GET the profile.

**Expected:** `400` — email format invalid, bio > 1000, github not an absolute http(s) URL (all are explicit
validator rules in `UpdateCandidateProfileRequestValidator`).
**Actual:** **`200`**; every value persisted, including the `javascript:alert(1)` URL and a 2000-char bio.
Also accepts missing required `name`/`email`.
**HTTP:** PUT → 200. **Impact:**
- **Stored-XSS risk:** a `javascript:` (or `data:`) URL saved in `github`/`linkedin` becomes a clickable link
  on the internal HR candidate view / candidate profile — if rendered as an `href` without scheme
  allow-listing, clicking it executes script in an authenticated context.
- **Data integrity:** oversized/blank/malformed fields corrupt profiles (a 200 KB bio was accepted during
  testing).
**Root cause:** consequence of **BUG-UAT-005** — the profile validator never runs, and the service does no
own validation.
**Suggested fix:** fix BUG-UAT-005 so `UpdateCandidateProfileRequestValidator` runs; additionally enforce a
URL scheme allow-list (`http`/`https` only) and output-encode links on render.
**Note (seed restoration):** this case mutated `nhatquang`'s seed `bio`/fields; they were immediately
restored to the exact `init.sql` values and re-verified (`bio` length back to 102, github/linkedin/location
intact). Recommend re-running such cases on a throwaway `[UAT]` account only.
**Related cases:** UAT-PROF-003, UAT-SEC-XSS-STORED.

---

## BUG-UAT-007 — `POST /hr/interviews` with out-of-range `startMinutes` → 500 — NEW

- **Severity:** Medium · **Priority:** P2 · **Module:** Interviews · **Status:** OPEN
- **Environment / URL:** `POST https://www.recruitpro.site/api/hr/interviews` · **Role:** HR · **Reproducibility:** 100%

**Steps to reproduce**
1. As HR, `POST /api/hr/interviews` with a valid owned `applicationId` and `startMinutes` of `-1`, `1440`, or
   `9999` (valid range is 0–1439; validator declares `InclusiveBetween`).

**Expected:** `400 INVALID_INPUT` (out-of-range minute-of-day).
**Actual:** **`500 SERVER_ERROR`** for all three (clean envelope + `traceId`, **no** stack/secret leak).
**HTTP:** POST → 500 `SERVER_ERROR`. **Impact:** invalid time input crashes the request instead of a graceful
`400`; likely an unhandled `ArgumentOutOfRangeException` when building a `TimeOnly`/`TimeSpan` from minutes.
No row is created (transaction rolls back — verified the target seed app still shows exactly 1 interview).
**Root cause:** consequence of **BUG-UAT-005** (the range rule never runs) + no defensive guard in the domain.
**Suggested fix:** fix BUG-UAT-005; add a guard that maps invalid minute values to `400`.
**Evidence:** `evidence/BUG-UAT-007-interview-500.json` · **Related:** UAT-INT-016, BUG-UAT-005.

---

## BUG-UAT-008 — `GET /jobs` with `page`/`pageSize` ≤ 0 → 500 (anonymous-reachable) — NEW

- **Severity:** Medium · **Priority:** P2 · **Module:** Jobs (public list) · **Status:** OPEN
- **Environment / URL:** `GET https://www.recruitpro.site/api/jobs?page=0` · **Role:** Guest · **Reproducibility:** 100%

**Steps to reproduce**
1. `GET /api/jobs?page=0&pageSize=5` (also `page=-1`, `pageSize=-1`, `pageSize=-5`).

**Expected:** `400` (or clamp to a valid page). Non-numeric values already return a clean `400`.
**Actual:** **`500 SERVER_ERROR`** (clean envelope + `traceId`, no leak).
**HTTP:** GET → 500. **Impact:** an unauthenticated caller can trigger a server error on the primary public
endpoint; likely a negative `Skip((page-1)*pageSize)` / `Take(negative)` in the query. Log noise + poor UX;
DoS-adjacent but low (single 500, no amplification).
**Root cause:** no validation/clamping of pagination parameters on `JobQueryRequest`.
**Suggested fix:** clamp `page>=1`, `1<=pageSize<=100` (or validate → `400`).
**Evidence:** `evidence/BUG-UAT-008-pagination-500.json` · **Related:** UAT-PUB-006e, UAT-PUB-006-500.

---

## BUG-UAT-010 — Broken-access-control asymmetry: non-owner HR can write a decision but cannot read the application — NEW

- **Severity:** Medium · **Priority:** P2 · **Module:** Applications / RBAC · **Status:** OPEN (PO-confirm; documented as intentional in code)
- **Environment:** `/api/hr/applications/{id}` (read) vs `/api/hr/applications/{id}/decision` (write) · **Role:** HR (non-owner) · **Reproducibility:** 100% (read-leg verified live; write-leg source-confirmed)

**Observed**
- `GET /api/hr/applications/{id}` for an application on a job the HR user does **not** own → **`403 FORBIDDEN`**
  (verified live: `thucuyen` reading the Data-Analyst application → 403).
- `PATCH /api/hr/applications/{id}/decision` for the **same non-owned** early-stage application has **no
  per-application ownership gate** — any `HR`/`Manager` may advance it (Applied→Screening, Screening→ManagerReview,
  Interview→Offer/Rejected).

**Expected:** consistent least-privilege — if an HR user cannot **read** an application, they should not be
able to **mutate** its status.
**Actual:** read is ownership-scoped (403) while early-stage decision-writes are only role-gated.
**Confirmed root cause (source):** `ApplicationService.UpdateApplicationDecisionAsync`
(`ApplicationService.cs:629-631`): *"write-authorization … deliberately permits HR early-stage transitions
without per-application ownership. We do NOT add a blanket ownership gate here."* Only the
`ManagerReview→Interview/Rejected` step is guarded (to the assigned DepartmentHead, `GuardManagerReviewDecisionAsync`).
**Impact:** a recruiter can drive another team's pipeline **blind** (change statuses on applications they
can't even view). Whether acceptable depends on the intended "shared HR pool" model (BR-OWN-003/007).
**Live-write demo:** could not be exercised without either mutating a seed application or creating a `[UAT]`
application (blocked — apply needs a CV, and CV upload/MinIO returns 500 on prod, see BUG-UAT-011). The
read-leg 403 was verified live; the missing write-gate is confirmed in source.
**Suggested fix:** align the decision endpoint's authorization with the read scope, or explicitly document
and confirm the shared-pool model with the PO. **Related:** UAT-SEC-OWN-ASYM, UAT-APP-030/032, Q-RBAC-01.

---

## BUG-UAT-003 — Semantic/AI discovery returns `400 INVALID_INPUT` for valid queries (re-confirmed)

- **Severity:** Medium · **Priority:** P3 · **Module:** Talent pool / discovery / recommendations · **Status:** OPEN
- **APIs:** `GET /api/hr/talent-pool/search?Query=java`, `POST /api/hr/candidate-discovery`, `GET /api/jobs/{id}/similar` (public) · **Role:** HR / Guest · **Reproducibility:** 100%

**Expected:** ranked `200` (or a clear `AI_PROVIDER_UNAVAILABLE`/`503`/empty `200`).
**Actual:** `400 INVALID_INPUT` for clearly-valid input (embeddings provider unavailable on prod).
**Note:** `GET /api/candidate/jobs/recommendations` returned `200` (deterministic fallback), so not all
discovery is dead — but the semantic endpoints are inert and the error is misleading.
**Impact:** semantic search / discovery / "similar jobs" non-functional; `400` misrepresents a config/infra
state as a client error. No crash / no leak.
**Suggested fix:** configure the embedding provider, or return a clear unavailable signal / empty `200`.
**Evidence:** `evidence/BUG-UAT-003-ai-discovery.json` · **Related:** UAT-AI-018, endpoints 62–67.

---

## BUG-UAT-004 — Missing HTTP security headers; server version disclosed (re-confirmed)

- **Severity:** Medium · **Priority:** P3 · **Module:** Edge / nginx (both SPA and API responses) · **Status:** OPEN
- **Reproducibility:** 100%

**Actual:** responses from `/` and `/api/*` are missing **all** of `Strict-Transport-Security`,
`X-Frame-Options` (or CSP `frame-ancestors`), `X-Content-Type-Options`, `Content-Security-Policy`,
`Referrer-Policy`. `Server: nginx/1.24.0 (Ubuntu)` discloses the exact version.
**Impact:** no HSTS (SSL-strip risk), clickjacking exposure, MIME-sniffing exposure, version fingerprinting.
**Suggested fix:** add HSTS, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, a baseline CSP,
`Referrer-Policy: strict-origin-when-cross-origin`, and `server_tokens off;` at the edge.
**Evidence:** `evidence/BUG-UAT-004-headers.json` · **Related:** UAT-SEC-008.

---

## BUG-UAT-009 — Salary currency reported as `USD` for VND-only amounts — NEW

- **Severity:** Medium (data/display correctness) · **Priority:** P3 · **Module:** Jobs (public list & detail) · **Status:** OPEN
- **Environment / URL:** `GET https://www.recruitpro.site/api/jobs` · **Role:** Guest · **Reproducibility:** 100%

**Steps to reproduce**
1. `GET /api/jobs?pageSize=3` → inspect `currency` vs `salaryMin/Max`.

**Expected:** `VND` (system is **VND-only**: `offer_currencies` seeds only VND; `CreateJobRequest.Currency`
defaults to `"VND"`).
**Actual:** every job returns `"currency": "USD"` with VND-magnitude salaries — e.g. Java Backend Developer
`salaryMin: 25000000, salaryMax: 40000000, currency: "USD"` (i.e. displayed as "25,000,000–40,000,000 USD",
which is nonsensical; these are ₫ amounts).
**Impact:** every public and internal salary label is mislabeled by currency — misleading to candidates and a
data-consistency defect. (Prior UAT saw a "…VNĐ" label; the API `currency` field is now `USD`, so the
inconsistency is between the seed/create default and the read mapping.)
**Suspected cause:** seeded `jobs` rows have no `currency` value (the `init.sql` insert omits the column), and
the read DTO maps missing currency to a hard-coded `"USD"` default that contradicts the VND-only master data
and the create-side `"VND"` default.
**Suggested fix:** default job currency to `VND` on read (or persist `VND` on seed), consistent with
`offer_currencies` and `CreateJobRequest`.
**Evidence:** `evidence/BUG-UAT-009-currency.json` · **Related:** UAT-PUB-CUR, Q-MDATA-01.

---

## BUG-UAT-011 — CV upload / resume path returns raw 500 when storage unavailable — NEW (infra)

- **Severity:** Low · **Priority:** P3 · **Module:** Files / MinIO · **Status:** OPEN (infra)
- **APIs:** `POST /api/candidates/register` (with CV), `POST /api/candidate/profile/resume` · **Reproducibility:** 100%

**Steps to reproduce**
1. Register a candidate **with** a small valid PDF `resume` → **`500 SERVER_ERROR`**.
2. Or, as a candidate, `POST /api/candidate/profile/resume` with a PDF → **`500 SERVER_ERROR`**.
   (Registration **without** a CV succeeds → `201`, so the base flow works; only the storage leg fails.)

**Expected:** a clean, specific error when object storage is unavailable (e.g. `503 STORAGE_UNAVAILABLE`),
or a working upload.
**Actual:** raw `500 SERVER_ERROR`. MinIO/object storage appears unavailable on prod, so **all** CV/resume
flows (upload, preview, download) and any flow that requires a CV (candidate **apply**) are blocked.
**Impact:** the end-to-end candidate journey (register-with-CV → apply) cannot complete on production;
`apply` returns `422 RESUME_REQUIRED` because no CV can be attached. Registration is transactional (the 500
rolls back — no orphan user created; verified).
**Suggested fix:** provision MinIO on prod; wrap storage calls to surface a clear unavailable error instead
of `500`. **Related:** UAT-REG-002, UAT-FILE-*, DATA-PREP-14.

---

## BUG-UAT-012 — Public `GET /departments` exposes internal head user email (PII) — NEW / Q-SEC-02

- **Severity:** Low · **Priority:** P3 · **Module:** Departments (public lookup) · **Status:** OPEN (PO-confirm)
- **Environment / URL:** `GET https://www.recruitpro.site/api/departments` (anonymous) · **Reproducibility:** 100%

**Actual:** the anonymous department list includes each department head's **name and email**
(`headUserEmail` present).
**Expected:** confirm whether exposing an internal employee's email to anonymous callers is intended.
**Impact:** minor PII / internal-directory exposure to the public internet.
**Suggested fix:** drop `headUserEmail` (and possibly `headUserName`) from the anonymous DTO; expose only to
authenticated internal roles. **Related:** UAT-PUB-013, Q-SEC-02.

---

## BUG-UAT-013 — OData endpoints not reachable on production — NEW

- **Severity:** Low · **Priority:** P3 · **Module:** OData (`/odata/Jobs`, `/odata/Applications`) · **Status:** OPEN
- **Reproducibility:** 100%

**Actual:** `https://www.recruitpro.site/odata/*` returns the **SPA HTML shell** (`200 text/html`, caught by
the client-side catch-all route), and `https://www.recruitpro.site/api/odata/*` returns `404`. The OData
feature (endpoints #138–139 in the API coverage) is therefore **not reachable** on prod.
**Impact:** functional gap — the documented OData query surface is unavailable. **No security exposure**
(the SPA shell contains no data; `/odata/Applications` does not serve application data). Note: because the
route is unreachable, its intended `HR,Manager` authorization could **not** be verified on prod.
**Suggested fix:** proxy `/odata` to the API at the edge (or mount under `/api/odata`) if the feature is
intended to ship. **Related:** UAT-PUB-012, UAT-API-014.

---

## BUG-UAT-001 — Authenticated UI unreachable after login (prior finding) — note

- **Severity:** Blocker (frontend) · **Priority:** P1 · **Status:** Not re-testable with available tooling.
- This run had **no browser-automation tool**, so the SPA session behaviour could not be re-exercised. What
  *was* confirmed at the API layer: login (candidate/internal/universal) returns `200` with
  `{ user, accessToken, refreshToken }`; the **refresh token is an opaque non-JWT string** (verified) and
  `POST /api/auth/refresh` works — this is exactly the condition the prior root-cause analysis
  (`authToken.ts hasValidStoredSession` requiring the refresh token to be a valid JWT) said breaks the SPA
  session. Whether the fixed frontend has been **redeployed** must be confirmed with a browser.
- **Recommendation:** re-run the authenticated-UI regression in a browser once a browser tool is available;
  the API session mechanics are correct.

---

## Minor observations (not filed as standalone defects)

- **schedule-data optional param:** `GET /api/hr/interviews/schedule-data` declares `applicationId` optional
  (`string? = null`) but returns `400 INVALID_INPUT` when omitted (works with a valid id). Minor contract
  inconsistency; handled gracefully (no crash). *(UAT-INT-001s2)*
- **Interview date binding:** a schedule request with `interviewDate:"2026-08-20"` + `startMinutes` stored an
  interview dated `0001-01-01T09:00:00` (date component dropped) — corroborates BUG-UAT-005 (no validation on
  the date field) and suggests the interview create contract expects a combined datetime. Low. *(observed in E2E)*
- **`UpdateApplicationDecisionRequestValidator` allow-list not enforced:** `"hired"`/`"applied"` reach the
  service (rejected there as `422`/parse) rather than `400` — a benign symptom of BUG-UAT-005.
