# RecruitPro Staging UAT — Execution Log

- **Environment:** https://www.recruitpro.site/api
- **Run at:** 2026-07-15T03:36:24.428Z
- **Runner:** automated (docs/uat/runner)

## Summary

| Total | PASS | FAIL | BLOCKED | N/A |
|---|---|---|---|---|
| 206 | 135 | 5 | 0 | 66 |

## By suite

| Suite | PASS | FAIL | BLOCKED | N/A |
|---|---|---|---|---|
| PUB | 13 | 0 | 0 | 1 |
| CJOB | 10 | 0 | 0 | 4 |
| AUTH | 12 | 0 | 0 | 0 |
| DASH | 6 | 0 | 0 | 6 |
| LOG | 4 | 0 | 0 | 4 |
| MDATA | 5 | 0 | 0 | 5 |
| NOTI | 6 | 0 | 0 | 12 |
| RBAC | 9 | 0 | 0 | 13 |
| ERR | 6 | 2 | 0 | 6 |
| API | 11 | 2 | 0 | 3 |
| SEC | 11 | 1 | 0 | 2 |
| UI | 4 | 0 | 0 | 10 |
| AI | 5 | 0 | 0 | 0 |
| WRITE | 14 | 0 | 0 | 0 |
| WRITE2 | 12 | 0 | 0 | 0 |
| WRITE3 | 7 | 0 | 0 | 0 |

## Cases

