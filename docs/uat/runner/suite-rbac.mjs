import { api, login, suite, testCase, expectStatus, expectErrorCode } from "./uat-lib.mjs";

// Suite RBAC — Users & RBAC (UAT_TEST_CASES.md §12, cases 001–022).
// Enforcement: [RequirePermission] against live DB grants. authenticated-no-perm → 403 FORBIDDEN;
// no token → 401 UNAUTHENTICATED. Reads + permission/validation negatives are encoded for real; any
// SUCCESSFUL mutation (status/roles/matrix change) is deferred to the write-phase per encoding rules.
// SAFETY: no code here disables/deletes a seed user or edits the live permission matrix.

// Resolve a seed user's id + role id via the admin directory (read-only), cached per run.
let _users = null;
async function seedUser(adminTok, username) {
  if (!_users) {
    const r = await api("GET", "/sysadmin/users?page=1&pageSize=100", { token: adminTok });
    if (r.status !== 200) throw new Error(`cannot list users: HTTP ${r.status}`);
    _users = r.json?.data?.items || [];
  }
  const u = _users.find((x) => x.username === username);
  if (!u) throw new Error(`seed user ${username} not found in directory`);
  return u;
}
async function roleId(adminTok, roleName) {
  const r = await api("GET", "/sysadmin/rbac/roles", { token: adminTok });
  if (r.status !== 200) throw new Error(`cannot list roles: HTTP ${r.status}`);
  const role = (r.json?.data || []).find((x) => x.name === roleName);
  if (!role) throw new Error(`role ${roleName} not found`);
  return role.id;
}

