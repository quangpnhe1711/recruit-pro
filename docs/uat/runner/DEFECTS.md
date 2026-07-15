# Staging UAT — Defects & Findings (Phase 1: read/negative suites)

Environment: `https://www.recruitpro.site/api` · Runner: `docs/uat/runner` (automated, real API).
Phase 1 scope: 12 suites, 168 cases — **97 PASS / 5 FAIL / 66 N/A** (N/A = deferred stateful write-phase or UI-only). The 5 FAILs collapse to **3 distinct findings** below.

---

## BUG-STG-001 — Negative/zero pagination crashes with HTTP 500 (NEW)
- **Severity:** Medium · **Priority:** P2 · **Case:** UAT-API-007
- **Endpoint:** `GET /api/hr/applications?page=-1&pageSize=0` (HR token)
- **Actual:** `500 SERVER_ERROR` (traceId `00-74474807eb9a5bf41d700963bde4e003-...`).
- **Expected:** clamp to valid bounds and return `200`, or reject with `400 VALIDATION_FAILED`.
- **Repro:** `curl -H "Authorization: Bearer <HR>" "https://www.recruitpro.site/api/hr/applications?page=-1&pageSize=0"`
- **Likely cause:** unguarded `Skip((page-1)*size)` / `Take(0)` or divide-by-size on the paged query. Add lower-bound clamps (`page>=1`, `pageSize` in `[1..max]`) at the query boundary — one guard covers every paged list.

## BUG-UAT-004 — Missing security headers / server version disclosed (KNOWN, still OPEN)
- **Severity:** Low–Medium · **Priority:** P3 · **Case:** UAT-SEC-008
- **Actual:** responses lack standard hardening headers and disclose the server banner (`server_tokens`).
- **Expected:** `X-Content-Type-Options: nosniff`, `X-Frame-Options`/CSP `frame-ancestors`, `Referrer-Policy`, HSTS; `server_tokens off` at the proxy.
- **Note:** confirmed still open on staging. Reverse-proxy config change (nginx `add_header` + `server_tokens off`); no app code needed.

## FINDING-CONTRACT-01 — Model-binding validation returns raw ProblemDetails (Low)
- **Severity:** Low · **Priority:** P2 · **Cases:** UAT-ERR-001, UAT-ERR-004, UAT-API-002
- **Where:** endpoints whose required model/body is missing (e.g. `POST /candidates/register`, `POST /hr/jobs {}`).
- **Actual:** ASP.NET `[ApiController]` automatic 400 returns framework **ProblemDetails** — PascalCase `errors: {Title:[...]}`, **no** `errorCode`, no `error.type`. Also `POST /candidates/register` is **multipart-only**, so a JSON body returns `415`.
- **Expected (per ERROR-CONTRACT):** the app envelope `{success:false, errorCode:"VALIDATION_FAILED", error:{fieldErrors:[{field(camelCase)…}]}}`.
- **Real impact (verified):** the FE normalizer *does* fall back to `data.errors` ([`apiError.ts:405`](../../recruit-pro-internal/src/common/utils/apiError.ts)), so errors still surface — but the field names are PascalCase and won't match the camelCase form fields, so an inline field error **degrades to a toast**; there is no stable `errorCode` to branch on. Not user-blocking.
- **Fix option:** register an `InvalidModelStateResponseFactory` / validation filter that reshapes model-binding 400s into the app envelope with camelCase field names; and/or accept `application/json` on register (or document multipart-only).

---

## Positives worth noting (verified on staging)
- **Auth/session hardening solid:** refresh single-use rotation + reuse rejection (AUTH-012/SEC-005), portal separation `PORTAL_ACCESS_DENIED` (AUTH-006/007/SEC-014), disabled account `ACCOUNT_DISABLED` (AUTH-005), tampered/missing bearer → 401 (AUTH-010/014/SEC-001). **BUG-UAT-001 (auth session) appears RESOLVED.**
- **BUG-UAT-002 appears FIXED:** public/candidate job detail returns 404 for Draft/PendingApproval (PUB-011, SEC-011).
- **RBAC enforced server-side:** non-admin → sysadmin 403, IDOR on user status/roles blocked at the permission gate before id resolution (RBAC-002/016/017, SEC-004/006).
- **No secret leakage** in payloads/errors; unique traceId per error (SEC-007, ERR-013).
- **OData & Swagger are NOT exposed** on this host (both fall through to the SPA shell) — no data/informational exposure.

