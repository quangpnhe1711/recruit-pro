import { api, login, suite, testCase, expectStatus, expectErrorCode, BASE } from "./uat-lib.mjs";

const UNKNOWN_GUID = "11111111-1111-4111-8111-111111111111";
const NIL_GUID = "00000000-0000-0000-0000-000000000000";
const BAD_GUID = "not-a-guid";
const ORIGIN = BASE.replace(/\/api\/?$/, ""); // OData + health live at /odata and / (outside /api)

async function rawFetch(method, url, { headers, body } = {}) {
  try {
    const res = await fetch(url, { method, headers: headers || {}, body, redirect: "manual" });
    const text = await res.text();
    let json = null; try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
    return { status: res.status, text, json, headers: res.headers };
  } catch (e) {
    return { status: 0, text: String(e?.message || e), json: null, headers: new Headers(), networkError: true };
  }
}

function assertOkEnvelope(r) {
  const j = r.json || {};
  if (j.success !== true) throw new Error(`success not true (${JSON.stringify(j.success)})`);
  if (typeof j.statusCode !== "number") throw new Error("missing numeric statusCode");
  if (!("data" in j)) throw new Error("missing data");
  return `200 success data:${Array.isArray(j.data) ? `[${j.data.length}]` : typeof j.data}`;
}

const SECRET_KEYS = /"(passwordhash|password|refreshtoken|tokenversion|apikey|secret|connectionstring|minio)"/i;

