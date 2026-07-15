import { api, login, suite, testCase, expectStatus, BASE } from "./uat-lib.mjs";

const ORIGIN = BASE.replace(/\/api\/?$/, "");
const UNKNOWN_GUID = "11111111-1111-4111-8111-111111111111";

async function rawFetch(method, url, { headers } = {}) {
  try {
    const res = await fetch(url, { method, headers: headers || {}, redirect: "manual" });
    const text = await res.text();
    return { status: res.status, text, headers: res.headers };
  } catch (e) {
    return { status: 0, text: String(e?.message || e), headers: new Headers(), networkError: true };
  }
}

export async function runUi() {
  suite("UI");

  await testCase("UAT-UI-001", "Role bundle source: login returns distinct roles/permissions per role", "P2", async () => {
    const roleOf = (t) => (t.user?.roles || []).map(String).map(x => x.toLowerCase()).sort();
    const cand = await login("nhatquang");
    const hr = await login("thucuyen");
    const mgr = await login("quocbao");
    const adm = await login("admin");
    const rc = roleOf(cand), rh = roleOf(hr), rm = roleOf(mgr), ra = roleOf(adm);
    if (!rc.includes("candidate") || !rh.includes("hr") || !rm.includes("manager") || !ra.includes("systemadmin"))
      throw new Error(`role bundles wrong: ${JSON.stringify({ rc, rh, rm, ra })}`);
    return { actualApi: `candidate/hr/manager/systemadmin bundles distinct`, notes: "sidebar/bottom-nav rendering is UI-only; role source verified via API" };
  });

  await testCase("UAT-UI-002", "requireAll route: interview schedule needs BOTH FE perms", "P2", async () => {
    return { result: "N/A", notes: "UI-only: FE RouteGuard requireAll (AND gate) is client-side routing" };
  });

  await testCase("UAT-UI-003", "Route home redirect per role", "P3", async () => {
    return { result: "N/A", notes: "UI-only: getRoleHomePath is client-side routing" };
  });

  await testCase("UAT-UI-004", "No dead ends: forbidden API → 403; unknown API route → 404", "P3", async () => {
    const cand = (await login("nhatquang")).accessToken;
    const forbidden = await api("GET", "/hr/applications", { token: cand });
    if (forbidden.status !== 403) throw new Error(`forbidden API expected 403, got ${forbidden.status}`);
    const unknown = await api("GET", "/this/route/does/not/exist");
    if (![404, 401].includes(unknown.status)) throw new Error(`unknown API route expected 404, got ${unknown.status}`);
    return { actualApi: `forbidden→403; unknown→${unknown.status}`, notes: "SPA catch-all/forbidden redirects are UI-side" };
  });

  await testCase("UAT-UI-005", "Deep-link refresh on nested route serves SPA fallback (200 html)", "P2", async () => {
    const r = await rawFetch("GET", `${ORIGIN}/hr/applications/${UNKNOWN_GUID}`);
    const ct = r.headers.get("content-type") || "";
    if (r.status !== 200) throw new Error(`deep-link expected 200 SPA fallback, got ${r.status}`);
    if (!/text\/html/i.test(ct)) throw new Error(`expected html, got ${ct}`);
    return { actualApi: `${r.status} ${ct}`, notes: "SPA fallback serves index.html; session rehydrate is UI-side" };
  });

  await testCase("UAT-UI-006", "Modal behavior (scroll/overlay/escape/unsaved)", "P3", async () => {
    return { result: "N/A", notes: "UI-only: modal interaction" };
  });

  await testCase("UAT-UI-007", "Pagination / table components (common) — API paging metadata", "P3", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const r = await api("GET", "/hr/applications?page=1&pageSize=5", { token: hr });
    expectStatus(r, 200, "GET /hr/applications (paged)");
    const d = r.json?.data || {};
    const hasPaging = ["items", "totalItems", "totalCount", "total", "page", "pageSize"].some(k => k in d);
    if (!hasPaging && !Array.isArray(d)) throw new Error(`no paging metadata: keys=${Object.keys(d)}`);
    return { actualApi: `200; keys=${Array.isArray(d) ? "[array]" : JSON.stringify(Object.keys(d))}`, notes: "table controls are UI; paging contract verified via API" };
  });

  await testCase("UAT-UI-008", "Loading / empty / error / success states (global)", "P2", async () => {
    return { result: "N/A", notes: "UI-only: spinner/skeleton/empty/error/toast rendering" };
  });

  await testCase("UAT-UI-009", "Toast placement & dedup", "P3", async () => {
    return { result: "N/A", notes: "UI-only: ToastContainer placement/dedup" };
  });

  await testCase("UAT-UI-010", "Vietnamese Unicode & long text rendering", "P3", async () => {
    return { result: "N/A", notes: "UI-only: text rendering/clipping (no echo endpoint to assert)" };
  });

  await testCase("UAT-UI-011", "Double-click / rapid actions guarded", "P3", async () => {
    return { result: "N/A", notes: "UI-only: button disable; server idempotency is UAT-API-008 (write-phase)" };
  });

  await testCase("UAT-UI-012", "Browser zoom / keyboard nav / basic a11y", "P3", async () => {
    return { result: "N/A", notes: "UI-only: zoom/tab-order/focus/labels" };
  });

  await testCase("UAT-UI-013", "Responsive & mobile drawer", "P2", async () => {
    return { result: "N/A", notes: "UI-only: responsive layout / mobile drawer" };
  });

  await testCase("UAT-UI-014", "Language switch (vi/en) persists", "P3", async () => {
    return { result: "N/A", notes: "UI-only: i18n rp.lang localStorage persistence" };
  });
}
