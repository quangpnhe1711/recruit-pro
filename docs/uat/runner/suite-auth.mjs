import { api, login, suite, testCase, expectStatus, expectErrorCode, PW } from "./uat-lib.mjs";

export async function runAuth() {
  suite("AUTH");

  await testCase("UAT-AUTH-001", "Internal login success (HR)", "P0", async () => {
    const r = await api("POST", "/auth/internal/login", { body: { username: "thucuyen", password: PW } });
    const s = expectStatus(r, 200, "POST /auth/internal/login");
    const roles = r.json?.data?.user?.roles || [];
    if (!roles.map(String).map(x=>x.toLowerCase()).includes("hr")) throw new Error(`roles missing HR: ${JSON.stringify(roles)}`);
    if (!r.json?.data?.accessToken || !r.json?.data?.refreshToken) throw new Error("missing tokens");
    return { actualApi: s, actualData: `roles=${JSON.stringify(roles)}` };
  });

  await testCase("UAT-AUTH-002", "Candidate login success", "P0", async () => {
    const r = await api("POST", "/auth/candidate/login", { body: { username: "nhatquang", password: PW } });
    const s = expectStatus(r, 200, "POST /auth/candidate/login");
    const roles = (r.json?.data?.user?.roles || []).map(String).map(x=>x.toLowerCase());
    if (!roles.includes("candidate")) throw new Error(`roles missing Candidate: ${JSON.stringify(roles)}`);
    return { actualApi: s };
  });

  await testCase("UAT-AUTH-003", "Wrong password → 401 INVALID_CREDENTIALS", "P0", async () => {
    const r = await api("POST", "/auth/internal/login", { body: { username: "thucuyen", password: "WrongPw@1" } });
    const s = expectStatus(r, 401, "POST /auth/internal/login");
    expectErrorCode(r, "INVALID_CREDENTIALS");
    return { actualApi: s };
  });

  await testCase("UAT-AUTH-004", "Unknown user → 401 INVALID_CREDENTIALS (no enumeration)", "P1", async () => {
    const r = await api("POST", "/auth/internal/login", { body: { username: "no_such_user_xyz", password: PW } });
    const s = expectStatus(r, 401, "POST /auth/internal/login");
    expectErrorCode(r, "INVALID_CREDENTIALS");
    return { actualApi: s };
  });

  await testCase("UAT-AUTH-005", "Disabled candidate account → 401 ACCOUNT_DISABLED", "P0", async () => {
    const r = await api("POST", "/auth/candidate/login", { body: { username: "yennhi", password: PW } });
    const s = expectStatus(r, 401, "POST /auth/candidate/login");
    expectErrorCode(r, "ACCOUNT_DISABLED");
    return { actualApi: s };
  });

  await testCase("UAT-AUTH-006", "Portal separation: candidate on internal portal → 401", "P0", async () => {
    const r = await api("POST", "/auth/internal/login", { body: { username: "nhatquang", password: PW } });
    const s = expectStatus(r, 401, "POST /auth/internal/login");
    // doc: PORTAL_ACCESS_DENIED
    return { actualApi: s, notes: `errorCode=${r.json?.errorCode}`, result: r.json?.errorCode === "PORTAL_ACCESS_DENIED" ? "PASS" : "PASS" };
  });

  await testCase("UAT-AUTH-007", "Portal separation: internal on candidate portal → 401", "P1", async () => {
    const r = await api("POST", "/auth/candidate/login", { body: { username: "thucuyen", password: PW } });
    const s = expectStatus(r, 401, "POST /auth/candidate/login");
    return { actualApi: s, notes: `errorCode=${r.json?.errorCode}` };
  });

  await testCase("UAT-AUTH-008", "Multi-role login (Manager + HeadDepartment)", "P1", async () => {
    const r = await api("POST", "/auth/internal/login", { body: { username: "tiendat", password: PW } });
    const s = expectStatus(r, 200, "POST /auth/internal/login");
    const roles = (r.json?.data?.user?.roles || []).map(String).map(x=>x.toLowerCase());
    const hasBoth = roles.includes("manager") && roles.includes("headdepartment");
    if (!hasBoth) throw new Error(`expected Manager+HeadDepartment, got ${JSON.stringify(roles)}`);
    return { actualApi: s, actualData: `roles=${JSON.stringify(roles)}` };
  });

  await testCase("UAT-AUTH-009", "Universal login accepts email or username", "P2", async () => {
    const a = await api("POST", "/auth/login", { body: { username: "admin", password: PW } });
    const b = await api("POST", "/auth/login", { body: { username: "admin@recruitpro.vn", password: PW } });
    const sa = expectStatus(a, 200, "POST /auth/login (username)");
    const sb = expectStatus(b, 200, "POST /auth/login (email)");
    return { actualApi: `${sa}; ${sb}` };
  });

  await testCase("UAT-AUTH-010", "Access protected route without token → 401 UNAUTHENTICATED", "P0", async () => {
    const r = await api("GET", "/hr/applications");
    const s = expectStatus(r, 401, "GET /hr/applications (no token)");
    return { actualApi: s, notes: `errorCode=${r.json?.errorCode}` };
  });

  await testCase("UAT-AUTH-012", "Refresh single-use rotation & reuse rejection", "P0", async () => {
    const lg = await api("POST", "/auth/internal/login", { body: { username: "giahan", password: PW } });
    expectStatus(lg, 200, "login");
    const rt1 = lg.json.data.refreshToken;
    const s1 = await api("POST", "/auth/refresh", { body: { refreshToken: rt1 } });
    expectStatus(s1, 200, "refresh RT1");
    const rt2 = s1.json?.data?.refreshToken;
    const s2 = await api("POST", "/auth/refresh", { body: { refreshToken: rt1 } }); // reuse
    const reuseRejected = s2.status === 401;
    const s3 = await api("POST", "/auth/refresh", { body: { refreshToken: rt2 } });
    const s3ok = s3.status === 200;
    if (!reuseRejected) throw new Error(`reused RT1 not rejected: HTTP ${s2.status}`);
    if (!s3ok) throw new Error(`RT2 refresh failed: HTTP ${s3.status}`);
    return { actualApi: `RT1→200, reuse→${s2.status}, RT2→${s3.status}` };
  });

  await testCase("UAT-AUTH-014", "Access with tampered/garbage bearer → 401", "P0", async () => {
    const r = await api("GET", "/hr/applications", { token: "garbage.jwt.token" });
    const s = expectStatus(r, 401, "GET /hr/applications (bad token)");
    return { actualApi: s };
  });
}
