import { api, login, suite, testCase, expectStatus } from "./uat-lib.mjs";

// DASH — Dashboard & analytics (UAT_TEST_CASES.md 1631–1701)
// Endpoints (DashboardController / ManagerAnalyticsController):
//   GET /api/candidate/dashboard            [Candidate]
//   GET /api/hr/dashboard                    [HR,Manager]
//   GET /api/manager/dashboard               [Manager,HeadDepartment]
//   GET /api/manager/reports/recruitment-analytics [Manager,HeadDepartment]
// None of these accept query params (no date-range on the API surface).

export async function runDash() {
  suite("DASH");

  await testCase("UAT-DASH-001", "Candidate dashboard", "P2", async () => {
    const t = await login("nhatquang");
    const r = await api("GET", "/candidate/dashboard", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /candidate/dashboard");
    return { actualApi: s, actualData: `keys=${Object.keys(r.json?.data || {}).join(",")}` };
  });

  await testCase("UAT-DASH-002", "HR dashboard", "P1", async () => {
    const t = await login("thucuyen");
    const r = await api("GET", "/hr/dashboard", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /hr/dashboard");
    return { actualApi: s, actualData: `keys=${Object.keys(r.json?.data || {}).join(",")}` };
  });

  await testCase("UAT-DASH-003", "Manager dashboard", "P2", async () => {
    const t = await login("tiendat");
    const r = await api("GET", "/manager/dashboard", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /manager/dashboard");
    return { actualApi: s, actualData: `keys=${Object.keys(r.json?.data || {}).join(",")}` };
  });

  await testCase("UAT-DASH-004", "Recruitment analytics report (funnel/conversion)", "P2", async () => {
    const t = await login("tiendat");
    const r = await api("GET", "/manager/reports/recruitment-analytics", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /manager/reports/recruitment-analytics");
    return { actualApi: s, actualData: `keys=${Object.keys(r.json?.data || {}).join(",")}`,
      notes: "funnel/Withdrawn-exclusion (BR-APPLICATION-011) requires manual value review" };
  });

  await testCase("UAT-DASH-005", "Metric consistency: dashboard vs API vs DB", "P1", async () => {
    return { result: "N/A", notes: "deferred: cross-source manual comparison (UI/API vs seed DB counts, Auto:No)" };
  });

  await testCase("UAT-DASH-006", "Date-range filters (default/custom/invalid/reversed)", "P2", async () => {
    return { result: "N/A", notes: "UI-only: dashboard/analytics endpoints take no date-range params (client-side filter)" };
  });

  await testCase("UAT-DASH-007", "Empty / single-record / large datasets", "P3", async () => {
    return { result: "N/A", notes: "UI-only: chart/empty-state rendering" };
  });

  await testCase("UAT-DASH-008", "Chart / tooltip / legend / table rendering", "P3", async () => {
    return { result: "N/A", notes: "UI-only: interactive rendering (Auto:No)" };
  });

  await testCase("UAT-DASH-009", "Recruiter sees only assigned data", "P1", async () => {
    // Non-destructive: both HRs read their dashboards; record numbers for manual scoping review.
    const tc = await login("thucuyen");
    const gh = await login("giahan");
    const a = await api("GET", "/hr/dashboard", { token: tc.accessToken });
    const b = await api("GET", "/hr/dashboard", { token: gh.accessToken });
    const sa = expectStatus(a, 200, "GET /hr/dashboard (thucuyen)");
    const sb = expectStatus(b, 200, "GET /hr/dashboard (giahan)");
    return { actualApi: `${sa}; ${sb}`,
      actualData: `thucuyen=${JSON.stringify(a.json?.data)?.slice(0,150)} | giahan=${JSON.stringify(b.json?.data)?.slice(0,150)}`,
      notes: "both 200; per-owner scoping (numbers differ?) needs manual review" };
  });

  await testCase("UAT-DASH-010", "Candidate cannot access analytics → 403", "P1", async () => {
    const t = await login("nhatquang");
    const hr = await api("GET", "/hr/dashboard", { token: t.accessToken });
    const mg = await api("GET", "/manager/dashboard", { token: t.accessToken });
    const an = await api("GET", "/manager/reports/recruitment-analytics", { token: t.accessToken });
    const s1 = expectStatus(hr, 403, "GET /hr/dashboard (candidate)");
    const s2 = expectStatus(mg, 403, "GET /manager/dashboard (candidate)");
    const s3 = expectStatus(an, 403, "GET /manager/reports/recruitment-analytics (candidate)");
    return { actualApi: `${s1}; ${s2}; ${s3}` };
  });

  await testCase("UAT-DASH-011", "Partial API failure degrades gracefully", "P3", async () => {
    return { result: "N/A", notes: "UI-only: recovery/rendering behavior (Auto:No)" };
  });

  await testCase("UAT-DASH-012", "Timezone boundary in analytics (month start/end)", "P3", async () => {
    return { result: "N/A", notes: "UI-only: bucketing observation (Auto:No)" };
  });
}
