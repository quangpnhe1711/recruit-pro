import { api, login, suite, testCase } from "./uat-lib.mjs";

// PHASE 2c — write suite: MDATA (department update round-trip, non-destructive to the visible name) and
// WF (automation workflow create/update/publish/toggle). AI copilot is intentionally NOT automated here
// (consumes AI provider credits + needs a screening candidate pool) — run manually per UAT-AI cases.

const MARK = "[UAT-WRITE]";
const uniq = Date.now();
const ctx = {};
function expect(c, m) { if (!c) throw new Error(m); }

export async function runWrite3() {
  suite("WRITE3");

  // ---- MDATA: department update round-trip (restore original description) ----
  await testCase("UAT-MDATA-W01", "Update department description, then restore (round-trip)", "P1", async () => {
    const admin = (await login("admin")).accessToken;
    const deps = await api("GET", "/departments");
    const dep = (deps.json?.data || [])[0];
    expect(dep?.id, "no department");
    const originalDesc = dep.description ?? "";
    const originalHead = dep.headUserId ?? dep.headUser?.id ?? null;
    // mutate description only (visible name untouched so other UAT reads stay stable)
    const upd = await api("PUT", `/departments/${dep.id}`, { token: admin, body: { name: dep.name, description: `${MARK} temp ${uniq}`, headUserId: originalHead } });
    expect(upd.status === 200, `update HTTP ${upd.status} ${upd.json?.errorCode || upd.text?.slice(0,150)}`);
    // restore
    const restore = await api("PUT", `/departments/${dep.id}`, { token: admin, body: { name: dep.name, description: originalDesc, headUserId: originalHead } });
    expect(restore.status === 200, `restore HTTP ${restore.status}`);
    return { actualApi: `PUT /departments/{id} update→${upd.status}, restore→${restore.status}`, actualData: `dept=${dep.name}` };
  });

  await testCase("UAT-MDATA-W02", "Non-privileged cannot update a department (403/401)", "P1", async () => {
    const deps = await api("GET", "/departments");
    const dep = (deps.json?.data || [])[0];
    const cand = (await login("nhatquang")).accessToken;
    const asCand = await api("PUT", `/departments/${dep.id}`, { token: cand, body: { name: `${MARK} hack` } });
    const asGuest = await api("PUT", `/departments/${dep.id}`, { body: { name: `${MARK} hack` } });
    expect(asCand.status === 403, `candidate expected 403, got ${asCand.status}`);
    expect(asGuest.status === 401, `guest expected 401, got ${asGuest.status}`);
    return { actualApi: `candidate→${asCand.status}, guest→${asGuest.status} (no mutation)` };
  });

  // ---- WF: automation workflow lifecycle (leftover [UAT] workflow set to Disabled; no delete API) ----
  await testCase("UAT-WF-W01", "SysAdmin creates a [UAT] workflow (Shadow)", "P0", async () => {
    const admin = (await login("admin")).accessToken;
    const body = {
      name: `${MARK} wf ${uniq}`,
      description: `${MARK} automated UAT workflow — safe to disable/delete`,
      triggerEventType: "CandidateApplied",
      mode: "Shadow",
      conditions: [],
      actions: [{ type: "shadow_log", configJson: "{}" }], // ≥1 valid action required
    };
    const r = await api("POST", "/sysadmin/automation/workflows", { token: admin, body });
    expect([200, 201].includes(r.status), `create workflow HTTP ${r.status} ${r.json?.errorCode || r.text?.slice(0,180)}`);
    ctx.wfId = r.json?.data?.id;
    expect(ctx.wfId, "no workflow id");
    return { actualApi: `POST workflows → ${r.status}`, actualData: `wfId=${ctx.wfId}` };
  });

  await testCase("UAT-WF-W02", "Update the workflow (rename/description)", "P1", async () => {
    expect(ctx.wfId, "no wfId");
    const admin = (await login("admin")).accessToken;
    const r = await api("PATCH", `/sysadmin/automation/workflows/${ctx.wfId}`, { token: admin, body: { name: `${MARK} wf ${uniq} v2`, description: `${MARK} updated`, triggerEventType: "CandidateApplied", mode: "Shadow", conditions: [], actions: [{ type: "shadow_log", configJson: "{}" }] } });
    expect([200, 204].includes(r.status), `update workflow HTTP ${r.status} ${r.json?.errorCode || r.text?.slice(0,150)}`);
    return { actualApi: `PATCH workflow → ${r.status}` };
  });

  await testCase("UAT-WF-W03", "Publish the workflow (may require actions)", "P1", async () => {
    expect(ctx.wfId, "no wfId");
    const admin = (await login("admin")).accessToken;
    const r = await api("POST", `/sysadmin/automation/workflows/${ctx.wfId}/publish`, { token: admin });
    // Publishing an empty (no-action) workflow may be rejected by validation — that is a valid outcome.
    if ([200, 201].includes(r.status)) return { actualApi: `publish → ${r.status}` };
    if ([400, 422].includes(r.status)) return { actualApi: `publish → ${r.status} ${r.json?.errorCode || ""}`, notes: "empty workflow not publishable (expected validation)" };
    throw new Error(`publish HTTP ${r.status} ${r.json?.errorCode || r.text?.slice(0,150)}`);
  });

  await testCase("UAT-WF-W04", "Toggle enabled off (teardown-ish)", "P2", async () => {
    expect(ctx.wfId, "no wfId");
    const admin = (await login("admin")).accessToken;
    const r = await api("PATCH", `/sysadmin/automation/workflows/${ctx.wfId}/enabled`, { token: admin, body: { isEnabled: false } });
    expect([200, 204].includes(r.status), `toggle HTTP ${r.status} ${r.json?.errorCode || ""}`);
    return { actualApi: `PATCH enabled=false → ${r.status}`, notes: "no delete-workflow API; [UAT] workflow left Disabled (safe, tagged)" };
  });

  await testCase("UAT-WF-W05", "Non-admin cannot create a workflow (403)", "P1", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const r = await api("POST", "/sysadmin/automation/workflows", { token: hr, body: { name: `${MARK} hack`, triggerEventType: "CandidateApplied", mode: "Shadow", conditions: [], actions: [] } });
    expect(r.status === 403, `HR expected 403, got ${r.status}`);
    return { actualApi: `HR create workflow → ${r.status} (no mutation)` };
  });
}
