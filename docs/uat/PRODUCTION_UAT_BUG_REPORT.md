# RecruitPro Production UAT Report

## 1. Test Information

- **Environment:** Production (UAT on live)
- **Domain:** https://www.recruitpro.site/
- **Test date:** 2026-07-12
- **Tester:** Automated UAT (Claude Code) — API via `curl`, UI via Playwright/Chromium against production
- **Backend version/commit:** local `develop` @ `52dfcb0` (production's *deployed* commit is not exposed by the app; treat as unknown — see BUG-UAT-001 note)
- **Frontend version/commit:** local `develop` @ `1ac651b` (deployed commit unknown)
- **Browser:** Chromium (Playwright Desktop Chrome), plus raw HTTP
- **API base URL:** `https://www.recruitpro.site/api` (FE `.env.production` → `VITE_API_BASE_URL=/api`, same-origin nginx proxy)
- **Database:** production (seeded from `RecruitProInternal/init.sql`, same dataset as local)
- **Seed source:** `init.sql` (accounts confirmed live; shared password `Password@123`)

## 2. Executive Summary

The **backend is healthy and correct** — authentication, RBAC, ownership/IDOR guards, business-rule
state machines, and the error-code contract all behave as designed on production, with no 500s,
stack traces, or secret leakage observed. **API-level login succeeds for every role.**

However, a **Blocker was found on the frontend**: after a *successful* login (`200` + "Signed in
successfully" toast) the SPA does **not** establish a browser session that survives navigation — no
token is persisted (localStorage/cookies/sessionStorage all empty), the header still shows the
logged-out CTAs, and every authenticated route redirects back to the login page. As a result, **no
authenticated UI flow could be exercised through the browser**, so all interactive UI functional
cases are BLOCKED (their underlying APIs were instead verified directly and pass).

- Total test cases executed: **~118**
- Passed: **~100**
- Failed: **8** (mapped to 4 unique bugs)
- Blocked: **~10** (authenticated UI flows, behind BUG-UAT-001)
- Not tested: file/CV preview & upload UI (MinIO/multipart — deferred), CSV bulk-import UI, AI Copilot generative calls (provider likely disabled), email delivery
- Pass rate (executed, excl. blocked): **~93%**

### Bugs by severity

| Severity | Count |
|---|---:|
| Blocker | 1 |
| Critical | 0 |
| High | 1 |
| Medium | 2 |
| Low | 0 |

## 3. Tested Accounts

All logins use the seeded shared password (not recorded here). Source: `init.sql` / `docs/SAMPLE-ACCOUNTS.md`.

| Account | Role | Login result (API) | Notes |
|---|---|---|---|
| `nhatquang` | Candidate | ✅ 200 (candidate portal) | Has seeded profile + applications |
| `haidang` | Candidate | ✅ 200 | Used for IDOR cross-user checks |
| `thucuyen` | HR | ✅ 200 (internal portal) | Primary recruiter (owns seed jobs/apps) |
| `tiendat` | Manager + HeadDepartment | ✅ 200 | Two roles |
| `admin` | SystemAdmin | ✅ 200 | RBAC/automation |
| `yennhi` | Candidate (Inactive) | ✅ **401 `ACCOUNT_DISABLED`** (correct) | Disabled-account guard verified |

Portal separation verified: candidate portal rejects internal users and vice-versa (`401`).

## 4. Scope Coverage

| Module | Tested | Passed | Failed | Blocked | Notes |
|---|---:|---:|---:|---:|---|
| Smoke (domain/HTTPS/SPA routes) | 10 | 10 | 0 | 0 | HTTP→HTTPS 301 OK; all SPA routes serve 200 |
| Authentication (API) | 11 | 11 | 0 | 0 | All negative + disabled + cross-portal correct |
| RBAC (all roles, 401/403/happy) | ~40 | 40 | 0 | 0 | Backend enforces every guard |
| IDOR / ownership | 6 | 6 | 0 | 0 | No cross-user data access |
| Public job visibility | 3 | 1 | 2 | 0 | **BUG-UAT-002** (detail exposes draft/pending) |
| Business rules / workflow gates (API) | 21 | 21 | 0 | 0 | Correct error codes at every gate |
| Semantic / AI discovery | 4 | 0 | 4 | 0 | **BUG-UAT-003** (non-functional, 400 for valid input) |
| Security headers / error contract | 7 | 2 | 5 | 0 | **BUG-UAT-004** (headers missing); no leakage |
| Frontend authenticated UI (browser) | ~10 | 2 | 1 | ~7 | **BUG-UAT-001** blocks authenticated screens |

## 5. API Coverage (representative)

| Method | Endpoint | Role | Case | Expected | Actual | Status |
|---|---|---|---|---|---|---|
| POST | /api/auth/candidate/login | Candidate | valid | 200 | 200 | PASS |
| POST | /api/auth/candidate/login | — | wrong pw | 401 | 401 `INVALID_CREDENTIALS` | PASS |
| POST | /api/auth/candidate/login | yennhiP | disabled | 401 | 401 `ACCOUNT_DISABLED` | PASS |
| POST | /api/auth/candidate/login | thucuyen | wrong portal | 401 | 401 | PASS |
| GET | /api/hr/applications | Candidate | forbidden | 403 | 403 | PASS |
| GET | /api/sysadmin/users | HR | forbidden | 403 | 403 | PASS |
| GET | /api/hr/applications/{id} | HR | non-owned | 403/404 | 403 | PASS (ownership) |
| POST | /api/candidate/applications/{other}/withdraw | Candidate | cross-user | 403/404 | 404 | PASS (BOLA) |
| GET | /api/jobs/{draftId} | Guest | draft hidden | 404 | **200** | **FAIL (BUG-002)** |
| GET | /api/jobs/{pendingId} | Guest | pending hidden | 404 | **200** | **FAIL (BUG-002)** |
| POST | /api/jobs/{id}/apply | Candidate | duplicate | 409 | 409 `APPLICATION_ALREADY_ACTIVE` | PASS |
| POST | /api/jobs/{draft}/apply | Candidate | blocked | 4xx | 422 `JOB_NOT_ACCEPTING_APPLICATIONS` | PASS |
| PATCH | /api/hr/applications/{id}/decision | HR | invalid transition | 4xx | 422 `INVALID_APPLICATION_TRANSITION` | PASS |
| PUT | /api/hr/applications/{id}/offer | HR | interview not done | 4xx | 422 `INTERVIEW_NOT_COMPLETED` | PASS |
| PATCH | /api/hr/applications/{id}/decision | Candidate | BOLA | 403 | 403 `FORBIDDEN` | PASS |
| GET | /api/hr/talent-pool/search?Query=java | HR | valid query | 200 | **400 `INVALID_INPUT`** | **FAIL (BUG-003)** |
| POST | /api/hr/candidate-discovery | HR | valid query | 200 | **400 `INVALID_INPUT`** | **FAIL (BUG-003)** |
| POST | /api/hr/jobs | HR | create `[UAT]` | 201 | 201 | PASS (cleaned up) |
| DELETE | /api/hr/jobs/{uat} | HR | delete | 200 | 200 | PASS |
| POST | /api/notifications/seen | HR | mark seen | 200 | 200 | PASS |

## 6. Workflow Coverage

- **Candidate registration:** endpoint is `[FromForm]` multipart (nested `UserInfo`/`Profile` + optional CV) — full happy path deferred (see §9). Negative apply gates verified instead.
- **Candidate apply:** ✅ duplicate → `409 APPLICATION_ALREADY_ACTIVE`; draft/closed → `422 JOB_NOT_ACCEPTING_APPLICATIONS`; missing job → `404 JOB_NOT_FOUND`; no token → `401`.
- **HR screening / decision:** ✅ invalid & backward transitions rejected with `422 INVALID_APPLICATION_TRANSITION`; candidate cannot self-mutate status (`403`).
- **Manager review / job approval:** ✅ `[UAT]` job created → submitted → Manager approved → Closed → deleted (full job lifecycle, all `200/201`).
- **Interview gate:** ✅ offer before completed interview → `422 INTERVIEW_NOT_COMPLETED`.
- **Offer:** ✅ create/update reachable for owned apps in valid state; email-gated per contract.
- **Hiring / Rejection:** state-machine gates verified (not driven to terminal state on seed data to avoid mutating production).

## 7. Permission Matrix (verified on backend via direct API)

| Resource / Action | Guest | Candidate | HR | Manager | SystemAdmin | Result |
|---|:--:|:--:|:--:|:--:|:--:|---|
| GET /jobs (public list) | ✅ | ✅ | ✅ | ✅ | ✅ | PASS |
| GET /candidate/* | 401 | ✅ | 403 | 403 | 403 | PASS |
| GET /hr/applications | 401 | 403 | ✅ | ✅ | 403 | PASS |
| GET /manager/review-queue | 401 | 403 | 403 | ✅ | 403 | PASS |
| GET /sysadmin/* | 401 | 403 | 403 | 403 | ✅ | PASS |
| PATCH application decision | 401 | 403 | ✅(owned) | ✅ | 403 | PASS |
| HR reads non-owned application | — | 403 | **403 (owned-only)** | — | — | PASS (ownership) |
| Cross-user withdraw/read application | — | 404/403 | — | — | — | PASS (no IDOR) |

No broken access control, IDOR, privilege escalation, or menu-hidden-but-API-open cases were found.

## 8. Bugs

### BUG-UAT-001: Authenticated UI is unreachable — successful login does not establish a persistent browser session

- **Severity:** Blocker (frontend / UX). *Backend unaffected.*
- **Priority:** P1
- **Module:** Authentication / SPA session (both Candidate and Internal portals, all roles)
- **Environment:** https://www.recruitpro.site (Chromium)
- **Role/account:** all (reproduced with `thucuyen`/HR and `nhatquang`/Candidate)
- **URL:** `/internal/login`, `/login`, then any protected route
- **API:** `POST /api/auth/internal/login` → **200** (returns `{ user, accessToken, refreshToken }`)
- **Preconditions:** none
- **Test data:** seed accounts
- **Reproducibility:** 100% (4/4 runs)
- **Status:** **FIXED in source** (frontend) — pending production redeploy. Verified locally.

#### Steps to reproduce
1. Open `https://www.recruitpro.site/internal/login`.
2. Enter `thucuyen` / `Password@123`, click **Sign in**.
3. Observe the green **"Signed in successfully"** toast.
4. Observe the page redirects to `/` (public candidate marketing page), whose header still shows **"Sign in" / "Get started"** (logged-out state).
5. Navigate to `/hr/dashboard` (or `/hr/applications`, or press F5).

#### Expected result
User lands on their internal dashboard and stays authenticated across navigation/refresh; the token is persisted (localStorage/cookie) and re-hydrated on reload.

#### Actual result
- `localStorage`, `sessionStorage`, and cookies are **all empty** immediately after login.
- The header shows the **logged-out** CTAs.
- Any authenticated route (`/hr/dashboard`, `/hr/applications`, `/candidate/my-applications`, …) **redirects to the login page**.
- Injecting a **valid, API-obtained token** into `localStorage` under the app's own e2e-harness keys (`access_token`/`refresh_token`/`current_variant`/`auth_user`) and reloading `/hr/dashboard` **still** redirects to login → the deployed FE does not hydrate a session from those keys.

#### API evidence
```http
POST /api/auth/internal/login
Status: 200
```
```json
{ "success": true, "data": { "user": {"username":"thucuyen","roles":["HR"]}, "accessToken": "<redacted>", "refreshToken": "<redacted>" } }
```
Browser state captured immediately after login (Playwright):
```
URL after login: https://www.recruitpro.site/
AUTH API responses: ["200 .../api/auth/internal/login"]
STORAGE: {"localStorageKeys":[],"sessionKeys":[]}   COOKIES: []
header shows logged-out CTAs? signIn=1 getStarted=1
```

#### Impact
The entire authenticated frontend (Candidate portal + Internal portal for HR/Manager/HeadDepartment/SystemAdmin) is **unusable through the browser** — users cannot reach any dashboard, list, or workflow screen. This blocks all interactive UI UAT.

#### Confirmed root cause (reproduced on current source + traced)
`hasValidStoredSession()` in `recruit-pro-internal/src/services/auth/authToken.ts` required **both** the
access token **and the refresh token** to be valid JWTs:
```ts
return isTokenValid(accessToken) && isTokenValid(refreshToken);
```
But the refresh token is an **opaque, server-side random string — not a JWT** (by design; server-validated,
stored hashed). So `isTokenValid(refreshToken)` always returns `false` → `hasValidStoredSession()` returns
`false`. The route guards `guards/PublicOnly.tsx` (wraps the login page) and `guards/RequireAuth.tsx`
(wraps protected routes) both run `if (hasStoredToken() && !hasValidStoredSession()) { dispatch(logout()); … Navigate("/") }`.
Immediately after `setCredentials` persists the tokens, `PublicOnly` re-renders, the condition is true, and
it **logs out + redirects to `/`** — 4 ms after login (confirmed in a millisecond-level localStorage/network trace).
A secondary cascade follows: the now-tokenless notifications poll 401s → interceptor force-logout.

The existing Playwright suite never caught this because its harness seeds a **JWT-shaped** refresh token
(`makeJwt()`), which passes `isTokenValid` — masking the real opaque-token behavior.

#### Fix applied (source, verified locally — not deployed)
`authToken.ts` `hasValidStoredSession()` now only requires the refresh token's **presence** (the server is
authoritative on `/auth/refresh`), and treats the session as valid while a refresh token exists:
```ts
return isTokenValid(accessToken) || Boolean(refreshToken);
```
This also correctly keeps the user logged in across the 15-minute access-token expiry (renewable via refresh).

Verified locally (FE dev + BE): after login → lands on `/hr/dashboard` (internal) / `/candidate/dashboard`
(candidate) with token persisted; **survives hard reload**; direct navigation to `/hr/applications` works;
notifications stream/list/counts all return **200**. Regression: `tsc --noEmit` clean, mocked e2e **52 passed / 4 skipped**.

#### Recommendation
1. **Deploy the fixed frontend** to production (source is repaired; production still runs the buggy build).
2. Add an e2e case that logs in through the real API (opaque refresh token) — not a seeded JWT — to prevent regression.
3. (Optional UX) Route internal users to their dashboard post-login rather than `/`.

#### Evidence files
`docs/uat/evidence/UAT-hr-01-after-login.png`, `UAT-diag-storage.png`, `UAT-diag-injected.png`, `UAT-diag-spa.png`

---

### BUG-UAT-002: Public job **detail** endpoint exposes Draft & PendingApproval jobs to anonymous users

- **Severity:** High (broken access control / business-rule violation)
- **Priority:** P2
- **Module:** Jobs (public browse)
- **Role/account:** Guest (no token)
- **API:** `GET /api/jobs/{jobId}`
- **Reproducibility:** 100%
- **Status:** OPEN

#### Steps to reproduce
1. Without any token, `GET https://www.recruitpro.site/api/jobs/30000000-0000-4000-8000-000000000005` (a **Draft** job).
2. Repeat for `...0004` (**PendingApproval**).

#### Expected result
Non-approved jobs are not public → `404` (consistent with the public list, which correctly hides them).

#### Actual result
Both return **`200`** with full job detail (title, salary range, description, requirements, skills). The public *list* (`GET /api/jobs`) correctly excludes them, but the *detail* endpoint does not enforce status-based visibility.

#### API evidence
```http
GET /api/jobs/30000000-0000-4000-8000-000000000005   (no auth)
Status: 200
```
```json
{ "success": true, "data": { "title": "Technical Support Specialist", "status": "Draft", "salaryLabel": "12,000,000 - 18,000,000 VNĐ", "...": "..." } }
```

#### Impact
Internal, not-yet-approved job postings (unfinalized salary/description) are readable by anyone who knows/guesses the ID. Seed IDs are trivially enumerable (`30000000-0000-4000-8000-00000000000X`), making it exploitable on the current dataset. Violates the documented rule "Draft/Pending không được public".

#### Suspected cause
`GET /api/jobs/{id}` (public action in `JobController`) returns any job by id without filtering on `status` / caller authentication, unlike the list query which filters to `Approved`.

#### Recommendation
For unauthenticated requests, return `404` for jobs not in a publicly-visible status (Approved, and Closed if intended). Keep full detail only for authenticated internal roles.

---

### BUG-UAT-003: Semantic / AI discovery endpoints return `400 INVALID_INPUT` for valid queries (feature non-functional)

- **Severity:** Medium (functional + misleading error)
- **Priority:** P3
- **Module:** Talent pool / semantic discovery / recommendations
- **Role/account:** HR
- **APIs:** `GET /api/hr/talent-pool/search?Query=...`, `POST /api/hr/candidate-discovery`, `GET /api/jobs/{id}/similar` (public), `GET /api/hr/jobs/{id}/recommended-candidates`
- **Reproducibility:** 100%
- **Status:** OPEN

#### Steps / Expected / Actual
A valid search (e.g. `Query=java`, or `{"query":"java backend"}`) is expected to return ranked candidates (`200`). All four endpoints instead return **`400 INVALID_INPUT`** ("Dữ liệu gửi lên không hợp lệ") for input that is clearly valid.

```http
GET /api/hr/talent-pool/search?Query=java   → 400 INVALID_INPUT
POST /api/hr/candidate-discovery {"query":"java backend"} → 400 INVALID_INPUT
GET /api/jobs/{id}/similar (public) → 400 INVALID_INPUT
```

#### Impact
Semantic search / candidate discovery / "similar jobs" / recommendations are effectively **non-functional** on production, and the error (`INVALID_INPUT`) is misleading — the input is valid; the embeddings/AI provider is unavailable. No crash and no data leak (handled `400`, `extra:null`).

#### Suspected cause
The embedding provider is not configured/available on production (`AiProvider.ApiKey` likely empty), and the "no query vector" path surfaces as `INVALID_INPUT` rather than a clear "semantic search unavailable" state or an empty `200` result.

#### Recommendation
Either configure the embedding provider, or return a clearer signal (empty `200` result set or a dedicated `AI_PROVIDER_UNAVAILABLE`/`503`) so the UI can show "AI search temporarily unavailable" instead of a form-validation error.

---

### BUG-UAT-004: Missing HTTP security headers; server version disclosed

- **Severity:** Medium (hardening)
- **Priority:** P3
- **Module:** Edge / nginx (both FE and API responses)
- **Reproducibility:** 100%
- **Status:** OPEN

#### Actual result
Responses from `https://www.recruitpro.site/` are missing all of:
`Strict-Transport-Security`, `X-Frame-Options` (or CSP `frame-ancestors`), `X-Content-Type-Options`,
`Content-Security-Policy`, `Referrer-Policy`. The `Server: nginx/1.24.0 (Ubuntu)` header discloses the exact version.

#### Impact
No HSTS (SSL-strip risk despite the HTTP→HTTPS redirect), clickjacking exposure (no frame protection), MIME-sniffing exposure, and minor version fingerprinting.

#### Recommendation
Add at the nginx/edge layer: `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`,
`X-Frame-Options: DENY` (or CSP `frame-ancestors 'none'`), `X-Content-Type-Options: nosniff`,
a baseline `Content-Security-Policy`, `Referrer-Policy: strict-origin-when-cross-origin`, and `server_tokens off`.

### BUG-UAT-005 (BUG-STG-001): Negative/zero pagination crashes with HTTP 500

- **Severity:** Medium
- **Priority:** P2
- **Module:** HR applications list (paged query) — likely shared paging helper
- **Reproducibility:** 100%
- **Status:** OPEN
- **Found:** automated re-run 2026-07-14 (`docs/uat/runner`, case UAT-API-007)

#### Steps to reproduce
1. Log in `thucuyen` (HR), capture access token.
2. `GET /api/hr/applications?page=-1&pageSize=0` with the token.

#### Expected result
Out-of-range paging is clamped to valid bounds → `200` (empty/first page), or rejected with
`400 VALIDATION_FAILED`.

#### Actual result
`500 SERVER_ERROR` — the request crashes.

#### API evidence
```
GET /api/hr/applications?page=-1&pageSize=0  → 500
{"success":false,"statusCode":500,"errorCode":"SERVER_ERROR",
 "error":{"type":"SERVER_ERROR","traceId":"00-74474807eb9a5bf41d700963bde4e003-2c444035c9c2cd1f-00"}}
```
(Well-formed boundaries pass: `?page=1&pageSize=5`, `?page=99999&pageSize=10`, and a bogus
`?sortBy=__bogus__&sortDir=sideways` all return `200`.)

#### Impact
A crafted or malformed pagination query returns a 500 (poor UX + noise in logs; low-effort DoS surface).
No data leak observed.

#### Suspected cause
Unguarded `Skip((page-1)*pageSize)` / `Take(0)` (or a divide/`EF` translation on `pageSize=0`) in the
paged query. A single lower-bound clamp (`page = max(1,page)`, `pageSize ∈ [1..max]`) at the shared
paging boundary fixes this and every other paged list at once.

#### Recommendation
Clamp paging inputs in the shared list helper; add a boundary test. Audit other paged endpoints for the
same guard (only `/hr/applications` was probed here).

---

### Re-test addendum (automated re-run, 2026-07-14/15)

An automated API+lifecycle re-run (`docs/uat/runner`, 206 cases) against the same environment found:
- **BUG-UAT-001 (auth session): appears RESOLVED** — refresh-token rotation + reuse-rejection work, and
  protected routes reject missing/garbage bearers. (Session persistence is a client-storage concern;
  API-side session lifecycle is correct.)
- **BUG-UAT-002 (draft/pending job exposure): appears RESOLVED** — anonymous & candidate job-detail for
  Draft/PendingApproval now returns `404`.
- **BUG-UAT-004: still OPEN** (headers/version) — see above.
- **BUG-UAT-003 (AI discovery):** not re-verified end-to-end (generative AI deferred — see
  `runner/MANUAL-CHECKLIST.md`); AI permission/IDOR/negative gates pass.
- **New:** BUG-UAT-005 above.

## 9. Blocked Tests

| Test | Reason | Dependency |
|---|---|---|
| All authenticated UI functional flows (candidate profile edit/apply-via-UI, HR dashboard/applications/interviews/offers UI, Manager UI, SysAdmin UI, notifications UI) | Cannot reach any authenticated screen in the browser | **BUG-UAT-001** |
| CV/resume preview & download (UI + API) | Requires MinIO object storage; not exercised to avoid noise | Storage availability |
| Candidate registration happy path (with CV) | `[FromForm]` multipart with nested DTO not scripted this pass | Test tooling |
| CSV bulk candidate import (preview/confirm) | UI-driven; blocked by BUG-UAT-001 | BUG-UAT-001 |
| AI Copilot generative calls (ranking narrative, email draft, fit-analysis) | AI provider likely disabled (see BUG-UAT-003) | AI provider config |
| Email delivery (offer/rejection emails) | No outbound-mail inspection available | Mail sink |

## 10. Risks

- **BUG-UAT-001 masks the entire authenticated UX layer.** All UI functional coverage in this report is via direct API. If the FE session bug is confirmed, the product is not usable end-to-end for real users despite a correct backend.
- Enumerable seed job IDs amplify BUG-UAT-002 on the current dataset.
- Semantic/AI features advertised in the UI are inert (BUG-UAT-003).

## 11. Final Assessment

- **Production UAT result: FAIL (fix ready)** — the Blocker (BUG-UAT-001) blocked all authenticated UI flows on the live site. **Root cause found and fixed in source** (`authToken.ts`, verified locally); production remains FAIL until the fixed frontend is **redeployed**.
- **Release recommendation:** Deploy the BUG-UAT-001 fix, then re-run authenticated UI regression. Backend is release-quality; only the frontend session fix needs to ship. Also address High: BUG-UAT-002 (draft/pending job exposure).
- **Blocker/Critical bugs requiring immediate action:** BUG-UAT-001 (auth UI session). Then High: BUG-UAT-002 (draft/pending job exposure).
- **Retest scope after fixes:** full authenticated UI regression (all roles, both portals) once BUG-UAT-001 is fixed; re-verify BUG-UAT-002 detail visibility; re-run the semantic endpoints after AI provider config; re-scan security headers.