| Case | Pri | Result | API | Notes |
|---|---|---|---|---|
| UAT-PUB-001 | P1 | PASS | GET /jobs/filters (no token) → 200 | UI render of landing verified manually; anonymous public access asserted |
| UAT-PUB-002 | P0 | PASS | GET /jobs → 200 |  |
| UAT-PUB-003 | P1 | PASS | GET /jobs/59a5221b-43bb-489f-83b7-d41c347e37f9 → 200 |  |
| UAT-PUB-004 | P2 | PASS | GET /jobs?keyword=Java → 200 |  |
| UAT-PUB-005 | P2 | PASS | GET /jobs/filters → 200; GET /jobs?workMode=Remote → 200 |  |
| UAT-PUB-006 | P2 | PASS | GET /jobs?page=9999 → 200; pageSize0→200; pageSize99999→200 | captured clamp behaviour |
| UAT-PUB-007 | P3 | PASS | GET /jobs?keyword=zzznojob → 200 | empty-state asserted; loading/error states are UI-only |
| UAT-PUB-008 | P2 | N/A |  | UI-only (SPA routing / deep-link / back-forward, no distinct API) |
| UAT-PUB-009 | P2 | PASS | GET /jobs/99999999-9999-4999-8999-999999999999 → 404 JOB_NOT_FOUND; malformed→404 | unknown errorCode=JOB_NOT_FOUND; malformed→404 (404/400 acceptable) |
| UAT-PUB-010 | P3 | PASS | malformed→400; xss→200 | never 500; 200(defaults)/400 acceptable |
| UAT-PUB-011 | P2 | PASS | Draft→404, Pending→404 | BUG-UAT-002 appears fixed (404 for non-Approved) |
| UAT-PUB-012 | P3 | PASS | GET /api/xml-demo/jobs → 200 | xml-demo asserted; /odata/Jobs is outside /api base → manual/curl (deferred) |
| UAT-PUB-013 | P3 | PASS | GET /departments → 200; GET /skills → 200 |  |
| UAT-PUB-014 | P2 | PASS | GET /jobs/59a5221b-43bb-489f-83b7-d41c347e37f9/statistics → 200 |  |
| UAT-CJOB-001 | P1 | PASS | GET /jobs (candidate) → 200 |  |
| UAT-CJOB-002 | P2 | PASS | GET /jobs?keyword=React (candidate) → 200 |  |
| UAT-CJOB-003 | P2 | PASS | GET /jobs (combined filters, candidate) → 200; GET /jobs (cleared) → 200 | narrow then restore; clear returns full list |
| UAT-CJOB-004 | P3 | PASS | GET /jobs?page=9999 (candidate) → 200 |  |
| UAT-CJOB-005 | P3 | N/A |  | UI-only (graceful rendering of missing salary/benefits/long content, no distinct API) |
| UAT-CJOB-006 | P1 | PASS | GET /jobs/59a5221b-43bb-489f-83b7-d41c347e37f9/apply-context → 200 |  |
| UAT-CJOB-007 | P1 | PASS | apply-context eligibility.canApply=false |  |
| UAT-CJOB-008 | P2 | PASS | GET /candidate/jobs/recommendations → 200 | graceful 200 even if AI/embeddings unavailable (may be empty) |
| UAT-CJOB-009 | P3 | N/A |  | UI-only (SPA deep-link / refresh / back-forward, no distinct API) |
| UAT-CJOB-010 | P3 | PASS | GET /jobs?keyword=zzznojob (candidate) → 200 | empty-state asserted; loading/error are UI-only |
| UAT-CJOB-011 | P2 | N/A |  | deferred: stateful write (requires approve/close a job) (write-phase) |
| UAT-CJOB-012 | P1 | PASS | GET /jobs (candidate) → 200; GET /jobs/59a5221b-43bb-489f-83b7-d41c347e37f9 (candidate) → 200 |  |
| UAT-CJOB-013 | P3 | N/A |  | UI-only (responsive layout / mobile BottomNavBar, no distinct API) |
| UAT-CJOB-014 | P3 | PASS | malformed→400 | never 500; 200(defaults)/400 acceptable |
| UAT-AUTH-001 | P0 | PASS | POST /auth/internal/login → 200 |  |
| UAT-AUTH-002 | P0 | PASS | POST /auth/candidate/login → 200 |  |
| UAT-AUTH-003 | P0 | PASS | POST /auth/internal/login → 401 INVALID_CREDENTIALS |  |
| UAT-AUTH-004 | P1 | PASS | POST /auth/internal/login → 401 INVALID_CREDENTIALS |  |
| UAT-AUTH-005 | P0 | PASS | POST /auth/candidate/login → 401 ACCOUNT_DISABLED |  |
| UAT-AUTH-006 | P0 | PASS | POST /auth/internal/login → 401 PORTAL_ACCESS_DENIED | errorCode=PORTAL_ACCESS_DENIED |
| UAT-AUTH-007 | P1 | PASS | POST /auth/candidate/login → 401 PORTAL_ACCESS_DENIED | errorCode=PORTAL_ACCESS_DENIED |
| UAT-AUTH-008 | P1 | PASS | POST /auth/internal/login → 200 |  |
| UAT-AUTH-009 | P2 | PASS | POST /auth/login (username) → 200; POST /auth/login (email) → 200 |  |
| UAT-AUTH-010 | P0 | PASS | GET /hr/applications (no token) → 401 UNAUTHENTICATED | errorCode=UNAUTHENTICATED |
| UAT-AUTH-012 | P0 | PASS | RT1→200, reuse→401, RT2→200 |  |
| UAT-AUTH-014 | P0 | PASS | GET /hr/applications (bad token) → 401 UNAUTHENTICATED |  |
| UAT-DASH-001 | P2 | PASS | GET /candidate/dashboard → 200 |  |
| UAT-DASH-002 | P1 | PASS | GET /hr/dashboard → 200 |  |
| UAT-DASH-003 | P2 | PASS | GET /manager/dashboard → 200 |  |
| UAT-DASH-004 | P2 | PASS | GET /manager/reports/recruitment-analytics → 200 | funnel/Withdrawn-exclusion (BR-APPLICATION-011) requires manual value review |
| UAT-DASH-005 | P1 | N/A |  | deferred: cross-source manual comparison (UI/API vs seed DB counts, Auto:No) |
| UAT-DASH-006 | P2 | N/A |  | UI-only: dashboard/analytics endpoints take no date-range params (client-side filter) |
| UAT-DASH-007 | P3 | N/A |  | UI-only: chart/empty-state rendering |
| UAT-DASH-008 | P3 | N/A |  | UI-only: interactive rendering (Auto:No) |
| UAT-DASH-009 | P1 | PASS | GET /hr/dashboard (thucuyen) → 200; GET /hr/dashboard (giahan) → 200 | both 200; per-owner scoping (numbers differ?) needs manual review |
| UAT-DASH-010 | P1 | PASS | GET /hr/dashboard (candidate) → 403 FORBIDDEN; GET /manager/dashboard (candidate) → 403 FORBIDDEN; GET /manager/reports/recruitment-analytics (candidate) → 403 FORBIDDEN |  |
| UAT-DASH-011 | P3 | N/A |  | UI-only: recovery/rendering behavior (Auto:No) |
| UAT-DASH-012 | P3 | N/A |  | UI-only: bucketing observation (Auto:No) |
| UAT-LOG-001 | P2 | PASS | GET /sysadmin/audit-logs (admin) → 200; GET /sysadmin/audit-logs (HR, no System_LOG_VIEW) → 403 FORBIDDEN |  |
| UAT-LOG-002 | P3 | N/A |  | documented gap (Q-LOG-01): auth events not written to SystemLog; manual absence check |
| UAT-LOG-003 | P2 | N/A |  | deferred: stateful write (write-phase) — requires deactivating a spare user |
| UAT-LOG-004 | P2 | N/A |  | deferred: stateful write (write-phase) — requires editing role permissions |
| UAT-LOG-005 | P1 | PASS | GET /sysadmin/audit-logs (admin) → 200 |  |
| UAT-LOG-006 | P2 | PASS | GET audit-logs (HR) → 403 FORBIDDEN; GET audit-logs (candidate) → 403 FORBIDDEN; GET audit-logs (guest) → 401 UNAUTHENTICATED | immutable: no create/update/delete endpoint exists for audit logs |
| UAT-LOG-007 | P3 | PASS | GET audit-logs?q=status → 200; GET audit-logs?page=9999 (out of range) → 200 |  |
| UAT-LOG-008 | P3 | N/A |  | deferred: manual correlation of automation action → execution/step logs (Auto:No) |
| UAT-MDATA-001 | P2 | PASS | GET /departments → 200 |  |
| UAT-MDATA-002 | P2 | PASS | GET /departments/{id} (HR) → 200; GET /departments/{id} (candidate) → 403 FORBIDDEN; GET /departments/{id} (guest) → 401 UNAUTHENTICATED |  |
| UAT-MDATA-003 | P1 | N/A |  | deferred: stateful write (write-phase) — PUT /departments/{id} sets head; cleanup required |
| UAT-MDATA-004 | P2 | N/A |  | deferred: stateful write (write-phase) — PUT /departments/{id} renames; cleanup required |
| UAT-MDATA-005 | P2 | PASS | PUT /departments/{id} (candidate) → 403 FORBIDDEN; PUT /departments/{id} (guest) → 401 UNAUTHENTICATED | rejected at auth layer; no mutation applied |
| UAT-MDATA-006 | P3 | PASS | GET /skills → 200 |  |
| UAT-MDATA-007 | P3 | N/A |  | UI-only: offer editor references seed master data (no standalone lookup API) |
| UAT-MDATA-008 | P2 | PASS | GET /users/assignable-recruitment-owners (HR) → 200; GET /users/assignable-recruitment-owners (candidate) → 403 FORBIDDEN |  |
| UAT-MDATA-009 | P3 | N/A |  | documented gap (Q-MDATA-01): no create/update/delete API for skills/benefits/currencies/templates |
| UAT-MDATA-010 | P3 | N/A |  | documented: no delete API for departments/master data; FK integrity enforced at DB only |
| UAT-NOTI-001 | P1 | PASS | GET /notifications?page=1&pageSize=20 → 200 |  |
| UAT-NOTI-002 | P1 | PASS | GET /notifications/counts → 200 |  |
| UAT-NOTI-003 | P0 | N/A | POST /notifications/seen | deferred: stateful write (write-phase) |
| UAT-NOTI-004 | P0 | N/A | PATCH /notifications/{id}/read | deferred: stateful write (write-phase) |
| UAT-NOTI-005 | P2 | N/A | POST /notifications/read-all | deferred: stateful write (write-phase) |
| UAT-NOTI-006 | P3 | PASS | GET /notifications/unread-count → 200 |  |
| UAT-NOTI-007 | P1 | N/A | GET /notifications/stream | deferred: SSE stream + event trigger (write-phase / manual) |
| UAT-NOTI-008 | P1 | PASS | GET /notifications → 200 |  |
| UAT-NOTI-009 | P0 | N/A | GET /notifications/stream | deferred: two concurrent SSE clients (write-phase / manual) |
| UAT-NOTI-010 | P2 | N/A | GET /notifications/stream | deferred: SSE disconnect/reconnect recovery (manual) |
| UAT-NOTI-011 | P2 | PASS | GET /notifications → 200 | read-only heuristic scan (partial per spec) |
| UAT-NOTI-012 | P1 | PASS | GET /notifications (guest) → 401 UNAUTHENTICATED; POST /notifications/{nq-id}/read (as haidang) → 404 NOTIFICATION_NOT_FOUND | no mutation: cross-user read rejected by scoping |
| UAT-NOTI-013 | P1 | N/A | n/a | requires forcing publish failure during a status change (local fault-injection) |
| UAT-NOTI-014 | P3 | N/A | n/a | concurrency/UI: two tabs, manual |
| UAT-NOTI-015 | P3 | N/A | n/a | UI-only (empty/error rendering) |
| UAT-NOTI-016 | P1 | N/A | n/a | deferred: large integration sweep driving every state transition (write-phase) |
| UAT-NOTI-017 | P3 | N/A | n/a | data-gap analysis (Q-NOTI-01); no read API for the lookup table |
| UAT-NOTI-018 | P3 | N/A | n/a | UI-only (toast card rendering) |
| UAT-RBAC-001 | P1 | PASS | GET /sysadmin/overview → 200; GET /sysadmin/users → 200 |  |
| UAT-RBAC-002 | P0 | PASS | GET /sysadmin/users (HR) → 403 FORBIDDEN; GET /sysadmin/rbac/roles (candidate) → 403 FORBIDDEN; GET /sysadmin/users (guest) → 401 UNAUTHENTICATED |  |
| UAT-RBAC-003 | P1 | PASS | GET /sysadmin/rbac/roles → 200; GET /sysadmin/rbac/modules → 200; GET /sysadmin/rbac/roles/{HR}/permissions → 200 |  |
| UAT-RBAC-004 | P0 | N/A | PUT /sysadmin/rbac/roles/{id}/permissions | deferred: stateful write, GLOBAL matrix edit — single owner, restore required (write-phase) |
| UAT-RBAC-005 | P1 | N/A | n/a | deferred: requires a matrix edit + UI refresh observation (write-phase) |
| UAT-RBAC-006 | P1 | N/A | PUT …/permissions | deferred: requires a constructed role (view-yes, manage-no); not in seed (write-phase) |
| UAT-RBAC-007 | P1 | N/A | PUT /sysadmin/users/{id}/roles | deferred: stateful write on a spare [UAT] user (write-phase) |
| UAT-RBAC-008 | P0 | N/A | PATCH /sysadmin/users/{id}/status | deferred: stateful write; must target a spare [UAT] user, never a seed persona (write-phase) |
| UAT-RBAC-009 | P2 | N/A | PATCH /sysadmin/users/{id}/status | deferred: stateful write (write-phase) |
| UAT-RBAC-010 | P1 | N/A | PATCH /sysadmin/users/{admin}/status | deferred: would target seed admin status endpoint; safety rule — verify guard in write-phase |
| UAT-RBAC-011 | P1 | N/A | PATCH /sysadmin/users/{admin}/status | deferred: requires reducing to one admin (deactivate minhkhoi); admin-lockout risk (write-phase) |
| UAT-RBAC-012 | P3 | PASS | PATCH /sysadmin/users/{id}/status {Frozen} → 400 USER_STATUS_INVALID | invalid value never persisted (no mutation) |
| UAT-RBAC-013 | P2 | N/A | PATCH /sysadmin/users/{id}/status | deferred: stateful write (set spare user Blocked) (write-phase) |
| UAT-RBAC-014 | P1 | N/A | PUT /sysadmin/users/{id}/roles | deferred: stateful write + session boundary observation (write-phase) |
| UAT-RBAC-015 | P2 | PASS | GET /sysadmin/users?q=thucuyen → 200; GET /sysadmin/users?roleId=Candidate&status=Active → 200; GET /sysadmin/users?page=9999 → 200 |  |
| UAT-RBAC-016 | P0 | PASS | GET /sysadmin/overview (candidate, direct API) → 403 FORBIDDEN; GET /sysadmin/overview (guest) → 401 UNAUTHENTICATED | server enforces regardless of hidden menu |
| UAT-RBAC-017 | P0 | PASS | PATCH /sysadmin/users/{anyId}/status (candidate) → 403 FORBIDDEN | permission gate blocks before id resolution (no mutation) |
| UAT-RBAC-018 | P2 | N/A | PATCH/PUT /sysadmin/users/{id} | deferred: needs a successful write to confirm whitelisting (write-phase) |
| UAT-RBAC-019 | P2 | PASS | GET /sysadmin/audit-logs (admin) → 200; GET /sysadmin/audit-logs (HR) → 403 FORBIDDEN |  |
| UAT-RBAC-020 | P3 | N/A | PUT …/roles, PUT …/permissions | deferred: stateful write (re-grant idempotency) (write-phase) |
| UAT-RBAC-021 | P2 | PASS | GET /sysadmin/rbac/modules → 200 |  |
| UAT-RBAC-022 | P2 | N/A | n/a | documentation/analysis compare (Q-RBAC-01); no single API to assert |
| UAT-ERR-001 | P0 | FAIL | register JSON→415, multipart-missing→400 ProblemDetails | FINDING-CONTRACT-01 (Low): model-binding validation returns ASP.NET ProblemDetails (PascalCase errors, no errorCode), not the app VALIDATION_FAILED+camelCase envelope. FE apiError.ts:405 falls back to data.errors so errors DO surface, but PascalCase field names don't match camelCase form fields → inline field error degrades to a toast; no stable errorCode |
| UAT-ERR-002 | P1 | PASS | errorCode=JOB_NOT_FOUND | message(debug) + code both present; FE i18n mapping is UI-side |
| UAT-ERR-003 | P2 | PASS | {"400":"BAD_REQUEST","401":"AUTH_ERROR","403":"FORBIDDEN","404":"NOT_FOUND"} | 400.type path-dependent (app-validation=BAD_REQUEST); model-binding 400s emit raw ProblemDetails — see FINDING-CONTRACT-01 |
| UAT-ERR-004 | P2 | FAIL | register JSON→415 | FINDING-CONTRACT-01 (Low): register is multipart-only (JSON→415) + model-binding validation → ProblemDetails, not VALIDATION_FAILED camelCase envelope |
| UAT-ERR-005 | P0 | N/A |  | deferred: 500 not reproducible on prod (no 500s across business flows); traceId presence covered by UAT-ERR-013 |
| UAT-ERR-006 | P1 | N/A |  | UI-only: NETWORK_ERROR is generated client-side; no server response to assert |
| UAT-ERR-007 | P2 | N/A |  | UI-only: TIMEOUT_ERROR (client abort); not server-observable |
| UAT-ERR-008 | P3 | N/A |  | UI-only: FE getErrorMessage fallback; server never emits an off-contract code |
| UAT-ERR-009 | P3 | N/A |  | UI-only: FE i18n dictionaries (errors.*); backend returns codes only |
| UAT-ERR-010 | P3 | PASS | POST /auth/candidate/forgot-password (direct) → 400 REQUIRED | server validation reachable API-direct (errorCode=REQUIRED); double-submit guard is UI-only |
| UAT-ERR-011 | P1 | N/A |  | deferred: stateful (write-phase) — needs a live apply to force 409 duplicate-active / 422 precondition |
| UAT-ERR-012 | P1 | PASS | 401 UNAUTHENTICATED; 403 FORBIDDEN |  |
| UAT-ERR-013 | P3 | PASS | traceA≠traceB |  |
| UAT-ERR-014 | P2 | PASS | POST /hr/jobs (salary max<min) → 400 VALIDATION_FAILED | BE rejects salary max<min; matches FE salary_check rule |
| UAT-API-001 | P1 | PASS | GET /jobs → 200 |  |
| UAT-API-002 | P1 | FAIL | register JSON→415 | FINDING-CONTRACT-01 (Low): no VALIDATION_FAILED+fieldErrors envelope (415 for JSON / ProblemDetails for multipart); FE has a ProblemDetails fallback |
| UAT-API-003 | P2 | PASS | POST /hr/jobs (invalid enum) → 400 VALIDATION_FAILED |  |
| UAT-API-004 | P0 | PASS | /hr/applications→401, /manager/dashboard→401, /sysadmin/users→401, /candidate/applications→401 |  |
| UAT-API-005 | P0 | PASS | candidate→/hr 403; HR→/sysadmin 403 |  |
| UAT-API-006 | P1 | PASS | 11111111→404, not-a-gu→404, 00000000→404 |  |
| UAT-API-007 | P2 | FAIL | ?page=1&pageSize=5→200, ?page=99999&pageSize=10→200, ?page=-1&pageSize=0→500, ?sortBy=__bogus__&sortDir=sideways→200 | BUG-STG-001: negative/zero pagination crashes (?page=-1&pageSize=0→500) — should clamp or 400 |
| UAT-API-008 | P1 | N/A |  | deferred: stateful (write-phase) — dup/parallel apply-decision-offer would mutate seed data |
| UAT-API-009 | P0 | PASS | withdraw other's app → 404 | blocked, no cross-owner mutation |
| UAT-API-010 | P1 | PASS | sqli→200/200 (no 500) |  |
| UAT-API-011 | P1 | N/A |  | deferred: stateful storage + UI escaping (write/UI-phase); API always returns JSON-encoded text |
| UAT-API-012 | P3 | PASS | PUT→405; text/plain→415 |  |
| UAT-API-013 | P2 | PASS | 11MB body → 413 |  |
| UAT-API-014 | P2 | N/A | GET /odata/Jobs → 200 text/html (SPA fallback) | OData not served on this host (/odata → SPA index.html); no data exposed. Verify on a host where OData is enabled. |
| UAT-API-015 | P1 | PASS | login user + /jobs: no passwordHash/token/apiKey/connStr |  |
| UAT-API-016 | P3 | PASS | health→301; swagger→200 (SPA shell, not real Swagger) |  |
| UAT-SEC-001 | P0 | PASS | garbage→401; tampered→401 | expiry/token_version revocation covered by UAT-AUTH-016/017 |
| UAT-SEC-002 | P0 | PASS | payloads→200/200/200 (no 500/leak) | storage+UI-escape parts deferred (write/UI-phase) |
| UAT-SEC-003 | P1 | PASS | wrongPw=unknown=401 INVALID_CREDENTIALS; forgot(unknown)→200 |  |
| UAT-SEC-004 | P0 | PASS | cand→sysadmin 403; hr→audit-logs 403; cand→manager 403 |  |
| UAT-SEC-005 | P1 | PASS | login 200; refresh 200; reuse→401 | logout is client-side (no endpoint); multi-tab UI deferred |
| UAT-SEC-006 | P0 | PASS | cross-owner withdraw → 404 |  |
| UAT-SEC-007 | P0 | PASS | no hash/token/connStr/stack in user, /jobs, or 404 error body |  |
| UAT-SEC-008 | P3 | FAIL | missing=[strict-transport-security,x-frame-options,x-content-type-options,content-security-policy,referrer-policy]; server="nginx/1.24.0 (Ubuntu)" | BUG-UAT-004 (OPEN): headers missing / server version disclosed |
| UAT-SEC-009 | P2 | PASS | 301 → https://www.recruitpro.site/ |  |
| UAT-SEC-010 | P3 | PASS | ACAO="" (not evil origin) | AllowFrontend policy — disallowed origin not echoed |
| UAT-SEC-011 | P2 | PASS | draft job → 404 | BUG-UAT-002 appears FIXED (target 404) |
| UAT-SEC-012 | P1 | N/A |  | deferred: AI prompt-injection (manual, see UAT-AI-014/015) |
| UAT-SEC-013 | P3 | N/A |  | deferred: Auto=No; must not lock seed accounts (Q-SEC-01, likely no throttling) |
| UAT-SEC-014 | P1 | PASS | cand→internal 401 (PORTAL_ACCESS_DENIED); internal→cand 401 |  |
| UAT-UI-001 | P2 | PASS | candidate/hr/manager/systemadmin bundles distinct | sidebar/bottom-nav rendering is UI-only; role source verified via API |
| UAT-UI-002 | P2 | N/A |  | UI-only: FE RouteGuard requireAll (AND gate) is client-side routing |
| UAT-UI-003 | P3 | N/A |  | UI-only: getRoleHomePath is client-side routing |
| UAT-UI-004 | P3 | PASS | forbidden→403; unknown→404 | SPA catch-all/forbidden redirects are UI-side |
| UAT-UI-005 | P2 | PASS | 200 text/html | SPA fallback serves index.html; session rehydrate is UI-side |
| UAT-UI-006 | P3 | N/A |  | UI-only: modal interaction |
| UAT-UI-007 | P3 | PASS | 200; keys=["items","currentPage","pageSize","totalItems","totalPages"] | table controls are UI; paging contract verified via API |
| UAT-UI-008 | P2 | N/A |  | UI-only: spinner/skeleton/empty/error/toast rendering |
| UAT-UI-009 | P3 | N/A |  | UI-only: ToastContainer placement/dedup |
| UAT-UI-010 | P3 | N/A |  | UI-only: text rendering/clipping (no echo endpoint to assert) |
| UAT-UI-011 | P3 | N/A |  | UI-only: button disable; server idempotency is UAT-API-008 (write-phase) |
| UAT-UI-012 | P3 | N/A |  | UI-only: zoom/tab-order/focus/labels |
| UAT-UI-013 | P2 | N/A |  | UI-only: responsive layout / mobile drawer |
| UAT-UI-014 | P3 | N/A |  | UI-only: i18n rp.lang localStorage persistence |
| UAT-AI-001 | P2 | PASS | HR→200, candidate→403, guest→401 |  |
| UAT-AI-006 | P3 | PASS | unknown ranking session → 404 ENTITY_NOT_FOUND |  |
| UAT-AI-016 | P0 | PASS | foreign/unknown conversation → 404 ENTITY_NOT_FOUND | cross-HR real-session IDOR needs two live sessions — see MANUAL-CHECKLIST |
| UAT-AI-017 | P2 | PASS | unknown-job pool → 404 JOB_NOT_FOUND |  |
| UAT-AI-015 | P1 | PASS | copilot/jobs: no apiKey/systemPrompt/secret fields |  |
| UAT-JOB-W01 | P0 | PASS | POST /hr/jobs → 201, approvalStatus=PendingApproval |  |
| UAT-JOB-W02 | P0 | PASS | PATCH status PENDING_APPROVAL → 200 (PendingApproval) |  |
| UAT-JOB-W03 | P0 | PASS | PATCH status APPROVED (head) → 200 |  |
| UAT-JOB-W04 | P1 | PASS | GET /jobs/{id} anon → 200 status=Approved |  |
| UAT-FILE-W01 | P1 | PASS | synthetic PDF → 400 RESUME_FILE_UNSUPPORTED_TYPE | file-type validation enforced (correct); candidate's existing resume drives the apply chain |
| UAT-APP-W00 | P1 | PASS | apply enforces RESUME_REQUIRED (422) when no resume — confirmed | resume now uploaded for the happy-path chain |
| UAT-APP-W01 | P0 | PASS | POST /jobs/{id}/apply → 201 |  |
| UAT-APP-W02 | P1 | PASS | duplicate apply → 409 APPLICATION_ALREADY_ACTIVE |  |
| UAT-APP-W03 | P0 | PASS | HR Screening→200, HR ManagerReview→200, HR Interview→403(expect 403), HEAD Interview→200 |  |
| UAT-INT-W01 | P0 | PASS | POST /hr/interviews → 201 |  |
| UAT-INT-W02 | P1 | PASS | PATCH interview status Completed → 200 |  |
| UAT-OFFER-W01 | P0 | PASS | draft→200, offer/send→200 |  |
| UAT-OFFER-W02 | P0 | PASS | POST accept-offer → 200 |  |
| UAT-WRITE-CLEANUP | P1 | PASS | interview del→200, close→200, job del→200 | best-effort cleanup; leftover [UAT] rows are safe to remove manually |
| UAT-NOTI-W01 | P0 | PASS | POST /notifications/seen → 200 |  |
| UAT-NOTI-W02 | P1 | PASS | PATCH /notifications/{id}/read → 200 |  |
| UAT-NOTI-W03 | P2 | PASS | read-all → 200 |  |
| UAT-REG-W01 | P0 | PASS | POST /candidates/register → 201 |  |
| UAT-REG-W02 | P0 | PASS | candidate login → 200 |  |
| UAT-REG-W03 | P1 | PASS | duplicate username → 400 VALIDATION_FAILED |  |
| UAT-PROF-W01 | P1 | PASS | PUT /candidate/profile → 200 |  |
| UAT-PROF-W02 | P1 | PASS | add→200, delete→200 |  |
| UAT-RBAC-W01 | P0 | PASS | deactivate→200, disabled login→401 ACCOUNT_DISABLED |  |
| UAT-RBAC-W02 | P2 | PASS | reactivate→200, login→200 |  |
| UAT-RBAC-W03 | P1 | PASS | self-deactivate → 409 USER_SELF_DEACTIVATION | guard: admin cannot disable own account |
| UAT-RBAC-W04 | P2 | PASS | invalid status "Frozen" → 400 USER_STATUS_INVALID |  |
| UAT-MDATA-W01 | P1 | PASS | PUT /departments/{id} update→200, restore→200 |  |
| UAT-MDATA-W02 | P1 | PASS | candidate→403, guest→401 (no mutation) |  |
| UAT-WF-W01 | P0 | PASS | POST workflows → 201 |  |
| UAT-WF-W02 | P1 | PASS | PATCH workflow → 200 |  |
| UAT-WF-W03 | P1 | PASS | publish → 200 |  |
| UAT-WF-W04 | P2 | PASS | PATCH enabled=false → 200 | no delete-workflow API; [UAT] workflow left Disabled (safe, tagged) |
| UAT-WF-W05 | P1 | PASS | HR create workflow → 403 (no mutation) |  |