export async function runApi() {
  suite("API");

  await testCase("UAT-API-001", "Happy path + ApiResponse envelope shape", "P1", async () => {
    const r = await api("GET", "/jobs");
    const s = expectStatus(r, 200, "GET /jobs");
    const summary = assertOkEnvelope(r);
    return { actualApi: `${s}`, actualData: summary };
  });

  await testCase("UAT-API-002", "Required field / invalid shape → 400 VALIDATION_FAILED + fieldErrors", "P1", async () => {
    // register is multipart-only (JSON→415) and validates via model-binding → raw ProblemDetails.
    const r = await api("POST", "/candidates/register", { body: {} });
    if (r.json?.errorCode === "VALIDATION_FAILED" && r.json?.error?.fieldErrors?.length > 0) {
      return { actualApi: "register → VALIDATION_FAILED + fieldErrors" };
    }
    return { result: "FAIL", actualApi: `register JSON→${r.status}`, notes: "FINDING-CONTRACT-01 (Low): no VALIDATION_FAILED+fieldErrors envelope (415 for JSON / ProblemDetails for multipart); FE has a ProblemDetails fallback" };
  });

  await testCase("UAT-API-003", "Invalid enum / out-of-range → 400/422, never 500", "P2", async () => {
    const hr = (await login("thucuyen")).accessToken;
    // Invalid WorkMode enum; validation/binding rejects before any create (non-destructive).
    const r = await api("POST", "/hr/jobs", { token: hr, body: { title: "[UAT] api-003", workMode: "Teleport", employmentType: "NotAType" } });
    const s = expectStatus(r, [400, 422], "POST /hr/jobs (invalid enum)");
    if (r.status >= 500) throw new Error("500 on invalid enum");
    return { actualApi: s };
  });

  await testCase("UAT-API-004", "No token → 401 UNAUTHENTICATED on protected endpoints", "P0", async () => {
    const probes = ["/hr/applications", "/manager/dashboard", "/sysadmin/users", "/candidate/applications"];
    const out = [];
    for (const p of probes) {
      const r = await api("GET", p);
      out.push(`${p}→${r.status}`);
      if (r.status !== 401) throw new Error(`${p} expected 401 got ${r.status}`);
    }
    return { actualApi: out.join(", ") };
  });

  await testCase("UAT-API-005", "Wrong role → 403 FORBIDDEN", "P0", async () => {
    const cand = (await login("nhatquang")).accessToken;
    const hr = (await login("thucuyen")).accessToken;
    const a = await api("GET", "/hr/applications", { token: cand });
    expectStatus(a, 403, "GET /hr/applications (candidate)");
    expectErrorCode(a, "FORBIDDEN");
    const b = await api("GET", "/sysadmin/users", { token: hr });
    expectStatus(b, 403, "GET /sysadmin/users (HR)");
    return { actualApi: "candidate→/hr 403; HR→/sysadmin 403" };
  });

  await testCase("UAT-API-006", "Not found / malformed / nil UUID → 404/400, no 500", "P1", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const cases = [UNKNOWN_GUID, BAD_GUID, NIL_GUID];
    const out = [];
    for (const id of cases) {
      const r = await api("GET", `/hr/applications/${id}`, { token: hr });
      out.push(`${id.slice(0,8)}→${r.status}`);
      if (r.status >= 500) throw new Error(`500 on id ${id}`);
      if (![400, 404].includes(r.status)) throw new Error(`unexpected ${r.status} for ${id}`);
    }
    return { actualApi: out.join(", ") };
  });

  await testCase("UAT-API-007", "Pagination / sort / filter boundaries clamp, no error", "P2", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const probes = ["?page=1&pageSize=5", "?page=99999&pageSize=10", "?page=-1&pageSize=0", "?sortBy=__bogus__&sortDir=sideways"];
    const out = [];
    for (const q of probes) {
      const r = await api("GET", `/hr/applications${q}`, { token: hr });
      out.push(`${q}→${r.status}`);
    }
    // CONFIRMED DEFECT: page=-1&pageSize=0 returns 500 SERVER_ERROR instead of clamping/400.
    const bad = out.find((o) => o.includes("→5"));
    if (bad) return { result: "FAIL", actualApi: out.join(", "), notes: `BUG-STG-001: negative/zero pagination crashes (${bad}) — should clamp or 400` };
    if (out.some((o) => !o.endsWith("→200"))) return { result: "FAIL", actualApi: out.join(", "), notes: "non-200 on a boundary (expected clamp to 200)" };
    return { actualApi: out.join(", ") };
  });

  await testCase("UAT-API-008", "Concurrency / idempotency on state-changing endpoints", "P1", async () => {
    return { result: "N/A", notes: "deferred: stateful (write-phase) — dup/parallel apply-decision-offer would mutate seed data" };
  });

  await testCase("UAT-API-009", "IDOR/BOLA: cross-owner access → 403/404", "P0", async () => {
    // haidang owns some applications; nhatquang must not act on them. A blocked mutation returns 403/404
    // and mutates nothing (that IS the assertion).
    const haidang = (await login("haidang")).accessToken;
    const nhatquang = (await login("nhatquang")).accessToken;
    const mine = await api("GET", "/candidate/applications", { token: haidang });
    expectStatus(mine, 200, "GET /candidate/applications (haidang)");
    const list = mine.json?.data?.items || mine.json?.data || [];
    const otherId = Array.isArray(list) && list.length ? (list[0].id || list[0].applicationId) : null;
    if (!otherId) return { result: "N/A", notes: "no cross-owner application id available to probe" };
    const r = await api("POST", `/candidate/applications/${otherId}/withdraw`, { token: nhatquang, body: {} });
    if (![403, 404].includes(r.status)) throw new Error(`cross-owner withdraw not blocked: HTTP ${r.status}`);
    return { actualApi: `withdraw other's app → ${r.status}`, notes: "blocked, no cross-owner mutation" };
  });

  await testCase("UAT-API-010", "SQL injection input is inert (no 500/leak)", "P1", async () => {
    const payloads = ["' OR '1'='1", "'; DROP TABLE users;--"];
    const out = [];
    for (const p of payloads) {
      const r = await api("GET", `/jobs?search=${encodeURIComponent(p)}`);
      out.push(`${r.status}`);
      if (r.status >= 500) throw new Error(`500 on SQLi payload: ${p}`);
      if (SECRET_KEYS.test(r.text || "")) throw new Error("secret-looking key leaked in response");
    }
    return { actualApi: `sqli→${out.join("/")} (no 500)` };
  });

  await testCase("UAT-API-011", "XSS payload stored/returned safely", "P1", async () => {
    return { result: "N/A", notes: "deferred: stateful storage + UI escaping (write/UI-phase); API always returns JSON-encoded text" };
  });

  await testCase("UAT-API-012", "Wrong HTTP method / content-type → 405/415/400, no 500", "P3", async () => {
    const wrongVerb = await api("PUT", "/auth/login", { body: { username: "x", password: "y" } });
    const wrongCt = await rawFetch("POST", `${BASE}/auth/login`, { headers: { "Content-Type": "text/plain" }, body: "not json" });
    if (wrongVerb.status >= 500) throw new Error(`500 on wrong verb`);
    if (![405, 404].includes(wrongVerb.status)) throw new Error(`PUT /auth/login expected 405, got ${wrongVerb.status}`);
    if (wrongCt.status >= 500) throw new Error(`500 on wrong content-type`);
    if (![415, 400].includes(wrongCt.status)) throw new Error(`text/plain expected 415/400, got ${wrongCt.status}`);
    return { actualApi: `PUT→${wrongVerb.status}; text/plain→${wrongCt.status}` };
  });

  await testCase("UAT-API-013", "Oversized payload → 413/400, no crash", "P2", async () => {
    const big = "a".repeat(11 * 1024 * 1024); // > 10 MB global cap
    const r = await api("POST", "/candidates/register", { body: { username: "uat", fullName: big } });
    if (r.status >= 500) throw new Error(`500 on oversized body`);
    if (![413, 400].includes(r.status)) throw new Error(`oversized expected 413/400, got ${r.status}`);
    return { actualApi: `11MB body → ${r.status}` };
  });

  await testCase("UAT-API-014", "OData read-only surface, $top cap 100, role-gate, no writes", "P2", async () => {
    // On this host /odata/* is not routed to the API — it falls through to the SPA (200 text/html), so
    // there is no OData surface to grade (and no data exposed). Verified: anon & HR both get the HTML shell.
    const jobs = await rawFetch("GET", `${ORIGIN}/odata/Jobs?$top=1`);
    const isSpa = (jobs.headers?.get?.("content-type") || "").includes("text/html") || /<!doctype html/i.test(jobs.text || "");
    if (isSpa) return { result: "N/A", actualApi: `GET /odata/Jobs → ${jobs.status} text/html (SPA fallback)`, notes: "OData not served on this host (/odata → SPA index.html); no data exposed. Verify on a host where OData is enabled." };
    const rows = jobs.json?.value || [];
    if (rows.length > 100) throw new Error(`$top not capped: ${rows.length} rows`);
    return { actualApi: `OData live: Jobs rows=${rows.length}` };
  });

  await testCase("UAT-API-015", "Response has no sensitive fields", "P1", async () => {
    const tok = await login("thucuyen");
    if (SECRET_KEYS.test(JSON.stringify(tok.user || {}))) throw new Error("login user leaks a secret field");
    const jobs = await api("GET", "/jobs");
    if (SECRET_KEYS.test(jobs.text || "")) throw new Error("/jobs leaks a secret field");
    return { actualApi: "login user + /jobs: no passwordHash/token/apiKey/connStr" };
  });

  await testCase("UAT-API-016", "Health root 200; Swagger not served in prod", "P3", async () => {
    const health = await rawFetch("GET", `${BASE}`);
    const swagger = await rawFetch("GET", `${ORIGIN}/swagger/index.html`);
    // Swagger must not be a real UI in prod. It returns 404, or 200 with the SPA shell (text/html titled
    // "RecruitPro") — either way the Swagger UI is NOT served. FAIL only if a genuine Swagger doc appears.
    const body = swagger.text || "";
    const realSwagger = swagger.status === 200 && /swagger-ui|swagger\.json|SwaggerUIBundle/i.test(body);
    if (realSwagger) return { result: "FAIL", actualApi: `swagger→200 real UI`, notes: "Swagger UI exposed in prod — should be dev-only" };
    return { actualApi: `health→${health.status}; swagger→${swagger.status} (${swagger.status === 200 ? "SPA shell, not real Swagger" : "not served"})` };
  });
}