---

# Phase 2 — Stateful WRITE suite (mutating staging, authorized)

Suite `write` — the full recruiting lifecycle end-to-end, self-contained + `[UAT]`-tagged + auto-cleanup:
**14/14 PASS.**

`create job (HR) → submit → head approve → public → candidate resume upload (real PDF) → apply →
duplicate rejected → HR Screening → HR ManagerReview → HR Interview =403 → HEAD Interview =200 →
schedule interview → mark Completed → draft+send offer → candidate accepts (Hired) → teardown`.

Business rules **verified correct** on staging (no defects):
- **BR-OWN-007** — ManagerReview→Interview is reserved for the assigned DepartmentHead; HR attempt → `403 FORBIDDEN`, head → `200`.
- **RESUME_REQUIRED** — apply without a resume → `422`; succeeds after resume upload.
- **BR-WF-001/002** — decision endpoint refuses direct Offer (`EMAIL_REQUIRED_FOR_OFFER`); the transition goes through `offer/send`, which gates on interview completion (`INTERVIEW_REQUIRED` if none) and emails the candidate.
- **INV-008/009** — interview only schedulable in ManagerReview/Interview; Hired only via candidate accept-offer.
- Duplicate apply rejected; interview `candidateId` must equal the application's user id.

Teardown verified: `interview del→200, close→200, job del→200`; **0 `[UAT]` jobs leftover**. Residue: a resume + one tagged application on spare persona `haidang` (no delete-user/-application API — safe, tagged).

# Phase 2b/2c — remaining write suites (all PASS)

**WRITE2 (12/12):** NOTI mark-seen/read/all · REG register `[UAT]` candidate + login + duplicate-reject · PROF profile update + experience add/delete (on the isolated registered user) · RBAC deactivate→disabled-login-401, reactivate, **self-deactivate guard →409**, invalid-status →400.
**WRITE3 (7/7):** MDATA department update round-trip (restore original) + non-privileged →403/401 · WF workflow create→update→publish→toggle (needs ≥1 valid action, `shadow_log`) + non-admin create →403.

Verified correct: registration uniqueness, disabled-account gate end-to-end (deactivate → login 401 → reactivate → login 200), admin self-lockout guard, workflow validation (rejects zero-action / unknown trigger / unknown action type / bad mode), department-update RBAC.

**Only remaining deferral — AI copilot (UAT-AI):** not automated — it consumes AI-provider credits and needs a seeded screening candidate pool; run manually per the UAT-AI cases.

Residue left on staging (tagged, safe): per full run — one `[UAT]` registered candidate (no delete-user API), one `[UAT]` workflow left Disabled (no delete-workflow API), and resumes/apps on spare persona `haidang`. All `[UAT]`-marked for manual cleanup. The self-cleaning lifecycle suite (`write`) leaves nothing.

---

## Deferred READ-phase cases now COVERED by write suites
The 66 N/A in the read suites are stateful writes deferred from Phase 1; their functionality is now exercised by the Phase-2 write cases (new ids, e.g. NOTI-003/004/005 → UAT-NOTI-W01/02/03; RBAC-008/009 → UAT-RBAC-W01/02; APP/INT/OFFER flows → the `write` lifecycle). SSE realtime + AI remain manual.

---

## Deferred to write-phase (66 N/A)
Stateful cases that mutate data (mark-read, status/role changes, matrix edits, department updates, apply/decision/offer flows, SSE realtime) — to run in the controlled write-phase with `[UAT]`-tagged entities and cleanup. See RESULTS.md for the per-case list.
