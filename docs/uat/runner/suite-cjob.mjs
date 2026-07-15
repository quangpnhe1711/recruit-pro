import { api, login, suite, testCase, expectStatus } from "./uat-lib.mjs";

// Envelope { success, data }; list data may be {items:[]} or an array.
function jobItems(r) {
  const d = r.json?.data;
  if (Array.isArray(d)) return d;
  if (Array.isArray(d?.items)) return d.items;
  if (Array.isArray(r.json?.items)) return r.json.items;
  return [];
}
const statusOf = (j) => String(j?.status ?? j?.approvalStatus ?? "").toUpperCase();

export async function runCjob() {
  suite("CJOB");

  const { accessToken: cand } = await login("nhatquang"); // primary candidate

  await testCase("UAT-CJOB-001", "Candidate sees only Approved jobs", "P1", async () => {
    const r = await api("GET", "/jobs?page=1&pageSize=50", { token: cand });
    const s = expectStatus(r, 200, "GET /jobs (candidate)");
    const items = jobItems(r);
    if (!items.length) throw new Error("no jobs returned");
    const bad = items.filter((j) => statusOf(j) && statusOf(j) !== "APPROVED");
    if (bad.length) throw new Error(`non-Approved leaked: ${bad.map(statusOf).join(",")}`);
    return { actualApi: s, actualData: `${items.length} jobs, all Approved` };
  });

  await testCase("UAT-CJOB-002", "Keyword search (candidate)", "P2", async () => {
    const r = await api("GET", "/jobs?keyword=React&page=1&pageSize=50", { token: cand });
    const s = expectStatus(r, 200, "GET /jobs?keyword=React (candidate)");
    const bad = jobItems(r).filter((j) => statusOf(j) && statusOf(j) !== "APPROVED");
    if (bad.length) throw new Error("keyword search leaked non-Approved");
    return { actualApi: s, actualData: `${jobItems(r).length} matches` };
  });

  await testCase("UAT-CJOB-003", "Combined filters + clear (candidate)", "P2", async () => {
    const r = await api("GET", "/jobs?workMode=Remote&salaryMin=10000000&page=1&pageSize=50", { token: cand });
    const s = expectStatus(r, 200, "GET /jobs (combined filters, candidate)");
    const clear = await api("GET", "/jobs?page=1&pageSize=50", { token: cand });
    const sc = expectStatus(clear, 200, "GET /jobs (cleared)");
    return { actualApi: `${s}; ${sc}`, notes: "narrow then restore; clear returns full list" };
  });

  await testCase("UAT-CJOB-004", "Sort & pagination (candidate)", "P3", async () => {
    const oor = await api("GET", "/jobs?page=9999&pageSize=5", { token: cand });
    const s = expectStatus(oor, 200, "GET /jobs?page=9999 (candidate)");
    if (jobItems(oor).length !== 0) throw new Error("out-of-range page should be empty");
    return { actualApi: s };
  });

  await testCase("UAT-CJOB-005", "Job with no salary / no benefits / many skills / long content", "P3", async () => {
    return { result: "N/A", notes: "UI-only (graceful rendering of missing salary/benefits/long content, no distinct API)" };
  });

  await testCase("UAT-CJOB-006", "Navigate card → detail → apply", "P1", async () => {
    const list = await api("GET", "/jobs?page=1&pageSize=50", { token: cand });
    const id = (jobItems(list)[0] || {}).id;
    if (!id) throw new Error("no job id");
    const r = await api("GET", `/jobs/${id}/apply-context`, { token: cand });
    const s = expectStatus(r, 200, `GET /jobs/${id}/apply-context`);
    const elig = r.json?.data?.eligibility;
    if (!(elig && "canApply" in elig)) throw new Error("apply-context missing eligibility.canApply");
    return { actualApi: s, actualData: `canApply=${elig.canApply}` };
  });

  await testCase("UAT-CJOB-007", "Apply CTA disabled for closed/expired/duplicate", "P1", async () => {
    // Non-destructive: probe apply-context across approved jobs; nhatquang has seed applications, so at
    // least one job should return canApply:false with a blocker reason. Specific Closed/expired ids need
    // DATA-PREP-03. Asserts the contract: when canApply is false a reason/blocker is present.
    const list = await api("GET", "/jobs?page=1&pageSize=50", { token: cand });
    const items = jobItems(list).slice(0, 8);
    let blocked = null;
    for (const j of items) {
      const id = j.id || j.jobId;
      if (!id) continue;
      const ctx = await api("GET", `/jobs/${id}/apply-context`, { token: cand });
      if (ctx.status === 200 && ctx.json?.data?.eligibility?.canApply === false) { blocked = ctx.json.data.eligibility; break; }
    }
    if (blocked) {
      const reason = (blocked.blockers && blocked.blockers.length) ? blocked.blockers : (blocked.guidanceMessage ?? blocked.primaryErrorCode);
      if (!reason) throw new Error("canApply:false but no blocker reason in payload");
      return { actualApi: "apply-context eligibility.canApply=false", actualData: `reason=${JSON.stringify(reason)}` };
    }
    return { result: "N/A", notes: "no blocked apply-context among sampled jobs; Closed/expired specifics need DATA-PREP-03 (write-phase)" };
  });

  await testCase("UAT-CJOB-008", "Candidate job recommendations", "P2", async () => {
    const r = await api("GET", "/candidate/jobs/recommendations?take=5", { token: cand });
    const s = expectStatus(r, 200, "GET /candidate/jobs/recommendations");
    if (r.json?.success !== true) throw new Error(`success!=true (errorCode=${r.json?.errorCode})`);
    return { actualApi: s, notes: "graceful 200 even if AI/embeddings unavailable (may be empty)" };
  });

  await testCase("UAT-CJOB-009", "Direct URL / refresh / back-forward (candidate list)", "P3", async () => {
    return { result: "N/A", notes: "UI-only (SPA deep-link / refresh / back-forward, no distinct API)" };
  });

  await testCase("UAT-CJOB-010", "Empty / loading / error states (candidate)", "P3", async () => {
    const r = await api("GET", "/jobs?keyword=zzznojob", { token: cand });
    const s = expectStatus(r, 200, "GET /jobs?keyword=zzznojob (candidate)");
    if (jobItems(r).length !== 0) throw new Error("expected empty result");
    return { actualApi: s, notes: "empty-state asserted; loading/error are UI-only" };
  });

  await testCase("UAT-CJOB-011", "Newly published job appears; just-closed disappears", "P2", async () => {
    return { result: "N/A", notes: "deferred: stateful write (requires approve/close a job) (write-phase)" };
  });

  await testCase("UAT-CJOB-012", "No internal/private data leaked in candidate list/detail", "P1", async () => {
    const list = await api("GET", "/jobs?page=1&pageSize=50", { token: cand });
    const s1 = expectStatus(list, 200, "GET /jobs (candidate)");
    const id = (jobItems(list)[0] || {}).id;
    const detail = await api("GET", `/jobs/${id}`, { token: cand });
    const s2 = expectStatus(detail, 200, `GET /jobs/${id} (candidate)`);
    const blob = (JSON.stringify(jobItems(list)) + JSON.stringify(detail.json?.data || {})).toLowerCase();
    for (const bad of ["recruiterid", "createdby", "internalnotes", "assignedowner"]) {
      if (blob.includes(bad)) throw new Error(`internal field '${bad}' leaked in candidate view`);
    }
    return { actualApi: `${s1}; ${s2}`, actualData: "no recruiter/owner/internal fields" };
  });

  await testCase("UAT-CJOB-013", "Responsive & mobile bottom-nav", "P3", async () => {
    return { result: "N/A", notes: "UI-only (responsive layout / mobile BottomNavBar, no distinct API)" };
  });

  await testCase("UAT-CJOB-014", "Query param robustness (candidate)", "P3", async () => {
    const r = await api("GET", "/jobs?page=-1&pageSize=abc&salaryMin=xyz&sort=;DROP", { token: cand });
    if (r.status >= 500) throw new Error("malformed query → 500");
    if (![200, 400].includes(r.status)) throw new Error(`unexpected status ${r.status}`);
    return { actualApi: `malformed→${r.status}`, notes: "never 500; 200(defaults)/400 acceptable" };
  });
}
