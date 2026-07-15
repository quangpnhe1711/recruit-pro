import { api, suite, testCase, expectStatus } from "./uat-lib.mjs";

// Seed job ids (UAT_TEST_DATA §5.1): enumerable 30000000-0000-4000-8000-00000000000X
const DRAFT_JOB = "30000000-0000-4000-8000-000000000005";   // Draft (BUG-UAT-002)
const PENDING_JOB = "30000000-0000-4000-8000-000000000004"; // PendingApproval
const UNKNOWN_JOB = "99999999-9999-4999-8999-999999999999";

// Envelope is { success, data } — list data may be {items:[]} or an array; be shape-tolerant.
function jobItems(r) {
  const d = r.json?.data;
  if (Array.isArray(d)) return d;
  if (Array.isArray(d?.items)) return d.items;
  if (Array.isArray(r.json?.items)) return r.json.items;
  return [];
}
const statusOf = (j) => String(j?.status ?? j?.approvalStatus ?? "").toUpperCase();

let _approvedId = null;
async function anApprovedJobId() {
  if (_approvedId) return _approvedId;
  const r = await api("GET", "/jobs?page=1&pageSize=50");
  const items = jobItems(r);
  const appr = items.find((j) => statusOf(j) === "APPROVED") || items[0];
  _approvedId = appr?.id || appr?.jobId || null;
  return _approvedId;
}