export async function runRbac() {
  suite("RBAC");

  await testCase("UAT-RBAC-001", "SysAdmin console overview & user list", "P1", async () => {
    const t = await login("admin");
    const ov = await api("GET", "/sysadmin/overview", { token: t.accessToken });
    const s1 = expectStatus(ov, 200, "GET /sysadmin/overview");
    const us = await api("GET", "/sysadmin/users?page=1&pageSize=20", { token: t.accessToken });
    const s2 = expectStatus(us, 200, "GET /sysadmin/users");
    const items = us.json?.data?.items;
    if (!Array.isArray(items) || !items.length) throw new Error("user list empty/not an array");
    if (!("status" in items[0]) || !("roles" in items[0])) throw new Error("user rows missing status/roles");
    return { actualApi: `${s1}; ${s2}`, actualData: `users=${us.json?.data?.meta?.totalItems ?? items.length}` };
  });

  await testCase("UAT-RBAC-002", "Non-admin cannot reach SysAdmin API/UI", "P0", async () => {
    const hr = await login("thucuyen");
    const cand = await login("nhatquang");
    const rHr = await api("GET", "/sysadmin/users", { token: hr.accessToken });
    const rCand = await api("GET", "/sysadmin/rbac/roles", { token: cand.accessToken });
    const rGuest = await api("GET", "/sysadmin/users");
    const s1 = expectStatus(rHr, 403, "GET /sysadmin/users (HR)");
    const s2 = expectStatus(rCand, 403, "GET /sysadmin/rbac/roles (candidate)");
    const s3 = expectStatus(rGuest, 401, "GET /sysadmin/users (guest)");
    return { actualApi: `${s1}; ${s2}; ${s3}` };
  });

  await testCase("UAT-RBAC-003", "View roles, modules, role permissions", "P1", async () => {
    const t = await login("admin");
    const roles = await api("GET", "/sysadmin/rbac/roles", { token: t.accessToken });
    const s1 = expectStatus(roles, 200, "GET /sysadmin/rbac/roles");
    const roleList = roles.json?.data || [];
    if (roleList.length !== 5) throw new Error(`expected 5 roles, got ${roleList.length}`);
    const mods = await api("GET", "/sysadmin/rbac/modules", { token: t.accessToken });
    const s2 = expectStatus(mods, 200, "GET /sysadmin/rbac/modules");
    const permCount = (mods.json?.data || []).reduce((n, m) => n + (m.actions?.length || 0), 0);
    if (permCount !== 27) throw new Error(`expected 27 permissions, got ${permCount}`);
    const hrId = roleList.find((r) => r.name === "HR")?.id;
    const rp = await api("GET", `/sysadmin/rbac/roles/${hrId}/permissions`, { token: t.accessToken });
    const s3 = expectStatus(rp, 200, "GET /sysadmin/rbac/roles/{HR}/permissions");
    const granted = rp.json?.data?.grantedCodes?.length;
    if (granted !== 12) throw new Error(`HR expected 12 grants, got ${granted}`);
    return { actualApi: `${s1}; ${s2}; ${s3}`, actualData: `roles=5, perms=27, HR grants=12` };
  });

  await testCase("UAT-RBAC-004", "Update role permission matrix (takes effect next request)", "P0", async () => {
    return { result: "N/A", actualApi: "PUT /sysadmin/rbac/roles/{id}/permissions", notes: "deferred: stateful write, GLOBAL matrix edit — single owner, restore required (write-phase)" };
  });

  await testCase("UAT-RBAC-005", "Permission change reflected in menus & routes (session)", "P1", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "deferred: requires a matrix edit + UI refresh observation (write-phase)" };
  });

  await testCase("UAT-RBAC-006", "RBAC endpoints need the specific permission (not just a role)", "P1", async () => {
    // Needs a role with PERMISSION_VIEW but not PERMISSION_MANAGE — only SystemAdmin holds PERMISSION_VIEW
    // in seed, so it must be constructed. Cannot demonstrate view-yes/manage-no with seed accounts.
    return { result: "N/A", actualApi: "PUT …/permissions", notes: "deferred: requires a constructed role (view-yes, manage-no); not in seed (write-phase)" };
  });

  await testCase("UAT-RBAC-007", "Create/assign roles to a user", "P1", async () => {
    return { result: "N/A", actualApi: "PUT /sysadmin/users/{id}/roles", notes: "deferred: stateful write on a spare [UAT] user (write-phase)" };
  });

  await testCase("UAT-RBAC-008", "Deactivate a user (status Inactive)", "P0", async () => {
    return { result: "N/A", actualApi: "PATCH /sysadmin/users/{id}/status", notes: "deferred: stateful write; must target a spare [UAT] user, never a seed persona (write-phase)" };
  });

  await testCase("UAT-RBAC-009", "Re-activate a disabled user", "P2", async () => {
    return { result: "N/A", actualApi: "PATCH /sysadmin/users/{id}/status", notes: "deferred: stateful write (write-phase)" };
  });

  await testCase("UAT-RBAC-010", "Cannot self-deactivate", "P1", async () => {
    // Expected 409 (guard blocks, no mutation) — but authoring a real deactivation call against the
    // seed admin is barred by the safety rule (a broken guard would disable admin). Defer to write-phase.
    return { result: "N/A", actualApi: "PATCH /sysadmin/users/{admin}/status", notes: "deferred: would target seed admin status endpoint; safety rule — verify guard in write-phase" };
  });

  await testCase("UAT-RBAC-011", "Cannot deactivate the last admin", "P1", async () => {
    return { result: "N/A", actualApi: "PATCH /sysadmin/users/{admin}/status", notes: "deferred: requires reducing to one admin (deactivate minhkhoi); admin-lockout risk (write-phase)" };
  });

  await testCase("UAT-RBAC-012", "Invalid user status value → 400", "P3", async () => {
    // Invalid enum value can never be persisted, so this is a non-destructive validation negative.
    const t = await login("admin");
    const giahan = await seedUser(t.accessToken, "giahan");
    const r = await api("PATCH", `/sysadmin/users/${giahan.id}/status`, { token: t.accessToken, body: { status: "Frozen" } });
    const s = expectStatus(r, 400, "PATCH /sysadmin/users/{id}/status {Frozen}");
    // service returns USER_STATUS_INVALID; a body validator may instead return VALIDATION_ERROR — both are 400, no write.
    const ec = r.json?.errorCode;
    if (ec !== "USER_STATUS_INVALID" && ec !== "VALIDATION_ERROR") throw new Error(`unexpected errorCode ${ec}`);
    return { actualApi: s, actualData: `errorCode=${ec}`, notes: "invalid value never persisted (no mutation)" };
  });

  await testCase("UAT-RBAC-013", "Blocked status also denies login", "P2", async () => {
    return { result: "N/A", actualApi: "PATCH /sysadmin/users/{id}/status", notes: "deferred: stateful write (set spare user Blocked) (write-phase)" };
  });

  await testCase("UAT-RBAC-014", "Role change mid-session", "P1", async () => {
    return { result: "N/A", actualApi: "PUT /sysadmin/users/{id}/roles", notes: "deferred: stateful write + session boundary observation (write-phase)" };
  });

  await testCase("UAT-RBAC-015", "User search / filter / pagination", "P2", async () => {
    const t = await login("admin");
    const byQ = await api("GET", "/sysadmin/users?q=thucuyen", { token: t.accessToken });
    const s1 = expectStatus(byQ, 200, "GET /sysadmin/users?q=thucuyen");
    if (!(byQ.json?.data?.items || []).some((u) => u.username === "thucuyen")) throw new Error("q filter did not return thucuyen");
    const candRole = await roleId(t.accessToken, "Candidate");
    const byRole = await api("GET", `/sysadmin/users?roleId=${candRole}&status=Active`, { token: t.accessToken });
    const s2 = expectStatus(byRole, 200, "GET /sysadmin/users?roleId=Candidate&status=Active");
    const oor = await api("GET", "/sysadmin/users?page=9999&pageSize=20", { token: t.accessToken });
    const s3 = expectStatus(oor, 200, "GET /sysadmin/users?page=9999");
    if ((oor.json?.data?.items || []).length !== 0) throw new Error("out-of-range page not empty");
    return { actualApi: `${s1}; ${s2}; ${s3}`, actualData: `q ok; role/status filter ok; page9999 empty` };
  });

  await testCase("UAT-RBAC-016", "Menu hidden != authorization (API still enforced)", "P0", async () => {
    const cand = await login("nhatquang");
    const rApi = await api("GET", "/sysadmin/overview", { token: cand.accessToken });
    const s1 = expectStatus(rApi, 403, "GET /sysadmin/overview (candidate, direct API)");
    const rGuest = await api("GET", "/sysadmin/overview");
    const s2 = expectStatus(rGuest, 401, "GET /sysadmin/overview (guest)");
    return { actualApi: `${s1}; ${s2}`, notes: "server enforces regardless of hidden menu" };
  });

  await testCase("UAT-RBAC-017", "IDOR on user status/roles (act on arbitrary user id)", "P0", async () => {
    // Non-admin PATCH on an arbitrary id → 403 at the permission gate, before id resolution (no mutation).
    const cand = await login("nhatquang");
    const anyId = "30000000-0000-4000-8000-000000000099";
    const r = await api("PATCH", `/sysadmin/users/${anyId}/status`, { token: cand.accessToken, body: { status: "Active" } });
    const s = expectStatus(r, 403, "PATCH /sysadmin/users/{anyId}/status (candidate)");
    return { actualApi: s, notes: "permission gate blocks before id resolution (no mutation)" };
  });

  await testCase("UAT-RBAC-018", "Mass assignment / unexpected fields on user update", "P2", async () => {
    return { result: "N/A", actualApi: "PATCH/PUT /sysadmin/users/{id}", notes: "deferred: needs a successful write to confirm whitelisting (write-phase)" };
  });

  await testCase("UAT-RBAC-019", "Audit logs list (permission-gated)", "P2", async () => {
    const admin = await login("admin");
    const ok = await api("GET", "/sysadmin/audit-logs?page=1&pageSize=20", { token: admin.accessToken });
    const s1 = expectStatus(ok, 200, "GET /sysadmin/audit-logs (admin)");
    if (!Array.isArray(ok.json?.data?.items)) throw new Error("audit-logs data.items not an array");
    // HR lacks System_LOG_VIEW → 403 (Manager DOES hold it, so HR is the correct negative actor).
    const hr = await login("thucuyen");
    const denied = await api("GET", "/sysadmin/audit-logs", { token: hr.accessToken });
    const s2 = expectStatus(denied, 403, "GET /sysadmin/audit-logs (HR)");
    return { actualApi: `${s1}; ${s2}`, actualData: `logs=${ok.json?.data?.meta?.totalItems ?? "?"}` };
  });

  await testCase("UAT-RBAC-020", "User-role & role-permission uniqueness (no duplicates)", "P3", async () => {
    return { result: "N/A", actualApi: "PUT …/roles, PUT …/permissions", notes: "deferred: stateful write (re-grant idempotency) (write-phase)" };
  });

  await testCase("UAT-RBAC-021", "Seed permission set matches init.sql (27 permissions)", "P2", async () => {
    const t = await login("admin");
    const mods = await api("GET", "/sysadmin/rbac/modules", { token: t.accessToken });
    const s = expectStatus(mods, 200, "GET /sysadmin/rbac/modules");
    const codes = (mods.json?.data || []).flatMap((m) => (m.actions || []).map((a) => a.code));
    const expected = [
      "USER_VIEW","USER_CREATE","USER_UPDATE","USER_DELETE","ROLE_VIEW","ROLE_MANAGE","PERMISSION_VIEW",
      "PERMISSION_MANAGE","Job_VIEW","Job_CREATE","Job_UPDATE","Job_DELETE","Job_APPROVE","Application_VIEW",
      "Application_APPLY","Application_REVIEW","Interview_VIEW","Interview_CREATE","Interview_UPDATE",
      "CANDIDATE_PROFILE_VIEW","CANDIDATE_PROFILE_UPDATE","DEPARTMENT_VIEW","DEPARTMENT_MANAGE","SKILL_VIEW",
      "SKILL_MANAGE","NOTIFICATION_VIEW","System_LOG_VIEW",
    ];
    if (codes.length !== 27) throw new Error(`expected 27 codes, got ${codes.length}`);
    const missing = expected.filter((c) => !codes.includes(c)); // exact code + casing
    if (missing.length) throw new Error(`missing/mis-cased codes: ${missing.join(",")}`);
    return { actualApi: s, actualData: "27 permission codes match init.sql (exact casing)" };
  });

  await testCase("UAT-RBAC-022", "FE permission map vs backend grants (mismatch risk)", "P2", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "documentation/analysis compare (Q-RBAC-01); no single API to assert" };
  });
}
