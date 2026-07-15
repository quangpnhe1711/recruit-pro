import { api, login, suite, testCase, expectStatus } from "./uat-lib.mjs";

// LOG — System & audit logs (UAT_TEST_CASES.md 2375–2427)
// Endpoint (SysAdminDirectoryController):
//   GET /api/sysadmin/audit-logs?q&userId&page&pageSize   [RequirePermission System_LOG_VIEW]
// Grants: SystemAdmin + Manager hold System_LOG_VIEW; HR does NOT. No write/delete endpoint (immutable).

export async function runLog() {
  suite("LOG");

  await testCase("UAT-LOG-001", "View audit logs (permission-gated)", "P2", async () => {
    const admin = await login("admin");
    const hr = await login("thucuyen");
    const ok = await api("GET", "/sysadmin/audit-logs?page=1&pageSize=20", { token: admin.accessToken });
    const denied = await api("GET", "/sysadmin/audit-logs", { token: hr.accessToken });
    const s1 = expectStatus(ok, 200, "GET /sysadmin/audit-logs (admin)");
    const s2 = expectStatus(denied, 403, "GET /sysadmin/audit-logs (HR, no System_LOG_VIEW)");
    const items = ok.json?.data?.items ?? ok.json?.data ?? [];
    return { actualApi: `${s1}; ${s2}`, actualData: `rows=${Array.isArray(items) ? items.length : "n/a"}` };
  });

  await testCase("UAT-LOG-002", "Login success/fail not in audit log (documented gap)", "P3", async () => {
    return { result: "N/A", notes: "documented gap (Q-LOG-01): auth events not written to SystemLog; manual absence check" };
  });

  await testCase("UAT-LOG-003", "User status change is audited", "P2", async () => {
    return { result: "N/A", notes: "deferred: stateful write (write-phase) — requires deactivating a spare user" };
  });

  await testCase("UAT-LOG-004", "RBAC change is audited", "P2", async () => {
    return { result: "N/A", notes: "deferred: stateful write (write-phase) — requires editing role permissions" };
  });

  await testCase("UAT-LOG-005", "Logs contain no secrets", "P1", async () => {
    const admin = await login("admin");
    const r = await api("GET", "/sysadmin/audit-logs?page=1&pageSize=100", { token: admin.accessToken });
    const s = expectStatus(r, 200, "GET /sysadmin/audit-logs (admin)");
    const blob = JSON.stringify(r.json?.data ?? {});
    // Scan for obvious secret markers (bcrypt hash prefix, JWT, refresh-token-ish, api key labels).
    const leaks = ["$2a$", "$2b$", "eyJ", "refreshToken", "accessToken", "apiKey", "api_key", "password", "BEGIN "]
      .filter((m) => blob.toLowerCase().includes(m.toLowerCase()));
    if (leaks.length) throw new Error(`possible secret markers in logs: ${leaks.join(", ")}`);
    return { actualApi: s, actualData: `scanned ${blob.length} chars, no secret markers` };
  });

  await testCase("UAT-LOG-006", "Logs read-only for non-privileged; not user-editable", "P2", async () => {
    const hr = await login("thucuyen");
    const cand = await login("nhatquang");
    const hrRead = await api("GET", "/sysadmin/audit-logs", { token: hr.accessToken });
    const candRead = await api("GET", "/sysadmin/audit-logs", { token: cand.accessToken });
    const guest = await api("GET", "/sysadmin/audit-logs");
    const s1 = expectStatus(hrRead, 403, "GET audit-logs (HR)");
    const s2 = expectStatus(candRead, 403, "GET audit-logs (candidate)");
    const s3 = expectStatus(guest, 401, "GET audit-logs (guest)");
    return { actualApi: `${s1}; ${s2}; ${s3}`, notes: "immutable: no create/update/delete endpoint exists for audit logs" };
  });

  await testCase("UAT-LOG-007", "Audit search / filter / pagination", "P3", async () => {
    const admin = await login("admin");
    const filtered = await api("GET", "/sysadmin/audit-logs?q=status&page=1&pageSize=5", { token: admin.accessToken });
    const oob = await api("GET", "/sysadmin/audit-logs?page=9999&pageSize=20", { token: admin.accessToken });
    const s1 = expectStatus(filtered, 200, "GET audit-logs?q=status");
    const s2 = expectStatus(oob, 200, "GET audit-logs?page=9999 (out of range)");
    const oobItems = oob.json?.data?.items ?? oob.json?.data ?? [];
    return { actualApi: `${s1}; ${s2}`,
      actualData: `oob rows=${Array.isArray(oobItems) ? oobItems.length : "n/a"} (expect empty)` };
  });

  await testCase("UAT-LOG-008", "Automation execution logs as the workflow audit trail", "P3", async () => {
    return { result: "N/A", notes: "deferred: manual correlation of automation action → execution/step logs (Auto:No)" };
  });
}