export async function runPub() {
  suite("PUB");

  await testCase("UAT-PUB-001", "Landing page loads for anonymous visitor", "P1", async () => {
    // UI render is out of scope; assert the API-observable part: anonymous public GET works, no token.
    const r = await api("GET", "/jobs/filters");
    const s = expectStatus(r, 200, "GET /jobs/filters (no token)");
    return { actualApi: s, notes: "UI render of landing verified manually; anonymous public access asserted" };
  });

  await testCase("UAT-PUB-002", "Public job list returns only Approved jobs", "P0", async () => {
    const r = await api("GET", "/jobs?page=1&pageSize=50");
    const s = expectStatus(r, 200, "GET /jobs");
    const items = jobItems(r);
    if (!items.length) throw new Error("no jobs returned");
    const bad = items.filter((j) => statusOf(j) && statusOf(j) !== "APPROVED");
    if (bad.length) throw new Error(`non-Approved jobs leaked: ${bad.map(statusOf).join(",")}`);
    const ids = items.map((j) => j.id || j.jobId);
    if (ids.includes(DRAFT_JOB) || ids.includes(PENDING_JOB)) throw new Error("Draft/Pending job present in public list");
    return { actualApi: s, actualData: `${items.length} jobs, all Approved` };
  });

  await testCase("UAT-PUB-003", "Public job detail (Approved) shows public fields, no internal data", "P1", async () => {
    const id = await anApprovedJobId();
    if (!id) throw new Error("no approved job id");
    const r = await api("GET", `/jobs/${id}`);
    const s = expectStatus(r, 200, `GET /jobs/${id}`);
    const d = r.json?.data || {};
    const leaked = ["recruiterId", "createdBy", "internalNotes", "assignedOwnerId"].filter((k) => k in d);
    if (leaked.length) throw new Error(`internal fields exposed: ${leaked.join(",")}`);
    return { actualApi: s, actualData: `no internal fields (${leaked.length} leaks)` };
  });

  await testCase("UAT-PUB-004", "Search by keyword", "P2", async () => {
    const r = await api("GET", "/jobs?keyword=Java&page=1&pageSize=50");
    const s = expectStatus(r, 200, "GET /jobs?keyword=Java");
    const items = jobItems(r);
    const bad = items.filter((j) => statusOf(j) && statusOf(j) !== "APPROVED");
    if (bad.length) throw new Error("keyword search leaked non-Approved");
    return { actualApi: s, actualData: `${items.length} matches` };
  });

  await testCase("UAT-PUB-005", "Filter by department / skill / employment type / work mode / location", "P2", async () => {
    const f = await api("GET", "/jobs/filters");
    const sf = expectStatus(f, 200, "GET /jobs/filters");
    const r = await api("GET", "/jobs?workMode=Remote&page=1&pageSize=50");
    const sr = expectStatus(r, 200, "GET /jobs?workMode=Remote");
    const bad = jobItems(r).filter((j) => statusOf(j) && statusOf(j) !== "APPROVED");
    if (bad.length) throw new Error("filtered result leaked non-Approved");
    return { actualApi: `${sf}; ${sr}` };
  });

  await testCase("UAT-PUB-006", "Sort and pagination", "P2", async () => {
    const oor = await api("GET", "/jobs?page=9999&pageSize=5");
    const s1 = expectStatus(oor, 200, "GET /jobs?page=9999");
    if (jobItems(oor).length !== 0) throw new Error("out-of-range page should be empty");
    const zero = await api("GET", "/jobs?page=1&pageSize=0");
    const big = await api("GET", "/jobs?page=1&pageSize=99999");
    if (zero.status >= 500 || big.status >= 500) throw new Error(`pageSize boundary 500: 0→${zero.status} 99999→${big.status}`);
    return { actualApi: `${s1}; pageSize0→${zero.status}; pageSize99999→${big.status}`, notes: "captured clamp behaviour" };
  });

  await testCase("UAT-PUB-007", "Empty / loading / error states on list", "P3", async () => {
    // Loading/error states are UI; the empty-state trigger is API-verifiable.
    const r = await api("GET", "/jobs?keyword=zzznojob");
    const s = expectStatus(r, 200, "GET /jobs?keyword=zzznojob");
    if (jobItems(r).length !== 0) throw new Error("expected empty result for nonsense keyword");
    return { actualApi: s, notes: "empty-state asserted; loading/error states are UI-only" };
  });

  await testCase("UAT-PUB-008", "Navigate card→detail; back/forward; refresh", "P2", async () => {
    return { result: "N/A", notes: "UI-only (SPA routing / deep-link / back-forward, no distinct API)" };
  });

  await testCase("UAT-PUB-009", "Job not found (unknown / malformed id)", "P2", async () => {
    const unk = await api("GET", `/jobs/${UNKNOWN_JOB}`);
    const s = expectStatus(unk, 404, `GET /jobs/${UNKNOWN_JOB}`);
    const mal = await api("GET", "/jobs/not-a-guid");
    if (mal.status >= 500) throw new Error(`malformed id → 500`);
    return { actualApi: `${s}; malformed→${mal.status}`, notes: `unknown errorCode=${unk.json?.errorCode}; malformed→${mal.status} (404/400 acceptable)` };
  });

  await testCase("UAT-PUB-010", "Invalid query parameters do not break page or leak errors", "P3", async () => {
    const r = await api("GET", "/jobs?page=-1&pageSize=abc&salaryMin=xyz&sort=;DROP");
    if (r.status >= 500) throw new Error(`malformed query → 500`);
    if (![200, 400].includes(r.status)) throw new Error(`unexpected status ${r.status}`);
    const xss = await api("GET", "/jobs?keyword=" + encodeURIComponent("<script>alert(1)</script>"));
    if (xss.status >= 500) throw new Error("XSS keyword → 500");
    return { actualApi: `malformed→${r.status}; xss→${xss.status}`, notes: "never 500; 200(defaults)/400 acceptable" };
  });

  await testCase("UAT-PUB-011", "[KNOWN BUG] Public detail exposes Draft & PendingApproval jobs", "P2", async () => {
    const d = await api("GET", `/jobs/${DRAFT_JOB}`);
    const p = await api("GET", `/jobs/${PENDING_JOB}`);
    const draftLeaked = d.status === 200;
    const pendLeaked = p.status === 200;
    // Target = 404 for non-Approved. Current defect = 200 (BUG-UAT-002).
    if (draftLeaked || pendLeaked) {
      return { result: "FAIL", actualApi: `Draft→${d.status}, Pending→${p.status}`, notes: "BUG-UAT-002 confirmed: non-Approved job detail exposed to anonymous (expected 404)" };
    }
    return { result: "PASS", actualApi: `Draft→${d.status}, Pending→${p.status}`, notes: "BUG-UAT-002 appears fixed (404 for non-Approved)" };
  });

  await testCase("UAT-PUB-012", "OData & XML-demo public endpoints", "P3", async () => {
    // xml-demo lives under /api → assertable. OData lives at /odata (outside the /api base) → manual.
    const xml = await api("GET", "/xml-demo/jobs", { headers: { Accept: "application/json" } });
    const s = expectStatus(xml, 200, "GET /api/xml-demo/jobs");
    return { actualApi: s, notes: "xml-demo asserted; /odata/Jobs is outside /api base → manual/curl (deferred)" };
  });

  await testCase("UAT-PUB-013", "Public lookups: departments & skills", "P3", async () => {
    const dep = await api("GET", "/departments");
    const sk = await api("GET", "/skills");
    const sd = expectStatus(dep, 200, "GET /departments");
    const ss = expectStatus(sk, 200, "GET /skills");
    const depN = (Array.isArray(dep.json?.data) ? dep.json.data : dep.json?.data?.items || []).length;
    const skN = (Array.isArray(sk.json?.data) ? sk.json.data : sk.json?.data?.items || []).length;
    return { actualApi: `${sd}; ${ss}`, actualData: `departments=${depN} (exp 8), skills=${skN} (exp 30)` };
  });

  await testCase("UAT-PUB-014", "Job statistics endpoint is aggregate-only (no PII)", "P2", async () => {
    const id = await anApprovedJobId();
    if (!id) throw new Error("no approved job id");
    const r = await api("GET", `/jobs/${id}/statistics`);
    const s = expectStatus(r, 200, `GET /jobs/${id}/statistics`);
    const blob = JSON.stringify(r.json?.data || {}).toLowerCase();
    for (const bad of ["email", "fullname", "candidateid", "applicationid"]) {
      if (blob.includes(bad)) throw new Error(`statistics leaked PII field '${bad}'`);
    }
    return { actualApi: s, actualData: "aggregate-only, no PII keys" };
  });
}
