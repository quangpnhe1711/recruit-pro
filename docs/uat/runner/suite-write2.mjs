import { api, login, suite, testCase } from "./uat-lib.mjs";

// PHASE 2b — write suite: NOTI (mark seen/read), REG (register an isolated [UAT] candidate),
// PROF (profile + experience CRUD on that isolated user), RBAC (roles/status on that isolated user).
// Registering a throwaway user means PROF/RBAC writes never touch a seed persona. Runs sequentially.

const MARK = "[UAT-WRITE]";
const uniq = Date.now(); // Node runner (not a workflow) — real timestamp is fine here for uniqueness.
const ctx = {};
function expect(c, m) { if (!c) throw new Error(m); }

export async function runWrite2() {
  suite("WRITE2");

  // ---- NOTI writes (own notifications; safe/reversible) ----
  await testCase("UAT-NOTI-W01", "Mark all notifications SEEN (bell open)", "P0", async () => {
    const tok = (await login("nhatquang")).accessToken;
    const r = await api("POST", "/notifications/seen", { token: tok });
    expect([200, 204].includes(r.status), `mark-all-seen HTTP ${r.status} ${r.json?.errorCode || ""}`);
    const counts = r.json?.data;
    return { actualApi: `POST /notifications/seen → ${r.status}`, actualData: counts ? `unseen=${counts.unseen}` : "" };
  });

  await testCase("UAT-NOTI-W02", "Mark one notification READ (or all-read fallback)", "P1", async () => {
    const tok = (await login("nhatquang")).accessToken;
    const list = await api("GET", "/notifications?page=1&pageSize=5", { token: tok });
    const items = list.json?.data?.items || [];
    const target = items.find((n) => n.isRead === false) || items[0];
    if (target?.id) {
      const r = await api("PATCH", `/notifications/${target.id}/read`, { token: tok });
      expect([200, 204].includes(r.status), `mark-read HTTP ${r.status}`);
      return { actualApi: `PATCH /notifications/{id}/read → ${r.status}` };
    }
    const all = await api("PATCH", "/notifications/read-all", { token: tok });
    expect([200, 204].includes(all.status), `read-all HTTP ${all.status}`);
    return { actualApi: `no items; PATCH /notifications/read-all → ${all.status}`, notes: "no unread notification to target" };
  });

  await testCase("UAT-NOTI-W03", "Mark ALL notifications read", "P2", async () => {
    const tok = (await login("nhatquang")).accessToken;
    const r = await api("PATCH", "/notifications/read-all", { token: tok });
    expect([200, 204].includes(r.status), `read-all HTTP ${r.status}`);
    const counts = await api("GET", "/notifications/counts", { token: tok });
    return { actualApi: `read-all → ${r.status}`, actualData: `unread=${counts.json?.data?.unread}` };
  });

  // ---- REG: register an isolated [UAT] candidate ----
  await testCase("UAT-REG-W01", "Register a new [UAT] candidate (multipart)", "P0", async () => {
    const fd = new FormData();
    ctx.username = `uat_cand_${uniq}`;
    ctx.email = `uat_${uniq}@example.com`;
    fd.append("UserInfo.Username", ctx.username);
    fd.append("UserInfo.FullName", `${MARK} Candidate ${uniq}`);
    fd.append("UserInfo.Email", ctx.email);
    fd.append("UserInfo.PasswordHash", "Password@123");
    fd.append("UserInfo.Phone", "0900000009");
    const r = await api("POST", "/candidates/register", { body: fd });
    expect([200, 201].includes(r.status), `register HTTP ${r.status} ${r.json?.errorCode || r.text?.slice(0,180)}`);
    ctx.userId = r.json?.data?.userId;
    ctx.candidateId = r.json?.data?.candidateId;
    expect(ctx.userId, "no userId from register");
    return { actualApi: `POST /candidates/register → ${r.status}`, actualData: `userId=${ctx.userId}` };
  });

  await testCase("UAT-REG-W02", "New candidate can log in", "P0", async () => {
    expect(ctx.username, "no username (REG-W01 failed)");
    const r = await api("POST", "/auth/candidate/login", { body: { username: ctx.username, password: "Password@123" } });
    expect(r.status === 200 && r.json?.data?.accessToken, `login HTTP ${r.status} ${r.json?.errorCode || ""}`);
    ctx.token = r.json.data.accessToken;
    return { actualApi: `candidate login → 200`, actualData: `roles=${JSON.stringify(r.json.data.user?.roles)}` };
  });

  await testCase("UAT-REG-W03", "Duplicate username registration is rejected", "P1", async () => {
    const fd = new FormData();
    fd.append("UserInfo.Username", ctx.username);
    fd.append("UserInfo.FullName", `${MARK} dup`);
    fd.append("UserInfo.Email", `dup_${uniq}@example.com`);
    fd.append("UserInfo.PasswordHash", "Password@123");
    fd.append("UserInfo.Phone", "0900000010");
    const r = await api("POST", "/candidates/register", { body: fd });
    expect([409, 400, 422].includes(r.status), `expected duplicate rejection, got ${r.status}`);
    return { actualApi: `duplicate username → ${r.status} ${r.json?.errorCode || ""}` };
  });

  // ---- PROF writes on the isolated [UAT] user ----
  await testCase("UAT-PROF-W01", "Update profile fields", "P1", async () => {
    expect(ctx.token, "no token");
    const body = {
      name: `${MARK} Candidate ${uniq}`, headline: `${MARK} Engineer`, email: ctx.email,
      phone: "0900000009", location: "Ha Noi", bio: `${MARK} bio`, github: "", linkedin: "",
    };
    const r = await api("PUT", "/candidate/profile", { token: ctx.token, body });
    expect(r.status === 200, `profile update HTTP ${r.status} ${r.json?.errorCode || r.text?.slice(0,150)}`);
    return { actualApi: `PUT /candidate/profile → ${r.status}`, actualData: `headline=${r.json?.data?.profile?.headline}` };
  });

  await testCase("UAT-PROF-W02", "Add then delete a work experience", "P1", async () => {
    expect(ctx.token, "no token");
    const expBody = {
      title: `${MARK} Backend Dev`, company: `${MARK} Co`,
      period: { startMonth: 1, startYear: 2022, endMonth: null, endYear: null, isCurrent: true },
      bullets: [`${MARK} built things`],
    };
    const add = await api("POST", "/candidate/profile/experience", { token: ctx.token, body: expBody });
    expect(add.status === 200 || add.status === 201, `add experience HTTP ${add.status} ${add.json?.errorCode || add.text?.slice(0,150)}`);
    const entries = add.json?.data?.experienceEntries || [];
    const created = entries.find((e) => (e.title || "").includes(MARK));
    expect(created?.id, `no created experience id (entries=${entries.length})`);
    const del = await api("DELETE", `/candidate/profile/experience/${created.id}`, { token: ctx.token });
    expect(del.status === 200 || del.status === 204, `delete experience HTTP ${del.status}`);
    return { actualApi: `add→${add.status}, delete→${del.status}`, actualData: `expId=${created.id}` };
  });

  // ---- RBAC writes on the isolated [UAT] user (admin actor) ----
  await testCase("UAT-RBAC-W01", "Admin deactivates the [UAT] user; disabled login → 401", "P0", async () => {
    expect(ctx.userId, "no userId");
    const admin = (await login("admin")).accessToken;
    const patch = await api("PATCH", `/sysadmin/users/${ctx.userId}/status`, { token: admin, body: { status: "Inactive" } });
    expect(patch.status === 200 || patch.status === 204, `deactivate HTTP ${patch.status} ${patch.json?.errorCode || ""}`);
    const relogin = await api("POST", "/auth/candidate/login", { body: { username: ctx.username, password: "Password@123" } });
    expect(relogin.status === 401, `disabled login expected 401, got ${relogin.status}`);
    return { actualApi: `deactivate→${patch.status}, disabled login→${relogin.status} ${relogin.json?.errorCode || ""}` };
  });

  await testCase("UAT-RBAC-W02", "Admin reactivates the [UAT] user; login works again", "P2", async () => {
    expect(ctx.userId, "no userId");
    const admin = (await login("admin")).accessToken;
    const patch = await api("PATCH", `/sysadmin/users/${ctx.userId}/status`, { token: admin, body: { status: "Active" } });
    expect(patch.status === 200 || patch.status === 204, `reactivate HTTP ${patch.status}`);
    const relogin = await api("POST", "/auth/candidate/login", { body: { username: ctx.username, password: "Password@123" } });
    expect(relogin.status === 200, `reactivated login expected 200, got ${relogin.status}`);
    return { actualApi: `reactivate→${patch.status}, login→${relogin.status}` };
  });

  await testCase("UAT-RBAC-W03", "SystemAdmin cannot deactivate itself (409)", "P1", async () => {
    const adminLogin = await login("admin");
    const admin = adminLogin.accessToken;
    const selfId = adminLogin.user?.id;
    expect(selfId, "no admin self id");
    const r = await api("PATCH", `/sysadmin/users/${selfId}/status`, { token: admin, body: { status: "Inactive" } });
    expect([409, 400, 422].includes(r.status), `self-deactivate expected 409/4xx-guard, got ${r.status}`);
    return { actualApi: `self-deactivate → ${r.status} ${r.json?.errorCode || ""}`, notes: "guard: admin cannot disable own account" };
  });

  await testCase("UAT-RBAC-W04", "Invalid status value rejected (no mutation)", "P2", async () => {
    expect(ctx.userId, "no userId");
    const admin = (await login("admin")).accessToken;
    const r = await api("PATCH", `/sysadmin/users/${ctx.userId}/status`, { token: admin, body: { status: "Frozen" } });
    expect([400, 422].includes(r.status), `invalid status expected 400/422, got ${r.status}`);
    return { actualApi: `invalid status "Frozen" → ${r.status} ${r.json?.errorCode || ""}` };
  });
}
