import { api, login, suite, testCase, expectStatus, expectErrorCode } from "./uat-lib.mjs";

// MDATA — Departments & master data (UAT_TEST_CASES.md 1855–1921)
// Endpoints:
//   GET  /api/departments                 (public lookup, no auth)
//   GET  /api/departments/{id}            [HR,Manager,HeadDepartment,SystemAdmin]
//   PUT  /api/departments/{id}            [HR,Manager,HeadDepartment,SystemAdmin]  (write)
//   GET  /api/skills                      (public)
//   GET  /api/users/assignable-recruitment-owners  [HR,HeadDepartment,SystemAdmin]
// Master data (skills/benefits/currencies/offer templates) is seed-only — no CRUD API (Q-MDATA-01).

async function firstDeptId() {
  const r = await api("GET", "/departments");
  return (r.json?.data || [])[0]?.id;
}

export async function runMdata() {
  suite("MDATA");

  await testCase("UAT-MDATA-001", "List departments (public lookup)", "P2", async () => {
    const r = await api("GET", "/departments");
    const s = expectStatus(r, 200, "GET /departments");
    const d = r.json?.data || [];
    if (d.length !== 8) throw new Error(`expected 8 departments, got ${d.length}`);
    const hasHeadFields = d.every((x) => "headUserId" in x && "headUserName" in x && "headUserEmail" in x);
    if (!hasHeadFields) throw new Error("department rows missing headUser* fields");
    return { actualApi: s, actualData: `count=${d.length}, headFields=ok` };
  });

  await testCase("UAT-MDATA-002", "Department detail (role-gated)", "P2", async () => {
    const id = await firstDeptId();
    if (!id) throw new Error("no department id from /departments");
    const hr = await login("thucuyen");
    const cand = await login("nhatquang");
    const asHr = await api("GET", `/departments/${id}`, { token: hr.accessToken });
    const asCand = await api("GET", `/departments/${id}`, { token: cand.accessToken });
    const asGuest = await api("GET", `/departments/${id}`);
    const s1 = expectStatus(asHr, 200, "GET /departments/{id} (HR)");
    const s2 = expectStatus(asCand, 403, "GET /departments/{id} (candidate)");
    const s3 = expectStatus(asGuest, 401, "GET /departments/{id} (guest)");
    return { actualApi: `${s1}; ${s2}; ${s3}` };
  });

  await testCase("UAT-MDATA-003", "Assign department head (valid role required)", "P1", async () => {
    return { result: "N/A", notes: "deferred: stateful write (write-phase) — PUT /departments/{id} sets head; cleanup required" };
  });

  await testCase("UAT-MDATA-004", "Update department name/description; duplicate name", "P2", async () => {
    return { result: "N/A", notes: "deferred: stateful write (write-phase) — PUT /departments/{id} renames; cleanup required" };
  });

  await testCase("UAT-MDATA-005", "Department update permission (candidate/guest)", "P2", async () => {
    const id = await firstDeptId();
    if (!id) throw new Error("no department id from /departments");
    const cand = await login("nhatquang");
    // Authorization is evaluated before the body is applied, so these are non-destructive (rejected).
    const asCand = await api("PUT", `/departments/${id}`, { token: cand.accessToken, body: { name: null } });
    const asGuest = await api("PUT", `/departments/${id}`, { body: { name: null } });
    const s1 = expectStatus(asCand, 403, "PUT /departments/{id} (candidate)");
    const s2 = expectStatus(asGuest, 401, "PUT /departments/{id} (guest)");
    return { actualApi: `${s1}; ${s2}`, notes: "rejected at auth layer; no mutation applied" };
  });

  await testCase("UAT-MDATA-006", "Skills lookup", "P3", async () => {
    const r = await api("GET", "/skills");
    const s = expectStatus(r, 200, "GET /skills");
    const d = r.json?.data || [];
    if (d.length !== 30) throw new Error(`expected 30 skills, got ${d.length}`);
    return { actualApi: s, actualData: `count=${d.length}` };
  });

  await testCase("UAT-MDATA-007", "Offer templates / benefits / currencies availability", "P3", async () => {
    return { result: "N/A", notes: "UI-only: offer editor references seed master data (no standalone lookup API)" };
  });

  await testCase("UAT-MDATA-008", "Assignable recruitment owners lookup", "P2", async () => {
    const hr = await login("thucuyen");
    const cand = await login("nhatquang");
    const ok = await api("GET", "/users/assignable-recruitment-owners", { token: hr.accessToken });
    const denied = await api("GET", "/users/assignable-recruitment-owners", { token: cand.accessToken });
    const s1 = expectStatus(ok, 200, "GET /users/assignable-recruitment-owners (HR)");
    const s2 = expectStatus(denied, 403, "GET /users/assignable-recruitment-owners (candidate)");
    const data = ok.json?.data || {};
    const recruiters = data.recruiters || [];
    const heads = data.departmentHeads || [];
    // candidates must never appear — check no role marker of Candidate leaked in the blob.
    const blob = JSON.stringify(data).toLowerCase();
    if (blob.includes("candidate")) throw new Error("candidate role leaked into assignable owners");
    return { actualApi: `${s1}; ${s2}`, actualData: `recruiters=${recruiters.length}, departmentHeads=${heads.length}` };
  });

  await testCase("UAT-MDATA-009", "Master-data CRUD gap (documented)", "P3", async () => {
    return { result: "N/A", notes: "documented gap (Q-MDATA-01): no create/update/delete API for skills/benefits/currencies/templates" };
  });

  await testCase("UAT-MDATA-010", "Delete/deactivate a referenced master record", "P3", async () => {
    return { result: "N/A", notes: "documented: no delete API for departments/master data; FK integrity enforced at DB only" };
  });
}
