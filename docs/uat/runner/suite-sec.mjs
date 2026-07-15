import { api, login, suite, testCase, expectStatus, expectErrorCode, BASE, PW } from "./uat-lib.mjs";

const ORIGIN = BASE.replace(/\/api\/?$/, "");
const DRAFT_JOB_ID = "30000000-0000-4000-8000-000000000005"; // seed Draft job (BUG-UAT-002)
const SECRET_KEYS = /"(passwordhash|password|refreshtoken|tokenversion|apikey|secret|connectionstring|minio)"/i;
const LEAK_STACK = /(stacktrace|\bat RecruitPro\.|Npgsql|System\.[A-Za-z.]+Exception|Host=.*Password=|Server=.*Database=)/i;

async function rawFetch(method, url, { headers, body, redirect = "manual" } = {}) {
  try {
    const res = await fetch(url, { method, headers: headers || {}, body, redirect });
    const text = await res.text();
    let json = null; try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
    return { status: res.status, text, json, headers: res.headers };
  } catch (e) {
    return { status: 0, text: String(e?.message || e), json: null, headers: new Headers(), networkError: true };
  }
}

export async function runSec() {
  suite("SEC");

  await testCase("UAT-SEC-001", "JWT integrity — tampered/garbage bearer rejected → 401", "P0", async () => {
    const good = (await login("thucuyen")).accessToken;
    const tampered = good.slice(0, -3) + "AAA"; // corrupt the signature segment
    const a = await api("GET", "/hr/applications", { token: "garbage.jwt.token" });
    const b = await api("GET", "/hr/applications", { token: tampered });
    if (a.status !== 401) throw new Error(`garbage token not 401: ${a.status}`);
    if (b.status !== 401) throw new Error(`tampered signature not 401: ${b.status}`);
    return { actualApi: `garbage→401; tampered→401`, notes: "expiry/token_version revocation covered by UAT-AUTH-016/017" };
  });

  await testCase("UAT-SEC-002", "SQLi/XSS input inert (no 500/leak)", "P0", async () => {
    const payloads = ["' OR '1'='1", "<script>alert('uat')</script>", "'; DROP TABLE users;--"];
    const out = [];
    for (const p of payloads) {
      const r = await api("GET", `/jobs?search=${encodeURIComponent(p)}`);
      out.push(r.status);
      if (r.status >= 500) throw new Error(`500 on payload: ${p}`);
      if (LEAK_STACK.test(r.text || "")) throw new Error("SQL/stack leak in response");
    }
    return { actualApi: `payloads→${out.join("/")} (no 500/leak)`, notes: "storage+UI-escape parts deferred (write/UI-phase)" };
  });

  await testCase("UAT-SEC-003", "Credential handling & no user enumeration", "P1", async () => {
    const wrongPw = await api("POST", "/auth/internal/login", { body: { username: "thucuyen", password: "WrongPw@1" } });
    const unknown = await api("POST", "/auth/internal/login", { body: { username: "no_such_user_xyz", password: PW } });
    if (wrongPw.status !== 401 || unknown.status !== 401) throw new Error(`status differ: ${wrongPw.status}/${unknown.status}`);
    if (wrongPw.json?.errorCode !== unknown.json?.errorCode) throw new Error(`errorCode differ (enumeration): ${wrongPw.json?.errorCode} vs ${unknown.json?.errorCode}`);
    expectErrorCode(wrongPw, "INVALID_CREDENTIALS");
    const forgot = await api("POST", "/auth/internal/forgot-password", { body: { identifier: "no_such_user_xyz@recruitpro.vn" } });
    if (forgot.status >= 500) throw new Error("500 on forgot-password unknown identifier");
    return { actualApi: `wrongPw=unknown=401 INVALID_CREDENTIALS; forgot(unknown)→${forgot.status}` };
  });

  await testCase("UAT-SEC-004", "RBAC enforced server-side regardless of UI", "P0", async () => {
    const cand = (await login("nhatquang")).accessToken;
    const hr = (await login("thucuyen")).accessToken;
    const a = await api("GET", "/sysadmin/users", { token: cand });
    const b = await api("GET", "/sysadmin/audit-logs", { token: hr });
    const c = await api("GET", "/manager/reports/recruitment-analytics", { token: cand });
    for (const [name, r] of [["cand→sysadmin", a], ["hr→audit-logs", b], ["cand→manager", c]]) {
      if (r.status !== 403) throw new Error(`${name} expected 403, got ${r.status}`);
    }
    return { actualApi: "cand→sysadmin 403; hr→audit-logs 403; cand→manager 403" };
  });

  await testCase("UAT-SEC-005", "Session lifecycle: login → refresh rotate → reuse rejected", "P1", async () => {
    const lg = await api("POST", "/auth/internal/login", { body: { username: "giahan", password: PW } });
    expectStatus(lg, 200, "login");
    const rt1 = lg.json.data.refreshToken;
    const s1 = await api("POST", "/auth/refresh", { body: { refreshToken: rt1 } });
    expectStatus(s1, 200, "refresh RT1");
    const reuse = await api("POST", "/auth/refresh", { body: { refreshToken: rt1 } });
    if (reuse.status !== 401) throw new Error(`reused RT not rejected: ${reuse.status}`);
    return { actualApi: `login 200; refresh 200; reuse→401`, notes: "logout is client-side (no endpoint); multi-tab UI deferred" };
  });

  await testCase("UAT-SEC-006", "IDOR/BOLA across owned resources → 403/404", "P0", async () => {
    const haidang = (await login("haidang")).accessToken;
    const nhatquang = (await login("nhatquang")).accessToken;
    const mine = await api("GET", "/candidate/applications", { token: haidang });
    expectStatus(mine, 200, "GET /candidate/applications (haidang)");
    const list = mine.json?.data?.items || mine.json?.data || [];
    const otherId = Array.isArray(list) && list.length ? (list[0].id || list[0].applicationId) : null;
    if (!otherId) return { result: "N/A", notes: "no cross-owner application id available to probe" };
    const r = await api("POST", `/candidate/applications/${otherId}/withdraw`, { token: nhatquang, body: {} });
    if (![403, 404].includes(r.status)) throw new Error(`cross-owner mutation not blocked: ${r.status}`);
    return { actualApi: `cross-owner withdraw → ${r.status}` };
  });

  await testCase("UAT-SEC-007", "No sensitive-data leakage in responses/errors", "P0", async () => {
    const tok = await login("thucuyen");
    if (SECRET_KEYS.test(JSON.stringify(tok.user || {}))) throw new Error("login user leaks secret field");
    const err = await api("GET", `/jobs/11111111-1111-4111-8111-111111111111`);
    if (LEAK_STACK.test(err.text || "")) throw new Error("error body leaks stack/SQL/connstr");
    const jobs = await api("GET", "/jobs");
    if (SECRET_KEYS.test(jobs.text || "")) throw new Error("/jobs leaks secret field");
    return { actualApi: "no hash/token/connStr/stack in user, /jobs, or 404 error body" };
  });

  await testCase("UAT-SEC-008", "[KNOWN BUG] Missing security headers; server version disclosed (BUG-UAT-004)", "P3", async () => {
    const r = await rawFetch("GET", `${ORIGIN}/`);
    const want = ["strict-transport-security", "x-frame-options", "x-content-type-options", "content-security-policy", "referrer-policy"];
    const missing = want.filter(h => !r.headers.get(h));
    const server = r.headers.get("server") || "";
    const versionDisclosed = /\d/.test(server); // e.g. "nginx/1.24.0 (Ubuntu)"
    const notes = `missing=[${missing.join(",")}]; server="${server}"`;
    if (missing.length === 0 && !versionDisclosed) return { actualApi: "all headers present, server hidden", notes: "BUG-UAT-004 appears FIXED" };
    return { result: "FAIL", actualApi: notes, notes: "BUG-UAT-004 (OPEN): headers missing / server version disclosed" };
  });

  await testCase("UAT-SEC-009", "HTTPS enforcement — HTTP → HTTPS redirect", "P2", async () => {
    const r = await rawFetch("GET", "http://www.recruitpro.site/");
    const loc = r.headers.get("location") || "";
    const redirected = [301, 302, 307, 308].includes(r.status);
    if (!redirected) throw new Error(`no HTTP→HTTPS redirect (status ${r.status})`);
    if (!/^https:/i.test(loc)) throw new Error(`redirect Location not https: ${loc}`);
    return { actualApi: `${r.status} → ${loc}` };
  });

  await testCase("UAT-SEC-010", "CORS: disallowed origin not reflected", "P3", async () => {
    const evil = "https://evil.example.com";
    const r = await api("GET", "/jobs", { headers: { Origin: evil } });
    const acao = r.headers?.get?.("access-control-allow-origin") || "";
    if (acao === evil || acao === "*") throw new Error(`permissive CORS: ACAO=${acao}`);
    return { actualApi: `ACAO="${acao}" (not evil origin)`, notes: "AllowFrontend policy — disallowed origin not echoed" };
  });

  await testCase("UAT-SEC-011", "[KNOWN BUG] Public job-detail exposes non-approved job (BUG-UAT-002)", "P2", async () => {
    const r = await api("GET", `/jobs/${DRAFT_JOB_ID}`);
    if (r.status === 404) return { actualApi: "draft job → 404", notes: "BUG-UAT-002 appears FIXED (target 404)" };
    if (r.status === 200) return { result: "FAIL", actualApi: "draft job → 200", notes: "BUG-UAT-002 (OPEN): non-approved job publicly readable; expected 404" };
    return { result: "FAIL", actualApi: `draft job → ${r.status}`, notes: "unexpected status for known-bug probe" };
  });

  await testCase("UAT-SEC-012", "Prompt-injection & AI secret protection", "P1", async () => {
    return { result: "N/A", notes: "deferred: AI prompt-injection (manual, see UAT-AI-014/015)" };
  });

  await testCase("UAT-SEC-013", "Rate limiting / brute force", "P3", async () => {
    return { result: "N/A", notes: "deferred: Auto=No; must not lock seed accounts (Q-SEC-01, likely no throttling)" };
  });

  await testCase("UAT-SEC-014", "Portal isolation: cross-portal login → 401", "P1", async () => {
    const candOnInternal = await api("POST", "/auth/internal/login", { body: { username: "nhatquang", password: PW } });
    const internalOnCand = await api("POST", "/auth/candidate/login", { body: { username: "thucuyen", password: PW } });
    if (candOnInternal.status !== 401) throw new Error(`candidate on internal not 401: ${candOnInternal.status}`);
    if (internalOnCand.status !== 401) throw new Error(`internal on candidate not 401: ${internalOnCand.status}`);
    return { actualApi: `cand→internal ${candOnInternal.status} (${candOnInternal.json?.errorCode}); internal→cand ${internalOnCand.status}` };
  });
}
